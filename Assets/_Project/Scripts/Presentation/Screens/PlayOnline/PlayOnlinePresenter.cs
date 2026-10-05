using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// Screen 23, Play Online (the owner, 5 Oct 2026; mockups 23 and 24 and the designer's
    /// online-match-options-prompts.md), opened by the main menu's PLAY ONLINE. Quick Match plays
    /// without a stake; Stake Match offers the stakes of <see cref="StakeRules"/>, the smallest
    /// chosen at first. The chips start on the last stake the player chose, on a chip or by playing
    /// it, also after a Quick Match and after a restart (the owner, 5 Oct 2026;
    /// <see cref="IOnlineMatchChoice.LastStake"/>). Choosing a stake
    /// or looking at its prizes takes no coins. Play Quick picks Quick Match and "Play for N coins"
    /// picks that stake, then the search opens as before (screen 05); when the wallet can't cover
    /// the stake (<see cref="IStakeLedger.CanAfford"/>), the shop's "Not enough coins" question
    /// offers the Earn Coins page or Not now, and nothing is searched or chosen. The "i" button
    /// opens the chosen stake's prizes by place under it, and closes them again; choosing the other
    /// stake while they are open shows that stake's. The popover's × or a tap anywhere that is not
    /// a control closes it, and Android back closes it before it leaves the screen. Back returns to
    /// the main menu.
    /// </summary>
    public sealed class PlayOnlinePresenter : IStartable, IDisposable
    {
        private readonly PlayOnlineView _view;
        private readonly ScreenRouter _router;
        private readonly IOnlineMatchChoice _choice;
        private readonly IWallet _wallet;
        private readonly IStakeLedger _stakes;
        private readonly IDialogs _dialogs;
        private readonly ILocalization _localization;
        private readonly ISettingsService _settings;
        private readonly MatchLauncher _launcher;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly List<string> _prizeTexts = new List<string>();

        private int _stake;
        private bool _prizesOpen;
        private bool _asking;
        private bool _disposed;

        public PlayOnlinePresenter(
            PlayOnlineView view,
            ScreenRouter router,
            IOnlineMatchChoice choice,
            IWallet wallet,
            IStakeLedger stakes,
            IDialogs dialogs,
            ILocalization localization,
            ISettingsService settings,
            MatchLauncher launcher)
        {
            _view = view;
            _router = router;
            _choice = choice;
            _wallet = wallet;
            _stakes = stakes;
            _dialogs = dialogs;
            _localization = localization;
            _settings = settings;
            _launcher = launcher;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.BackRequested += OnAndroidBack;
            _view.PlayQuickClicked += OnPlayQuick;
            _view.StakeChosen += OnStakeChosen;
            _view.InfoClicked += OnInfo;
            _view.PlayStakeClicked += OnPlayStake;
            _view.PrizesCloseClicked += ClosePrizes;
            _wallet.Changed += RenderBalance;
            _localization.LanguageChanged += Render;
            _settings.Changed += RenderMotion;
            _router.Changed += OnScreenChanged;

            // The chips start on the last stake chosen, after a Quick Match and a restart too.
            _stake = StakeRules.IsStake(_choice.LastStake) ? _choice.LastStake : StakeRules.Stakes[0];
            RenderMotion();
            Render();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.BackRequested -= OnAndroidBack;
            _view.PlayQuickClicked -= OnPlayQuick;
            _view.StakeChosen -= OnStakeChosen;
            _view.InfoClicked -= OnInfo;
            _view.PlayStakeClicked -= OnPlayStake;
            _view.PrizesCloseClicked -= ClosePrizes;
            _wallet.Changed -= RenderBalance;
            _localization.LanguageChanged -= Render;
            _settings.Changed -= RenderMotion;
            _router.Changed -= OnScreenChanged;

            // A question still open is answered "no" and closes with the screen.
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        /// <summary>The screen opens with its prizes closed, showing the balance as it is now.</summary>
        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.PlayOnline)
            {
                _prizesOpen = false;
                Render();
            }
            else if (_prizesOpen)
            {
                _prizesOpen = false;
                _view.ShowPrizes(false, animate: false);
            }
        }

        private void Render()
        {
            if (_disposed)
            {
                return;
            }

            IReadOnlyList<int> stakes = StakeRules.Stakes;
            for (int i = 0; i < _view.ChipCount; i++)
            {
                _view.SetChip(i, i < stakes.Count ? CoinsFormat.Coins(stakes[i]) : string.Empty);
            }

            _view.SetSelectedChip(IndexOf(_stake));
            _view.SetPlayStake(_localization.Format("ui.play_online.stake_cta", CoinsFormat.Coins(_stake)));
            RenderPrizes();
            RenderBalance();
            _view.ShowPrizes(_prizesOpen, animate: false);
        }

        private void RenderBalance() => _view.SetBalance(CoinsFormat.Coins(_wallet.Coins));

        private void RenderMotion() => _view.SetReduceMotion(_settings.ReduceMotion);

        /// <summary>The chosen stake's prizes, 1st first (<see cref="StakeRules.PrizesFor"/>).</summary>
        private void RenderPrizes()
        {
            IReadOnlyList<int> prizes = StakeRules.PrizesFor(_stake);
            _prizeTexts.Clear();
            for (int i = 0; i < prizes.Count; i++)
            {
                _prizeTexts.Add(CoinsFormat.Coins(prizes[i]));
            }

            _view.SetPrizes(_localization.Format("ui.play_online.prizes_title", CoinsFormat.Coins(_stake)), _prizeTexts);
        }

        private void OnStakeChosen(int index)
        {
            IReadOnlyList<int> stakes = StakeRules.Stakes;
            if (index < 0 || index >= stakes.Count || stakes[index] == _stake)
            {
                return;
            }

            // Only shown: nothing is chosen for the search, and no coins move, until Play. The
            // chips remember it for the next time the screen opens.
            _stake = stakes[index];
            _choice.RememberStake(_stake);
            _view.SetPlayStake(_localization.Format("ui.play_online.stake_cta", CoinsFormat.Coins(_stake)));
            RenderPrizes();
        }

        private void OnInfo()
        {
            _prizesOpen = !_prizesOpen;
            _view.ShowPrizes(_prizesOpen, animate: true);
        }

        private void ClosePrizes()
        {
            if (!_prizesOpen)
            {
                return;
            }

            _prizesOpen = false;
            _view.ShowPrizes(false, animate: true);
        }

        private void OnBack()
        {
            if (_router.Current == ScreenId.PlayOnline)
            {
                _router.Pop();
            }
        }

        /// <summary>Android back closes the prizes first, as it would close a dialog.</summary>
        private void OnAndroidBack()
        {
            if (_prizesOpen)
            {
                ClosePrizes();
                return;
            }

            OnBack();
        }

        private void OnPlayQuick()
        {
            if (!CanLeave())
            {
                return;
            }

            _choice.Choose(StakeRules.None);
            _launcher.Enter(ScreenId.Matchmaking);
        }

        private void OnPlayStake()
        {
            if (!CanLeave())
            {
                return;
            }

            if (!_stakes.CanAfford(_stake))
            {
                _ = OfferEarnCoinsAsync(_stake);
                return;
            }

            _choice.Choose(_stake);
            _launcher.Enter(ScreenId.Matchmaking);
        }

        private bool CanLeave() => !_disposed && !_asking && _router.Current == ScreenId.PlayOnline;

        /// <summary>Not enough coins for the stake: the Earn Coins page, or Not now (the shop's question).</summary>
        private async Task OfferEarnCoinsAsync(int stake)
        {
            _asking = true;
            try
            {
                DialogChoice answer = await _dialogs.AskAsync(
                    new DialogRequest(
                        _localization.Get("ui.play_online.not_enough.title"),
                        _localization.Format("ui.play_online.not_enough.body", CoinsFormat.Coins(stake), CoinsFormat.Coins(_wallet.Coins)),
                        _localization.Get("ui.play_online.not_enough.earn"),
                        _localization.Get("ui.play_online.not_enough.later")),
                    _lifetime.Token);
                if (answer == DialogChoice.Confirm && !_disposed && _router.Current == ScreenId.PlayOnline && _router.Has(ScreenId.EarnCoins))
                {
                    _router.Push(ScreenId.EarnCoins);
                }
            }
            catch (OperationCanceledException)
            {
                // The menu closed under the question.
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _asking = false;
            }
        }

        private static int IndexOf(int stake)
        {
            IReadOnlyList<int> stakes = StakeRules.Stakes;
            for (int i = 0; i < stakes.Count; i++)
            {
                if (stakes[i] == stake)
                {
                    return i;
                }
            }

            return 0;
        }
    }
}
