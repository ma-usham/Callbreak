using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// Create Room and Join Room in the real Menu scene (GDD §11 screens 02 and 03): the default
    /// table name, a failed create answered on the screen, and Paste Code. The room service is a
    /// stand-in, so nothing here reaches the network.
    /// </summary>
    public sealed class FriendsFlowTests
    {
        private GameFixture _game;
        private Rooms _rooms;
        private Clipboard _clipboard;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _rooms = new Rooms();
            _clipboard = new Clipboard();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator CreateRoom_DefaultNameFollowsThePlayersName_UntilTheyTypeTheirOwn()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            var profile = _game.Services.Resolve<IProfileService>();

            yield return OpenCreateRoom(router);
            TMP_InputField field = NameField(router);
            Assert.AreEqual(profile.DisplayName + "'s Table", field.text, "GDD 11 screen 02: the default is \"{Name}'s Table\"");

            // Renamed while the screen is left: the default is rebuilt, and fitted into 20 characters.
            Assert.IsTrue(router.Back());
            Assert.AreEqual(NameCheck.Ok, profile.TrySetDisplayName("Bartholomew Smi"));
            GameFixture.Press(router, "CreateButton");
            yield return null;
            Assert.AreEqual("Bartholomew's Table", NameField(router).text);

            // A name the player typed stays theirs.
            field = NameField(router);
            field.text = "Friday Cards";
            Assert.IsTrue(router.Back());
            Assert.AreEqual(NameCheck.Ok, profile.TrySetDisplayName("Asha"));
            GameFixture.Press(router, "CreateButton");
            yield return null;
            Assert.AreEqual("Friday Cards", NameField(router).text);
        }

        [UnityTest]
        public IEnumerator CreateRoom_Failure_StaysOnTheScreenWithItsMessage_AndTheButtonRetries()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            var localization = _game.Services.Resolve<ILocalization>();
            yield return OpenCreateRoom(router);
            UIScreen screen = router.Get<UIScreen>(ScreenId.CreateRoom);
            TMP_Text buttonLabel = Label(screen, "CreateButtonLabel");

            _rooms.CreateError = RoomError.RateLimited;
            GameFixture.Press(router, "CreateButton");
            yield return null;

            Assert.AreEqual(ScreenId.CreateRoom, router.Current, "GDD 11 screen 02: a failure stays on this screen");
            Assert.AreEqual(1, _rooms.Creates);
            Assert.IsTrue(Shows(screen, localization.Get("ui.error.too_many_attempts")), "the reason is on the screen");
            Assert.AreEqual(localization.Get("ui.common.retry"), buttonLabel.text, "the button offers Retry");

            GameFixture.Press(router, "CreateButton");
            yield return null;
            Assert.AreEqual(2, _rooms.Creates, "Retry tries again");

            // Editing the name takes the message away and the button is Create Room again.
            NameField(router).text = "Second try";
            Assert.IsFalse(Shows(screen, localization.Get("ui.error.too_many_attempts")));
            Assert.AreEqual(localization.Get("ui.create_room.cta"), buttonLabel.text);
        }

        [UnityTest]
        public IEnumerator JoinRoom_Paste_FindsTheCodeInAnInviteMessage_AndLeavesTheBoxesAloneWithoutOne()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "PlayWithFriendsButton");
            GameFixture.Press(router, "JoinButton");
            yield return null;
            Assert.AreEqual(ScreenId.JoinRoom, router.Current);
            CodeInput code = router.Get<UIScreen>(ScreenId.JoinRoom).GetComponentInChildren<CodeInput>(true);

            _clipboard.Text = "Join my Callbreak table! Code AB2CDE · callbreak://join/AB2CDE";
            GameFixture.Press(router, "PasteButton");
            Assert.AreEqual("AB2CDE", code.Code, "the code, not the first six letters of the message");
            Assert.IsTrue(GameFixture.CanPress(router.Get<UIScreen>(ScreenId.JoinRoom), "JoinButton"));

            _clipboard.Text = "see you at eight";
            GameFixture.Press(router, "PasteButton");
            Assert.AreEqual("AB2CDE", code.Code, "a clipboard without a code changes nothing");

            _clipboard.Text = "xy-7k 9m";
            GameFixture.Press(router, "PasteButton");
            Assert.AreEqual("XY7K9M", code.Code, "GDD 11 screen 03: spaces, dashes and case do not matter");
        }

        private IEnumerator Boot()
        {
            yield return _game.Boot(extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IRoomService>(_rooms);
                builder.RegisterInstance<IClipboard>(_clipboard);
            });
        }

        private static IEnumerator OpenCreateRoom(ScreenRouter router)
        {
            GameFixture.Press(router, "PlayWithFriendsButton");
            GameFixture.Press(router, "CreateButton");
            yield return null;
            Assert.AreEqual(ScreenId.CreateRoom, router.Current);
        }

        private static TMP_InputField NameField(ScreenRouter router)
        {
            TMP_InputField field = router.Get<UIScreen>(ScreenId.CreateRoom).GetComponentInChildren<TMP_InputField>(true);
            Assert.IsNotNull(field, "Create Room has its name field");
            return field;
        }

        private static TMP_Text Label(UIScreen screen, string objectName)
        {
            foreach (TMP_Text label in screen.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == objectName)
                {
                    return label;
                }
            }

            Assert.Fail("No label " + objectName + " on " + screen.name + ".");
            return null;
        }

        /// <summary>True when an active label on the screen shows exactly this text.</summary>
        private static bool Shows(UIScreen screen, string text)
        {
            foreach (TMP_Text label in screen.GetComponentsInChildren<TMP_Text>(false))
            {
                if (label.text == text)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A room service that opens no rooms: every attempt ends with the error it is told to give.</summary>
        private sealed class Rooms : IRoomService
        {
            public RoomError CreateError = RoomError.Unknown;

            public int Creates { get; private set; }

            public IRoomSession Current => null;

            public Task<RoomResult> CreateRoomAsync(RoomSettings settings, CancellationToken ct)
            {
                Creates++;
                return Task.FromResult(new RoomResult(null, CreateError));
            }

            public Task<RoomResult> JoinRoomAsync(string code, CancellationToken ct)
            {
                return Task.FromResult(new RoomResult(null, RoomError.NotFound));
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
