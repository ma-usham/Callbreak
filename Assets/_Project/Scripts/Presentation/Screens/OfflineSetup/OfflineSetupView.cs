using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 06: pick the bots' difficulty (Easy · Normal · Hard) and start an offline
    /// match. The chosen card shows the crimson outline and check badge. A Start button that is
    /// not interactable shows its disabled look with its label and chevron dimmed.
    /// </summary>
    public sealed class OfflineSetupView : UIScreen
    {
        public const int Easy = 0;
        public const int Normal = 1;
        public const int Hard = 2;

        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _profileButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _startButton;
        [SerializeField] private TMP_Text _startLabel;
        [SerializeField] private UnityEngine.UI.Image _startChevron;
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private SelectableGroup _difficulty;

        public event Action BackClicked;
        public event Action ProfileClicked;
        public event Action SettingsClicked;

        /// <summary>Raised when the player taps another card: 0 = Easy, 1 = Normal, 2 = Hard.</summary>
        public event Action<int> DifficultyChanged;

        public event Action StartClicked;

        public void SetPlayerName(string playerName)
        {
            if (_playerName.text != playerName)
            {
                _playerName.text = playerName;
            }
        }

        /// <summary>Shows the selection (0 = Easy, 1 = Normal, 2 = Hard) without raising <see cref="DifficultyChanged"/>.</summary>
        public void SetDifficulty(int difficulty)
        {
            _difficulty.SetSelectedWithoutNotify(difficulty);
        }

        public void SetStartInteractable(bool interactable)
        {
            _startButton.interactable = interactable;
            float alpha = interactable ? 1f : DimmedAlpha;
            SetAlpha(_startLabel, alpha);
            SetAlpha(_startChevron, alpha);
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _profileButton.onClick.AddListener(OnProfile);
            _settingsButton.onClick.AddListener(OnSettings);
            _startButton.onClick.AddListener(OnStart);
            _difficulty.Changed += OnDifficultyChanged;
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _profileButton.onClick.RemoveListener(OnProfile);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _startButton.onClick.RemoveListener(OnStart);
            _difficulty.Changed -= OnDifficultyChanged;
        }

        private static void SetAlpha(UnityEngine.UI.Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnProfile() => ProfileClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnStart() => StartClicked?.Invoke();

        private void OnDifficultyChanged(int difficulty) => DifficultyChanged?.Invoke(difficulty);
    }
}
