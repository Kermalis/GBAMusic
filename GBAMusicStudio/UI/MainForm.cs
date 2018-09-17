﻿using GBAMusicStudio.Core;
using GBAMusicStudio.MIDI;
using GBAMusicStudio.Properties;
using GBAMusicStudio.Util;
using Microsoft.WindowsAPICodePack.Taskbar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace GBAMusicStudio.UI
{
    [DesignerCategory("")]
    class MainForm : Form
    {
        bool stopUI = false, drag = false;
        TrackEditor trackEditor; VoiceTableEditor voiceTableEditor;
        List<sbyte> pianoNotes = new List<sbyte>();
        public readonly bool[] PianoTracks = new bool[16];

        readonly int iWidth = 528, iHeight = 800 + 25; // +25 for menustrip (24) and splitcontainer separator (1)
        readonly float sfWidth = 2.35f; // Song combobox and volumebar width
        readonly float spfHeight = 5.5f; // Split panel 1 height

        IContainer components;
        MenuStrip mainMenu;
        ToolStripMenuItem fileToolStripMenuItem, openROMToolStripMenuItem, openMIDIToolStripMenuItem, openASMToolStripMenuItem, configToolStripMenuItem,
            dataToolStripMenuItem, teToolStripMenuItem, vteToolStripMenuItem, eSf2ToolStripMenuItem, eASMToolStripMenuItem, eMIDIToolStripMenuItem;
        Timer timer;
        readonly object timerLock = new object();
        ThemedNumeric songNumerical, tableNumerical;
        ThemedButton playButton, stopButton, pauseButton;
        ThemedLabel creatorLabel, gameLabel, codeLabel;
        SplitContainer splitContainer;
        PianoControl piano;
        ColorSlider positionBar, volumeBar;
        TrackInfoControl trackInfo;
        ImageComboBox.ImageComboBox songsComboBox;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
        public MainForm()
        {
            components = new Container();

            // Main Menu
            openROMToolStripMenuItem = new ToolStripMenuItem { Text = "Apri ROM", ShortcutKeys = Keys.Control | Keys.O };
            openROMToolStripMenuItem.Click += OpenROM;

            openMIDIToolStripMenuItem = new ToolStripMenuItem { Text = "Apri File MIDI", Enabled = false, ShortcutKeys = Keys.Control | Keys.M };
            openMIDIToolStripMenuItem.Click += OpenMIDIConverter;

            openASMToolStripMenuItem = new ToolStripMenuItem { Text = "Apri File ASM", Enabled = false, ShortcutKeys = Keys.Control | Keys.Shift | Keys.M };
            openASMToolStripMenuItem.Click += OpenAssembler;

            configToolStripMenuItem = new ToolStripMenuItem { Text = "Ricarica Config", ShortcutKeys = Keys.Control | Keys.R };
            configToolStripMenuItem.Click += ReloadConfig;

            fileToolStripMenuItem = new ToolStripMenuItem { Text = "File" };
            fileToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { openROMToolStripMenuItem, openMIDIToolStripMenuItem, openASMToolStripMenuItem, configToolStripMenuItem });


            teToolStripMenuItem = new ToolStripMenuItem { Text = "Editor Traccia", Enabled = false, ShortcutKeys = Keys.Control | Keys.T };
            teToolStripMenuItem.Click += OpenTrackEditor;

            vteToolStripMenuItem = new ToolStripMenuItem { Text = "Editor VoiceTable", Enabled = false, ShortcutKeys = Keys.Control | Keys.V };
            vteToolStripMenuItem.Click += OpenVoiceTableEditor;

            eSf2ToolStripMenuItem = new ToolStripMenuItem { Text = "Esporta Voicetable In SF2", Enabled = false };
            eSf2ToolStripMenuItem.Click += ExportSF2;

            eASMToolStripMenuItem = new ToolStripMenuItem { Text = "Esporta Canzone In ASM", Enabled = false };
            eASMToolStripMenuItem.Click += ExportASM;

            eMIDIToolStripMenuItem = new ToolStripMenuItem { Text = "Esporta Canzone In MIDI", Enabled = false };
            eMIDIToolStripMenuItem.Click += ExportMIDI;

            dataToolStripMenuItem = new ToolStripMenuItem { Text = "Data" };
            dataToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { teToolStripMenuItem, vteToolStripMenuItem, eSf2ToolStripMenuItem, eASMToolStripMenuItem, eMIDIToolStripMenuItem });


            mainMenu = new MenuStrip { Size = new Size(iWidth, 24) };
            mainMenu.Items.AddRange(new ToolStripItem[] { fileToolStripMenuItem, dataToolStripMenuItem });

            // Buttons
            playButton = new ThemedButton { ForeColor = Color.MediumSpringGreen, Location = new Point(5, 3), Text = "Play" };
            playButton.Click += Play;
            pauseButton = new ThemedButton { ForeColor = Color.DeepSkyBlue, Location = new Point(85, 3), Text = "Pausa" };
            pauseButton.Click += Pause;
            stopButton = new ThemedButton { ForeColor = Color.MediumVioletRed, Location = new Point(166, 3), Text = "Stop" };
            stopButton.Click += Stop;

            playButton.Enabled = pauseButton.Enabled = stopButton.Enabled = false;
            playButton.Size = stopButton.Size = new Size(75, 23);
            pauseButton.Size = new Size(76, 23);

            // Numericals
            songNumerical = new ThemedNumeric { Enabled = false, Location = new Point(246, 4) };
            tableNumerical = new ThemedNumeric { Location = new Point(246, 35), Maximum = 0, Visible = false };

            songNumerical.Size = tableNumerical.Size = new Size(45, 23);
            songNumerical.ValueChanged += LoadSong;
            tableNumerical.ValueChanged += TableIndexChanged;

            // Labels
            creatorLabel = new ThemedLabel { Location = new Point(3, 43), Size = new Size(72, 13) };
            gameLabel = new ThemedLabel { Location = new Point(3, 30), Size = new Size(66, 13) };
            codeLabel = new ThemedLabel { Location = new Point(3, 56), Size = new Size(63, 13) };

            creatorLabel.AutoSize = gameLabel.AutoSize = codeLabel.AutoSize = true;
            creatorLabel.TextAlign = gameLabel.TextAlign = codeLabel.TextAlign = ContentAlignment.MiddleCenter;

            // Timer
            timer = new Timer(components);
            timer.Tick += UpdateUI;

            // Piano
            piano = new PianoControl { Anchor = AnchorStyles.Bottom, Location = new Point(0, 125 - 50 - 1), Size = new Size(iWidth, 50) };

            // Volume bar & Position bar
            int sWidth = (int)(iWidth / sfWidth);
            int sX = iWidth - sWidth - 4;
            positionBar = new ColorSlider()
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = false,
                Location = new Point(sX, 45),
                Maximum = 0,
                Size = new Size(sWidth, 27)
            };
            positionBar.MouseUp += SetPosition;
            positionBar.MouseDown += (o, e) => drag = true;
            volumeBar = new ColorSlider()
            {
                LargeChange = 20,
                Location = new Point(83, 45),
                Maximum = 100,
                Size = new Size(155, 27),
                SmallChange = 5
            };
            volumeBar.ValueChanged += (o, e) => SoundMixer.Instance.MasterVolume = (volumeBar.Value / (float)volumeBar.Maximum);
            volumeBar.Value = Config.Instance.Volume; // Update MusicPlayer volume

            // Playlist box
            ImageList il = new ImageList(components)
            {
                ColorDepth = ColorDepth.Depth16Bit,
                ImageSize = new Size(64, 64),
                TransparentColor = Color.Transparent
            };
            il.Images.AddRange(new Image[] { Resources.PlaylistIcon, Resources.SongIcon });
            songsComboBox = new ImageComboBox.ImageComboBox()
            {
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Enabled = false,
                ImageList = il,
                Indent = 15,
                Location = new Point(sX, 4),
                Size = new Size(sWidth, 23)
            };
            songsComboBox.SelectedIndexChanged += SongsComboBox_SelectedIndexChanged;

            // Track info
            trackInfo = new TrackInfoControl()
            {
                Dock = DockStyle.Fill,
                Size = new Size(iWidth, 690)
            };

            // Split container
            splitContainer = new SplitContainer()
            {
                BackColor = Theme.TitleBar,
                Dock = DockStyle.Fill,
                FixedPanel = FixedPanel.Panel1,
                IsSplitterFixed = true,
                Orientation = Orientation.Horizontal,
                Size = new Size(iWidth, iHeight),
                SplitterDistance = 125,
                SplitterWidth = 1
            };
            splitContainer.Panel1.Controls.AddRange(new Control[] { playButton, creatorLabel, gameLabel, codeLabel, pauseButton, stopButton, songNumerical, tableNumerical, songsComboBox, piano, positionBar, volumeBar });
            splitContainer.Panel2.Controls.Add(trackInfo);

            // MainForm
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(iWidth, iHeight);
            Controls.AddRange(new Control[] { splitContainer, mainMenu });
            Icon = Resources.Icon;
            MainMenuStrip = mainMenu;
            MinimumSize = new Size(8 + iWidth + 8, 30 + iHeight + 8); // Borders
            SongPlayer.Instance.SongEnded += () => stopUI = true;
            Resize += OnResize;
            Text = "GBA Music Studio";
        }

        void SetPosition(object sender, EventArgs e)
        {
            SongPlayer.Instance.SetPosition(positionBar.Value);
            drag = false;
        }
        void SetSongMaximum() => songNumerical.Maximum = ROM.Instance.Game.SongTableSizes[(int)tableNumerical.Value] - 1;
        void TableIndexChanged(object sender, EventArgs e)
        {
            SetSongMaximum();
            LoadSong(sender, e);
        }

        public void PreviewASM(Assembler asm, string headerLabel, string caption)
        {
            Text = "GBA Music Studio - " + caption;
            bool playing = SongPlayer.Instance.State == PlayerState.Playing; // Play new song if one is already playing
            Stop(null, null);
            SongPlayer.Instance.SetSong(new M4AASMSong(asm, headerLabel));
            UpdateTrackInfo(playing);
        }
        void LoadSong(object sender, EventArgs e)
        {
            APlaylist mainPlaylist = ROM.Instance.Game.Playlists[0];
            List<ASong> songs = mainPlaylist.Songs.ToList();
            ASong song = songs.SingleOrDefault(s => s.Index == songNumerical.Value);
            if (song != null)
            {
                Text = "GBA Music Studio - " + song.Name;
                songsComboBox.SelectedIndex = songs.IndexOf(song) + 1; // + 1 for the Playlist index
            }
            else
            {
                Text = "GBA Music Studio";
                songsComboBox.SelectedIndex = 0;
            }
            bool playing = SongPlayer.Instance.State == PlayerState.Playing; // Play new song if one is already playing
            bool paused = SongPlayer.Instance.State == PlayerState.Paused;
            Stop(null, null);
            try
            {
                // Pause which stops the thread inside from processing during loading, which would increase the stream latency
                if (!paused)
                    SongPlayer.Instance.Pause();
                var loadedSong = ROM.Instance.SongTables[(int)tableNumerical.Value][(int)songNumerical.Value];
                SongPlayer.Instance.SetSong(loadedSong);
                // Then "un pause" it, setting it to the stopped state
                if (!paused)
                    SongPlayer.Instance.Stop();
                UpdateTrackInfo(playing);
                MIDIKeyboard.Instance.Start();
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(ex.Message, "Error Loading Song " + songNumerical.Value);
                return;
            }
        }
        void SongsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!((songsComboBox.SelectedItem as ImageComboBox.ImageComboBoxItem).Item is ASong song)) return; // A playlist was selected
            songsComboBox.SelectedIndexChanged -= SongsComboBox_SelectedIndexChanged;
            songNumerical.Value = song.Index;
            songsComboBox.SelectedIndexChanged += SongsComboBox_SelectedIndexChanged;
        }

        void OpenROM(object sender, EventArgs e)
        {
            var d = new OpenFileDialog { Title = "Apri ROM GBA", Filter = "File GBA|*.gba" };
            if (d.ShowDialog() != DialogResult.OK) return;

            Stop(null, null);

            try
            {
                new ROM(d.FileName);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(ex.Message, "Error Loading ROM");
                return;
            }

            UpdateMenuInfo();
            SongPlayer.Instance.Reset();
            LoadSong(null, null);
        }
        void OpenMIDIConverter(object sender, EventArgs e)
        {
            new MIDIConverterDialog { Owner = this }.Show();
        }
        void OpenAssembler(object sender, EventArgs e)
        {
            new AssemblerDialog { Owner = this }.Show();
        }
        void OpenTrackEditor(object sender, EventArgs e)
        {
            if (trackEditor != null)
            {
                trackEditor.Focus();
                return;
            }
            trackEditor = new TrackEditor { Owner = this };
            trackEditor.FormClosed += (o, s) => trackEditor = null;
            trackEditor.Show();
        }
        void OpenVoiceTableEditor(object sender, EventArgs e)
        {
            if (voiceTableEditor != null)
            {
                voiceTableEditor.Focus();
                return;
            }
            voiceTableEditor = new VoiceTableEditor { Owner = this };
            voiceTableEditor.FormClosed += (o, s) => voiceTableEditor = null;
            voiceTableEditor.Show();
        }
        void ReloadConfig(object sender, EventArgs e)
        {
            Config.Instance.Load();
            if (ROM.Instance != null)
            {
                ROM.Instance.HandleConfigLoaded();
                UpdateMenuInfo();
            }
        }
        void ExportSF2(object sender, EventArgs e)
        {
            var d = new SaveFileDialog { Title = "Esporta File SF2", Filter = "File SF2|*.sf2" };
            if (d.ShowDialog() != DialogResult.OK) return;

            try
            {
                VoiceTableSaver.Save(d.FileName);
                FlexibleMessageBox.Show($"Voicetable salvata in {d.FileName}.", Text);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(ex.Message, "Errore Nell'Esportazione Del File SF2");
            }
        }
        void ExportASM(object sender, EventArgs e)
        {
            var d = new SaveFileDialog { Title = "Esporta File ASM", Filter = "File ASM|*.s" };
            if (d.ShowDialog() != DialogResult.OK) return;

            try
            {
                SongPlayer.Instance.Song.SaveAsASM(d.FileName);
                FlexibleMessageBox.Show($"Canzone salvata in {d.FileName}.", Text);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(ex.Message, "Errore Nell'Esportazione Della Canzone");
            }
        }
        void ExportMIDI(object sender, EventArgs e)
        {
            var d = new SaveFileDialog { Title = "Esporta File MIDI", Filter = "File MIDI|*.mid" };
            if (d.ShowDialog() != DialogResult.OK) return;

            try
            {
                SongPlayer.Instance.Song.SaveAsMIDI(d.FileName);
                FlexibleMessageBox.Show($"Canzone salvata in {d.FileName}.", Text);
            }
            catch (Exception ex)
            {
                FlexibleMessageBox.Show(ex.Message, "Errore Nell'Esportazione Della Canzone");
            }
        }

        void UpdateMenuInfo()
        {
            AGame game = ROM.Instance.Game;
            codeLabel.Text = game.Code;
            gameLabel.Text = game.Name;
            creatorLabel.Text = game.Creator;

            tableNumerical.Maximum = game.SongTables.Length - 1;
            tableNumerical.Value = 0;
            tableNumerical.Visible = game.SongTables.Length > 1;
            SetSongMaximum();
            PopulatePlaylists(game.Playlists);

            openMIDIToolStripMenuItem.Enabled = openASMToolStripMenuItem.Enabled =
                teToolStripMenuItem.Enabled = vteToolStripMenuItem.Enabled = eSf2ToolStripMenuItem.Enabled = eASMToolStripMenuItem.Enabled = eMIDIToolStripMenuItem.Enabled =
                songsComboBox.Enabled = songNumerical.Enabled = playButton.Enabled = true;
        }
        void UpdateTrackInfo(bool play)
        {
            trackInfo.DeleteData(); // Refresh track count
            UpdateSongPosition(0);
            UpdateTaskbarState();
            positionBar.Maximum = SongPlayer.Instance.Song.NumTicks;
            positionBar.LargeChange = positionBar.Maximum / 10;
            positionBar.SmallChange = positionBar.LargeChange / 4;
            trackEditor?.UpdateTracks();
            voiceTableEditor?.UpdateTable();
            if (play)
                Play(null, null);
            else
                pauseButton.Text = "Pausa";
            teToolStripMenuItem.Enabled = true;
        }
        void PopulatePlaylists(List<APlaylist> playlists)
        {
            songsComboBox.ComboBoxClear();
            foreach (var playlist in playlists)
            {
                songsComboBox.ComboBoxAddItem(new ImageComboBox.ImageComboBoxItem(playlist) { ImageIndex = 0 });
                songsComboBox.Items.AddRange(playlist.Songs.Select(s => new ImageComboBox.ImageComboBoxItem(s) { ImageIndex = 1, IndentLevel = 1 }).ToArray());
            }
            songNumerical.Value = playlists[0].Songs[0].Index;
            songsComboBox.SelectedIndex = 0; // Select main playlist
        }

        void Play(object sender, EventArgs e)
        {
            SongPlayer.Instance.Play();
            positionBar.Enabled = pauseButton.Enabled = stopButton.Enabled = true;
            pauseButton.Text = "Pausa";
            timer.Interval = (int)(1000f / Config.Instance.RefreshRate);
            timer.Start();
            UpdateTaskbarState();
        }
        void Pause(object sender, EventArgs e)
        {
            SongPlayer.Instance.Pause(); // Change state
            if (SongPlayer.Instance.State != PlayerState.Paused)
            {
                stopButton.Enabled = true;
                pauseButton.Text = "Pausa";
                timer.Start();
            }
            else
            {
                stopButton.Enabled = false;
                pauseButton.Text = "Riprendere";
                timer.Stop();
                System.Threading.Monitor.Enter(timerLock);
                ClearPianoNotes();
            }
            UpdateTaskbarState();
        }
        void Stop(object sender, EventArgs e)
        {
            SongPlayer.Instance.Stop();
            positionBar.Enabled = pauseButton.Enabled = stopButton.Enabled = false;
            timer.Stop();
            System.Threading.Monitor.Enter(timerLock);
            ClearPianoNotes();
            trackInfo.DeleteData();
            UpdateSongPosition(0);
            UpdateTaskbarState();
        }

        void ClearPianoNotes()
        {
            foreach (byte n in pianoNotes)
                if (n >= piano.LowNoteID && n <= piano.HighNoteID)
                {
                    piano[n - piano.LowNoteID].NoteOnColor = Color.DeepSkyBlue;
                    piano.ReleasePianoKey(n);
                }
            pianoNotes.Clear();
        }
        void UpdateUI(object sender, EventArgs e)
        {
            if (!System.Threading.Monitor.TryEnter(timerLock)) return;
            try
            {
                if (stopUI)
                {
                    Stop(null, null);
                    stopUI = false;
                    return;
                }
                ClearPianoNotes();
                var info = trackInfo.Info;
                SongPlayer.Instance.GetSongState(info);
                for (int i = SongPlayer.Instance.NumTracks - 1; i >= 0; i--)
                {
                    if (!PianoTracks[i]) continue;

                    var notes = info.Notes[i];
                    pianoNotes.AddRange(notes);
                    foreach (var n in notes)
                        if (n >= piano.LowNoteID && n <= piano.HighNoteID)
                        {
                            piano[n - piano.LowNoteID].NoteOnColor = Config.Instance.GetColor(info.Voices[i], ROM.Instance.Game.Remap, true);
                            piano.PressPianoKey(n);
                        }
                }
                if (!drag)
                {
                    UpdateSongPosition(info.Position);
                }
                trackInfo.Invalidate();
            }
            finally
            {
                System.Threading.Monitor.Exit(timerLock);
            }
        }
        void UpdateSongPosition(int position)
        {
            positionBar.Value = position.Clamp(0, positionBar.Maximum);
            if (Config.Instance.TaskbarProgress && TaskbarManager.IsPlatformSupported)
                TaskbarManager.Instance.SetProgressValue(positionBar.Value, positionBar.Maximum, Handle);
        }
        void UpdateTaskbarState()
        {
            if (Config.Instance.TaskbarProgress && TaskbarManager.IsPlatformSupported)
            {
                TaskbarProgressBarState state = TaskbarProgressBarState.NoProgress;
                if (SongPlayer.Instance.State == PlayerState.Playing) state = TaskbarProgressBarState.Normal;
                else if (SongPlayer.Instance.State == PlayerState.Paused) state = TaskbarProgressBarState.Paused;

                TaskbarManager.Instance.SetProgressState(state, Handle);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Stop(null, null);
            SongPlayer.Instance.ShutDown();
            MIDIKeyboard.Instance.Stop();
            base.OnFormClosing(e);
        }
        void OnResize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized) return;

            // Position bar & song combobox
            int sWidth = (int)(splitContainer.Width / sfWidth);
            int sX = splitContainer.Width - sWidth - 4;
            songsComboBox.Location = new Point(sX, 4);
            songsComboBox.Size = new Size(sWidth, 23);
            positionBar.Location = new Point(sX, 45);
            positionBar.Size = new Size(sWidth, 27);

            splitContainer.SplitterDistance = (int)((Height - 38) / spfHeight) - 24 - 1;

            // Piano
            piano.Size = new Size(splitContainer.Width, (int)(splitContainer.Panel1.Height / 2.5f)); // Force it to initialize piano keys again
            int targetWhites = piano.Width / 10; // Minimum width of a white key is 10 pixels
            int targetAmount = (targetWhites / 7 * 12).Clamp(1, 128); // 7 white keys per octave
            int offset = targetAmount / 2 - ((targetWhites / 7) % 2);
            piano.LowNoteID = Math.Max(0, 60 - offset);
            piano.HighNoteID = (60 + offset - 1) >= 120 ? 127 : (60 + offset - 1);

            int wWidth = piano[0].Width; // White key width
            int dif = splitContainer.Width - wWidth * piano.WhiteKeyCount;
            piano.Location = new Point(dif / 2, splitContainer.Panel1.Height - piano.Height - 1);
            piano.Invalidate(true);
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (playButton.Enabled && keyData == (Keys.Space))
            {
                if (SongPlayer.Instance.State == PlayerState.Stopped)
                    Play(null, null);
                else
                    Pause(null, null);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
