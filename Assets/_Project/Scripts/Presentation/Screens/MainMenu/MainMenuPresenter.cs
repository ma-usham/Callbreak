using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 00: one tap to every mode. Online entry points need a connection; without one
    /// a toast points to Play Offline. An unfinished offline match is offered back ("Resume your
    /// match?") once when the app opens and whenever Play Offline is tapped. Android back asks
    /// "Exit Callbreak?".
    /// </summary>
    public sealed class MainMenuPresenter : IStartable, IDisposable
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

        private bool _asking;

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
            IAppExit exit)
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
            _network.Changed += OnNetworkChanged;
            Render();
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
            _view.PlayOnlineClicked -= OnPlayOnline;
            _view.PlayWithFriendsClicked -= OnPlayWithFriends;
            _view.PlayOfflineClicked -= OnPlayOffline;
            _view.BackRequested -= OnBack;
            _profile.Changed -= Render;
            _network.Changed -= OnNetworkChanged;
        }

        private void Render() => _view.SetPlayerName(_profile.DisplayName);

        private void OnNetworkChanged(bool online) => _view.SetOnlineAvailable(online);

        private void OnProfile() => _router.Push(ScreenId.Profile);

        private void OnSettings() => _router.Push(ScreenId.Settings);

        private void OnShop() => _router.Push(ScreenId.CardCollection);

        private void OnPlayOffline()
        {
            if (_offlineStore.HasSave)
            {
                _ = OfferResumeAsync("ui.dialog.resume_match.new_game", openSetupOnDecline: true);
                return;
            }

            _launcher.Enter(ScreenId.OfflineSetup);
        }

        private void OnPlayOnline()
        {
            if (!RequireOnline())
            {
                return;
            }

            _launcher.Enter(ScreenId.Matchmaking);
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
                    CancellationToken.None);
                if (exit)
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
                bool resume = await _dialogs.ConfirmAsync(
                    new DialogRequest(
                        _localization.Get("ui.dialog.resume_match.title"),
                        string.Empty,
                        _localization.Get("ui.dialog.resume_match.confirm"),
                        _localization.Get(declineKey)),
                    CancellationToken.None);
                if (resume)
                {
                    _launcher.Launch(new MatchLaunchRequest(MatchMode.Offline, _settings.Difficulty, resumeSaved: true));
                }
                else if (openSetupOnDecline && _router.Current == ScreenId.MainMenu)
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
