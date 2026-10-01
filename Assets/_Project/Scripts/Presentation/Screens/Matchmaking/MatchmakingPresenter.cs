using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 05: find a Quick Match table. The search starts when the screen opens, the
    /// seats fill in as they are found, the elapsed time is the real one, Cancel withdraws at once,
    /// and a found table goes straight to the table after its "Table found" beat. A search that
    /// fails asks "Couldn't join the table · Retry / Play Offline".
    /// </summary>
    public sealed class MatchmakingPresenter : IStartable, IDisposable
    {
        private readonly MatchmakingView _view;
        private readonly ScreenRouter _router;
        private readonly IMatchmakingService _matchmaking;
        private readonly IProfileService _profile;
        private readonly ISettingsService _settings;
        private readonly ILocalization _localization;
        private readonly IDialogs _dialogs;
        private readonly MatchLauncher _launcher;
        private readonly AvatarLibrary _avatars;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private CancellationTokenSource _search;
        private bool _asking;
        private bool _disposed;

        public MatchmakingPresenter(
            MatchmakingView view,
            ScreenRouter router,
            IMatchmakingService matchmaking,
            IProfileService profile,
            ISettingsService settings,
            ILocalization localization,
            IDialogs dialogs,
            MatchLauncher launcher,
            AvatarLibrary avatars)
        {
            _view = view;
            _router = router;
            _matchmaking = matchmaking;
            _profile = profile;
            _settings = settings;
            _localization = localization;
            _dialogs = dialogs;
            _launcher = launcher;
            _avatars = avatars;
        }

        public void Start()
        {
            _view.BackClicked += OnCancel;
            _view.BackRequested += OnCancel;
            _view.CancelClicked += OnCancel;
            _view.ProfileClicked += OnProfile;
            _view.SettingsClicked += OnSettings;
            _view.ShopClicked += OnShop;
            _router.Changed += OnScreenChanged;
            _profile.Changed += RenderHeader;
            RenderHeader();
            if (_router.Current == ScreenId.Matchmaking)
            {
                OnScreenChanged(ScreenId.Matchmaking);
            }
        }

        public void Dispose()
        {
            _view.BackClicked -= OnCancel;
            _view.BackRequested -= OnCancel;
            _view.CancelClicked -= OnCancel;
            _view.ProfileClicked -= OnProfile;
            _view.SettingsClicked -= OnSettings;
            _view.ShopClicked -= OnShop;
            _router.Changed -= OnScreenChanged;
            _profile.Changed -= RenderHeader;
            StopSearch();
            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.Matchmaking && _search == null && !_asking)
            {
                _ = SearchAsync();
            }
        }

        private void RenderHeader()
        {
            _view.SetPlayerName(_profile.DisplayName);
            _view.SetAvatar(_avatars.Get(_profile.AvatarId));
        }

        private void OnProfile() => _router.Push(ScreenId.Profile);

        private void OnSettings() => _router.Push(ScreenId.Settings);

        private void OnShop() => _router.Push(ScreenId.CardCollection);

        private void OnCancel()
        {
            StopSearch();
            if (_router.Current == ScreenId.Matchmaking)
            {
                _router.Pop();
            }
        }

        private void StopSearch()
        {
            CancellationTokenSource search = _search;
            _search = null;
            if (search != null)
            {
                search.Cancel();
                search.Dispose();
            }
        }

        private async Task SearchAsync()
        {
            var search = new CancellationTokenSource();
            _search = search;
            _view.SetCancelInteractable(true);
            Render(null);
            try
            {
                MatchmakingResult result = await _matchmaking.FindMatchAsync(new Reporter(this, search), search.Token);
                if (_search != search)
                {
                    return;
                }

                _search = null;
                search.Dispose();
                if (result.Found)
                {
                    _view.SetCancelInteractable(false);
                    _launcher.Launch(new MatchLaunchRequest(MatchMode.QuickMatch, _settings.Difficulty));
                    return;
                }

                if (result.Error != MatchmakingError.Cancelled)
                {
                    await OfferRetryAsync(result.Error);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (_search == search)
                {
                    _search = null;
                    search.Dispose();
                    await OfferRetryAsync(MatchmakingError.Unavailable);
                }
            }
        }

        /// <summary>
        /// GDD screen 05: "Couldn't join the table · Retry / Play Offline". Retry searches again,
        /// Play Offline opens the offline setup in this screen's place, and a dialog that is simply
        /// closed goes back to where the player came from.
        /// </summary>
        private async Task OfferRetryAsync(MatchmakingError error)
        {
            if (_disposed || _asking)
            {
                return;
            }

            _asking = true;
            _view.SetCancelInteractable(false);
            DialogChoice choice = DialogChoice.Dismissed;
            try
            {
                string reason = error == MatchmakingError.Offline ? _localization.Get("ui.toast.no_internet")
                    : error == MatchmakingError.UpdateRequired ? _localization.Get("ui.error.update_required")
                    : string.Empty;
                choice = await _dialogs.AskAsync(
                    new DialogRequest(
                        _localization.Get("ui.error.join_table_failed"),
                        reason,
                        _localization.Get("ui.common.retry"),
                        _localization.Get("ui.main_menu.play_offline")),
                    _lifetime.Token);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                _asking = false;
            }

            if (_disposed || _search != null || _router.Current != ScreenId.Matchmaking)
            {
                return;
            }

            switch (choice)
            {
                case DialogChoice.Confirm:
                    _ = SearchAsync();
                    break;
                case DialogChoice.Cancel when _router.Has(ScreenId.OfflineSetup):
                    _router.Replace(ScreenId.OfflineSetup);
                    break;
                default:
                    _router.Pop();
                    break;
            }
        }

        /// <summary>Four seat circles, the status lines and the honest elapsed time.</summary>
        private void Render(MatchmakingProgress progress)
        {
            _view.SetReduceMotion(_settings.ReduceMotion);
            int connected = 0;
            for (int i = 0; i < _view.SeatCount; i++)
            {
                RoomSeat seat = progress != null && i < progress.Seats.Count ? progress.Seats[i] : null;
                bool local = i == 0;
                if (local || (seat != null && seat.Occupied))
                {
                    connected++;
                    string name = local ? _localization.Get("ui.common.you") : seat.Name;
                    string status = !local && seat.IsBot ? _localization.Get("ui.matchmaking.seat_bot") : _localization.Get("ui.matchmaking.seat_ready");
                    _view.SetSeat(i, true, name, status);
                }
                else
                {
                    _view.SetSeat(i, false, string.Empty, _localization.Get("ui.matchmaking.seat_searching"));
                }
            }

            bool found = progress != null && progress.TableFound;
            _view.SetStatus(
                _localization.Get(found ? "ui.matchmaking.table_found" : "ui.matchmaking.finding"),
                _localization.Format("ui.matchmaking.connected", connected));
            int seconds = progress != null ? (int)progress.ElapsedSeconds : 0;
            _view.SetElapsed((seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture));
        }

        /// <summary>Progress arrives on the main thread, straight from the service's tick.</summary>
        private sealed class Reporter : IProgress<MatchmakingProgress>
        {
            private readonly MatchmakingPresenter _owner;
            private readonly CancellationTokenSource _search;

            public Reporter(MatchmakingPresenter owner, CancellationTokenSource search)
            {
                _owner = owner;
                _search = search;
            }

            public void Report(MatchmakingProgress value)
            {
                if (_owner._search == _search)
                {
                    _owner.Render(value);
                }
            }
        }
    }
}
