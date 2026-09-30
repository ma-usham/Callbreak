using System;
using Callbreak.Presentation.Framework;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 12: the 30-second rules primer. All copy is fixed text from the string table
    /// (ui.how_to_play.*), set in the prefab, so the view only reports the three taps.
    /// </summary>
    public sealed class HowToPlayView : UIScreen
    {
        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _gotItButton;

        public event Action BackClicked;
        public event Action SettingsClicked;
        public event Action GotItClicked;

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _settingsButton.onClick.AddListener(OnSettings);
            _gotItButton.onClick.AddListener(OnGotIt);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _gotItButton.onClick.RemoveListener(OnGotIt);
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnGotIt() => GotItClicked?.Invoke();
    }
}
