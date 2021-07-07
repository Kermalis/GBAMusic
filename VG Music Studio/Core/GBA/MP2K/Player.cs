using Kermalis.EndianBinaryIO;
using Kermalis.VGMusicStudio.Properties;
using Kermalis.VGMusicStudio.Util;
using Sanford.Multimedia.Midi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Kermalis.VGMusicStudio.Core.GBA.MP2K
{
    internal sealed class Player : IPlayer
    {
        public sealed class MIDISaveArgs
        {
            public bool SaveCommandsBeforeTranspose;
            public bool ReverseVolume;
            public List<(int AbsoluteTick, (byte Numerator, byte Denominator))> TimeSignatures;
        }

        private readonly Mixer _mixer;
        private readonly Config _config;
        private readonly TimeBarrier _time;
        private Thread _thread;
        private byte[] _songBinary;
        public int VoiceTableOffset { get; private set; } = -1;
        private string[] _voiceTypeCache;
        private Track[] _tracks;
        private ushort _tempo;
        private int _tempoStack;
        private long _elapsedLoops;

        public List<SongEvent>[] Events { get; private set; }
        public long MaxTicks { get; private set; }
        public long ElapsedTicks { get; private set; }
        public bool ShouldFadeOut { get; set; }
        public long NumLoops { get; set; }
        private int _longestTrack;

        public PlayerState State { get; private set; }
        public event SongEndedEvent SongEnded;

        public Player(Mixer mixer, Config config)
        {
            _mixer = mixer;
            _config = config;

            _time = new TimeBarrier(GBA.Utils.AGB_FPS);
        }
        private void CreateThread()
        {
            _thread = new Thread(Tick) { Name = "MP2K Player Tick" };
            _thread.Start();
        }
        private void WaitThread()
        {
            if (_thread != null && (_thread.ThreadState == System.Threading.ThreadState.Running || _thread.ThreadState == System.Threading.ThreadState.WaitSleepJoin))
            {
                _thread.Join();
            }
        }

        private void InitEmulation()
        {
            _tempo = 150;
            _tempoStack = 0;
            _elapsedLoops = 0;
            ElapsedTicks = 0;
            _mixer.ResetFade();
            for (int trackIndex = 0; trackIndex < _tracks.Length; trackIndex++)
            {
                _tracks[trackIndex].Init();
            }
        }
        private void SetTicks()
        {
            MaxTicks = 0;
            bool u = false;
            for (int trackIndex = 0; trackIndex < Events.Length; trackIndex++)
            {
                Events[trackIndex] = Events[trackIndex].OrderBy(e => e.Offset).ToList();
                List<SongEvent> evs = Events[trackIndex];
                Track track = _tracks[trackIndex];
                track.Init();
                ElapsedTicks = 0;
                while (true)
                {
                    SongEvent e = evs.Single(ev => ev.Offset == track.DataOffset);
                    if (track.CallStackDepth == 0 && e.Ticks.Count > 0)
                    {
                        break;
                    }
                    else
                    {
                        e.Ticks.Add(ElapsedTicks);
                        ExecuteNext(track, ref u);
                        if (track.Stopped)
                        {
                            break;
                        }
                        else
                        {
                            ElapsedTicks += track.Rest;
                            track.Rest = 0;
                        }
                    }
                }
                if (ElapsedTicks > MaxTicks)
                {
                    _longestTrack = trackIndex;
                    MaxTicks = ElapsedTicks;
                }
                track.StopAllChannels();
            }
        }
        public void LoadSong(long index)
        {
            SongEntry entry = _config.Reader.ReadObject<SongEntry>(_config.SongTableOffsets[0] + (index * 8));
            LoadSong(_config.ROM, _config.Reader, entry.HeaderOffset - GBA.Utils.CartridgeOffset);
        }
        public void LoadSong(byte[] songBinary, EndianBinaryReader songBinaryReader, long songHeaderOffset)
        {
            _songBinary = songBinary;
            if (_tracks != null)
            {
                for (int i = 0; i < _tracks.Length; i++)
                {
                    _tracks[i].StopAllChannels();
                }
                _tracks = null;
            }
            Events = null;
            SongHeader header = songBinaryReader.ReadObject<SongHeader>(songHeaderOffset);
            int oldVoiceTableOffset = VoiceTableOffset;
            VoiceTableOffset = header.VoiceTableOffset - GBA.Utils.CartridgeOffset;
            if (oldVoiceTableOffset != VoiceTableOffset)
            {
                _voiceTypeCache = new string[byte.MaxValue + 1];
            }
            _tracks = new Track[header.NumTracks];
            Events = new List<SongEvent>[header.NumTracks];
            for (byte trackIndex = 0; trackIndex < header.NumTracks; trackIndex++)
            {
                int trackStart = header.TrackOffsets[trackIndex] - GBA.Utils.CartridgeOffset;
                _tracks[trackIndex] = new Track(trackIndex, trackStart);
                Events[trackIndex] = new List<SongEvent>();
                bool EventExists(long offset)
                {
                    return Events[trackIndex].Any(e => e.Offset == offset);
                }

                byte runCmd = 0, prevKey = 0, prevVelocity = 0x7F;
                int callStackDepth = 0;
                AddEvents(trackStart);
                void AddEvents(long startOffset)
                {
                    songBinaryReader.BaseStream.Position = startOffset;
                    bool cont = true;
                    while (cont)
                    {
                        long offset = songBinaryReader.BaseStream.Position;
                        void AddEvent(ICommand command)
                        {
                            Events[trackIndex].Add(new SongEvent(offset, command));
                        }
                        void EmulateNote(byte key, byte velocity, byte addedDuration)
                        {
                            prevKey = key;
                            prevVelocity = velocity;
                            if (!EventExists(offset))
                            {
                                AddEvent(new NoteCommand
                                {
                                    Key = key,
                                    Velocity = velocity,
                                    Duration = runCmd == 0xCF ? -1 : (Utils.RestTable[runCmd - 0xCF] + addedDuration)
                                });
                            }
                        }

                        byte cmd = songBinaryReader.ReadByte();
                        if (cmd >= 0xBD) // Commands that work within running status
                        {
                            runCmd = cmd;
                        }

                        #region TIE & Notes

                        if (runCmd >= 0xCF && cmd <= 0x7F) // Within running status
                        {
                            byte velocity, addedDuration;
                            byte[] peek = songBinaryReader.PeekBytes(2);
                            if (peek[0] > 0x7F)
                            {
                                velocity = prevVelocity;
                                addedDuration = 0;
                            }
                            else if (peek[1] > 3)
                            {
                                velocity = songBinaryReader.ReadByte();
                                addedDuration = 0;
                            }
                            else
                            {
                                velocity = songBinaryReader.ReadByte();
                                addedDuration = songBinaryReader.ReadByte();
                            }
                            EmulateNote(cmd, velocity, addedDuration);
                        }
                        else if (cmd >= 0xCF)
                        {
                            byte key, velocity, addedDuration;
                            byte[] peek = songBinaryReader.PeekBytes(3);
                            if (peek[0] > 0x7F)
                            {
                                key = prevKey;
                                velocity = prevVelocity;
                                addedDuration = 0;
                            }
                            else if (peek[1] > 0x7F)
                            {
                                key = songBinaryReader.ReadByte();
                                velocity = prevVelocity;
                                addedDuration = 0;
                            }
                            // TIE (0xCF) cannot have an added duration so it needs to stop here
                            else if (cmd == 0xCF || peek[2] > 3)
                            {
                                key = songBinaryReader.ReadByte();
                                velocity = songBinaryReader.ReadByte();
                                addedDuration = 0;
                            }
                            else
                            {
                                key = songBinaryReader.ReadByte();
                                velocity = songBinaryReader.ReadByte();
                                addedDuration = songBinaryReader.ReadByte();
                            }
                            EmulateNote(key, velocity, addedDuration);
                        }

                        #endregion

                        #region Rests

                        else if (cmd >= 0x80 && cmd <= 0xB0)
                        {
                            if (!EventExists(offset))
                            {
                                AddEvent(new RestCommand { Rest = Utils.RestTable[cmd - 0x80] });
                            }
                        }

                        #endregion

                        #region Commands

                        else if (runCmd < 0xCF && cmd <= 0x7F)
                        {
                            switch (runCmd)
                            {
                                case 0xBD:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new VoiceCommand { Voice = cmd });
                                    }
                                    break;
                                }
                                case 0xBE:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new VolumeCommand { Volume = cmd });
                                    }
                                    break;
                                }
                                case 0xBF:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new PanpotCommand { Panpot = (sbyte)(cmd - 0x40) });
                                    }
                                    break;
                                }
                                case 0xC0:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new PitchBendCommand { Bend = (sbyte)(cmd - 0x40) });
                                    }
                                    break;
                                }
                                case 0xC1:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new PitchBendRangeCommand { Range = cmd });
                                    }
                                    break;
                                }
                                case 0xC2:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LFOSpeedCommand { Speed = cmd });
                                    }
                                    break;
                                }
                                case 0xC3:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LFODelayCommand { Delay = cmd });
                                    }
                                    break;
                                }
                                case 0xC4:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LFODepthCommand { Depth = cmd });
                                    }
                                    break;
                                }
                                case 0xC5:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LFOTypeCommand { Type = (LFOType)cmd });
                                    }
                                    break;
                                }
                                case 0xC8:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new TuneCommand { Tune = (sbyte)(cmd - 0x40) });
                                    }
                                    break;
                                }
                                case 0xCD:
                                {
                                    byte arg = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LibraryCommand { Command = cmd, Argument = arg });
                                    }
                                    break;
                                }
                                case 0xCE:
                                {
                                    prevKey = cmd;
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new EndOfTieCommand { Key = cmd });
                                    }
                                    break;
                                }
                                default: throw new Exception(string.Format(Strings.ErrorMP2KInvalidRunningStatusCommand, trackIndex, offset, runCmd));
                            }
                        }
                        else if (cmd > 0xB0 && cmd < 0xCF)
                        {
                            switch (cmd)
                            {
                                case 0xB1:
                                case 0xB6:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new FinishCommand { Prev = cmd == 0xB6 });
                                    }
                                    cont = false;
                                    break;
                                }
                                case 0xB2:
                                {
                                    int jumpOffset = songBinaryReader.ReadInt32() - GBA.Utils.CartridgeOffset;
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new JumpCommand { Offset = jumpOffset });
                                        if (!EventExists(jumpOffset))
                                        {
                                            AddEvents(jumpOffset);
                                        }
                                    }
                                    cont = false;
                                    break;
                                }
                                case 0xB3:
                                {
                                    int callOffset = songBinaryReader.ReadInt32() - GBA.Utils.CartridgeOffset;
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new CallCommand { Offset = callOffset });
                                    }
                                    if (callStackDepth < 3)
                                    {
                                        long backup = songBinaryReader.BaseStream.Position;
                                        callStackDepth++;
                                        AddEvents(callOffset);
                                        songBinaryReader.BaseStream.Position = backup;
                                    }
                                    else
                                    {
                                        throw new Exception(string.Format(Strings.ErrorMP2KSDATNestedCalls, trackIndex));
                                    }
                                    break;
                                }
                                case 0xB4:
                                {
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new ReturnCommand());
                                    }
                                    if (callStackDepth != 0)
                                    {
                                        cont = false;
                                        callStackDepth--;
                                    }
                                    break;
                                }
                                /*case 0xB5: // TODO: Logic so this isn't an infinite loop
                                {
                                    byte times = songBinaryReader.ReadByte();
                                    int repeatOffset = songBinaryReader.ReadInt32() - GBA.Utils.CartridgeOffset;
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new RepeatCommand { Times = times, Offset = repeatOffset });
                                    }
                                    break;
                                }*/
                                case 0xB9:
                                {
                                    byte op = songBinaryReader.ReadByte();
                                    byte address = songBinaryReader.ReadByte();
                                    byte data = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new MemoryAccessCommand { Operator = op, Address = address, Data = data });
                                    }
                                    break;
                                }
                                case 0xBA:
                                {
                                    byte priority = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new PriorityCommand { Priority = priority });
                                    }
                                    break;
                                }
                                case 0xBB:
                                {
                                    byte tempoArg = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new TempoCommand { Tempo = (ushort)(tempoArg * 2) });
                                    }
                                    break;
                                }
                                case 0xBC:
                                {
                                    sbyte transpose = songBinaryReader.ReadSByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new TransposeCommand { Transpose = transpose });
                                    }
                                    break;
                                }
                                // Commands that work within running status:
                                case 0xBD:
                                {
                                    byte voice = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new VoiceCommand { Voice = voice });
                                    }
                                    break;
                                }
                                case 0xBE:
                                {
                                    byte volume = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new VolumeCommand { Volume = volume });
                                    }
                                    break;
                                }
                                case 0xBF:
                                {
                                    byte panArg = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new PanpotCommand { Panpot = (sbyte)(panArg - 0x40) });
                                    }
                                    break;
                                }
                                case 0xC0:
                                {
                                    byte bendArg = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new PitchBendCommand { Bend = (sbyte)(bendArg - 0x40) });
                                    }
                                    break;
                                }
                                case 0xC1:
                                {
                                    byte range = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new PitchBendRangeCommand { Range = range });
                                    }
                                    break;
                                }
                                case 0xC2:
                                {
                                    byte speed = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LFOSpeedCommand { Speed = speed });
                                    }
                                    break;
                                }
                                case 0xC3:
                                {
                                    byte delay = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LFODelayCommand { Delay = delay });
                                    }
                                    break;
                                }
                                case 0xC4:
                                {
                                    byte depth = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LFODepthCommand { Depth = depth });
                                    }
                                    break;
                                }
                                case 0xC5:
                                {
                                    byte type = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LFOTypeCommand { Type = (LFOType)type });
                                    }
                                    break;
                                }
                                case 0xC8:
                                {
                                    byte tuneArg = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new TuneCommand { Tune = (sbyte)(tuneArg - 0x40) });
                                    }
                                    break;
                                }
                                case 0xCD:
                                {
                                    byte command = songBinaryReader.ReadByte();
                                    byte arg = songBinaryReader.ReadByte();
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new LibraryCommand { Command = command, Argument = arg });
                                    }
                                    break;
                                }
                                case 0xCE:
                                {
                                    int key = songBinaryReader.PeekByte() <= 0x7F ? (prevKey = songBinaryReader.ReadByte()) : -1;
                                    if (!EventExists(offset))
                                    {
                                        AddEvent(new EndOfTieCommand { Key = key });
                                    }
                                    break;
                                }
                                default: throw new Exception(string.Format(Strings.ErrorAlphaDreamDSEMP2KSDATInvalidCommand, trackIndex, offset, cmd));
                            }
                        }

                        #endregion
                    }
                }
            }
            SetTicks();
        }
        public void SetCurrentPosition(long ticks)
        {
            if (Events == null)
            {
                SongEnded?.Invoke();
            }
            else if (State == PlayerState.Playing || State == PlayerState.Paused || State == PlayerState.Stopped)
            {
                if (State == PlayerState.Playing)
                {
                    Pause();
                }
                InitEmulation();
                bool u = false;
                while (true)
                {
                    if (ElapsedTicks == ticks)
                    {
                        goto finish;
                    }
                    else
                    {
                        while (_tempoStack >= 150)
                        {
                            _tempoStack -= 150;
                            for (int trackIndex = 0; trackIndex < _tracks.Length; trackIndex++)
                            {
                                Track track = _tracks[trackIndex];
                                if (!track.Stopped)
                                {
                                    track.Tick();
                                    while (track.Rest == 0 && !track.Stopped)
                                    {
                                        ExecuteNext(track, ref u);
                                    }
                                }
                            }
                            ElapsedTicks++;
                            if (ElapsedTicks == ticks)
                            {
                                goto finish;
                            }
                        }
                        _tempoStack += _tempo;
                    }
                }
            finish:
                for (int i = 0; i < _tracks.Length; i++)
                {
                    _tracks[i].StopAllChannels();
                }
                Pause();
            }
        }
        // TODO: Don't use events, read from rom
        public void SaveAsMIDI(string fileName, MIDISaveArgs args)
        {
            // TODO: FINE vs PREV
            // TODO: https://github.com/Kermalis/VGMusicStudio/issues/36
            // TODO: Nested calls
            // TODO: REPT
            byte baseVolume = 0x7F;
            if (args.ReverseVolume)
            {
                baseVolume = Events.SelectMany(e => e).Where(e => e.Command is VolumeCommand).Select(e => ((VolumeCommand)e.Command).Volume).Max();
                Debug.WriteLine($"Reversing volume back from {baseVolume}.");
            }

            using (var midi = new Sequence(24) { Format = 1 })
            {
                var metaTrack = new Sanford.Multimedia.Midi.Track();
                midi.Add(metaTrack);
                var ts = new TimeSignatureBuilder();
                foreach ((int AbsoluteTick, (byte Numerator, byte Denominator)) e in args.TimeSignatures)
                {
                    ts.Numerator = e.Item2.Numerator;
                    ts.Denominator = e.Item2.Denominator;
                    ts.ClocksPerMetronomeClick = 24;
                    ts.ThirtySecondNotesPerQuarterNote = 8;
                    ts.Build();
                    metaTrack.Insert(e.AbsoluteTick, ts.Result);
                }

                for (int trackIndex = 0; trackIndex < Events.Length; trackIndex++)
                {
                    var track = new Sanford.Multimedia.Midi.Track();
                    midi.Add(track);

                    bool foundTranspose = false;
                    int endOfPattern = 0;
                    long startOfPatternTicks = 0, endOfPatternTicks = 0;
                    sbyte transpose = 0;
                    var playing = new List<NoteCommand>();
                    for (int i = 0; i < Events[trackIndex].Count; i++)
                    {
                        SongEvent e = Events[trackIndex][i];
                        int ticks = (int)(e.Ticks[0] + (endOfPatternTicks - startOfPatternTicks));

                        // Preliminary check for saving events before transpose
                        switch (e.Command)
                        {
                            case TransposeCommand keysh: foundTranspose = true; break;
                            default: // If we should not save before transpose then skip this event
                            {
                                if (!args.SaveCommandsBeforeTranspose && !foundTranspose)
                                {
                                    continue;
                                }
                                break;
                            }
                        }
                        // Now do the event magic...
                        switch (e.Command)
                        {
                            case CallCommand patt:
                            {
                                int callCmd = Events[trackIndex].FindIndex(c => c.Offset == patt.Offset);
                                endOfPattern = i;
                                endOfPatternTicks = e.Ticks[0];
                                i = callCmd - 1; // -1 for incoming ++
                                startOfPatternTicks = Events[trackIndex][callCmd].Ticks[0];
                                break;
                            }
                            case EndOfTieCommand eot:
                            {
                                NoteCommand nc = eot.Key == -1 ? playing.LastOrDefault() : playing.LastOrDefault(no => no.Key == eot.Key);
                                if (nc != null)
                                {
                                    int key = nc.Key + transpose;
                                    if (key < 0)
                                    {
                                        key = 0;
                                    }
                                    else if (key > 0x7F)
                                    {
                                        key = 0x7F;
                                    }
                                    track.Insert(ticks, new ChannelMessage(ChannelCommand.NoteOff, trackIndex, key));
                                    playing.Remove(nc);
                                }
                                break;
                            }
                            case FinishCommand _:
                            {
                                // If the track is not only the finish command, place the finish command at the correct tick
                                if (track.Count > 1)
                                {
                                    track.EndOfTrackOffset = (int)(e.Ticks[0] - track.GetMidiEvent(track.Count - 2).AbsoluteTicks);
                                }
                                goto endOfTrack;
                            }
                            case JumpCommand goTo:
                            {
                                if (trackIndex == 0)
                                {
                                    int jumpCmd = Events[trackIndex].FindIndex(c => c.Offset == goTo.Offset);
                                    metaTrack.Insert((int)Events[trackIndex][jumpCmd].Ticks[0], new MetaMessage(MetaType.Marker, new byte[] { (byte)'[' }));
                                    metaTrack.Insert(ticks, new MetaMessage(MetaType.Marker, new byte[] { (byte)']' }));
                                }
                                break;
                            }
                            case LFODelayCommand lfodl:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 26, lfodl.Delay));
                                break;
                            }
                            case LFODepthCommand mod:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, (int)ControllerType.ModulationWheel, mod.Depth));
                                break;
                            }
                            case LFOSpeedCommand lfos:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 21, lfos.Speed));
                                break;
                            }
                            case LFOTypeCommand modt:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 22, (byte)modt.Type));
                                break;
                            }
                            case LibraryCommand xcmd:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 30, xcmd.Command));
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 29, xcmd.Argument));
                                break;
                            }
                            case MemoryAccessCommand memacc:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 13, memacc.Operator));
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 14, memacc.Address));
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 12, memacc.Data));
                                break;
                            }
                            case NoteCommand note:
                            {
                                int key = note.Key + transpose;
                                if (key < 0)
                                {
                                    key = 0;
                                }
                                else if (key > 0x7F)
                                {
                                    key = 0x7F;
                                }
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.NoteOn, trackIndex, key, note.Velocity));
                                if (note.Duration != -1)
                                {
                                    track.Insert(ticks + note.Duration, new ChannelMessage(ChannelCommand.NoteOff, trackIndex, key));
                                }
                                else
                                {
                                    playing.Add(note);
                                }
                                break;
                            }
                            case PanpotCommand pan:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, (int)ControllerType.Pan, pan.Panpot + 0x40));
                                break;
                            }
                            case PitchBendCommand bend:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.PitchWheel, trackIndex, 0, bend.Bend + 0x40));
                                break;
                            }
                            case PitchBendRangeCommand bendr:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 20, bendr.Range));
                                break;
                            }
                            case PriorityCommand prio:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, (int)ControllerType.VolumeFine, prio.Priority));
                                break;
                            }
                            case ReturnCommand _:
                            {
                                if (endOfPattern != 0)
                                {
                                    i = endOfPattern;
                                    endOfPattern = 0;
                                    startOfPatternTicks = endOfPatternTicks = 0;
                                }
                                break;
                            }
                            case TempoCommand tempo:
                            {
                                var change = new TempoChangeBuilder { Tempo = 60000000 / tempo.Tempo };
                                change.Build();
                                metaTrack.Insert(ticks, change.Result);
                                break;
                            }
                            case TransposeCommand keysh:
                            {
                                transpose = keysh.Transpose;
                                break;
                            }
                            case TuneCommand tune:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, 24, tune.Tune));
                                break;
                            }
                            case VoiceCommand voice:
                            {
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.ProgramChange, trackIndex, voice.Voice));
                                break;
                            }
                            case VolumeCommand vol:
                            {
                                double d = baseVolume / (double)0x7F;
                                int volume = (int)(vol.Volume / d);
                                // If there are rounding errors, fix them (happens if baseVolume is not 127 and baseVolume is not vol.Volume)
                                if (volume * baseVolume / 0x7F == vol.Volume - 1)
                                {
                                    volume++;
                                }
                                track.Insert(ticks, new ChannelMessage(ChannelCommand.Controller, trackIndex, (int)ControllerType.Volume, volume));
                                break;
                            }
                        }
                    }
                endOfTrack:;
                }
                midi.Save(fileName);
            }
        }
        public void Play()
        {
            if (Events == null)
            {
                SongEnded?.Invoke();
            }
            else if (State == PlayerState.Playing || State == PlayerState.Paused || State == PlayerState.Stopped)
            {
                Stop();
                InitEmulation();
                State = PlayerState.Playing;
                CreateThread();
            }
        }
        public void Pause()
        {
            if (State == PlayerState.Playing)
            {
                State = PlayerState.Paused;
                WaitThread();
            }
            else if (State == PlayerState.Paused || State == PlayerState.Stopped)
            {
                State = PlayerState.Playing;
                CreateThread();
            }
        }
        public void Stop()
        {
            if (State == PlayerState.Playing || State == PlayerState.Paused)
            {
                State = PlayerState.Stopped;
                WaitThread();
            }
        }
        public void Record(string fileName)
        {
            _mixer.CreateWaveWriter(fileName);
            InitEmulation();
            State = PlayerState.Recording;
            CreateThread();
            WaitThread();
            _mixer.CloseWaveWriter();
        }
        public void Dispose()
        {
            if (State == PlayerState.Playing || State == PlayerState.Paused || State == PlayerState.Stopped)
            {
                State = PlayerState.ShutDown;
                WaitThread();
            }
        }
        public void GetSongState(UI.SongInfoControl.SongInfo info)
        {
            info.Tempo = _tempo;
            for (int trackIndex = 0; trackIndex < _tracks.Length; trackIndex++)
            {
                Track track = _tracks[trackIndex];
                UI.SongInfoControl.SongInfo.Track tin = info.Tracks[trackIndex];
                tin.Position = track.DataOffset;
                tin.Rest = track.Rest;
                tin.Voice = track.Voice;
                tin.LFO = track.LFODepth;
                if (_voiceTypeCache[track.Voice] == null)
                {
                    byte t = _config.ROM[VoiceTableOffset + (track.Voice * 0xC)];
                    if (t == (byte)VoiceFlags.KeySplit)
                    {
                        _voiceTypeCache[track.Voice] = "Key Split";
                    }
                    else if (t == (byte)VoiceFlags.Drum)
                    {
                        _voiceTypeCache[track.Voice] = "Drum";
                    }
                    else
                    {
                        switch ((VoiceType)(t & 0x7))
                        {
                            case VoiceType.PCM8: _voiceTypeCache[track.Voice] = "PCM8"; break;
                            case VoiceType.Square1: _voiceTypeCache[track.Voice] = "Square 1"; break;
                            case VoiceType.Square2: _voiceTypeCache[track.Voice] = "Square 2"; break;
                            case VoiceType.PCM4: _voiceTypeCache[track.Voice] = "PCM4"; break;
                            case VoiceType.Noise: _voiceTypeCache[track.Voice] = "Noise"; break;
                            case VoiceType.Invalid5: _voiceTypeCache[track.Voice] = "Invalid 5"; break;
                            case VoiceType.Invalid6: _voiceTypeCache[track.Voice] = "Invalid 6"; break;
                            case VoiceType.Invalid7: _voiceTypeCache[track.Voice] = "Invalid 7"; break;
                        }
                    }
                }
                tin.Type = _voiceTypeCache[track.Voice];
                tin.Volume = track.GetVolume();
                tin.PitchBend = track.GetPitch();
                tin.Panpot = track.GetPanpot();

                Channel[] channels = track.Channels.ToArray();
                if (channels.Length == 0)
                {
                    tin.Keys[0] = byte.MaxValue;
                    tin.LeftVolume = 0f;
                    tin.RightVolume = 0f;
                }
                else
                {
                    int numKeys = 0;
                    float left = 0f;
                    float right = 0f;
                    for (int j = 0; j < channels.Length; j++)
                    {
                        Channel c = channels[j];
                        if (c.State < EnvelopeState.Releasing)
                        {
                            tin.Keys[numKeys++] = c.Note.OriginalKey;
                        }
                        ChannelVolume vol = c.GetVolume();
                        if (vol.LeftVol > left)
                        {
                            left = vol.LeftVol;
                        }
                        if (vol.RightVol > right)
                        {
                            right = vol.RightVol;
                        }
                    }
                    tin.Keys[numKeys] = byte.MaxValue; // There's no way for numKeys to be after the last index in the array
                    tin.LeftVolume = left;
                    tin.RightVolume = right;
                }
            }
        }

        // TODO: Don't use config.Reader (Or make ReadObjectCached(offset))
        private void PlayNote(Track track, byte key, byte velocity, byte addedDuration)
        {
            int k = key + track.Transpose;
            if (k < 0)
            {
                k = 0;
            }
            else if (k > 0x7F)
            {
                k = 0x7F;
            }
            key = (byte)k;
            track.PrevKey = key;
            track.PrevVelocity = velocity;
            if (track.Ready)
            {
                bool fromDrum = false;
                int offset = VoiceTableOffset + (track.Voice * 12);
                while (true)
                {
                    VoiceEntry v = _config.Reader.ReadObject<VoiceEntry>(offset);
                    if (v.Type == (int)VoiceFlags.KeySplit)
                    {
                        fromDrum = false; // In case there is a multi within a drum
                        byte inst = _config.Reader.ReadByte(v.Int8 - GBA.Utils.CartridgeOffset + key);
                        offset = v.Int4 - GBA.Utils.CartridgeOffset + (inst * 12);
                    }
                    else if (v.Type == (int)VoiceFlags.Drum)
                    {
                        fromDrum = true;
                        offset = v.Int4 - GBA.Utils.CartridgeOffset + (key * 12);
                    }
                    else
                    {
                        var note = new Note
                        {
                            Duration = track.RunCmd == 0xCF ? -1 : (Utils.RestTable[track.RunCmd - 0xCF] + addedDuration),
                            Velocity = velocity,
                            OriginalKey = key,
                            Key = fromDrum ? v.RootKey : key
                        };
                        var type = (VoiceType)(v.Type & 0x7);
                        int instPan = v.Pan;
                        instPan = (instPan & 0x80) != 0 ? instPan - 0xC0 : 0;
                        switch (type)
                        {
                            case VoiceType.PCM8:
                            {
                                bool bFixed = (v.Type & (int)VoiceFlags.Fixed) != 0;
                                bool bCompressed = _config.HasPokemonCompression && ((v.Type & (int)VoiceFlags.Compressed) != 0);
                                _mixer.AllocPCM8Channel(track, v.ADSR, note,
                                    track.GetVolume(), track.GetPanpot(), instPan, track.GetPitch(),
                                    bFixed, bCompressed, v.Int4 - GBA.Utils.CartridgeOffset);
                                return;
                            }
                            case VoiceType.Square1:
                            case VoiceType.Square2:
                            {
                                _mixer.AllocPSGChannel(track, v.ADSR, note,
                                    track.GetVolume(), track.GetPanpot(), instPan, track.GetPitch(),
                                    type, (SquarePattern)v.Int4);
                                return;
                            }
                            case VoiceType.PCM4:
                            {
                                _mixer.AllocPSGChannel(track, v.ADSR, note,
                                    track.GetVolume(), track.GetPanpot(), instPan, track.GetPitch(),
                                    type, v.Int4 - GBA.Utils.CartridgeOffset);
                                return;
                            }
                            case VoiceType.Noise:
                            {
                                _mixer.AllocPSGChannel(track, v.ADSR, note,
                                    track.GetVolume(), track.GetPanpot(), instPan, track.GetPitch(),
                                    type, (NoisePattern)v.Int4);
                                return;
                            }
                        }
                    }
                }
            }
        }
        private void ExecuteNext(Track track, ref bool update)
        {
            byte cmd = _songBinary[track.DataOffset++];
            if (cmd >= 0xBD) // Commands that work within running status
            {
                track.RunCmd = cmd;
            }
            if (track.RunCmd >= 0xCF && cmd <= 0x7F) // Within running status
            {
                byte peek0 = _songBinary[track.DataOffset];
                byte peek1 = _songBinary[track.DataOffset + 1];
                byte velocity, addedDuration;
                if (peek0 > 0x7F)
                {
                    velocity = track.PrevVelocity;
                    addedDuration = 0;
                }
                else if (peek1 > 3)
                {
                    track.DataOffset++;
                    velocity = peek0;
                    addedDuration = 0;
                }
                else
                {
                    track.DataOffset += 2;
                    velocity = peek0;
                    addedDuration = peek1;
                }
                PlayNote(track, cmd, velocity, addedDuration);
            }
            else if (cmd >= 0xCF)
            {
                byte peek0 = _songBinary[track.DataOffset];
                byte peek1 = _songBinary[track.DataOffset + 1];
                byte peek2 = _songBinary[track.DataOffset + 2];
                byte key, velocity, addedDuration;
                if (peek0 > 0x7F)
                {
                    key = track.PrevKey;
                    velocity = track.PrevVelocity;
                    addedDuration = 0;
                }
                else if (peek1 > 0x7F)
                {
                    track.DataOffset++;
                    key = peek0;
                    velocity = track.PrevVelocity;
                    addedDuration = 0;
                }
                else if (cmd == 0xCF || peek2 > 3)
                {
                    track.DataOffset += 2;
                    key = peek0;
                    velocity = peek1;
                    addedDuration = 0;
                }
                else
                {
                    track.DataOffset += 3;
                    key = peek0;
                    velocity = peek1;
                    addedDuration = peek2;
                }
                PlayNote(track, key, velocity, addedDuration);
            }
            else if (cmd >= 0x80 && cmd <= 0xB0)
            {
                track.Rest = Utils.RestTable[cmd - 0x80];
            }
            else if (track.RunCmd < 0xCF && cmd <= 0x7F)
            {
                switch (track.RunCmd)
                {
                    case 0xBD:
                    {
                        track.Voice = cmd;
                        //track.Ready = true; // This is unnecessary because if we're in running status of a voice command, then Ready was already set
                        break;
                    }
                    case 0xBE:
                    {
                        track.Volume = cmd;
                        update = true;
                        break;
                    }
                    case 0xBF:
                    {
                        track.Panpot = (sbyte)(cmd - 0x40);
                        update = true;
                        break;
                    }
                    case 0xC0:
                    {
                        track.PitchBend = (sbyte)(cmd - 0x40);
                        update = true;
                        break;
                    }
                    case 0xC1:
                    {
                        track.PitchBendRange = cmd;
                        update = true;
                        break;
                    }
                    case 0xC2:
                    {
                        track.LFOSpeed = cmd;
                        track.LFOPhase = 0;
                        track.LFODelayCount = 0;
                        update = true;
                        break;
                    }
                    case 0xC3:
                    {
                        track.LFODelay = cmd;
                        track.LFOPhase = 0;
                        track.LFODelayCount = 0;
                        update = true;
                        break;
                    }
                    case 0xC4:
                    {
                        track.LFODepth = cmd;
                        update = true;
                        break;
                    }
                    case 0xC5:
                    {
                        track.LFOType = (LFOType)cmd;
                        update = true;
                        break;
                    }
                    case 0xC8:
                    {
                        track.Tune = (sbyte)(cmd - 0x40);
                        update = true;
                        break;
                    }
                    case 0xCD:
                    {
                        track.DataOffset++;
                        break;
                    }
                    case 0xCE:
                    {
                        track.PrevKey = cmd;
                        int k = cmd + track.Transpose;
                        if (k < 0)
                        {
                            k = 0;
                        }
                        else if (k > 0x7F)
                        {
                            k = 0x7F;
                        }
                        track.ReleaseChannels(k);
                        break;
                    }
                    default: throw new Exception(string.Format(Strings.ErrorMP2KInvalidRunningStatusCommand, track.Index, track.DataOffset, track.RunCmd));
                }
            }
            else if (cmd > 0xB0 && cmd < 0xCF)
            {
                switch (cmd)
                {
                    case 0xB1:
                    case 0xB6:
                    {
                        track.Stopped = true;
                        //track.ReleaseAllTieingChannels(); // Necessary?
                        break;
                    }
                    case 0xB2:
                    {
                        track.DataOffset = (_songBinary[track.DataOffset++] | (_songBinary[track.DataOffset++] << 8) | (_songBinary[track.DataOffset++] << 16) | (_songBinary[track.DataOffset++] << 24)) - GBA.Utils.CartridgeOffset;
                        break;
                    }
                    case 0xB3:
                    {
                        if (track.CallStackDepth < 3)
                        {
                            int callOffset = (_songBinary[track.DataOffset++] | (_songBinary[track.DataOffset++] << 8) | (_songBinary[track.DataOffset++] << 16) | (_songBinary[track.DataOffset++] << 24)) - GBA.Utils.CartridgeOffset;
                            track.CallStack[track.CallStackDepth] = track.DataOffset;
                            track.CallStackDepth++;
                            track.DataOffset = callOffset;
                        }
                        else
                        {
                            throw new Exception(string.Format(Strings.ErrorMP2KSDATNestedCalls, track.Index));
                        }
                        break;
                    }
                    case 0xB4:
                    {
                        if (track.CallStackDepth != 0)
                        {
                            track.CallStackDepth--;
                            track.DataOffset = track.CallStack[track.CallStackDepth];
                        }
                        break;
                    }
                    /*case 0xB5: // TODO: Logic so this isn't an infinite loop
                    {
                        byte times = config.Reader.ReadByte();
                        int repeatOffset = config.Reader.ReadInt32() - GBA.Utils.CartridgeOffset;
                        if (!EventExists(offset))
                        {
                            AddEvent(new RepeatCommand { Times = times, Offset = repeatOffset });
                        }
                        break;
                    }*/
                    case 0xB9:
                    {
                        track.DataOffset += 3;
                        break;
                    }
                    case 0xBA:
                    {
                        track.Priority = _songBinary[track.DataOffset++];
                        break;
                    }
                    case 0xBB:
                    {
                        _tempo = (ushort)(_songBinary[track.DataOffset++] * 2);
                        break;
                    }
                    case 0xBC:
                    {
                        track.Transpose = (sbyte)_songBinary[track.DataOffset++];
                        break;
                    }
                    // Commands that work within running status:
                    case 0xBD:
                    {
                        track.Voice = _songBinary[track.DataOffset++];
                        track.Ready = true;
                        break;
                    }
                    case 0xBE:
                    {
                        track.Volume = _songBinary[track.DataOffset++];
                        update = true;
                        break;
                    }
                    case 0xBF:
                    {
                        track.Panpot = (sbyte)(_songBinary[track.DataOffset++] - 0x40);
                        update = true;
                        break;
                    }
                    case 0xC0:
                    {
                        track.PitchBend = (sbyte)(_songBinary[track.DataOffset++] - 0x40);
                        update = true;
                        break;
                    }
                    case 0xC1:
                    {
                        track.PitchBendRange = _songBinary[track.DataOffset++];
                        update = true;
                        break;
                    }
                    case 0xC2:
                    {
                        track.LFOSpeed = _songBinary[track.DataOffset++];
                        track.LFOPhase = 0;
                        track.LFODelayCount = 0;
                        update = true;
                        break;
                    }
                    case 0xC3:
                    {
                        track.LFODelay = _songBinary[track.DataOffset++];
                        track.LFOPhase = 0;
                        track.LFODelayCount = 0;
                        update = true;
                        break;
                    }
                    case 0xC4:
                    {
                        track.LFODepth = _songBinary[track.DataOffset++];
                        update = true;
                        break;
                    }
                    case 0xC5:
                    {
                        track.LFOType = (LFOType)_songBinary[track.DataOffset++];
                        update = true;
                        break;
                    }
                    case 0xC8:
                    {
                        track.Tune = (sbyte)(_songBinary[track.DataOffset++] - 0x40);
                        update = true;
                        break;
                    }
                    case 0xCD:
                    {
                        track.DataOffset += 2;
                        break;
                    }
                    case 0xCE:
                    {
                        byte peek = _songBinary[track.DataOffset];
                        if (peek > 0x7F)
                        {
                            track.ReleaseChannels(track.PrevKey);
                        }
                        else
                        {
                            track.DataOffset++;
                            track.PrevKey = peek;
                            int k = peek + track.Transpose;
                            if (k < 0)
                            {
                                k = 0;
                            }
                            else if (k > 0x7F)
                            {
                                k = 0x7F;
                            }
                            track.ReleaseChannels(k);
                        }
                        break;
                    }
                    default: throw new Exception(string.Format(Strings.ErrorAlphaDreamDSEMP2KSDATInvalidCommand, track.Index, track.DataOffset, cmd));
                }
            }
        }

        private void Tick()
        {
            _time.Start();
            while (true)
            {
                PlayerState state = State;
                bool playing = state == PlayerState.Playing;
                bool recording = state == PlayerState.Recording;
                if (!playing && !recording)
                {
                    goto stop;
                }

                void MixerProcess()
                {
                    _mixer.Process(playing, recording);
                }

                while (_tempoStack >= 150)
                {
                    _tempoStack -= 150;
                    bool allDone = true;
                    for (int trackIndex = 0; trackIndex < _tracks.Length; trackIndex++)
                    {
                        Track track = _tracks[trackIndex];
                        track.Tick();
                        bool update = false;
                        while (track.Rest == 0 && !track.Stopped)
                        {
                            ExecuteNext(track, ref update);
                        }
                        if (trackIndex == _longestTrack)
                        {
                            if (ElapsedTicks == MaxTicks)
                            {
                                if (!track.Stopped)
                                {
                                    List<SongEvent> evs = Events[trackIndex];
                                    for (int i = 0; i < evs.Count; i++)
                                    {
                                        SongEvent ev = evs[i];
                                        if (ev.Offset == track.DataOffset)
                                        {
                                            ElapsedTicks = ev.Ticks[0] - track.Rest;
                                            break;
                                        }
                                    }
                                    _elapsedLoops++;
                                    if (ShouldFadeOut && !_mixer.IsFading() && _elapsedLoops > NumLoops)
                                    {
                                        _mixer.BeginFadeOut();
                                    }
                                }
                            }
                            else
                            {
                                ElapsedTicks++;
                            }
                        }
                        if (!track.Stopped)
                        {
                            allDone = false;
                        }
                        if (track.Channels.Count > 0)
                        {
                            allDone = false;
                            if (update || track.LFODepth > 0)
                            {
                                track.UpdateChannels();
                            }
                        }
                    }
                    if (_mixer.IsFadeDone())
                    {
                        allDone = true;
                    }
                    if (allDone)
                    {
                        MixerProcess();
                        State = PlayerState.Stopped;
                        SongEnded?.Invoke();
                    }
                }
                _tempoStack += _tempo;
                MixerProcess();
                if (playing)
                {
                    _time.Wait();
                }
            }
        stop:
            _time.Stop();
        }
    }
}
