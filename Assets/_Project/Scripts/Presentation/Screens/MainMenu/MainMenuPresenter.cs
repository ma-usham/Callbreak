using System;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Presentation.Framework;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 00: one tap to every mode. Online entry points need a connection; without one
    /// a toast points to Play Offline. Android back asks before quitting (a second back within two
    /// seconds quits).
    /// </summary>
    public sealed class MainMenuPresenter : IStartable, IDisposable
    {
        private const double ExitWindowSeconds = 2.0;

        private readonly MainMenuView _view;
        private readonly ScreenRouter _router;
        private readonly IProfileService _profile;
        private readonly INetworkStatus _network;
        private readonly IToast _toast;
        private readonly ILocalization _localization;
        private readonly IClock _clock;
        private readonly IAppExit _exit;

        private double _lastBackAt = double.NegativeInfinity;

        public MainMenuPresenter(
            MainMenuView view,
            ScreenRouter router,
            IProfileService profile,
            INetworkStatus network,
            IToast toast,
            ILocalization localization,
            IClock clock,
            IAppExit exit)
        {
            _view = view;
            _router = router;
            _profile = profile;
            _network = network;
            _toast = toast;
            _localization = localization;
            _clock = clock;
            _exit = exit;
        }

        public void Start()
        {
            _view.ProfileClicked += OnProfile;
            _view.SettingsClicked += OnSettings;
            _view.ShopClicked += OnShop;
            _view.PlayOnlineClicked += OnPlayOnline;
            _view.PlayWithFriendsClicked += OnPlayWithFriends;
            _view.PlayOfflineClicked += OnPlayOffline;
            _view.BackRequested += OnBack;
            _profile.Changed += Render;
            Render();
        }

        public void Dispose()
        {
            _view.ProfileClicked -= OnProfile;
            _view.SettingsClicked -= OnSettings;
            _view.ShopClicked -= OnShop;
            _view.PlayOnlineClicked -= OnPlayOnline;
            _view.PlayWithFriendsClicked -= OnPlayWithFriends;
            _view.PlayOfflineClicked -= OnPlayOffline;
            _view.BackRequested -= OnBack;
            _profile.Changed -= Render;
        }

        private void Render() => _view.SetPlayerName(_profile.DisplayName);

        private void OnProfile() => _router.Push(ScreenId.Profile);

        private void OnSettings() => _router.Push(ScreenId.Settings);

        private void OnShop() => _router.Push(ScreenId.CardCollection);

        private void OnPlayOffline() => _router.Push(ScreenId.OfflineSetup);

        private void OnPlayOnline()
        {
            if (!RequireOnline())
            {
                return;
            }

            _router.Push(ScreenId.Matchmaking);
        }

        private void OnPlayWithFriends()
        {
            if (!RequireOnline())
            {
                return;
            }

            _router.Push(ScreenId.PlayWithFriends);
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

        private void OnBack()
        {
            double now = _clock.Now;
            if (now - _lastBackAt <= ExitWindowSeconds)
            {
                _exit.Quit();
                return;
            }

            _lastBackAt = now;
            _toast.Show(_localization.Get("ui.toast.back_to_exit"));
        }
    }
}
