using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Callbreak.Application.Config;
using Callbreak.Application.Ports;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Wires the audio files into the game (GDD §14). For every clip under
    /// <c>Assets/_Project/Audio/SFX</c> named <c>SFX_&lt;Cue&gt;_&lt;nn&gt;</c> and every loop under
    /// <c>Assets/_Project/Audio/Music</c> named <c>MUS_&lt;Track&gt;_...</c> it sets the GDD's import
    /// settings and lists the clip in the <see cref="AudioCueLibrary"/> the audio service reads:
    /// each <see cref="SoundCue"/> gets all its numbered takes, each <see cref="MusicTrack"/> its loop.
    /// <para>
    /// Import settings: effects shorter than <see cref="StingSeconds"/> are ADPCM, Decompress On Load,
    /// mono; longer stings are Vorbis 0.6, Compressed In Memory; music is Vorbis 0.5, Streaming,
    /// stereo; everything keeps its 44.1 kHz.
    /// </para>
    /// <para>
    /// Volumes: a cue or track that is new to the library gets the default below; one that is already
    /// there keeps its volume, so a mix tuned in the Inspector survives a rebuild. The files are all
    /// normalised alike (effects peak at -3 dBFS, music at -14 LUFS), so these volumes are the mix.
    /// </para>
    /// Re-run it after the files change (Tools/AudioGen, or a composer's replacements under the same names).
    /// </summary>
    public static class AudioLibraryBuilder
    {
        public const string SfxFolder = "Assets/_Project/Audio/SFX";
        public const string MusicFolder = "Assets/_Project/Audio/Music";
        public const string LibraryPath = "Assets/_Project/Config/Audio/AudioCueLibrary.asset";

        /// <summary>Effects at least this long count as the GDD's "longer SFX / stings".</summary>
        public const float StingSeconds = 1f;

        /// <summary>
        /// Unity's ADPCM works in blocks of this many samples and resamples a clip to fit a whole number of
        /// them (measured: a 3528-sample tick came back 3584 samples long and 27 cents flat), so a short
        /// effect should be a multiple of this long. Tools/AudioGen writes them that way.
        /// </summary>
        public const int AdpcmBlockSamples = 64;

        private const float StingQuality = 0.6f;
        private const float MusicQuality = 0.5f;
        private const float DefaultMusicVolume = 0.7f;

        private static readonly Regex SfxName = new Regex(@"^SFX_([A-Za-z]+)_(\d+)$");

        // Set from the loudest 100 ms of each file (Tools/AudioGen prints it as "loud"), so that in the game
        // cards sit near -18 LUFS, stings near -17, the victory near -13, interface sounds near -21 and the
        // timer tick near -24, over music at about -17 (menu) and -23 (table).
        // Recomputed for the generated sources (ElevenLabs, 1 Oct 2026; ArtSource/Audio/Sources) so each cue
        // keeps the loudness it had in the game before. The coin collect (ElevenLabs, 9 Oct 2026) sits with the
        // interface sounds: they land at -20.0 to -20.9, it at -20.5.
        private static readonly Dictionary<SoundCue, float> DefaultVolume = new Dictionary<SoundCue, float>
        {
            { SoundCue.CardShuffle, 1f },
            { SoundCue.CardDeal, 0.73f },
            { SoundCue.CardPlay, 1f },
            { SoundCue.TrickCollect, 1f },
            { SoundCue.CallConfirm, 1f },
            { SoundCue.YourTurn, 0.68f },
            { SoundCue.TimerTick, 0.5f },
            { SoundCue.CallMet, 0.55f },
            { SoundCue.CallMissed, 0.4f },
            { SoundCue.RoundEnd, 0.53f },
            { SoundCue.Victory, 0.83f },
            { SoundCue.UiTap, 0.74f },
            { SoundCue.UiBack, 0.82f },
            { SoundCue.Toggle, 0.87f },
            { SoundCue.CoinCollect, 0.52f },
        };

        [MenuItem("Callbreak/Build/Audio Library")]
        private static void BuildFromMenu()
        {
            Debug.Log(Build());
        }

        /// <summary>Applies the import settings, fills the library and returns a one-line report.</summary>
        public static string Build()
        {
            AssetDatabase.Refresh();
            var library = AssetDatabase.LoadAssetAtPath<AudioCueLibrary>(LibraryPath);
            if (library == null)
            {
                return $"[AudioLibrary] {LibraryPath} is missing; nothing built.";
            }

            var problems = new List<string>();
            int reimported = 0;

            // Sound effects, grouped by cue and ordered by take number.
            var takes = new Dictionary<SoundCue, SortedDictionary<int, AudioClip>>();
            foreach (string path in ClipPaths(SfxFolder))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                Match match = SfxName.Match(name);
                if (!match.Success || !Enum.TryParse(match.Groups[1].Value, out SoundCue cue) || !Enum.IsDefined(typeof(SoundCue), cue))
                {
                    problems.Add($"{name}: not SFX_<Cue>_<nn> with a SoundCue name");
                    continue;
                }

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    problems.Add($"{name}: not imported as an AudioClip");
                    continue;
                }

                // The file's own length decides, not the imported clip's: ADPCM rounds that up to whole blocks.
                bool wav = TryReadWav(path, out int frames, out int rate);
                bool sting = (wav ? (float)frames / rate : clip.length) >= StingSeconds;
                if (wav && !sting && frames % AdpcmBlockSamples != 0)
                {
                    problems.Add($"{name}: {frames} samples is not a multiple of {AdpcmBlockSamples}, so the ADPCM import resamples it (slightly retuned); trim or pad the file");
                }

                if (Apply(path, sting ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad,
                          sting ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.ADPCM, StingQuality, !sting))
                {
                    reimported++;
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }

                if (!takes.TryGetValue(cue, out SortedDictionary<int, AudioClip> list))
                {
                    list = new SortedDictionary<int, AudioClip>();
                    takes[cue] = list;
                }

                int take = int.Parse(match.Groups[2].Value);
                if (list.ContainsKey(take))
                {
                    problems.Add($"{name}: a second take {take} of {cue}");
                    continue;
                }

                list[take] = clip;
            }

            // Music loops: the first clip whose name starts with MUS_<Track>_.
            var loops = new Dictionary<MusicTrack, AudioClip>();
            foreach (string path in ClipPaths(MusicFolder))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                MusicTrack track = MusicTrack.None;
                foreach (MusicTrack candidate in Enum.GetValues(typeof(MusicTrack)))
                {
                    if (candidate != MusicTrack.None && name.StartsWith($"MUS_{candidate}_", StringComparison.Ordinal))
                    {
                        track = candidate;
                    }
                }

                if (track == MusicTrack.None)
                {
                    problems.Add($"{name}: not MUS_<Track>_... with a MusicTrack name");
                    continue;
                }

                if (loops.ContainsKey(track))
                {
                    problems.Add($"{name}: a second loop for {track}; using {loops[track].name}");
                    continue;
                }

                if (Apply(path, AudioClipLoadType.Streaming, AudioCompressionFormat.Vorbis, MusicQuality, false))
                {
                    reimported++;
                }

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    problems.Add($"{name}: not imported as an AudioClip");
                    continue;
                }

                loops[track] = clip;
            }

            // The library, in enum order. Existing entries keep their volume.
            var serialized = new SerializedObject(library);
            SerializedProperty cues = serialized.FindProperty("_cues");
            SerializedProperty music = serialized.FindProperty("_music");
            if (cues == null || music == null)
            {
                return "[AudioLibrary] AudioCueLibrary no longer has _cues and _music; update AudioLibraryBuilder.";
            }

            Dictionary<int, float> cueVolumes = Volumes(cues, "_cue");
            Dictionary<int, float> musicVolumes = Volumes(music, "_track");

            var allCues = (SoundCue[])Enum.GetValues(typeof(SoundCue));
            cues.arraySize = allCues.Length;
            int clips = 0;
            for (int i = 0; i < allCues.Length; i++)
            {
                SoundCue cue = allCues[i];
                SerializedProperty entry = cues.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_cue").intValue = (int)cue;
                SerializedProperty list = entry.FindPropertyRelative("_clips");
                AudioClip[] found = takes.TryGetValue(cue, out SortedDictionary<int, AudioClip> sorted) ? sorted.Values.ToArray() : Array.Empty<AudioClip>();
                list.arraySize = found.Length;
                for (int c = 0; c < found.Length; c++)
                {
                    list.GetArrayElementAtIndex(c).objectReferenceValue = found[c];
                }

                entry.FindPropertyRelative("_volume").floatValue = cueVolumes.TryGetValue((int)cue, out float kept)
                    ? kept
                    : DefaultVolume.TryGetValue(cue, out float preset) ? preset : 1f;
                clips += found.Length;
                if (found.Length == 0)
                {
                    problems.Add($"{cue}: no clip (it will be silent)");
                }
            }

            MusicTrack[] tracks = ((MusicTrack[])Enum.GetValues(typeof(MusicTrack))).Where(t => t != MusicTrack.None).ToArray();
            music.arraySize = tracks.Length;
            for (int i = 0; i < tracks.Length; i++)
            {
                MusicTrack track = tracks[i];
                SerializedProperty entry = music.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("_track").intValue = (int)track;
                entry.FindPropertyRelative("_clip").objectReferenceValue = loops.TryGetValue(track, out AudioClip loop) ? loop : null;
                entry.FindPropertyRelative("_volume").floatValue = musicVolumes.TryGetValue((int)track, out float kept) ? kept : DefaultMusicVolume;
                if (loop == null)
                {
                    problems.Add($"{track}: no loop (it will be silent)");
                }
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            string result = $"[AudioLibrary] {allCues.Length} cues with {clips} clips, {loops.Count} of {tracks.Length} music loops; {reimported} import settings changed";
            return problems.Count == 0 ? result : result + "; problems: " + string.Join("; ", problems);
        }

        private static IEnumerable<string> ClipPaths(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return Array.Empty<string>();
            }

            return AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .OrderBy(p => p, StringComparer.Ordinal);
        }

        /// <summary>The frame count and sample rate from a WAV file's header; false for any other file.</summary>
        private static bool TryReadWav(string path, out int frames, out int rate)
        {
            const uint Riff = 0x46464952, Wave = 0x45564157, Fmt = 0x20746D66, Data = 0x61746164;
            frames = 0;
            rate = 0;
            if (!path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            try
            {
                using (var reader = new BinaryReader(File.OpenRead(path)))
                {
                    if (reader.ReadUInt32() != Riff)
                    {
                        return false;
                    }

                    reader.ReadUInt32();
                    if (reader.ReadUInt32() != Wave)
                    {
                        return false;
                    }

                    int blockAlign = 0;
                    while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                    {
                        uint id = reader.ReadUInt32();
                        uint size = reader.ReadUInt32();
                        long next = reader.BaseStream.Position + size + (size & 1);
                        if (id == Fmt)
                        {
                            reader.ReadUInt16();
                            reader.ReadUInt16();
                            rate = reader.ReadInt32();
                            reader.ReadInt32();
                            blockAlign = reader.ReadUInt16();
                        }
                        else if (id == Data)
                        {
                            if (blockAlign <= 0 || rate <= 0)
                            {
                                return false;
                            }

                            frames = (int)(size / blockAlign);
                            return true;
                        }

                        reader.BaseStream.Position = next;
                    }
                }
            }
            catch (IOException)
            {
                // Unreadable right now: fall back to the imported clip's length.
            }

            return false;
        }

        /// <summary>The volume each entry already has, by enum value (the first entry wins).</summary>
        private static Dictionary<int, float> Volumes(SerializedProperty entries, string keyField)
        {
            var volumes = new Dictionary<int, float>();
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                int key = entry.FindPropertyRelative(keyField).intValue;
                if (!volumes.ContainsKey(key))
                {
                    volumes[key] = entry.FindPropertyRelative("_volume").floatValue;
                }
            }

            return volumes;
        }

        /// <summary>
        /// Sets one clip's import settings and re-imports it only if something differs. Force To Mono's
        /// "Normalize" is switched off, so a clip plays at the level it was authored at.
        /// </summary>
        private static bool Apply(string path, AudioClipLoadType loadType, AudioCompressionFormat format, float quality, bool mono)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                return false;
            }

            bool vorbis = format == AudioCompressionFormat.Vorbis;
            bool preload = loadType != AudioClipLoadType.Streaming;   // a streamed clip has nothing to preload
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            var importerObject = new SerializedObject(importer);
            SerializedProperty normalize = importerObject.FindProperty("m_Normalize");
            bool changed = settings.loadType != loadType
                || settings.compressionFormat != format
                || settings.sampleRateSetting != AudioSampleRateSetting.PreserveSampleRate
                || (vorbis && !Mathf.Approximately(settings.quality, quality))
                || (preload && !settings.preloadAudioData)
                || importer.forceToMono != mono
                || importer.loadInBackground
                || (normalize != null && normalize.boolValue);
            if (!changed)
            {
                return false;
            }

            if (normalize != null)
            {
                normalize.boolValue = false;
                importerObject.ApplyModifiedPropertiesWithoutUndo();
            }

            settings.loadType = loadType;
            settings.compressionFormat = format;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            if (preload)
            {
                settings.preloadAudioData = true;
            }

            if (vorbis)
            {
                settings.quality = quality;
            }

            importer.defaultSampleSettings = settings;
            importer.forceToMono = mono;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
            return true;
        }
    }
}
