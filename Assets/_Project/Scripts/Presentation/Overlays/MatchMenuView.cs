using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Overlays
{
    /// <summary>
    /// GDD §11 screen 13, the match menu: "Game Menu" over the dimmed table, with a close ×, the
    /// mode tag ("Offline match · Paused" or "Online game"), Resume Game (Back to Game online),
    /// Scorecard, the Sound switch, Leave Match, and "Your match is paused." for offline matches.
    /// Leaving is confirmed by the shared dialog, not here. It is a panel over the table, not a
    /// page: it does not slide, the dim fades in and the panel pops in like the dialog's.
    /// </summary>
    public sealed class MatchMenuView : UIScreen
    {
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [SerializeField] private UnityEngine.UI.Button _resumeButton;
        [SerializeField] private TMP_Text _resumeLabel;
        [SerializeField] private UnityEngine.UI.Button _scorecardButton;
        [SerializeField] private UnityEngine.UI.Button _leaveButton;
        [SerializeField] private ToggleSwitch _soundToggle;
        [SerializeField] private TMP_Text _modeLabel;

        // The mode tag hugs its words (the owner, 3 Oct 2026: "Online game" sat in the pill drawn for
        // "Offline match · Paused"). Its art and its words' area span the tag, so only its width
        // changes, round its centre. The margins are mockup 13's: the offline tag keeps its width.
        [SerializeField] private RectTransform _modePill;
        [SerializeField] private float _modePadding = 49f;
        [SerializeField] private GameObject _pausedNote;

        private MotionHandle _panelMotion;

        public override bool Slides => false;

        /// <summary>With Reduce motion the panel only fades with the screen (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        // The "Game Menu" modal: every button sits on it.
        private Transform Panel => _resumeButton.transform.parent;

        public event Action CloseClicked;

        public event Action ResumeClicked;

        public event Action ScorecardClicked;

        public event Action LeaveClicked;

        /// <summary>The Sound switch was flipped: its new state.</summary>
        public event Action<bool> SoundChanged;

        /// <summary>"Offline match · Paused" or "Online game"; the tag hugs its words.</summary>
        public void SetMode(string text)
        {
            text ??= string.Empty;
            if (_modeLabel.text != text)
            {
                _modeLabel.text = text;
            }

            // A MatchMenu prefab built before the tag hugged its words keeps its width.
            if (_modePill != null)
            {
                float width = Mathf.Ceil(_modeLabel.GetPreferredValues(text).x) + _modePadding;
                _modePill.sizeDelta = new Vector2(width, _modePill.sizeDelta.y);
            }
        }

        /// <summary>The first button's label: "Resume Game" offline, "Back to Game" online.</summary>
        public void SetResumeLabel(string text)
        {
            text ??= string.Empty;
            if (_resumeLabel != null && _resumeLabel.text != text)
            {
                _resumeLabel.text = text;
            }
        }

        /// <summary>"Your match is paused." shows for offline matches only.</summary>
        public void SetPausedNoteVisible(bool visible)
        {
            if (_pausedNote.activeSelf != visible)
            {
                _pausedNote.SetActive(visible);
            }
        }

        public void SetSound(bool on)
        {
            _soundToggle.SetIsOnWithoutNotify(on);
        }

        public override void OnShown()
        {
            UiMotion.Stop(ref _panelMotion);
            _panelMotion = UiMotion.PopIn(Panel, null, ReduceMotion);
        }

        public override void OnHidden()
        {
            UiMotion.Stop(ref _panelMotion);
            _panelMotion = UiMotion.PopOut(Panel, null, ReduceMotion, null);
        }

        private void OnDestroy()
        {
            UiMotion.Stop(ref _panelMotion);
        }

        private void OnEnable()
        {
            _closeButton.onClick.AddListener(OnClose);
            _resumeButton.onClick.AddListener(OnResume);
            _scorecardButton.onClick.AddListener(OnScorecard);
            _leaveButton.onClick.AddListener(OnLeave);
            _soundToggle.Changed += OnSound;
        }

        private void OnDisable()
        {
            _closeButton.onClick.RemoveListener(OnClose);
            _resumeButton.onClick.RemoveListener(OnResume);
            _scorecardButton.onClick.RemoveListener(OnScorecard);
            _leaveButton.onClick.RemoveListener(OnLeave);
            _soundToggle.Changed -= OnSound;
        }

        private void OnClose() => CloseClicked?.Invoke();

        private void OnResume() => ResumeClicked?.Invoke();

        private void OnScorecard() => ScorecardClicked?.Invoke();

        private void OnLeave() => LeaveClicked?.Invoke();

        private void OnSound(bool on) => SoundChanged?.Invoke(on);
    }
}
