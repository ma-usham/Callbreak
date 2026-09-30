using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 11: sound, language and comfort options. Every control raises its intent
    /// the moment it changes; the presenter applies and saves it. Setters never raise events.
    /// </summary>
    public sealed class SettingsView : UIScreen
    {
        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _profileButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private UnityEngine.UI.Image _avatar;
        [SerializeField] private ToggleSwitch _musicToggle;
        [SerializeField] private ToggleSwitch _soundEffectsToggle;
        [SerializeField] private UnityEngine.UI.Slider _volumeSlider;
        [SerializeField] private TMP_Text _volumeValue;
        [SerializeField] private UnityEngine.UI.Button _languageButton;
        [SerializeField] private TMP_Text _languageValue;
        [SerializeField] private ToggleSwitch _vibrationToggle;
        [SerializeField] private ToggleSwitch _reduceMotionToggle;
        [SerializeField] private UnityEngine.UI.Button _howToPlayButton;
        [SerializeField] private UnityEngine.UI.Button _aboutButton;
        [SerializeField] private UnityEngine.UI.Button _doneButton;

        public event Action BackClicked;
        public event Action ProfileClicked;
        public event Action SettingsClicked;
        public event Action<bool> MusicChanged;
        public event Action<bool> SoundEffectsChanged;

        /// <summary>The volume slider moved; the value is 0-1.</summary>
        public event Action<float> VolumeChanged;
        public event Action LanguageClicked;
        public event Action<bool> VibrationChanged;
        public event Action<bool> ReduceMotionChanged;
        public event Action HowToPlayClicked;
        public event Action AboutClicked;
        public event Action DoneClicked;

        /// <summary>The app is going to the background: anything not yet written should be written now.</summary>
        public event Action Suspended;

        public void SetPlayerName(string playerName) => SetText(_playerName, playerName);

        /// <summary>The picked avatar preset. A missing sprite keeps the current picture.</summary>
        public void SetAvatar(Sprite avatar)
        {
            if (avatar != null && _avatar.sprite != avatar)
            {
                _avatar.sprite = avatar;
            }
        }

        public void SetMusic(bool on) => _musicToggle.SetIsOnWithoutNotify(on);

        public void SetSoundEffects(bool on) => _soundEffectsToggle.SetIsOnWithoutNotify(on);

        /// <summary>Moves the volume slider (0-1) without raising <see cref="VolumeChanged"/>.</summary>
        public void SetVolume(float volume) => _volumeSlider.SetValueWithoutNotify(Mathf.Clamp01(volume));

        /// <summary>The value shown next to "Volume", e.g. "70%".</summary>
        public void SetVolumeText(string text) => SetText(_volumeValue, text);

        /// <summary>The current language's own name on the dropdown button ("English", "नेपाली").</summary>
        public void SetLanguage(string languageName) => SetText(_languageValue, languageName);

        public void SetVibration(bool on) => _vibrationToggle.SetIsOnWithoutNotify(on);

        public void SetReduceMotion(bool on) => _reduceMotionToggle.SetIsOnWithoutNotify(on);

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _profileButton.onClick.AddListener(OnProfile);
            _settingsButton.onClick.AddListener(OnSettings);
            _musicToggle.Changed += OnMusic;
            _soundEffectsToggle.Changed += OnSoundEffects;
            _volumeSlider.onValueChanged.AddListener(OnVolume);
            _languageButton.onClick.AddListener(OnLanguage);
            _vibrationToggle.Changed += OnVibration;
            _reduceMotionToggle.Changed += OnReduceMotion;
            _howToPlayButton.onClick.AddListener(OnHowToPlay);
            _aboutButton.onClick.AddListener(OnAbout);
            _doneButton.onClick.AddListener(OnDone);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _profileButton.onClick.RemoveListener(OnProfile);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _musicToggle.Changed -= OnMusic;
            _soundEffectsToggle.Changed -= OnSoundEffects;
            _volumeSlider.onValueChanged.RemoveListener(OnVolume);
            _languageButton.onClick.RemoveListener(OnLanguage);
            _vibrationToggle.Changed -= OnVibration;
            _reduceMotionToggle.Changed -= OnReduceMotion;
            _howToPlayButton.onClick.RemoveListener(OnHowToPlay);
            _aboutButton.onClick.RemoveListener(OnAbout);
            _doneButton.onClick.RemoveListener(OnDone);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Suspended?.Invoke();
            }
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnProfile() => ProfileClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnMusic(bool on) => MusicChanged?.Invoke(on);

        private void OnSoundEffects(bool on) => SoundEffectsChanged?.Invoke(on);

        private void OnVolume(float value) => VolumeChanged?.Invoke(value);

        private void OnLanguage() => LanguageClicked?.Invoke();

        private void OnVibration(bool on) => VibrationChanged?.Invoke(on);

        private void OnReduceMotion(bool on) => ReduceMotionChanged?.Invoke(on);

        private void OnHowToPlay() => HowToPlayClicked?.Invoke();

        private void OnAbout() => AboutClicked?.Invoke();

        private void OnDone() => DoneClicked?.Invoke();
    }
}
