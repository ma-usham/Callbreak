using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Overlays
{
    /// <summary>
    /// GDD §11 screen 13, the match menu: "Game Menu" over the dimmed table, with a close ×, the
    /// mode tag ("Offline match · Paused" or "Online match · Game continues"), Resume Game,
    /// Scorecard, the Sound switch, Leave Match, and "Your match is paused." for offline matches.
    /// Leaving is confirmed by the shared dialog, not here.
    /// </summary>
    public sealed class MatchMenuView : UIScreen
    {
        [SerializeField] private UnityEngine.UI.Button _closeButton;
        [SerializeField] private UnityEngine.UI.Button _resumeButton;
        [SerializeField] private UnityEngine.UI.Button _scorecardButton;
        [SerializeField] private UnityEngine.UI.Button _leaveButton;
        [SerializeField] private ToggleSwitch _soundToggle;
        [SerializeField] private TMP_Text _modeLabel;
        [SerializeField] private GameObject _pausedNote;

        public event Action CloseClicked;

        public event Action ResumeClicked;

        public event Action ScorecardClicked;

        public event Action LeaveClicked;

        /// <summary>The Sound switch was flipped: its new state.</summary>
        public event Action<bool> SoundChanged;

        /// <summary>"Offline match · Paused" or "Online match · Game continues".</summary>
        public void SetMode(string text)
        {
            text ??= string.Empty;
            if (_modeLabel.text != text)
            {
                _modeLabel.text = text;
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
