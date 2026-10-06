using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// Create Room and Join Room in the real Menu scene (GDD §11 screens 02 and 03): the default
    /// table name, a failed create answered on the screen, Paste Code, the code boxes' caret, and
    /// the screen rising above the on-screen keyboard. The room service and the keyboard are
    /// stand-ins, so nothing here reaches the network.
    /// </summary>
    public sealed class FriendsFlowTests
    {
        private GameFixture _game;
        private Rooms _rooms;
        private Clipboard _clipboard;
        private ScreenKeyboard _keyboard;

        // The profile's clock: a saved name stays 7 days (ADR 041), so a test that renames twice moves it on.
        private System.DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _rooms = new Rooms();
            _clipboard = new Clipboard();
            _keyboard = new ScreenKeyboard();
            _now = new System.DateTime(2026, 10, 6, 12, 0, 0, System.DateTimeKind.Utc);
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

            // A name the player typed stays theirs, through a rename a week later (ADR 041: not sooner).
            field = NameField(router);
            field.text = "Friday Cards";
            Assert.IsTrue(router.Back());
            _now += ProfileService.NameChangeInterval;
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

        [UnityTest]
        public IEnumerator JoinRoom_TheCaret_StandsInTheNextBoxToFill_WhileTheBoxesHaveFocus()
        {
            // The owner, 3 Oct 2026: no cursor in the room code. The hidden field's own caret is
            // invisible by design, so the boxes draw one where the next character goes.
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "PlayWithFriendsButton");
            GameFixture.Press(router, "JoinButton");
            yield return null;
            Assert.AreEqual(ScreenId.JoinRoom, router.Current);
            CodeInput code = router.Get<UIScreen>(ScreenId.JoinRoom).GetComponentInChildren<CodeInput>(true);
            TMP_InputField field = code.GetComponentInChildren<TMP_InputField>(true);
            Assert.IsNotNull(code.Caret, "the boxes have a caret of their own");
            Assert.AreEqual(-1, code.CaretBox, "no caret before the boxes are tapped");
            Assert.IsFalse(code.Caret.enabled);

            code.Focus();
            yield return GameFixture.Until(() => code.CaretBox == 0, "the caret to stand in the first box");
            AssertCaretShowsIn(code, 0, "as the boxes take the keyboard");

            yield return Type(field, "ab");
            Assert.AreEqual("AB", code.Code);
            Assert.AreEqual(2, code.CaretBox, "two characters in, the caret is in the third box");
            AssertCaretShowsIn(code, 2, "after typing");

            yield return Type(field, "cdef");
            Assert.AreEqual("ABCDEF", code.Code);
            Assert.AreEqual(-1, code.CaretBox, "all six filled: no box to fill");
            Assert.IsFalse(code.Caret.enabled, "no caret once all six are filled");

            yield return PressKey(field, "backspace");
            Assert.AreEqual("ABCDE", code.Code);
            Assert.AreEqual(5, code.CaretBox, "backspace frees the last box");
            AssertCaretShowsIn(code, 5, "after backspace");

            // A tap anywhere else takes the keyboard away.
            EventSystem.current.SetSelectedGameObject(null);
            yield return GameFixture.Until(() => code.CaretBox == -1, "the caret to go with the focus");
            Assert.IsFalse(code.Caret.enabled, "no caret without focus");
        }

        [UnityTest]
        public IEnumerator JoinRoom_TheKeyboard_LiftsTheScreen_SoTheBoxesStayAboveIt()
        {
            // The owner, 3 Oct 2026: the keyboard covered the room code. A phone held sideways: the
            // keyboard covers 60% of the screen, over the middle where the boxes are.
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "PlayWithFriendsButton");
            GameFixture.Press(router, "JoinButton");
            yield return null;
            Assert.AreEqual(ScreenId.JoinRoom, router.Current);
            JoinRoomView view = router.Get<JoinRoomView>(ScreenId.JoinRoom);
            CodeInput code = view.GetComponentInChildren<CodeInput>(true);
            TMP_InputField field = code.GetComponentInChildren<TMP_InputField>(true);
            var boxes = (RectTransform)code.transform;
            yield return new WaitForSecondsRealtime(0.4f);
            float keyboard = Screen.height * 0.6f;
            Assert.Less(Edge(boxes, 0), keyboard, "at rest the keyboard would cover the boxes");

            _keyboard.Height = keyboard;
            yield return null;
            yield return null;
            Assert.AreEqual(0f, view.Lift, "the screen waits for the boxes to take the keyboard");

            code.Focus();
            yield return GameFixture.Until(() => view.Lift > 0f && Edge(boxes, 0) > keyboard, "the screen to rise until the boxes are above the keyboard");
            Assert.Less(Edge(boxes, 1), Screen.height, "the boxes stay on the screen");

            // Lifted, the boxes still take taps and the caret still stands in the middle of its box.
            yield return Type(field, "a");
            AssertCaretShowsIn(code, 1, "while the screen is lifted");
            GameObject hit = TopHit(ScreenCentre(code, 3));
            Assert.AreSame(field.gameObject, ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit), "a tap on a lifted box reaches the code field (it lands on " + hit.name + ")");

            _keyboard.Height = 0f;
            yield return GameFixture.Until(() => view.Lift == 0f, "the screen to settle back once the keyboard has gone");
            Assert.Less(Edge(boxes, 0), keyboard, "the boxes are back where they were");
        }

        /// <summary>The bottom (corner 0) or top (corner 1) of a rect on the overlay canvas, in screen pixels.</summary>
        private static float Edge(RectTransform rect, int corner)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[corner].y;
        }

        /// <summary>The middle of box <paramref name="box"/> on the screen (the canvas is screen-space overlay).</summary>
        private static Vector2 ScreenCentre(CodeInput code, int box)
        {
            var face = (RectTransform)code.transform.Find($"CodeBox{box}/Face");
            Assert.IsNotNull(face, $"CodeBox{box} has its face");
            return RectTransformUtility.WorldToScreenPoint(null, face.TransformPoint(face.rect.center));
        }

        /// <summary>What a tap at this point of the screen reaches first.</summary>
        private static GameObject TopHit(Vector2 point)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.IsNotEmpty(hits, "something takes a tap at " + point);
            return hits[0].gameObject;
        }

        /// <summary>
        /// Keys typed into the hidden field the way a keyboard sends them, all in one frame; after
        /// each the field updates its text, as its own event handling does.
        /// </summary>
        private static IEnumerator Type(TMP_InputField field, string keys)
        {
            foreach (char key in keys)
            {
                field.ProcessEvent(Event.KeyboardEvent(key.ToString()));
                field.ForceLabelUpdate();
            }

            yield return null;
        }

        /// <summary>One named key (e.g. "backspace") pressed on the hidden field, the way <see cref="Type"/> types.</summary>
        private static IEnumerator PressKey(TMP_InputField field, string key)
        {
            field.ProcessEvent(Event.KeyboardEvent(key));
            field.ForceLabelUpdate();
            yield return null;
        }

        /// <summary>The caret is drawn now (a change restarts its blink) and centred on box <paramref name="box"/>.</summary>
        private static void AssertCaretShowsIn(CodeInput code, int box, string when)
        {
            Assert.IsTrue(code.Caret.enabled, "the caret shows at once " + when);
            var face = (RectTransform)code.transform.Find($"CodeBox{box}/Face");
            Assert.IsNotNull(face, $"CodeBox{box} has its face");
            RectTransform bar = code.Caret.rectTransform;
            Vector2 centre = face.InverseTransformPoint(bar.TransformPoint(bar.rect.center));
            Assert.AreEqual(face.rect.center.x, centre.x, 0.5f, $"the caret is centred across box {box} {when}");
            Assert.AreEqual(face.rect.center.y, centre.y, 0.5f, $"the caret is centred up and down in box {box} {when}");
        }

        private IEnumerator Boot()
        {
            // The profile over the test's save folder, on the test's clock; an instance, since a
            // second factory for the same contract is refused by the container.
            ProfileService profile = null;
            yield return _game.Boot(
                seed: saves => profile = new ProfileService(saves, new NameFilter(), () => _now),
                extra: builder =>
                {
                    builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                    builder.RegisterInstance<IRoomService>(_rooms);
                    builder.RegisterInstance<IClipboard>(_clipboard);
                    builder.RegisterInstance<IOnScreenKeyboard>(_keyboard);
                    builder.RegisterInstance<IProfileService>(profile);
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

        /// <summary>An on-screen keyboard the test puts up (its height in screen pixels) and takes down (0).</summary>
        private sealed class ScreenKeyboard : IOnScreenKeyboard
        {
            public float Height { get; set; }
        }
    }
}
