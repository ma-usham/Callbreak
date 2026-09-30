using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 11: sound, language and comfort options. Every change applies at once and is
    /// saved at once (the volume while it is dragged, its file write when the drag settles); Done
    /// simply closes. The language button steps through the languages that have strings.
    /// </summary>
    public sealed class SettingsPresenter : IStartable, ITickable, IDisposable
    {
        /// <summary>The volume is written to disk this long after the slider last moved.</summary>
        private const double VolumeSaveDelaySeconds = 0.4;

        private static readonly Dictionary<string, string> LanguageNames = new Dictionary<string, string>
        {
            { "en", "English" },
            { "ne", "नेपाली" },
        };

        private readonly SettingsView _view;
        private readonly ScreenRouter _router;
        private readonly ISettingsService _settings;
        private readonly IProfileService _profile;
        private readonly ILocalization _localization;
        private readonly IDialogs _dialogs;
        private readonly IClock _clock;
        private readonly IHaptics _haptics;

        private bool _volumeDirty;
        private float _pendingVolume;
        private double _volumeSaveAt;

        public SettingsPresenter(
            SettingsView view,
            ScreenRouter router,
            ISettingsService settings,
            IProfileService profile,
            ILocalization localization,
            IDialogs dialogs,
            IClock clock,
            IHaptics haptics)
        {
            _view = view;
            _router = router;
            _settings = settings;
            _profile = profile;
            _localization = localization;
            _dialogs = dialogs;
            _clock = clock;
            _haptics = haptics;
        }

        public void Start()
        {
            _view.BackClicked += Close;
            _view.DoneClicked += Close;
            _view.BackRequested += Close;
            _view.ProfileClicked += OnProfile;
            _view.MusicChanged += _settings.SetMusic;
            _view.SoundEffectsChanged += _settings.SetSoundEffects;
            _view.VolumeChanged += OnVolume;
            _view.LanguageClicked += OnLanguage;
            _view.VibrationChanged += OnVibration;
            _view.ReduceMotionChanged += _settings.SetReduceMotion;
            _view.HowToPlayClicked += OnHowToPlay;
            _view.AboutClicked += OnAbout;
            _settings.Changed += Render;
            _profile.Changed += Render;
            _localization.LanguageChanged += Render;
            Render();
        }

        public void Tick()
        {
            if (_volumeDirty && _clock.Now >= _volumeSaveAt)
            {
                FlushVolume();
            }
        }

        public void Dispose()
        {
            FlushVolume();
            _view.BackClicked -= Close;
            _view.DoneClicked -= Close;
            _view.BackRequested -= Close;
            _view.ProfileClicked -= OnProfile;
            _view.MusicChanged -= _settings.SetMusic;
            _view.SoundEffectsChanged -= _settings.SetSoundEffects;
            _view.VolumeChanged -= OnVolume;
            _view.LanguageClicked -= OnLanguage;
            _view.VibrationChanged -= OnVibration;
            _view.ReduceMotionChanged -= _settings.SetReduceMotion;
            _view.HowToPlayClicked -= OnHowToPlay;
            _view.AboutClicked -= OnAbout;
            _settings.Changed -= Render;
            _profile.Changed -= Render;
            _localization.LanguageChanged -= Render;
        }

        private void Render()
        {
            _view.SetPlayerName(_profile.DisplayName);
            _view.SetMusic(_settings.Music);
            _view.SetSoundEffects(_settings.SoundEffects);
            float volume = _volumeDirty ? _pendingVolume : _settings.Volume;
            _view.SetVolume(volume);
            _view.SetVolumeText(PercentText(volume));
            _view.SetLanguage(NameOf(_localization.Language));
            _view.SetVibration(_settings.Vibration);
            _view.SetReduceMotion(_settings.ReduceMotion);
        }

        private void Close()
        {
            FlushVolume();
            _router.Pop();
        }

        private void OnProfile() => _router.Push(ScreenId.Profile);

        private void OnHowToPlay() => _router.Push(ScreenId.HowToPlay);

        private void OnVolume(float volume)
        {
            _pendingVolume = Mathf.Clamp01(volume);
            _volumeDirty = true;
            _volumeSaveAt = _clock.Now + VolumeSaveDelaySeconds;
            _view.SetVolumeText(PercentText(_pendingVolume));
        }

        private void FlushVolume()
        {
            if (!_volumeDirty)
            {
                return;
            }

            _volumeDirty = false;
            _settings.SetVolume(_pendingVolume);
        }

        private void OnVibration(bool on)
        {
            _settings.SetVibration(on);
            if (on)
            {
                _haptics.Play(HapticPattern.Light);
            }
        }

        /// <summary>Steps to the next language that has strings; with only one there is nothing to change.</summary>
        private void OnLanguage()
        {
            IReadOnlyList<string> languages = _localization.Languages;
            if (languages.Count < 2)
            {
                return;
            }

            int current = 0;
            for (int i = 0; i < languages.Count; i++)
            {
                if (languages[i] == _localization.Language)
                {
                    current = i;
                    break;
                }
            }

            _localization.SetLanguage(languages[(current + 1) % languages.Count]);
        }

        private void OnAbout()
        {
            string body = _localization.Format("ui.about.body", UnityEngine.Application.version);
            _ = _dialogs.ConfirmAsync(
                new DialogRequest(_localization.Get("ui.settings.about"), body, _localization.Get("ui.settings.done"), null),
                CancellationToken.None);
        }

        private static string PercentText(float volume) =>
            Mathf.RoundToInt(volume * 100f).ToString(CultureInfo.InvariantCulture) + "%";

        private static string NameOf(string language) =>
            LanguageNames.TryGetValue(language, out string name) ? name : language;
    }
}
