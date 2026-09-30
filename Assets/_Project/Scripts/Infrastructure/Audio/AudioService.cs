using System;
using System.Collections.Generic;
using Callbreak.Application.Config;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;
using UnityEngine;

namespace Callbreak.Infrastructure.Audio
{
    /// <summary>
    /// <see cref="IAudioService"/> (GDD §14). Sound effects play on a small pool of 2D AudioSources;
    /// card sounds vary their pitch by up to ±3% so a deal doesn't sound mechanical, and a cue with
    /// several clips never repeats the same clip twice in a row. Only one UI cue sounds per frame,
    /// the first one asked for: a presenter that answers a press with its own cue (back, toggle)
    /// is heard instead of the button's tap, not on top of it. Music uses two looping sources that
    /// crossfade (equal power) over 1.5 s; the table loop sits 6 dB under the SFX level, and
    /// <see cref="Duck"/> lowers music by 3 dB. Music, Sound effects and Volume follow
    /// <see cref="ISettingsService"/> live. Clips come from the <see cref="AudioCueLibrary"/>; a
    /// missing library, cue or clip is silent. Nothing here throws.
    /// The caller provides the GameObject that holds the sources (the bootstrap's persistent
    /// object), so no scene lookup is needed; the service adds its sources and a runner to it.
    /// </summary>
    public sealed class AudioService : IAudioService, IDisposable
    {
        public const int SfxVoices = 8;
        public const float CrossfadeSeconds = 1.5f;
        public const float TableMusicDecibels = -6f;
        public const float DuckDecibels = -3f;
        public const float CardPitchVariation = 0.03f;

        private const float DuckSeconds = 0.25f;

        private readonly AudioCueLibrary _library;
        private readonly ISettingsService _settings;
        private readonly GameObject _sfxObject;
        private readonly GameObject _musicObject;
        private readonly AudioServiceRunner _runner;
        private readonly AudioSource[] _voices = new AudioSource[SfxVoices];
        private readonly float[] _voiceGain = new float[SfxVoices];
        private readonly AudioSource[] _music = new AudioSource[2];
        private readonly float[] _trackGain = new float[2];
        private readonly float[] _fade = new float[2];
        private readonly float[] _fadeTarget = new float[2];
        private readonly Dictionary<SoundCue, int> _lastClip = new Dictionary<SoundCue, int>();
        private readonly System.Random _random = new System.Random();

        private MusicTrack _track = MusicTrack.None;
        private int _current;
        private int _nextVoice;
        private int _uiCueFrame = -1;
        private float _duck = 1f;
        private float _duckTarget = 1f;
        private bool _musicMuted;
        private bool _warned;
        private bool _disposed;

        public AudioService(GameObject host, AudioCueLibrary library, ISettingsService settings)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _library = library;

            _sfxObject = CreateChild(host, "SFX");
            for (int i = 0; i < SfxVoices; i++)
            {
                _voices[i] = CreateSource(_sfxObject, false, 128);
            }

            _musicObject = CreateChild(host, "Music");
            for (int i = 0; i < _music.Length; i++)
            {
                _music[i] = CreateSource(_musicObject, true, 0);
                _music[i].outputAudioMixerGroup = library != null ? library.MusicGroup : null;
            }

            _runner = host.GetComponent<AudioServiceRunner>();
            if (_runner == null)
            {
                _runner = host.AddComponent<AudioServiceRunner>();
            }

            _runner.Tick = Update;
            _musicMuted = !settings.Music;
            _settings.Changed += ApplySettings;
        }

        /// <summary>The track last asked for (it may be silent if it has no clip or music is off).</summary>
        public MusicTrack CurrentTrack => _track;

        /// <summary>How many cues were given to a voice so far, for tests and diagnostics.</summary>
        public int CuesPlayed { get; private set; }

        /// <summary>The cue last given to a voice.</summary>
        public SoundCue LastCue { get; private set; }

        public void Play(SoundCue cue)
        {
            if (_disposed || _library == null || !_settings.SoundEffects)
            {
                return;
            }

            try
            {
                if (!_library.TryGetCue(cue, out AudioCueLibrary.CueEntry entry))
                {
                    return;
                }

                AudioClip clip = PickClip(cue, entry.Clips);
                if (clip == null)
                {
                    return;
                }

                bool ui = IsUiCue(cue);
                if (ui && Time.frameCount == _uiCueFrame)
                {
                    return;
                }

                int index = NextVoice();
                AudioSource voice = _voices[index];
                if (voice == null)
                {
                    return;
                }

                if (ui)
                {
                    _uiCueFrame = Time.frameCount;
                }

                voice.Stop();
                voice.clip = clip;
                voice.pitch = IsCardCue(cue) ? 1f + ((float)_random.NextDouble() * 2f - 1f) * CardPitchVariation : 1f;
                voice.outputAudioMixerGroup = ui ? _library.UiGroup : _library.SfxGroup;
                _voiceGain[index] = entry.Volume;
                voice.volume = entry.Volume * _settings.Volume;
                voice.Play();
                CuesPlayed++;
                LastCue = cue;
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        public void PlayMusic(MusicTrack track)
        {
            if (_disposed || track == _track)
            {
                return;
            }

            _track = track;
            try
            {
                StartTrack();
                ApplyMusicVolumes();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        public void Duck(bool ducked)
        {
            _duckTarget = ducked ? DecibelsToGain(DuckDecibels) : 1f;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _settings.Changed -= ApplySettings;
            if (_runner != null)
            {
                _runner.Tick = null;
                DestroyObject(_runner);
            }

            DestroyObject(_sfxObject);
            DestroyObject(_musicObject);
        }

        public static float DecibelsToGain(float decibels) => Mathf.Pow(10f, decibels / 20f);

        private void StartTrack()
        {
            // Whatever plays now fades out.
            _fadeTarget[0] = 0f;
            _fadeTarget[1] = 0f;

            AudioClip clip = null;
            float gain = 0f;
            if (_track != MusicTrack.None && _library != null && _library.TryGetMusic(_track, out AudioCueLibrary.MusicEntry entry) && entry.Clip != null)
            {
                clip = entry.Clip;
                gain = entry.Volume * (_track == MusicTrack.Table ? DecibelsToGain(TableMusicDecibels) : 1f);
            }

            if (clip == null)
            {
                return;
            }

            // Switching back before a fade-out ends picks the fading source up where it is.
            int next = -1;
            for (int i = 0; i < _music.Length; i++)
            {
                if (_music[i] != null && _music[i].clip == clip && _fade[i] > 0f)
                {
                    next = i;
                }
            }

            if (next < 0)
            {
                next = _fade[0] <= _fade[1] ? 0 : 1;
                AudioSource source = _music[next];
                if (source == null)
                {
                    return;
                }

                source.Stop();
                source.clip = clip;
                _fade[next] = 0f;
                if (!_musicMuted)
                {
                    source.Play();
                }
            }

            _trackGain[next] = gain;
            _fadeTarget[next] = 1f;
            _current = next;
        }

        private void Update(float deltaTime)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                float step = deltaTime / CrossfadeSeconds;
                for (int i = 0; i < _music.Length; i++)
                {
                    _fade[i] = Mathf.MoveTowards(_fade[i], _fadeTarget[i], step);
                    if (_fade[i] <= 0f && _fadeTarget[i] <= 0f && _music[i] != null && _music[i].isPlaying)
                    {
                        _music[i].Stop();
                    }
                }

                _duck = Mathf.MoveTowards(_duck, _duckTarget, deltaTime * (1f - DecibelsToGain(DuckDecibels)) / DuckSeconds);
                ApplyMusicVolumes();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        private void ApplySettings()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                bool muted = !_settings.Music;
                if (muted && !_musicMuted)
                {
                    _musicMuted = true;
                    for (int i = 0; i < _music.Length; i++)
                    {
                        AudioSource source = _music[i];
                        if (source == null)
                        {
                            continue;
                        }

                        if (i == _current && _fadeTarget[i] > 0f)
                        {
                            source.Pause();
                        }
                        else
                        {
                            source.Stop();
                            _fade[i] = 0f;
                        }
                    }
                }
                else if (!muted && _musicMuted)
                {
                    _musicMuted = false;
                    AudioSource source = _music[_current];
                    if (source != null && source.clip != null && _fadeTarget[_current] > 0f)
                    {
                        source.UnPause();
                        if (!source.isPlaying)
                        {
                            source.Play();
                        }
                    }
                }

                float master = _settings.Volume;
                for (int i = 0; i < _voices.Length; i++)
                {
                    AudioSource voice = _voices[i];
                    if (voice == null)
                    {
                        continue;
                    }

                    if (!_settings.SoundEffects)
                    {
                        voice.Stop();
                    }
                    else
                    {
                        voice.volume = _voiceGain[i] * master;
                    }
                }

                ApplyMusicVolumes();
            }
            catch (Exception e)
            {
                Warn(e);
            }
        }

        private void ApplyMusicVolumes()
        {
            float level = _musicMuted ? 0f : _settings.Volume * _duck;
            for (int i = 0; i < _music.Length; i++)
            {
                if (_music[i] != null)
                {
                    // Equal-power curve: the two sources' combined loudness stays level mid-fade.
                    _music[i].volume = level * _trackGain[i] * Mathf.Sin(_fade[i] * Mathf.PI * 0.5f);
                }
            }
        }

        private int NextVoice()
        {
            for (int n = 0; n < _voices.Length; n++)
            {
                int i = (_nextVoice + n) % _voices.Length;
                if (_voices[i] != null && !_voices[i].isPlaying)
                {
                    _nextVoice = (i + 1) % _voices.Length;
                    return i;
                }
            }

            // Every voice is busy: take the one started longest ago.
            int oldest = _nextVoice;
            _nextVoice = (oldest + 1) % _voices.Length;
            return oldest;
        }

        private AudioClip PickClip(SoundCue cue, IReadOnlyList<AudioClip> clips)
        {
            int count = clips.Count;
            if (count == 0)
            {
                return null;
            }

            int index = 0;
            if (count > 1)
            {
                int last = _lastClip.TryGetValue(cue, out int previous) ? previous : -1;
                index = _random.Next(last >= 0 && last < count ? count - 1 : count);
                if (last >= 0 && last < count && index >= last)
                {
                    index++;
                }
            }

            for (int n = 0; n < count; n++)
            {
                int i = (index + n) % count;
                if (clips[i] != null)
                {
                    _lastClip[cue] = i;
                    return clips[i];
                }
            }

            return null;
        }

        private static bool IsCardCue(SoundCue cue) =>
            cue == SoundCue.CardShuffle || cue == SoundCue.CardDeal || cue == SoundCue.CardPlay || cue == SoundCue.TrickCollect;

        private static bool IsUiCue(SoundCue cue) => cue == SoundCue.UiTap || cue == SoundCue.UiBack || cue == SoundCue.Toggle;

        private static GameObject CreateChild(GameObject host, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(host.transform, false);
            return child;
        }

        private static AudioSource CreateSource(GameObject owner, bool loop, int priority)
        {
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.priority = priority;
            source.volume = 0f;
            return source;
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(target);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private void Warn(Exception e)
        {
            if (_warned)
            {
                return;
            }

            _warned = true;
            Debug.LogWarning($"[AudioService] {e.GetType().Name}: {e.Message}");
        }
    }
}
