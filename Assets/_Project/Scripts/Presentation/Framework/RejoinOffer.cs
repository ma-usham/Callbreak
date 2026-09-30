using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// GDD §27: after an app kill, the next launch offers "Rejoin match" while the match this
    /// device was playing as a guest may still be running. Rejoining takes the seat back through
    /// the lobby, which goes straight on to the table; the host refuses once the seat hold is over
    /// and a bot has the seat for good, and the match then counts as left (GDD §8). A room that is
    /// gone records nothing. "Not now", closing the offer, or a connection problem keeps the
    /// ticket: it is offered again at the next launch while it lasts, and the match counts as left
    /// once it runs out.
    /// </summary>
    public sealed class RejoinOffer : IStartable, IDisposable
    {
        private readonly ScreenRouter _router;
        private readonly IAppFlow _flow;
        private readonly IRejoinStore _tickets;
        private readonly IRoomService _rooms;
        private readonly INetworkStatus _network;
        private readonly IDialogs _dialogs;
        private readonly IToast _toast;
        private readonly ILocalization _localization;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private bool _disposed;

        public RejoinOffer(
            ScreenRouter router,
            IAppFlow flow,
            IRejoinStore tickets,
            IRoomService rooms,
            INetworkStatus network,
            IDialogs dialogs,
            IToast toast,
            ILocalization localization)
        {
            _router = router;
            _flow = flow;
            _tickets = tickets;
            _rooms = rooms;
            _network = network;
            _dialogs = dialogs;
            _toast = toast;
            _localization = localization;
        }

        public void Start()
        {
            // Only when the app opens; without a connection the ticket simply runs out.
            bool appOpened = _flow.PreviousState == AppState.Boot && _flow.PendingMenuEntry == MenuEntry.MainMenu;
            bool canRejoin = _router.Has(ScreenId.PlayWithFriends) && _router.Has(ScreenId.PrivateLobby);
            if (appOpened && canRejoin && _network.IsOnline && _tickets.TryGet(out string code))
            {
                _ = OfferAsync(code);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }

        private async Task OfferAsync(string code)
        {
            try
            {
                CancellationToken ct = _lifetime.Token;
                bool rejoin = await _dialogs.ConfirmAsync(
                    new DialogRequest(
                        _localization.Get("ui.dialog.rejoin_match.title"),
                        string.Empty,
                        _localization.Get("ui.dialog.rejoin_match.confirm"),
                        _localization.Get("ui.dialog.rejoin_match.cancel")),
                    ct);
                if (_disposed || !rejoin)
                {
                    return;
                }

                RoomResult result = await _rooms.JoinRoomAsync(code, ct);
                if (_disposed)
                {
                    if (result.Succeeded)
                    {
                        _ = result.Session.LeaveAsync(CancellationToken.None);
                    }

                    return;
                }

                if (!result.Succeeded)
                {
                    Settle(result.Error);
                    _toast.Show(_localization.Get("ui.error.join_table_failed"));
                    return;
                }

                // The lobby takes it from here: with the match running it opens the table.
                _dialogs.HandleBack();
                _router.ShowStack(new[] { ScreenId.MainMenu, ScreenId.PlayWithFriends, ScreenId.PrivateLobby });
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>What a failed rejoin does with the ticket.</summary>
        private void Settle(RoomError error)
        {
            switch (error)
            {
                case RoomError.AlreadyStarted:
                    // The seat hold is over and a bot has the seat for good.
                    _tickets.Abandon();
                    break;

                case RoomError.Offline:
                case RoomError.RateLimited:
                case RoomError.ConnectionLost:
                case RoomError.Unknown:
                    // The match may still be waiting: the next launch asks again.
                    break;

                default:
                    // The room is gone (the host left), or this player or build is turned away.
                    _tickets.Clear();
                    break;
            }
        }
    }
}
