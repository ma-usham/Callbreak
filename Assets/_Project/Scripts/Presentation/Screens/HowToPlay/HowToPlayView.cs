using System;
using Callbreak.Presentation.Framework;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 12: the 30-second rules primer. All copy is fixed text from the string table
    /// (ui.how_to_play.*), set in the prefab, so the view only reports the three taps. On a first
    /// launch it is the backdrop of the name question (ADR 022): only its background and the logo
    /// show until the name is answered, and then the rules.
    /// </summary>
    public sealed class HowToPlayView : UIScreen
    {
        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _gotItButton;
        [SerializeField] private GameObject _panel;

        public event Action BackClicked;
        public event Action SettingsClicked;
        public event Action GotItClicked;

        /// <summary>False while only the background and the logo show.</summary>
        public bool RulesVisible => _panel == null || _panel.activeSelf;

        /// <summary>The rules panel and the header buttons; without them the screen is the background and the logo.</summary>
        public void SetRulesVisible(bool visible)
        {
            if (_panel != null)
            {
                _panel.SetActive(visible);
            }

            _backButton.gameObject.SetActive(visible);
            _settingsButton.gameObject.SetActive(visible);
        }

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
