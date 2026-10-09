using System;
using System.Collections.Generic;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// Screen 26, Daily Rewards (the owner, 8 Oct 2026; mockup 26; ADR 046), opened by the main menu's
    /// button below No Ads and, once a day while a reward is ready, by itself
    /// (<see cref="DailyRewardsMoment"/>). Everything it shows comes from <see cref="IDailyRewards"/>
    /// and <see cref="IWallet"/>: the seven tiles' amounts and looks (<see cref="DailyRewardsDisplay"/>),
    /// "Day n of 7", the button's "Claim n Coins" and the balance. Claim pays today's reward once; the
    /// button is then off ("Claimed") until the next day, which comes at local midnight without an
    /// event, so the page reads the rewards again while it is up. A claim's coins fly into the wallet,
    /// which counts up from the old balance as they land, with the coin sound at the first landing
    /// (the owner, 8 Oct 2026); with Reduce motion only the sound is left.
    /// </summary>
    public sealed class DailyRewardsPresenter : IStartable, ITickable, IDisposable
    {
        /// <summary>How often the open page looks whether a new day has come, in seconds.</summary>
        private const float DayCheckSeconds = 1f;

        private readonly DailyRewardsView _view;
        private readonly ScreenRouter _router;
        private readonly IWallet _wallet;
        private readonly IDailyRewards _rewards;
        private readonly ILocalization _localization;
        private readonly IAudioService _audio;
        private readonly ISettingsService _settings;

        // While a claim's coins fly, the wallet shows their count-up, not the balance.
        private bool _collecting;

        // What the page shows: today's day of the week and whether it was ready.
        private int _shownDay = -1;
        private bool _shownReady;
        private float _nextDayCheck;

        public DailyRewardsPresenter(
            DailyRewardsView view,
            ScreenRouter router,
            IWallet wallet,
            IDailyRewards rewards,
            ILocalization localization,
            IAudioService audio,
            ISettingsService settings)
        {
            _view = view;
            _router = router;
            _wallet = wallet;
            _rewards = rewards;
            _localization = localization;
            _audio = audio;
            _settings = settings;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.BackRequested += OnBack;
            _view.ClaimClicked += OnClaim;
            _view.CoinsLanding += OnCoinsLanding;
            _view.CoinsCollected += OnCoinsCollected;
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
            _view.ClaimClicked -= OnClaim;
            _view.CoinsLanding -= OnCoinsLanding;
            _view.CoinsCollected -= OnCoinsCollected;
            _wallet.Changed -= RenderBalance;
            _rewards.Changed -= Render;
            _localization.LanguageChanged -= Render;
            _router.Changed -= OnScreenChanged;
        }

        /// <summary>While the page is up: a new day (local midnight) shows the next reward.</summary>
        public void Tick()
        {
            if (_router.Current != ScreenId.DailyRewards || Time.unscaledTime < _nextDayCheck)
            {
                return;
            }

            _nextDayCheck = Time.unscaledTime + DayCheckSeconds;
            if (_rewards.Day != _shownDay || _rewards.CanClaim != _shownReady)
            {
                Render();
            }
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.DailyRewards)
            {
                Render();
            }
        }

        private void Render()
        {
            IReadOnlyList<int> rewards = _rewards.Rewards;
            int today = _rewards.Day;
            bool ready = _rewards.CanClaim;
            _shownDay = today;
            _shownReady = ready;
            for (int i = 0; i < _view.DayCount; i++)
            {
                bool shown = i < rewards.Count;
                _view.SetDay(
                    i,
                    shown ? _localization.Format(DailyRewardsDisplay.DayKey, i + 1) : string.Empty,
                    shown ? _localization.Format(DailyRewardsDisplay.AmountKey, rewards[i]) : string.Empty,
                    DailyRewardsDisplay.StateOf(i, today, ready));
            }

            _view.SetHeading(_localization.Format(DailyRewardsDisplay.ProgressKey, today + 1, rewards.Count));
            _view.SetClaim(
                ready ? _localization.Format(DailyRewardsDisplay.ClaimKey, rewards[today]) : _localization.Get(DailyRewardsDisplay.ClaimedKey),
                ready);
            _view.SetFooter(_localization.Format(DailyRewardsDisplay.NextKey, DailyRewardsDisplay.NextDayNumber(today, rewards.Count)));
            RenderBalance();
        }

        private void RenderBalance()
        {
            if (!_collecting)
            {
                _view.SetBalance(CoinsFormat.Coins(_wallet.Coins));
            }
        }

        private void OnBack()
        {
            if (_router.Current == ScreenId.DailyRewards)
            {
                _router.Pop();
            }
        }

        private void OnClaim()
        {
            if (!_rewards.CanClaim)
            {
                Render();
                return;
            }

            // The rewards say Changed when the coins are in, which renders the page; the wallet keeps
            // the old balance until the coins land.
            int before = _wallet.Coins;
            int day = _rewards.Day;
            _collecting = true;
            int paid = _rewards.Claim();
            if (paid <= 0)
            {
                _collecting = false;
                Render();
                return;
            }

            _view.CollectCoins(day, before, before + paid, _settings.ReduceMotion);
        }

        private void OnCoinsLanding() => _audio.Play(SoundCue.CoinCollect);

        private void OnCoinsCollected()
        {
            _collecting = false;
            RenderBalance();
        }
    }
}
