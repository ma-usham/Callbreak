using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 05: Quick Match search. The crimson arc spins around the still spade while
    /// the presenter reports progress, fills the four seats as players connect (the player's own
    /// first) and ticks the time in the pill: the search's countdown, then the table's start
    /// countdown (ADR 029), or the honest elapsed time. Cancel withdraws the ticket. A button that is
    /// not interactable shows its disabled look with its label dimmed.
    /// </summary>
    public sealed class MatchmakingView : UIScreen
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Image _backIcon;
        [SerializeField] private UnityEngine.UI.Button _profileButton;
        [SerializeField] private UnityEngine.UI.Button _settingsButton;
        [SerializeField] private UnityEngine.UI.Button _shopButton;
        [SerializeField] private UnityEngine.UI.Button _cancelButton;
        [SerializeField] private TMP_Text _cancelLabel;
        [SerializeField] private UnityEngine.UI.Image _avatar;
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private TMP_Text _statusTitle;
        [SerializeField] private TMP_Text _statusSubtitle;
        [SerializeField] private MatchmakingSeat[] _seat;
        [SerializeField] private TMP_Text _elapsed;

        private bool _reduceMotion;
        private int _clockShown;
        private int _rollFrom;
        private int _rollTo;
        private MotionHandle _roll;

        public event Action BackClicked;
        public event Action ProfileClicked;
        public event Action SettingsClicked;
        public event Action ShopClicked;
        public event Action CancelClicked;

        public int SeatCount => _seat.Length;

        public void SetPlayerName(string playerName)
        {
            SetText(_playerName, playerName);
        }

        /// <summary>The player's avatar preset in the header. A missing sprite keeps the picture it shows (the default one).</summary>
        public void SetAvatar(Sprite avatar)
        {
            if (avatar != null)
            {
                _avatar.sprite = avatar;
            }
        }

        /// <summary>
        /// The whole screen for one search report: the seats, the status lines, the pill, and
        /// whether Cancel and the back arrow can be used (not while a found table counts down).
        /// </summary>
        public void Show(MatchmakingScreenState state)
        {
            for (int i = 0; i < _seat.Length && i < state.Seats.Count; i++)
            {
                MatchmakingScreenState.Seat seat = state.Seats[i];
                SetSeat(i, seat.Filled, seat.Name, seat.Status);
            }

            SetStatus(state.Title, state.Subtitle);
            SetClock(state.ClockSeconds, state.Clock);
            SetCancelInteractable(state.CanLeave);
            SetBackInteractable(state.CanLeave);
        }

        /// <summary>"Finding players..." · "3 of 4 players connected" (or "Table found", an error line...).</summary>
        public void SetStatus(string title, string subtitle)
        {
            SetText(_statusTitle, title);
            SetText(_statusSubtitle, subtitle);
        }

        /// <summary>Seat 0 is the local player. Filled shows the avatar disc, otherwise the dashed searching circle.</summary>
        public void SetSeat(int index, bool filled, string playerName, string status)
        {
            _seat[index].Show(filled, playerName, status);
        }

        /// <summary>A filled seat's portrait (ADR 041); a null keeps the one it shows.</summary>
        public void SetSeatAvatar(int index, Sprite avatar)
        {
            _seat[index].SetAvatar(avatar);
        }

        /// <summary>With Reduce motion a seat that fills only fades its player in, and the pill's time just changes (GDD §14).</summary>
        public void SetReduceMotion(bool on)
        {
            _reduceMotion = on;
            foreach (MatchmakingSeat seat in _seat)
            {
                seat.ReduceMotion = on;
            }
        }

        /// <summary>
        /// The time in the pill, "00:20". When the table's start countdown takes over from an own
        /// countdown further away, the pill counts down to it quickly instead of jumping
        /// (<see cref="MatchmakingDisplay.RollsDown"/>); a count already on its way finishes.
        /// </summary>
        public void SetClock(int seconds, MatchmakingDisplay.ClockKind clock)
        {
            if (_roll.IsActive())
            {
                if (seconds == _rollTo)
                {
                    return;
                }

                UiMotion.Stop(ref _roll);
            }

            if (MatchmakingDisplay.RollsDown(_clockShown, seconds, clock, _reduceMotion) && isActiveAndEnabled)
            {
                _rollFrom = _clockShown;
                _rollTo = seconds;
                _roll = LMotion.Create(0f, 1f, UiMotion.Medium)
                    .Bind(this, (t, view) => view.ShowClock(MatchmakingDisplay.RollStep(view._rollFrom, view._rollTo, t)));
                return;
            }

            ShowClock(seconds);
        }

        private void ShowClock(int seconds)
        {
            _clockShown = seconds;
            SetText(_elapsed, MatchmakingDisplay.FormatClock(seconds));
        }

        public void SetCancelInteractable(bool interactable)
        {
            _cancelButton.interactable = interactable;
            SetAlpha(_cancelLabel, interactable ? 1f : DimmedAlpha);
        }

        /// <summary>The back arrow, greyed like the other disabled buttons (its disc tinted, its arrow dimmed).</summary>
        public void SetBackInteractable(bool interactable)
        {
            _backButton.interactable = interactable;
            if (_backIcon != null)
            {
                SetAlpha(_backIcon, interactable ? 1f : DimmedAlpha);
            }
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _profileButton.onClick.AddListener(OnProfile);
            _settingsButton.onClick.AddListener(OnSettings);
            _shopButton.onClick.AddListener(OnShop);
            _cancelButton.onClick.AddListener(OnCancel);
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _profileButton.onClick.RemoveListener(OnProfile);
            _settingsButton.onClick.RemoveListener(OnSettings);
            _shopButton.onClick.RemoveListener(OnShop);
            _cancelButton.onClick.RemoveListener(OnCancel);
            UiMotion.Finish(ref _roll);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
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

        private void OnShop() => ShopClicked?.Invoke();

        private void OnCancel() => CancelClicked?.Invoke();
    }
}
