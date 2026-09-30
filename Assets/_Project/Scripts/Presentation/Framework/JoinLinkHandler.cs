using System;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Invite links in the menu (GDD §11 screens 00 and 03): a waiting <c>callbreak://join/CODE</c>
    /// opens Join Room, which fills its boxes with the code. A link that arrives during a match
    /// waits until the player is back in the menu; one that arrives while the player already sits
    /// in a lobby or a Quick Match search is dropped.
    /// </summary>
    public sealed class JoinLinkHandler : IStartable, IDisposable
    {
        private readonly ScreenRouter _router;
        private readonly MatchLauncher _launcher;
        private readonly IDeepLinks _deepLinks;
        private readonly IRoomService _rooms;
        private readonly INetworkStatus _network;
        private readonly IDialogs _dialogs;
        private readonly IToast _toast;
        private readonly ILocalization _localization;

        public JoinLinkHandler(
            ScreenRouter router,
            MatchLauncher launcher,
            IDeepLinks deepLinks,
            IRoomService rooms,
            INetworkStatus network,
            IDialogs dialogs,
            IToast toast,
            ILocalization localization)
        {
            _router = router;
            _launcher = launcher;
            _deepLinks = deepLinks;
            _rooms = rooms;
            _network = network;
            _dialogs = dialogs;
            _toast = toast;
            _localization = localization;
        }

        public void Start()
        {
            _deepLinks.JoinCodeArrived += Open;
            Open();
        }

        public void Dispose()
        {
            _deepLinks.JoinCodeArrived -= Open;
        }

        private void Open()
        {
            if (!_deepLinks.HasJoinCode)
            {
                return;
            }

            bool canJoin = _router.Has(ScreenId.PlayWithFriends) && _router.Has(ScreenId.JoinRoom);
            if (!canJoin || _rooms.Current != null || _router.Current == ScreenId.Matchmaking)
            {
                _deepLinks.TakeJoinCode();
                return;
            }

            if (!_network.IsOnline)
            {
                _deepLinks.TakeJoinCode();
                _toast.Show(_localization.Get("ui.toast.no_internet"));
                return;
            }

            // Join Room takes the code when it opens; the primer still comes first for a new player.
            _dialogs.HandleBack();
            _launcher.CancelPending();
            _router.ShowStack(new[] { ScreenId.MainMenu, ScreenId.PlayWithFriends });
            _launcher.Enter(ScreenId.JoinRoom);
        }
    }
}
