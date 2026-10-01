using System.Collections;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The two names a player types, the room name (GDD §11 screen 02) and the display name
    /// (screen 14): what is typed starts where the field's words start at rest, like the
    /// placeholder, also after deleting what was there. TMP_InputField scrolls by moving its text
    /// and, after a deletion, moves it back to where it expects the text to rest; a text that did
    /// not fill its typing area was pulled to the middle of the field.
    /// </summary>
    public sealed class NameFieldTests
    {
        private GameFixture _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
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
