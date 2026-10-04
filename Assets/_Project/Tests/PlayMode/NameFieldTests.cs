using System;
using System.Collections;
using System.Reflection;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The two names a player types, the room name (GDD §11 screen 02) and the display name
    /// (screen 14): what is typed starts where the field's words start at rest, like the
    /// placeholder, also after deleting what was there. TMP_InputField scrolls by moving its text
    /// and, after a deletion, moves it back to where it expects the text to rest; a text that did
    /// not fill its typing area was pulled to the middle of the field. Both fields stay above the
    /// on-screen keyboard (a stand-in here) and edit in place, so a phone draws their caret.
    /// </summary>
    public sealed class NameFieldTests
    {
        private GameFixture _game;
        private ScreenKeyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _keyboard = new ScreenKeyboard();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator CreateRoom_ANewRoomName_StartsWhereThePlaceholderDoes()
        {
            yield return _game.Boot();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "PlayWithFriendsButton");
            GameFixture.Press(router, "CreateButton");
            yield return null;
            Assert.AreEqual(ScreenId.CreateRoom, router.Current);
            TMP_InputField field = Field(router, ScreenId.CreateRoom);
            yield return Settle(field);
            float atRest = PenStart(field.textComponent);

            yield return Focus(field);
            yield return DeleteAll(field);
            var placeholder = (TMP_Text)field.placeholder;
            Assert.IsTrue(placeholder.enabled, "the empty field shows its placeholder");
            Assert.AreEqual(atRest, PenStart(placeholder), 1f, "the placeholder starts where the room name did");

            yield return Type(field, "abc");

            Assert.AreEqual(atRest, PenStart(field.textComponent), 1f, "the new name starts where the placeholder does, not mid-field");
        }

        [UnityTest]
        public IEnumerator Profile_ANewDisplayName_StartsWhereTheOldOneDid()
        {
            yield return _game.Boot();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "ProfileButton");
            yield return null;
            Assert.AreEqual(ScreenId.Profile, router.Current);
            TMP_InputField field = Field(router, ScreenId.Profile);
            yield return Settle(field);
            float atRest = PenStart(field.textComponent);

            yield return Focus(field);
            yield return DeleteAll(field);
            yield return Type(field, "abc");

            Assert.AreEqual(atRest, PenStart(field.textComponent), 1f, "the new name starts where the old one did, not mid-field");
        }

        [UnityTest]
        public IEnumerator CreateRoom_TheKeyboard_LiftsTheScreen_SoTheNameStaysAboveIt()
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<IOnScreenKeyboard>(_keyboard));
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "PlayWithFriendsButton");
            GameFixture.Press(router, "CreateButton");
            yield return null;
            Assert.AreEqual(ScreenId.CreateRoom, router.Current);
            CreateRoomView view = router.Get<CreateRoomView>(ScreenId.CreateRoom);
            yield return KeyboardLiftsTheField(Field(router, ScreenId.CreateRoom), () => view.Lift);
        }

        [UnityTest]
        public IEnumerator Profile_TheKeyboard_LiftsTheScreen_SoTheNameStaysAboveIt()
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<IOnScreenKeyboard>(_keyboard));
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "ProfileButton");
            yield return null;
            Assert.AreEqual(ScreenId.Profile, router.Current);
            ProfileView view = router.Get<ProfileView>(ScreenId.Profile);
            yield return KeyboardLiftsTheField(Field(router, ScreenId.Profile), () => view.Lift);
        }

        /// <summary>
        /// A phone held sideways: the keyboard covers 60% of the screen, over the field. The field
        /// edits in place (so a phone draws its caret, as on the name question) and the screen
        /// rises until the field is above the keyboard, then settles back once it has gone.
        /// </summary>
        private IEnumerator KeyboardLiftsTheField(TMP_InputField field, Func<float> lift)
        {
            Assert.IsTrue(HidesMobileInputOnAPhone(field), "on a phone the field edits in place: the keyboard's own box is hidden and the field draws the caret");
            yield return Settle(field);
            var rect = (RectTransform)field.transform;
            float keyboard = Screen.height * 0.6f;
            Assert.Less(Edge(rect, 0), keyboard, "at rest the keyboard would cover the field");

            _keyboard.Height = keyboard;
            yield return Focus(field);
            yield return GameFixture.Until(() => lift() > 0f && Edge(rect, 0) > keyboard, "the screen to rise until the field is above the keyboard");
            Assert.Less(Edge(rect, 1), Screen.height, "the field stays on the screen");

            _keyboard.Height = 0f;
            yield return GameFixture.Until(() => lift() == 0f, "the screen to settle back once the keyboard has gone");
            Assert.Less(Edge(rect, 0), keyboard, "the field is back where it was");
        }

        /// <summary>
        /// The flag a phone reads (Android, iOS): in the Editor <see cref="TMP_InputField.shouldHideMobileInput"/>
        /// always answers true, whatever the field says.
        /// </summary>
        private static bool HidesMobileInputOnAPhone(TMP_InputField field)
        {
            FieldInfo flag = typeof(TMP_InputField).GetField("m_HideMobileInput", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(flag, "TMP_InputField keeps the flag in m_HideMobileInput");
            return (bool)flag.GetValue(field);
        }

        /// <summary>The bottom (corner 0) or top (corner 1) of a rect on the overlay canvas, in screen pixels.</summary>
        private static float Edge(RectTransform rect, int corner)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[corner].y;
        }

        private static TMP_InputField Field(ScreenRouter router, ScreenId screen)
        {
            TMP_InputField field = router.Get<UIScreen>(screen).GetComponentInChildren<TMP_InputField>(true);
            Assert.IsNotNull(field, screen + " has its name field");
            return field;
        }

        /// <summary>
        /// Waits out the screen's arrival (GDD §14: it slides in for 220 ms) until the field stays
        /// put from one frame to the next, so that every measurement is taken in the same place.
        /// </summary>
        private static IEnumerator Settle(TMP_InputField field)
        {
            yield return new WaitForSecondsRealtime(0.3f);
            float last = float.NaN;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (field.transform.position.x != last && Time.realtimeSinceStartup < deadline)
            {
                last = field.transform.position.x;
                yield return null;
            }
        }

        private static IEnumerator Focus(TMP_InputField field)
        {
            field.Select();
            field.ActivateInputField();
            yield return null;
            yield return null;
            Assert.IsTrue(field.isFocused, "the field takes the keyboard");
            field.caretPosition = field.text.Length;
            yield return null;
        }

        /// <summary>Deletes the field's text the way a player does: Backspace, one key at a time.</summary>
        private static IEnumerator DeleteAll(TMP_InputField field)
        {
            for (int guard = 0; field.text.Length > 0 && guard < 64; guard++)
            {
                field.ProcessEvent(Event.KeyboardEvent("backspace"));
                field.ForceLabelUpdate();
                yield return null;
            }

            Assert.AreEqual(string.Empty, field.text, "Backspace emptied the field");
        }

        private static IEnumerator Type(TMP_InputField field, string text)
        {
            foreach (char c in text)
            {
                field.ProcessEvent(Event.KeyboardEvent(c.ToString()));
                field.ForceLabelUpdate();
                yield return null;
            }

            yield return null;
            Assert.AreEqual(text, field.text, "the keys were typed into the field");
        }

        /// <summary>An on-screen keyboard the test puts up (its height in screen pixels) and takes down (0).</summary>
        private sealed class ScreenKeyboard : IOnScreenKeyboard
        {
            public float Height { get; set; }
        }

        /// <summary>Where the first letter's pen position is, in world space: the start of the words.</summary>
        private static float PenStart(TMP_Text text)
        {
            text.ForceMeshUpdate();
            Assert.Greater(text.textInfo.characterCount, 0, text.name + " shows some text");
            TMP_CharacterInfo first = text.textInfo.characterInfo[0];
            return text.transform.TransformPoint(new Vector3(first.origin, first.baseLine, 0f)).x;
        }
    }
}
