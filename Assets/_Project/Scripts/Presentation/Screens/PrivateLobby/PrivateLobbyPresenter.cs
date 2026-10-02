using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
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
    /// GDD §11 screen 04: gather up to four players and start. It mirrors the room session (seats,
    /// ready flags, code) and turns the taps into intents: copy or share the code (the device's
    /// share sheet, or where there is none a panel showing the invite with Copy), the guest's
    /// ready toggle, the host's Start (asking first when seats would be filled with bots) and the
    /// host's long-press to remove a player. Leaving the screen leaves the room.
    /// </summary>
    public sealed class PrivateLobbyPresenter : IStartable, IDisposable
    {
        private readonly PrivateLobbyView _view;
        private readonly ScreenRouter _router;
        private readonly IRoomService _rooms;
        private readonly ISettingsService _settings;
        private readonly IClipboard _clipboard;
        private readonly IShareService _share;
        private readonly IDialogs _dialogs;
        private readonly IToast _toast;
        private readonly ILocalization _localization;
        private readonly AvatarLibrary _avatars;
        private readonly MatchLauncher _launcher;

        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private IRoomSession _room;
        private bool _asking;

        public PrivateLobbyPresenter(
            PrivateLobbyView view,
            ScreenRouter router,
            IRoomService rooms,
            ISettingsService settings,
            IClipboard clipboard,
            IShareService share,
            IDialogs dialogs,
            IToast toast,
            ILocalization localization,
            AvatarLibrary avatars,
            MatchLauncher launcher)
        {
            _view = view;
            _router = router;
            _rooms = rooms;
            _settings = settings;
            _clipboard = clipboard;
            _share = share;
            _dialogs = dialogs;
            _toast = toast;
            _localization = localization;
            _avatars = avatars;
            _launcher = launcher;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.BackRequested += OnBack;
            _view.SettingsClicked += OnSettings;
            _view.CopyCodeClicked += OnCopyCode;
            _view.InviteClicked += OnInvite;
            _view.StartClicked += OnStart;
            _view.ReadyClicked += OnReady;
            _view.SeatLongPressed += OnSeatLongPressed;
            _router.Changed += OnScreenChanged;
            _localization.LanguageChanged += Render;
            if (_router.Current == ScreenId.PrivateLobby)
            {
                OnScreenChanged(ScreenId.PrivateLobby);
            }
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.BackRequested -= OnBack;
            _view.SettingsClicked -= OnSettings;
            _view.CopyCodeClicked -= OnCopyCode;
            _view.InviteClicked -= OnInvite;
            _view.StartClicked -= OnStart;
            _view.ReadyClicked -= OnReady;
            _view.SeatLongPressed -= OnSeatLongPressed;
            _router.Changed -= OnScreenChanged;
            _localization.LanguageChanged -= Render;
            Bind(null);
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id != ScreenId.PrivateLobby)
            {
                return;
            }

            IRoomSession current = _rooms.Current;
            if (current == null)
            {
                // The room ended while another screen (or the table) was on top.
                Bind(null);
                _router.Pop();
                return;
            }

            Bind(current);
            Render();

            // The match is already on (a seat taken back after an app kill, or a host who started
            // the next match first): its start was announced before this screen could listen.
            IMatchSession match = current.Match;
            if (match != null && match.Status != SessionStatus.Ended && match.Status != SessionStatus.Failed)
            {
                OnMatchStarting();
            }
        }

        private void Bind(IRoomSession room)
        {
            if (_room == room)
            {
                return;
            }

            if (_room != null)
            {
                _room.Changed -= Render;
                _room.MatchStarting -= OnMatchStarting;
                _room.Closed -= OnClosed;
            }

            _room = room;
            if (_room != null)
            {
                _room.Changed += Render;
                _room.MatchStarting += OnMatchStarting;
                _room.Closed += OnClosed;
            }
        }

        private void Render()
        {
            IRoomSession room = _room;
            if (room == null)
            {
                return;
            }

            _view.SetReduceMotion(_settings.ReduceMotion);
            IReadOnlyList<RoomSeat> seats = room.Seats;
            int occupied = 0;
            bool guestsReady = true;
            bool localReady = false;
            for (int i = 0; i < PrivateLobbyView.SeatCount; i++)
            {
                RoomSeat seat = seats[i];
                if (!seat.Occupied)
                {
                    _view.SetSeat(i, _localization.Get("ui.private_lobby.empty_seat"), string.Empty, SeatStatusStyle.Waiting, false, false);
                    _view.SetSeatAvatar(i, null);
                    continue;
                }

                occupied++;
                if (!seat.IsHost && !seat.IsReady)
                {
                    guestsReady = false;
                }

                if (seat.IsLocal)
                {
                    localReady = seat.IsReady;
                }

                string name = seat.IsLocal ? _localization.Get("ui.common.you") : seat.Name;
                if (seat.IsHost)
                {
                    _view.SetSeat(i, name, _localization.Get("ui.private_lobby.host"), SeatStatusStyle.Host, false, true);
                }
                else if (seat.IsReady)
                {
                    _view.SetSeat(i, name, _localization.Get("ui.private_lobby.ready"), SeatStatusStyle.Ready, true, true);
                }
                else
                {
                    _view.SetSeat(i, name, _localization.Get("ui.private_lobby.not_ready"), SeatStatusStyle.NotReady, false, true);
                }

                _view.SetSeatAvatar(i, _avatars.Get(seat.AvatarId));
            }

            _view.SetTitle(room.Settings.Name);
            _view.SetSubtitle(occupied == PrivateLobbyView.SeatCount
                ? _localization.Get("ui.private_lobby.everyone")
                : _localization.Format("ui.private_lobby.waiting", occupied));
            _view.SetCode(room.Code);
            _view.SetFooter(_localization.Format("ui.private_lobby.footer", occupied, room.Settings.Rounds, room.Settings.TurnSeconds));
            _view.SetHostMode(room.IsHost);
            _view.SetStartInteractable(!room.IsHost || guestsReady);
            _view.SetReadyLabel(_localization.Get(localReady ? "ui.private_lobby.not_ready" : "ui.private_lobby.ready"));
        }

        private void OnBack()
        {
            IRoomSession room = _room;
            Bind(null);
            if (room != null)
            {
                _ = room.LeaveAsync(CancellationToken.None);
            }

            _router.Pop();
        }

        private void OnSettings() => _router.Push(ScreenId.Settings);

        private void OnCopyCode()
        {
            if (_room == null)
            {
                return;
            }

            _clipboard.SetText(_room.Code);
            _toast.Show(_localization.Get("ui.toast.code_copied"));
        }

        private void OnInvite()
        {
            if (_room == null || _asking)
            {
                return;
            }

            string text = _localization.Format("ui.private_lobby.invite_text", _room.Code);
            if (!_share.ShareText(_localization.Get("ui.private_lobby.invite_title"), text))
            {
                // No share sheet here (the Editor, a desktop, iOS for now): the invite opens in a
                // panel of its own, ready to copy.
                _ = OfferInviteAsync(text);
            }
        }

        /// <summary>The invite as it would be sent, with Copy Invite and Close.</summary>
        private async Task OfferInviteAsync(string text)
        {
            _asking = true;
            try
            {
                bool copy = await _dialogs.ConfirmAsync(
                    new DialogRequest(
                        _localization.Get("ui.dialog.invite.title"),
                        text,
                        _localization.Get("ui.dialog.invite.confirm"),
                        _localization.Get("ui.dialog.invite.cancel")),
                    _lifetime.Token);
                if (copy)
                {
                    _clipboard.SetText(text);
                    _toast.Show(_localization.Get("ui.toast.invite_copied"));
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

        private void OnReady()
        {
            IRoomSession room = _room;
            if (room == null || room.IsHost || room.LocalSeatIndex < 0)
            {
                return;
            }

            bool ready = room.Seats[room.LocalSeatIndex].IsReady;
            _ = room.SetReadyAsync(!ready, CancellationToken.None);
        }

        private void OnStart()
        {
            IRoomSession room = _room;
            if (room == null || !room.IsHost || _asking)
            {
                return;
            }

            int empty = 0;
            foreach (RoomSeat seat in room.Seats)
            {
                if (!seat.Occupied)
                {
                    empty++;
                }
            }

            if (empty == 0)
            {
                _ = room.StartAsync(false, CancellationToken.None);
                return;
            }

            _ = ConfirmFillAsync(room, empty);
        }

        /// <summary>GDD: "Fill 2 seats with bots?" before a table with empty seats starts.</summary>
        private async Task ConfirmFillAsync(IRoomSession room, int empty)
        {
            _asking = true;
            try
            {
                string title = empty == 1
                    ? _localization.Get("ui.dialog.fill_bots.title_one")
                    : _localization.Format("ui.dialog.fill_bots.title", empty);
                bool fill = await _dialogs.ConfirmAsync(
                    new DialogRequest(title, string.Empty, _localization.Get("ui.dialog.fill_bots.confirm"), _localization.Get("ui.dialog.fill_bots.cancel")),
                    _lifetime.Token);
                if (fill && _room == room)
                {
                    await room.StartAsync(true, CancellationToken.None);
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

        private void OnSeatLongPressed(int index)
        {
            IRoomSession room = _room;
            // The host's own seat is not always the first: a room taken over is hosted from the seat of
            // the player who took it over (ADR 030).
            if (room == null || !room.IsHost || _asking || index < 0 || index >= room.Seats.Count || !room.Seats[index].Occupied || room.Seats[index].IsHost)
            {
                return;
            }

            _ = ConfirmRemoveAsync(room, index, room.Seats[index].Name);
        }

        private async Task ConfirmRemoveAsync(IRoomSession room, int index, string name)
        {
            _asking = true;
            try
            {
                bool remove = await _dialogs.ConfirmAsync(
                    new DialogRequest(
                        _localization.Format("ui.dialog.remove_player.title", name),
                        string.Empty,
                        _localization.Get("ui.dialog.remove_player.confirm"),
                        _localization.Get("ui.dialog.remove_player.cancel")),
                    _lifetime.Token);
                if (remove && _room == room && index < room.Seats.Count && room.Seats[index].Occupied && room.Seats[index].Name == name)
                {
                    await room.KickAsync(index, CancellationToken.None);
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

        private void OnMatchStarting()
        {
            _launcher.Launch(new MatchLaunchRequest(MatchMode.PrivateRoom, _settings.Difficulty));
        }

        private void OnClosed(RoomError reason)
        {
            Bind(null);
            if (reason != RoomError.None)
            {
                _toast.Show(_localization.Get(RoomMessages.KeyFor(reason)));
            }

            if (_router.Current == ScreenId.PrivateLobby)
            {
                _router.Pop();
            }
        }
    }
}
