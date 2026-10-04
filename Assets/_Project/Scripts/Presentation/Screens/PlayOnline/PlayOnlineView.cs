using System;
using System.Collections.Generic;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// Screen 23, Play Online (mockups 23 and 24; the designer's online-match-options-prompts.md):
    /// one panel with Quick Match on the left and Stake Match on the right, the wallet at the top
    /// right. Stake Match has one chip per stake (the chosen one crimson, the others ivory), the
    /// "i" button and the play button that names the stake; the "i" opens the prize popover under
    /// it, which pops in from its pointer and shrinks away (with Reduce motion it only fades). While
    /// the popover is open, a tap on anything that is not a control (the panel, the scenery) closes
    /// it: an invisible catcher lies under every control of the screen, and the controls keep
    /// working (a chip shows the other stake's prizes). Renders what its presenter gives it and
    /// reports every intent.
    /// </summary>
    public sealed class PlayOnlineView : UIScreen
    {
        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private TMP_Text _balance;
        [SerializeField] private UnityEngine.UI.Button _playQuickButton;

        // The stake chips (their looks and label colours follow the selection) and their amounts.
        [SerializeField] private SelectableGroup _stakeSelector;
        [SerializeField] private TMP_Text[] _chipLabel;

        [SerializeField] private UnityEngine.UI.Button _infoButton;
        [SerializeField] private UnityEngine.UI.Button _playStakeButton;
        [SerializeField] private TMP_Text _playStakeLabel;

        // The prize popover: its heading, the prize of each place, its close button and the
        // catcher under the screen's controls that closes it on a tap elsewhere.
        [SerializeField] private RectTransform _popover;
        [SerializeField] private TMP_Text _prizeHeading;
        [SerializeField] private TMP_Text[] _prizeAmount;
        [SerializeField] private UnityEngine.UI.Button _popoverClose;
        [SerializeField] private UnityEngine.UI.Button _popoverDismiss;

        private MotionHandle _popoverMotion;
        private bool _prizesOpen;
        private bool _reduceMotion;

        public event Action BackClicked;
        public event Action PlayQuickClicked;

        /// <summary>The player tapped another stake chip (its index, smallest stake first).</summary>
        public event Action<int> StakeChosen;

        /// <summary>The "i" button was tapped.</summary>
        public event Action InfoClicked;

        public event Action PlayStakeClicked;

        /// <summary>The popover's × was tapped, or the screen somewhere that is not a control.</summary>
        public event Action PrizesCloseClicked;

        public int ChipCount => _chipLabel.Length;

        public int PrizeCount => _prizeAmount.Length;

        /// <summary>Whether the prize popover is open (or opening): it is still on screen while it shrinks away.</summary>
        public bool PrizesOpen => _prizesOpen;

        public int SelectedChip => _stakeSelector.SelectedIndex;

        public void SetBalance(string coins) => SetText(_balance, coins);

        /// <summary>The amount written on a stake chip ("50").</summary>
        public void SetChip(int index, string amount) => SetText(_chipLabel[index], amount);

        /// <summary>Shows the chosen chip, without raising <see cref="StakeChosen"/>; a chip that is already chosen keeps its fade.</summary>
        public void SetSelectedChip(int index)
        {
            if (_stakeSelector.SelectedIndex != index)
            {
                _stakeSelector.SetSelectedWithoutNotify(index);
            }
        }

        /// <summary>The Stake Match play button's label ("Play for 50 coins").</summary>
        public void SetPlayStake(string label) => SetText(_playStakeLabel, label);

        /// <summary>The popover's heading ("50-coin prizes") and the prize of each place, 1st first.</summary>
        public void SetPrizes(string heading, IReadOnlyList<string> amounts)
        {
            SetText(_prizeHeading, heading);
            for (int i = 0; i < _prizeAmount.Length; i++)
            {
                SetText(_prizeAmount[i], amounts != null && i < amounts.Count ? amounts[i] : string.Empty);
            }
        }

        /// <summary>
        /// Opens or closes the prize popover. <paramref name="animate"/> false puts it in its state
        /// at once (when the screen is not on show).
        /// </summary>
        public void ShowPrizes(bool open, bool animate)
        {
            // Already in that state, or on its way there (the popover as built is open, the view not).
            if (open == _prizesOpen && (open || !_popover.gameObject.activeSelf || _popoverMotion.IsActive()))
            {
                if (!animate)
                {
                    UiMotion.Finish(ref _popoverMotion);
                }

                return;
            }

            _prizesOpen = open;
            UiMotion.Stop(ref _popoverMotion);
            CanvasGroup group = UiMotion.GroupOf(_popover.gameObject);

            // Only an open popover catches the taps that close it.
            _popoverDismiss.gameObject.SetActive(open);
            if (open)
            {
                _popover.gameObject.SetActive(true);
                if (animate && isActiveAndEnabled)
                {
                    _popoverMotion = UiMotion.PopIn(_popover, group, _reduceMotion, seconds: UiMotion.Short);
                }
                else
                {
                    _popover.localScale = Vector3.one;
                    group.alpha = 1f;
                }

                return;
            }

            if (animate && isActiveAndEnabled && _popover.gameObject.activeSelf)
            {
                _popoverMotion = UiMotion.PopOut(_popover, group, _reduceMotion, HidePopover);
            }
            else
            {
                HidePopover();
            }
        }

        /// <summary>With Reduce motion the popover only fades in and out (GDD §14).</summary>
        public void SetReduceMotion(bool on) => _reduceMotion = on;

        public override void OnHidden()
        {
            // Whatever was open is closed for the next visit.
            ShowPrizes(false, animate: false);
        }

        private void HidePopover()
        {
            _popover.gameObject.SetActive(false);
            _popover.localScale = Vector3.one;
            UiMotion.GroupOf(_popover.gameObject).alpha = 1f;
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _playQuickButton.onClick.AddListener(OnPlayQuick);
            _infoButton.onClick.AddListener(OnInfo);
            _playStakeButton.onClick.AddListener(OnPlayStake);
            _popoverClose.onClick.AddListener(OnPrizesClose);
            _popoverDismiss.onClick.AddListener(OnPrizesClose);
            _stakeSelector.Changed += OnStakeChanged;
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _playQuickButton.onClick.RemoveListener(OnPlayQuick);
            _infoButton.onClick.RemoveListener(OnInfo);
            _playStakeButton.onClick.RemoveListener(OnPlayStake);
            _popoverClose.onClick.RemoveListener(OnPrizesClose);
            _popoverDismiss.onClick.RemoveListener(OnPrizesClose);
            _stakeSelector.Changed -= OnStakeChanged;

            // A pop cut short ends in its final state.
            UiMotion.Finish(ref _popoverMotion);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnPlayQuick() => PlayQuickClicked?.Invoke();

        private void OnInfo() => InfoClicked?.Invoke();

        private void OnPlayStake() => PlayStakeClicked?.Invoke();

        private void OnPrizesClose() => PrizesCloseClicked?.Invoke();

        private void OnStakeChanged(int index) => StakeChosen?.Invoke(index);
    }
}
