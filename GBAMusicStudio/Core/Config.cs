﻿using Kermalis.GBAMusicStudio.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace Kermalis.GBAMusicStudio.Core
{
    class ASong
    {
        public readonly int Index;
        public readonly string Name;

        public ASong(int index, string name)
        {
            Index = index; Name = name;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ASong other))
            {
                return false;
            }
            return other.Index == Index && other.Name == Name;
        }
        public override int GetHashCode() => unchecked(Index.GetHashCode() ^ Name.GetHashCode());
        public override string ToString() => Name;
    }
    class APlaylist
    {
        public readonly string Name;
        public readonly ASong[] Songs;

        public APlaylist(string name, ASong[] songs)
        {
            Name = name; Songs = songs;
        }

        public override string ToString()
        {
            int songCount = Songs.Length;
            CultureInfo cul = System.Threading.Thread.CurrentThread.CurrentUICulture;

            if (cul.Equals(CultureInfo.CreateSpecificCulture("it")) // Italian
                || cul.Equals(CultureInfo.CreateSpecificCulture("it-it"))) // Italian (Italy)
            {
                // PlaylistName - (1 Canzoni)
                // PlaylistName - (2 Canzoni)
                return $"{Name} - ({songCount} {(songCount == 1 ? "Canzone" : "Canzoni")})";
            }
            else // Fallback to en-US
            {
                // PlaylistName - (1 Song)
                // PlaylistName - (2 Songs)
                return $"{Name} - ({songCount} {(songCount == 1 ? "Song" : "Songs")})";
            }
        }
    }
    class AnEngine
    {
        public readonly EngineType Type;
        public readonly ReverbType ReverbType;
        public readonly byte Reverb;
        public readonly byte Volume; // 0-F
        public readonly int Frequency;

        public readonly bool HasGoldenSunSynths;
        public readonly bool HasPokemonCompression;

        public AnEngine(EngineType type, ReverbType reverbType, byte reverb, byte volume, int frequency, bool hasGoldenSunSynths, bool hasPokemonCompression)
        {
            Type = type; ReverbType = reverbType; Reverb = reverb; Volume = volume; Frequency = frequency;
            HasGoldenSunSynths = hasGoldenSunSynths; HasPokemonCompression = hasPokemonCompression;
        }

        public override string ToString() => Type.ToString();
    }
    class AGame
    {
        public readonly string Code, Name, Creator;
        public readonly AnEngine Engine;
        public readonly int[] SongTables, SongTableSizes;
        public readonly List<APlaylist> Playlists;
        public readonly string Remap;

        // MLSS only
        public readonly int VoiceTable, SampleTable, SampleTableSize;

        public AGame(string code, string name, string creator, AnEngine engine, int[] tables, int[] tableSizes, List<APlaylist> playlists, string remap,
            int voiceTable, int sampleTable, int sampleTableSize)
        {
            Code = code; Name = name; Creator = creator; Engine = engine;
            SongTables = tables; SongTableSizes = tableSizes;
            Playlists = playlists; Remap = remap;

            VoiceTable = voiceTable; SampleTable = sampleTable; SampleTableSize = sampleTableSize;
        }

        public override string ToString() => Name;
    }
    class ARemap
    {
        public readonly Tuple<byte, byte>[] Remaps;

        public ARemap(Tuple<byte, byte>[] remaps)
        {
            Remaps = remaps;
        }
    }

    class Config
    {
        public static Config Instance { get; } = new Config();

        readonly int DefaultTableSize = 1000;

        public byte DirectCount { get; private set; }
        public bool All256Voices { get; private set; }
        public bool MIDIKeyboardFixedVelocity { get; private set; }
        public bool TaskbarProgress { get; private set; }
        public byte RefreshRate { get; private set; }
        public bool CenterIndicators { get; private set; }
        public bool PanpotIndicators { get; private set; }
        public PlaylistMode PlaylistMode { get; private set; }
        public byte PlaylistSongLoops { get; private set; }
        public int PlaylistFadeOutLength { get; private set; }
        public HSLColor[] Colors { get; private set; }
        public Dictionary<string, ARemap> InstrumentRemaps { get; private set; }

        public Dictionary<string, AGame> Games { get; private set; }

        private Config() => Load();
        public void Load() { LoadConfig(); LoadGames(); }
        void LoadConfig()
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(File.ReadAllText("Config.yaml")));

            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;
            DirectCount = (byte)Utils.ParseValue(mapping.Children["DirectCount"].ToString());
            All256Voices = bool.Parse(mapping.Children["All256Voices"].ToString());
            MIDIKeyboardFixedVelocity = bool.Parse(mapping.Children["MIDIKeyboardFixedVelocity"].ToString());
            TaskbarProgress = bool.Parse(mapping.Children["TaskbarProgress"].ToString());
            RefreshRate = (byte)Utils.ParseValue(mapping.Children["RefreshRate"].ToString());
            CenterIndicators = bool.Parse(mapping.Children["CenterIndicators"].ToString());
            PanpotIndicators = bool.Parse(mapping.Children["PanpotIndicators"].ToString());
            PlaylistMode = (PlaylistMode)Enum.Parse(typeof(PlaylistMode), mapping.Children["PlaylistMode"].ToString());
            PlaylistSongLoops = (byte)Utils.ParseValue(mapping.Children["PlaylistSongLoops"].ToString());
            PlaylistFadeOutLength = (int)Utils.ParseValue(mapping.Children["PlaylistFadeOutLength"].ToString());

            var cmap = (YamlMappingNode)mapping.Children["Colors"];
            Colors = new HSLColor[256];
            foreach (KeyValuePair<YamlNode, YamlNode> c in cmap)
            {
                int i = (int)Utils.ParseValue(c.Key.ToString());
                IDictionary<YamlNode, YamlNode> children = ((YamlMappingNode)c.Value).Children;
                double h = 0, s = 0, l = 0;
                foreach (KeyValuePair<YamlNode, YamlNode> v in children)
                {
                    if (v.Key.ToString() == "H")
                    {
                        h = byte.Parse(v.Value.ToString());
                    }
                    else if (v.Key.ToString() == "S")
                    {
                        s = byte.Parse(v.Value.ToString());
                    }
                    else if (v.Key.ToString() == "L")
                    {
                        l = byte.Parse(v.Value.ToString());
                    }
                }
                var color = new HSLColor(h, s, l);
                Colors[i] = Colors[i + 0x80] = color;
            }

            var rmap = (YamlMappingNode)mapping.Children["InstrumentRemaps"];
            InstrumentRemaps = new Dictionary<string, ARemap>();
            foreach (KeyValuePair<YamlNode, YamlNode> r in rmap)
            {
                var remaps = new List<Tuple<byte, byte>>();

                IDictionary<YamlNode, YamlNode> children = ((YamlMappingNode)r.Value).Children;
                foreach (KeyValuePair<YamlNode, YamlNode> v in children)
                {
                    remaps.Add(new Tuple<byte, byte>(byte.Parse(v.Key.ToString()), byte.Parse(v.Value.ToString())));
                }

                InstrumentRemaps.Add(r.Key.ToString(), new ARemap(remaps.ToArray()));
            }
        }
        void LoadGames()
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(File.ReadAllText("Games.yaml")));

            var mapping = (YamlMappingNode)yaml.Documents[0].RootNode;

            Games = new Dictionary<string, AGame>();
            foreach (KeyValuePair<YamlNode, YamlNode> g in mapping)
            {
                string code, name, creator;
                EngineType engineType = EngineType.M4A; ReverbType reverbType = ReverbType.Normal;
                byte engineReverb = 0, engineVolume = 0xF;
                int engineFrequency = 13379;
                bool engineHasGoldenSunSynths = false, engineHasPokemonCompression = false;
                int[] tables, tableSizes;
                List<APlaylist> playlists;
                string remap = string.Empty;
                int voiceTable = 0, sampleTable = 0, sampleTableSize = 0;

                code = g.Key.ToString();
                var game = (YamlMappingNode)g.Value;

                // Basic info
                name = game.Children["Name"].ToString();

                // SongTables
                string[] songTables = game.Children["SongTable"].ToString().Split(' ');
                tables = new int[songTables.Length]; tableSizes = new int[songTables.Length];
                for (int i = 0; i < songTables.Length; i++)
                {
                    tables[i] = (int)Utils.ParseValue(songTables[i]);
                }

                // MLSS info
                if (game.Children.TryGetValue("VoiceTable", out YamlNode vTable))
                {
                    voiceTable = (int)Utils.ParseValue(vTable.ToString());
                }
                if (game.Children.TryGetValue("SampleTable", out YamlNode sTable))
                {
                    sampleTable = (int)Utils.ParseValue(sTable.ToString());
                }

                // If we are to copy another game's config
                if (game.Children.TryGetValue("Copy", out YamlNode copy))
                {
                    game = (YamlMappingNode)mapping.Children[copy];
                }

                // SongTable Sizes (Copyable)
                string[] sizes = { };
                if (game.Children.TryGetValue("SongTableSize", out YamlNode soTableSize))
                {
                    sizes = soTableSize.ToString().Split(' ');
                }
                for (int i = 0; i < songTables.Length; i++)
                {
                    tableSizes[i] = DefaultTableSize;
                    if (i < sizes.Length)
                    {
                        tableSizes[i] = (int)Utils.ParseValue(sizes[i]);
                    }
                }
                // MLSS Sample Table Size (Copyable)
                if (game.Children.TryGetValue("SampleTableSize", out YamlNode saTableSize))
                {
                    sampleTableSize = (int)Utils.ParseValue(saTableSize.ToString());
                }

                // Creator name (required) (Copyable)
                creator = game.Children["Creator"].ToString();

                // Remap (Copyable)
                if (game.Children.TryGetValue("Remap", out YamlNode rmap))
                {
                    remap = rmap.ToString();
                }

                // Engine (Copyable)
                if (game.Children.TryGetValue("Engine", out YamlNode yeng))
                {
                    var eng = (YamlMappingNode)yeng;
                    if (eng.Children.TryGetValue("Type", out YamlNode type))
                    {
                        engineType = (EngineType)Enum.Parse(typeof(EngineType), type.ToString());
                    }
                    if (eng.Children.TryGetValue("ReverbType", out YamlNode rType))
                    {
                        reverbType = (ReverbType)Enum.Parse(typeof(ReverbType), rType.ToString());
                    }
                    if (eng.Children.TryGetValue("Reverb", out YamlNode reverb))
                    {
                        engineReverb = (byte)Utils.ParseValue(reverb.ToString());
                    }
                    if (eng.Children.TryGetValue("Volume", out YamlNode volume))
                    {
                        engineVolume = (byte)Utils.ParseValue(volume.ToString());
                    }
                    if (eng.Children.TryGetValue("Frequency", out YamlNode frequency))
                    {
                        engineFrequency = (int)Utils.ParseValue(frequency.ToString());
                    }
                    if (eng.Children.TryGetValue("GoldenSunSynths", out YamlNode synths))
                    {
                        engineHasGoldenSunSynths = bool.Parse(synths.ToString());
                    }
                    if (eng.Children.TryGetValue("PokemonCompression", out YamlNode compression))
                    {
                        engineHasPokemonCompression = bool.Parse(compression.ToString());
                    }
                }
                var engine = new AnEngine(engineType, reverbType, engineReverb, engineVolume, engineFrequency, engineHasGoldenSunSynths, engineHasPokemonCompression);

                // Playlists (Copyable)
                playlists = new List<APlaylist>();
                if (game.Children.TryGetValue("Music", out YamlNode ymusic))
                {
                    var music = (YamlMappingNode)ymusic;
                    foreach (KeyValuePair<YamlNode, YamlNode> kvp in music)
                    {
                        var songs = new List<ASong>();
                        foreach (KeyValuePair<YamlNode, YamlNode> song in (YamlMappingNode)kvp.Value) // No hex values. It prevents putting in duplicates by having one hex and one dec of the same song index
                        {
                            songs.Add(new ASong(int.Parse(song.Key.ToString()), song.Value.ToString()));
                        }
                        playlists.Add(new APlaylist(kvp.Key.ToString(), songs.ToArray()));
                    }
                }

                // The complete playlist
                if (!playlists.Any(p => p.Name == "Music"))
                {
                    playlists.Insert(0, new APlaylist("Music", playlists.Select(p => p.Songs).UniteAll().OrderBy(s => s.Index).ToArray()));
                }

                Games.Add(code, new AGame(code, name, creator, engine, tables, tableSizes, playlists, remap,
                    voiceTable, sampleTable, sampleTableSize));
            }
        }

        public HSLColor GetColor(byte voice, string key, bool from)
        {
            return Colors[GetRemap(voice, key, from)];
        }
        public byte GetRemap(byte voice, string key, bool from)
        {
            if (InstrumentRemaps.TryGetValue(key, out ARemap remap))
            {
                Tuple<byte, byte> r = remap.Remaps.FirstOrDefault(t => (from ? t.Item1 : t.Item2) == voice);
                if (r == null)
                {
                    return voice;
                }
                return from ? r.Item2 : r.Item1;
            }
            return voice;
        }
    }
}
