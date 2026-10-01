using System;
using Callbreak.Presentation.Framework;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 12: the 30-second rules primer. All copy is fixed text from the string table
    /// (ui.how_to_play.*), set in the prefab, so the view only reports the three taps. On a first
    /// launch it is the backdrop of the name question (ADR 022): only its background and the logo
    /// show until the name is answered, and then the rules. Shown for the first time, the rules
    /// sit over the background and logo dimmed as under the name question, with no back or
    /// settings button: Got It is the way on.
    /// </summary>
    public sealed class HowToPlayView : UIScreen
    {
        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _gotItButton;
        [SerializeField] private GameObject _panel;
        [SerializeField] private GameObject _dim;

        private bool _rulesVisible = true;
        private bool _firstTime;

        public event Action BackClicked;
        public event Action SettingsClicked;
        public event Action GotItClicked;

        /// <summary>False while only the background and the logo show.</summary>
        public bool RulesVisible => _rulesVisible;

        /// <summary>True while the primer is shown for the first time: dimmed behind the rules, no header buttons.</summary>
        public bool IsFirstTime => _firstTime;

        /// <summary>The rules panel; without it the screen is the background and the logo.</summary>
        public void SetRulesVisible(bool visible)
        {
            _rulesVisible = visible;
            Apply();
        }

        /// <summary>The first showing's look: the dim behind the rules, and no back or settings button.</summary>
        public void SetFirstTime(bool firstTime)
        {
            _firstTime = firstTime;
            Apply();
        }

        private void Apply()
        {
            if (_panel != null)
            {
                _panel.SetActive(_rulesVisible);
            }

            if (_dim != null)
            {
                _dim.SetActive(_rulesVisible && _firstTime);
            }

            bool buttons = _rulesVisible && !_firstTime;
            _backButton.gameObject.SetActive(buttons);
            _settingsButton.gameObject.SetActive(buttons);
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
