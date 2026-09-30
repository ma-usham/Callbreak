using System;
using System.Collections.Generic;
using Callbreak.Application.Ports;
using UnityEngine;
using UnityEngine.Audio;

namespace Callbreak.Application.Config
{
    /// <summary>
    /// Every sound the game can play (GDD §14): one or more clips per <see cref="SoundCue"/> (one is
    /// picked at random, so repeated card sounds vary) with a volume, and one loop per
    /// <see cref="MusicTrack"/>. The optional mixer groups route sources through Master › Music ·
    /// SFX · UI once the mixer exists. Anything left empty is simply silent.
    /// </summary>
    [CreateAssetMenu(menuName = "Callbreak/Config/Audio Cue Library", fileName = "AudioCueLibrary")]
    public sealed class AudioCueLibrary : ScriptableObject
    {
        [SerializeField] private CueEntry[] _cues = Array.Empty<CueEntry>();
        [SerializeField] private MusicEntry[] _music = Array.Empty<MusicEntry>();
        [SerializeField] private AudioMixerGroup _musicGroup;
        [SerializeField] private AudioMixerGroup _sfxGroup;
        [SerializeField] private AudioMixerGroup _uiGroup;

        public AudioMixerGroup MusicGroup => _musicGroup;

        public AudioMixerGroup SfxGroup => _sfxGroup;

        public AudioMixerGroup UiGroup => _uiGroup;

        public bool TryGetCue(SoundCue cue, out CueEntry entry)
        {
            if (_cues != null)
            {
                foreach (CueEntry candidate in _cues)
                {
                    if (candidate != null && candidate.Cue == cue)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        public bool TryGetMusic(MusicTrack track, out MusicEntry entry)
        {
            if (_music != null)
            {
                foreach (MusicEntry candidate in _music)
                {
                    if (candidate != null && candidate.Track == track)
                    {
                        entry = candidate;
                        return true;
                    }
                }
            }

            entry = null;
            return false;
        }

        [Serializable]
        public sealed class CueEntry
        {
            [SerializeField] private SoundCue _cue;
            [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();
            [SerializeField, Range(0f, 1f)] private float _volume = 1f;

            public SoundCue Cue => _cue;

            public IReadOnlyList<AudioClip> Clips => _clips ?? Array.Empty<AudioClip>();

            public float Volume => _volume;
        }

        [Serializable]
        public sealed class MusicEntry
        {
            [SerializeField] private MusicTrack _track;
            [SerializeField] private AudioClip _clip;
            [SerializeField, Range(0f, 1f)] private float _volume = 1f;

            public MusicTrack Track => _track;

            public AudioClip Clip => _clip;

            public float Volume => _volume;
        }
    }
}
