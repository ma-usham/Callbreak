using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// Screen 22, Earn Coins, opened by the main menu wallet's "+" (the owner, 4 Oct 2026; mockup 22
    /// and the designer's rewarded-coins-prompts.md). Everything it shows comes from
    /// <see cref="IAdRewards"/> and <see cref="IWallet"/>: the blocks' payouts, the progress, the
    /// block the next ad pays (crimson), the watched ones (a check), the button's "+N coins" and the
    /// balance, all following the services as coins are credited. Watch ad shows the next ad; the
    /// button is busy until it is over, so a second tap does nothing. An ad that pays nothing (none
    /// available, or not finished) says so in a toast. Once every ad of the set is watched the
    /// button is off until the set resets, and the line under it counts down to the reset (from the
    /// set's first ad on, as in the designer's prototype); the set resets on the wall clock, without
    /// an event, so the countdown reads the rewards again when it runs out.
    /// </summary>
    public sealed class EarnCoinsPresenter : IStartable, ITickable, IDisposable
    {
        private readonly EarnCoinsView _view;
        private readonly ScreenRouter _router;
        private readonly IWallet _wallet;
        private readonly IAdRewards _rewards;
        private readonly ILocalization _localization;
        private readonly IToast _toast;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _watching;
        private bool _disposed;

        // What the page shows: the deadline it counts down to (null: none) and the second shown.
        private DateTime? _shownDeadline;
        private long _shownSeconds = -1;

        public EarnCoinsPresenter(
            EarnCoinsView view,
            ScreenRouter router,
            IWallet wallet,
            IAdRewards rewards,
            ILocalization localization,
            IToast toast)
        {
            _view = view;
            _router = router;
            _wallet = wallet;
            _rewards = rewards;
            _localization = localization;
            _toast = toast;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.BackRequested += OnBack;
            _view.WatchClicked += OnWatch;
            _wallet.Changed += RenderBalance;
            _rewards.Changed += Render;
            _localization.LanguageChanged += Render;
            _router.Changed += OnScreenChanged;
            Render();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.BackRequested -= OnBack;
            _view.WatchClicked -= OnWatch;
            _wallet.Changed -= RenderBalance;
            _rewards.Changed -= Render;
            _localization.LanguageChanged -= Render;
            _router.Changed -= OnScreenChanged;

            // An ad still playing is given up with the page: it credits nothing.
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        /// <summary>While the page shows a deadline: the countdown, and the set's reset when it comes.</summary>
        public void Tick()
        {
            if (_shownDeadline == null || _router.Current != ScreenId.EarnCoins)
            {
                return;
            }

            long seconds = CoinsFormat.SecondsLeft(_shownDeadline.Value - DateTime.UtcNow);
            if (seconds == 0L || _rewards.ResetsAtUtc != _shownDeadline)
            {
                Render();
            }
            else if (seconds != _shownSeconds)
            {
                RenderReset();
            }
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.EarnCoins)
            {
                Render();
            }
        }

        private void Render()
        {
            IReadOnlyList<int> rewards = _rewards.Rewards;
            int watched = _rewards.Watched;
            int next = _rewards.NextReward;
            bool allWatched = next <= 0;
            for (int i = 0; i < _view.BlockCount; i++)
            {
                bool shown = i < rewards.Count;
                _view.SetBlock(
                    i,
                    shown ? _localization.Format("ui.earn_coins.ad", i + 1) : string.Empty,
                    shown ? _localization.Format("ui.earn_coins.reward", rewards[i]) : string.Empty,
                    next: shown && !allWatched && i == watched,
                    watched: shown && i < watched);
            }

            _view.SetProgress(_localization.Format("ui.earn_coins.progress", watched, rewards.Count));
            _view.SetWatch(
                allWatched ? _localization.Get("ui.earn_coins.all_claimed") : _localization.Format("ui.earn_coins.watch", next),
                showPlay: !allWatched,
                interactable: !allWatched && !_watching);
            RenderBalance();
            RenderReset();
        }

        private void RenderBalance() => _view.SetBalance(CoinsFormat.Coins(_wallet.Coins));

        /// <summary>"Rewards reset every 5 hours" until the set's first ad; then the time left.</summary>
        private void RenderReset()
        {
            _shownDeadline = _rewards.ResetsAtUtc;
            if (_shownDeadline == null)
            {
                _shownSeconds = -1;
                _view.SetReset(_localization.Get("ui.earn_coins.reset_every"));
                return;
            }

            _shownSeconds = CoinsFormat.SecondsLeft(_shownDeadline.Value - DateTime.UtcNow);
            _view.SetReset(_localization.Format("ui.earn_coins.resets_in", CoinsFormat.TimeLeft(_shownSeconds)));
        }

        private void OnBack()
        {
            if (_router.Current == ScreenId.EarnCoins)
            {
                _router.Pop();
            }
        }

        private void OnWatch()
        {
            if (_watching || _rewards.NextReward <= 0)
            {
                return;
            }

            _ = WatchAsync();
        }

        private async Task WatchAsync()
        {
            _watching = true;
            Render();
            int credited = 0;
            bool cancelled = false;
            try
            {
                credited = await _rewards.WatchNextAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _watching = false;
            }

            if (_disposed)
            {
                return;
            }

            if (credited <= 0 && !cancelled)
            {
                _toast.Show(_localization.Get("ui.earn_coins.unavailable"));
            }

            Render();
        }
    }
}
