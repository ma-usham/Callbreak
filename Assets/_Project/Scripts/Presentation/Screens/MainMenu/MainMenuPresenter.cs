using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 00: one tap to every mode. The card fan shows the four aces of the player's
    /// deck. Online entry points need a connection; without one a toast points to Play Offline. While an unfinished offline match exists a banner offers
    /// "Resume match"; the match is also offered back ("Resume your offline match?") once when the app
    /// opens and whenever Play Offline is tapped. Android back asks "Exit Callbreak?". The coin
    /// wallet (mockup 23) shows the balance as it changes; its "+" opens Earn Coins. The No Ads
    /// button left of it sells the one-time purchase that removes the ads between matches
    /// (<see cref="NoAdsOffer"/>), with the game under the curtain's black cover while the store's
    /// sheet is up (ADR 042). The small button below No Ads opens Daily Rewards (the owner, 8 Oct
    /// 2026; ADR 046), with a red dot while today's reward is ready. Prize coins won at the table (a
    /// Stake Match, <see cref="IUnseenCoins"/>) arrive here (the owner, 9 Oct 2026): the wallet first
    /// shows the balance without them, and once the menu is on screen with no dialog over it they fly
    /// from its middle into the wallet, which counts up, with the coin sound at the first landing; with
    /// Reduce motion only the sound is left.
    /// </summary>
    public sealed class MainMenuPresenter : IStartable, ITickable, IDisposable
    {
        private readonly MainMenuView _view;
        private readonly ScreenRouter _router;
        private readonly IProfileService _profile;
        private readonly ISettingsService _settings;
        private readonly INetworkStatus _network;
        private readonly IToast _toast;
        private readonly ILocalization _localization;
        private readonly IDialogs _dialogs;
        private readonly IOfflineMatchStore _offlineStore;
        private readonly IAppFlow _flow;
        private readonly MatchLauncher _launcher;
        private readonly IAppExit _exit;
        private readonly AvatarLibrary _avatars;
        private readonly ICosmeticsService _cosmetics;
        private readonly CardSpriteProvider _cards;
        private readonly IWallet _wallet;
        private readonly NoAdsOffer _noAds;
        private readonly IDailyRewards _dailyRewards;
        private readonly IUnseenCoins _unseen;
        private readonly IAudioService _audio;
        private readonly ILoadingCurtain _curtain;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _asking;
        private bool _disposed;

        // Prize coins still to be shown arriving, held out of the balance until they have flown in.
        private int _heldCoins;
        private bool _collecting;

        public MainMenuPresenter(
            MainMenuView view,
            ScreenRouter router,
            IProfileService profile,
            ISettingsService settings,
            INetworkStatus network,
            IToast toast,
            ILocalization localization,
            IDialogs dialogs,
            IOfflineMatchStore offlineStore,
            IAppFlow flow,
            MatchLauncher launcher,
            IAppExit exit,
            AvatarLibrary avatars,
            ICosmeticsService cosmetics,
            CardSpriteProvider cards,
            IWallet wallet,
            INoAds noAds,
            ILoadingCurtain curtain,
            IDailyRewards dailyRewards,
            IUnseenCoins unseen,
            IAudioService audio)
        {
            _view = view;
            _router = router;
            _profile = profile;
            _settings = settings;
            _network = network;
            _toast = toast;
            _localization = localization;
            _dialogs = dialogs;
            _offlineStore = offlineStore;
            _flow = flow;
            _launcher = launcher;
            _exit = exit;
            _avatars = avatars;
            _cosmetics = cosmetics;
            _cards = cards;
            _wallet = wallet;
            _noAds = new NoAdsOffer(noAds, toast, localization, curtain);
            _dailyRewards = dailyRewards;
            _unseen = unseen;
            _audio = audio;
            _curtain = curtain;
        }

        public void Start()
        {
            _view.ProfileClicked += OnProfile;
            _view.SettingsClicked += OnSettings;
            _view.ShopClicked += OnShop;
            _view.EarnCoinsClicked += OnEarnCoins;
            _view.NoAdsClicked += OnNoAds;
            _view.DailyRewardsClicked += OnDailyRewards;
            _view.CoinsLanding += OnCoinsLanding;
            _view.CoinsCollected += OnCoinsCollected;
            _view.PlayOnlineClicked += OnPlayOnline;
            _view.PlayWithFriendsClicked += OnPlayWithFriends;
            _view.PlayOfflineClicked += OnPlayOffline;
            _view.ResumeClicked += OnResume;
            _view.BackRequested += OnBack;
            _profile.Changed += Render;
            _settings.Changed += RenderMotion;
            _cosmetics.Changed += RenderFan;
            _wallet.Changed += RenderCoins;
            _noAds.Changed += RenderNoAds;
            _noAds.Start();
            _dailyRewards.Changed += RenderDailyRewards;
            _network.Changed += OnNetworkChanged;
            _router.Changed += OnScreenChanged;
            _heldCoins = _unseen.Amount;
            Render();
            RenderFan();
            RenderCoins();
            RenderNoAds();
            RenderDailyRewards();
            RenderMotion();
            RenderResume();
            OnNetworkChanged(_network.IsOnline);

            // GDD §8: the next launch offers the unfinished offline match.
            if (_flow.PreviousState == AppState.Boot && _flow.PendingMenuEntry == MenuEntry.MainMenu && _offlineStore.HasSave)
            {
                _ = OfferResumeAsync("ui.dialog.resume_match.cancel", openSetupOnDecline: false);
            }
        }

        public void Dispose()
        {
            _view.ProfileClicked -= OnProfile;
            _view.SettingsClicked -= OnSettings;
            _view.ShopClicked -= OnShop;
            _view.EarnCoinsClicked -= OnEarnCoins;
            _view.NoAdsClicked -= OnNoAds;
            _view.DailyRewardsClicked -= OnDailyRewards;
            _view.CoinsLanding -= OnCoinsLanding;
            _view.CoinsCollected -= OnCoinsCollected;
            _view.PlayOnlineClicked -= OnPlayOnline;
            _view.PlayWithFriendsClicked -= OnPlayWithFriends;
            _view.PlayOfflineClicked -= OnPlayOffline;
            _view.ResumeClicked -= OnResume;
            _view.BackRequested -= OnBack;
            _profile.Changed -= Render;
            _settings.Changed -= RenderMotion;
            _cosmetics.Changed -= RenderFan;
            _wallet.Changed -= RenderCoins;
            _noAds.Changed -= RenderNoAds;
            _noAds.Dispose();
            _dailyRewards.Changed -= RenderDailyRewards;
            _network.Changed -= OnNetworkChanged;
            _router.Changed -= OnScreenChanged;

            // A question still open is answered "no" and closes with the screen; a No Ads purchase
            // still open is no longer waited for (the store finishes it on its own).
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private void Render()
        {
            _view.SetPlayerName(_profile.DisplayName);
            _view.SetAvatar(_avatars.Get(_profile.AvatarId));
        }

        private void RenderMotion() => _view.SetReduceMotion(_settings.ReduceMotion);

        /// <summary>GDD §13: "the main-menu fan shows that deck's four aces".</summary>
        private void RenderFan()
        {
            string deck = _cosmetics.SelectedDeckId;
            for (int suit = 0; suit < 4; suit++)
            {
                _view.SetFanFace((Suit)suit, _cards.Face(deck, new Card((Suit)suit, Rank.Ace)));
            }
        }

        /// <summary>Once the menu is on screen with nothing over it, the prize coins held out of the balance fly into the wallet.</summary>
        public void Tick()
        {
            if (_heldCoins <= 0 || _collecting || _disposed
                || _router.Current != ScreenId.MainMenu || _curtain.IsCovering || _dialogs.IsOpen || _flow.State != AppState.Menu)
            {
                return;
            }

            _collecting = true;
            int to = _wallet.Coins;
            _view.CollectCoins(Math.Max(0, to - _heldCoins), to, _settings.ReduceMotion);
        }

        /// <summary>The balance; while prize coins are still to arrive, without them, and while they fly, their count-up.</summary>
        private void RenderCoins()
        {
            if (!_collecting)
            {
                _view.SetCoins(CoinsFormat.Coins(Math.Max(0, _wallet.Coins - _heldCoins)));
            }
        }

        private void OnCoinsLanding() => _audio.Play(SoundCue.CoinCollect);

        private void OnCoinsCollected()
        {
            _collecting = false;
            _heldCoins = 0;
            _unseen.Clear();
            RenderCoins();
        }

        private void RenderNoAds() => _view.SetNoAdsVisible(_noAds.Visible);

        private void RenderResume() => _view.SetResumeAvailable(_offlineStore.HasSave);

        /// <summary>The red dot on the Daily Rewards button. A new day comes without an event: the menu reads it again whenever it is on top.</summary>
        private void RenderDailyRewards() => _view.SetDailyRewardsReady(_dailyRewards.CanClaim);

        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.MainMenu)
            {
                RenderResume();
                RenderDailyRewards();
            }
        }

        private void OnResume()
        {
            if (_asking)
            {
                return;
            }

            if (!_offlineStore.HasSave)
            {
                RenderResume();
                return;
            }

            ResumeSaved();
        }

        private void ResumeSaved()
        {
            _launcher.Launch(new MatchLaunchRequest(MatchMode.Offline, _settings.Difficulty, resumeSaved: true));
        }

        private void OnNetworkChanged(bool online) => _view.SetOnlineAvailable(online);

        private void OnProfile() => _router.Push(ScreenId.Profile);

        private void OnSettings() => _router.Push(ScreenId.Settings);

        private void OnShop() => _router.Push(ScreenId.Shop);

        private void OnEarnCoins() => _router.Push(ScreenId.EarnCoins);

        private void OnDailyRewards()
        {
            if (_router.Has(ScreenId.DailyRewards))
            {
                _router.Push(ScreenId.DailyRewards);
            }
        }

        private void OnNoAds() => _ = BuyNoAdsAsync();

        /// <summary>The store's sheet is the confirmation; the offer says in a toast what became of it.</summary>
        private async Task BuyNoAdsAsync()
        {
            try
            {
                await _noAds.TapAsync(_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnPlayOffline()
        {
            if (_offlineStore.HasSave)
            {
                _ = OfferResumeAsync("ui.dialog.resume_match.new_game", openSetupOnDecline: true);
                return;
            }

            _launcher.Enter(ScreenId.OfflineSetup);
        }

        /// <summary>The Play Online screen first: Quick Match or Stake Match (mockups 23 and 24).</summary>
        private void OnPlayOnline()
        {
            if (!RequireOnline())
            {
                return;
            }

            _launcher.Enter(_router.Has(ScreenId.PlayOnline) ? ScreenId.PlayOnline : ScreenId.Matchmaking);
        }

        private void OnPlayWithFriends()
        {
            if (!RequireOnline())
            {
                return;
            }

            _launcher.Enter(ScreenId.PlayWithFriends);
        }

        private bool RequireOnline()
        {
            if (_network.IsOnline)
            {
                return true;
            }

            _toast.Show(_localization.Get("ui.toast.no_internet"));
            return false;
        }

        private void OnBack() => _ = ConfirmExitAsync();

        private async Task ConfirmExitAsync()
        {
            if (_asking)
            {
                return;
            }

            _asking = true;
            try
            {
                bool exit = await _dialogs.ConfirmAsync(
                    new DialogRequest(
                        _localization.Get("ui.dialog.exit.title"),
                        string.Empty,
                        _localization.Get("ui.dialog.exit.confirm"),
                        _localization.Get("ui.dialog.exit.cancel")),
                    _lifetime.Token);
                if (exit && !_disposed)
                {
                    _exit.Quit();
                }
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

        private async Task OfferResumeAsync(string declineKey, bool openSetupOnDecline)
        {
            if (_asking)
            {
                return;
            }

            _asking = true;
            try
            {
                DialogChoice choice = await _dialogs.AskAsync(
                    new DialogRequest(
                        _localization.Get("ui.dialog.resume_match.title"),
                        string.Empty,
                        _localization.Get("ui.dialog.resume_match.confirm"),
                        _localization.Get(declineKey)),
                    _lifetime.Token);
                if (_disposed)
                {
                    return;
                }

                // A dialog that was only closed (a tap outside, back) is neither answer.
                if (choice == DialogChoice.Confirm)
                {
                    ResumeSaved();
                }
                else if (choice == DialogChoice.Cancel && openSetupOnDecline && _router.Current == ScreenId.MainMenu)
                {
                    _launcher.Enter(ScreenId.OfflineSetup);
                }
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
    }
}
