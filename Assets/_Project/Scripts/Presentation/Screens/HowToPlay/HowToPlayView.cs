using System;
using Callbreak.Presentation.Framework;
using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 12: the 30-second rules primer. All copy is fixed text from the string table
    /// (ui.how_to_play.*), set in the prefab, so the view only reports the three taps. On a first
    /// launch it is the backdrop of the name question (ADR 022): only its background and the logo
    /// show until the name is answered, and then the rules. Shown for the first time, the rules
    /// sit over the background and logo dimmed as under the name question, with no back or
    /// settings button: Got It is the way on. When the rules appear after the name, the dim fades
    /// in while the name panel's fades out, so the background stays about as dark, and the rules
    /// pop in as the name panel goes (ADR 023).
    /// </summary>
    public sealed class HowToPlayView : UIScreen
    {
        // The name panel is mostly gone by then.
        private const float RevealDelay = 0.1f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _gotItButton;
        [SerializeField] private GameObject _panel;
        [SerializeField] private GameObject _dim;

        private bool _rulesVisible = true;
        private bool _firstTime;
        private MotionHandle _panelMotion;
        private MotionHandle _dimMotion;

        /// <summary>With Reduce motion the rules only fade in (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        public event Action BackClicked;
        public event Action SettingsClicked;
        public event Action GotItClicked;

        /// <summary>False while only the background and the logo show.</summary>
        public bool RulesVisible => _rulesVisible;

        /// <summary>True while the primer is shown for the first time: dimmed behind the rules, no header buttons.</summary>
        public bool IsFirstTime => _firstTime;

        /// <summary>
        /// The rules panel; without it the screen is the background and the logo. Shown again
        /// while the screen is up, the rules come in (see the class summary).
        /// </summary>
        public void SetRulesVisible(bool visible)
        {
            bool reveal = visible && !_rulesVisible && isActiveAndEnabled;
            _rulesVisible = visible;
            Apply();
            if (reveal)
            {
                Reveal();
            }
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

        private void Reveal()
        {
            if (_dim != null && _dim.activeSelf)
            {
                // Linear, as the name panel's dim fades out over the same time: together they
                // stay within a few percent of one dim.
                CanvasGroup dim = UiMotion.GroupOf(_dim);
                UiMotion.Stop(ref _dimMotion);
                dim.alpha = 0f;
                _dimMotion = LMotion.Create(0f, 1f, UiMotion.Short)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(dim, (alpha, group) => group.alpha = alpha);
            }

            if (_panel != null)
            {
                UiMotion.Stop(ref _panelMotion);
                _panelMotion = UiMotion.PopIn(_panel.transform, UiMotion.GroupOf(_panel), ReduceMotion, delay: RevealDelay);
            }
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

            // A reveal cut short leaves the dim and the rules as they are at rest.
            UiMotion.Finish(ref _dimMotion);
            UiMotion.Finish(ref _panelMotion);
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnSettings() => SettingsClicked?.Invoke();

        private void OnGotIt() => GotItClicked?.Invoke();
    }
}
