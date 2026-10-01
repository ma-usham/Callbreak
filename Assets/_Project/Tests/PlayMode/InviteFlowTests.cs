using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// Invite Friends in the private lobby (GDD §11 screen 04): a phone's share sheet when there is
    /// one; where there is none (the Editor, a desktop, iOS until its plugin exists) a panel shows
    /// the invite, ready to copy. The room is a stand-in, so nothing here reaches the network.
    /// </summary>
    public sealed class InviteFlowTests
    {
        private const string Code = "AB2CDE";

        private GameFixture _game;
        private OpenRooms _rooms;
        private Share _share;
        private Clipboard _clipboard;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _rooms = new OpenRooms();
            _share = new Share();
            _clipboard = new Clipboard();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator Invite_WithoutAShareSheet_ShowsTheInviteInAPanel_ThatCopiesIt()
        {
            _share.Available = false;
            yield return OpenLobby();

            GameFixture.Press(_game.MenuRouter, "InviteButton");
            yield return null;

            Assert.AreEqual(1, _share.Calls, "the share sheet is tried first");
            Assert.IsTrue(_game.Dialogs.IsOpen, "with no share sheet, a panel opens");
            string invite = _share.LastText;
            StringAssert.Contains(Code, invite);
            Assert.IsTrue(DialogShows(invite), "the panel shows the invite as it would be sent");
            Assert.AreEqual(string.Empty, _clipboard.Text, "nothing is copied until the player asks");

            _game.PressDialog(confirm: true);
            yield return null;

            Assert.AreEqual(invite, _clipboard.Text, "Copy puts the whole invite on the clipboard");
            Assert.IsFalse(_game.Dialogs.IsOpen);
        }

        [UnityTest]
        public IEnumerator Invite_WithAShareSheet_OpensOnlyTheSheet()
        {
            _share.Available = true;
            yield return OpenLobby();

            GameFixture.Press(_game.MenuRouter, "InviteButton");
            yield return null;

            Assert.AreEqual(1, _share.Calls);
            StringAssert.Contains(Code, _share.LastText);
            Assert.IsFalse(_game.Dialogs.IsOpen, "the device's own sheet does the sharing");
            Assert.AreEqual(string.Empty, _clipboard.Text);
        }

        private IEnumerator OpenLobby()
        {
            yield return _game.Boot(extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IRoomService>(_rooms);
                builder.RegisterInstance<IShareService>(_share);
                builder.RegisterInstance<IClipboard>(_clipboard);
            });
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "PlayWithFriendsButton");
            GameFixture.Press(router, "CreateButton");
            yield return null;
            GameFixture.Press(router, "CreateButton");
            yield return GameFixture.Until(() => router.Current == ScreenId.PrivateLobby, "the lobby of the new room");
        }

        private bool DialogShows(string text)
        {
            DialogView dialog = _game.Project.GetComponentInChildren<DialogView>(true);
            foreach (TMP_Text label in dialog.GetComponentsInChildren<TMP_Text>(false))
            {
                if (label.text == text)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Opens one room, hosted by this player, with the other three seats empty.</summary>
        private sealed class OpenRooms : IRoomService
        {
            public IRoomSession Current { get; private set; }

            public Task<RoomResult> CreateRoomAsync(RoomSettings settings, CancellationToken ct)
            {
                Current = new Room(settings);
                return Task.FromResult(new RoomResult(Current, RoomError.None));
            }

            public Task<RoomResult> JoinRoomAsync(string code, CancellationToken ct)
            {
                return Task.FromResult(new RoomResult(null, RoomError.NotFound));
            }
        }

        private sealed class Room : IRoomSession
        {
            public Room(RoomSettings settings)
            {
                Settings = settings;
                Seats = new[]
                {
                    new RoomSeat(0, true, "Player", 0, "classic-crimson", true, true, false, true),
                    new RoomSeat(1, false, string.Empty, 0, string.Empty, false, false, false, false),
                    new RoomSeat(2, false, string.Empty, 0, string.Empty, false, false, false, false),
                    new RoomSeat(3, false, string.Empty, 0, string.Empty, false, false, false, false),
                };
            }

            public event Action Changed
            {
                add { }
                remove { }
            }

            public event Action MatchStarting
            {
                add { }
                remove { }
            }

            public event Action<RoomError> Closed
            {
                add { }
                remove { }
            }

            public string Code => InviteFlowTests.Code;

            public RoomSettings Settings { get; }

            public IReadOnlyList<RoomSeat> Seats { get; }

            public bool IsHost => true;

            public int LocalSeatIndex => 0;

            public IMatchSession Match => null;

            public Task SetReadyAsync(bool ready, CancellationToken ct) => Task.CompletedTask;

            public Task<bool> StartAsync(bool fillWithBots, CancellationToken ct) => Task.FromResult(false);

            public Task KickAsync(int seatIndex, CancellationToken ct) => Task.CompletedTask;

            public Task LeaveAsync(CancellationToken ct) => Task.CompletedTask;

            public void Dispose()
            {
            }
        }

        /// <summary>A share sheet that is there or not, and remembers what it was given.</summary>
        private sealed class Share : IShareService
        {
            public bool Available;

            public int Calls { get; private set; }

            public string LastText { get; private set; } = string.Empty;

            public bool ShareText(string title, string text)
            {
                Calls++;
                LastText = text;
                return Available;
            }
        }

        private sealed class Clipboard : IClipboard
        {
            public string Text = string.Empty;

            public string GetText() => Text;

            public void SetText(string text) => Text = text ?? string.Empty;
        }
    }
}
