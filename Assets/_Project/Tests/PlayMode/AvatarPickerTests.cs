using System.Collections;
using System.Reflection;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// ADR 041 on the real screens: the name question at the start offers the five portraits and a
    /// tap saves one; on the Profile screen the pencil (or the big avatar) opens the picker, a tap
    /// saves a portrait and the big avatar follows, Android back and Done close it; a name saved
    /// there stays 7 days, the field taking no typing, its line saying how long, and a tap on the
    /// field nudging that line.
    /// </summary>
    public sealed class AvatarPickerTests
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
        public IEnumerator TheNameQuestion_OffersEveryPortrait_AndATapSavesOne()
        {
            yield return _game.Boot(freshInstall: true);
            NamePromptView prompt = Object.FindFirstObjectByType<NamePromptView>(FindObjectsInactive.Include);
            Assert.IsNotNull(prompt, "the Menu scene has the name question");
            yield return GameFixture.Until(() => prompt.IsShowing, "the name question to open");
            var library = _game.Services.Resolve<AvatarLibrary>();
            var profile = _game.Services.Resolve<IProfileService>();
            AvatarPicker picker = prompt.Avatars;

            Assert.AreEqual(library.Count, picker.Count, "every portrait is offered");
            Assert.AreEqual(profile.AvatarId, picker.Chosen, "the player's portrait is ringed");
            for (int i = 0; i < picker.Count; i++)
            {
                Assert.IsNotNull(library.Framed(i), "preset " + i + " has a framed portrait");
                Assert.AreSame(library.Framed(i), Picture(picker, i), "choice " + i + " shows portrait " + i + " in its gold ring (ADR 043)");
            }

            Assert.AreSame(library.Framed(profile.AvatarId), BigPortrait(prompt), "the big portrait is the player's");

            picker.ChoiceAt(3).onClick.Invoke();

            Assert.AreEqual(3, profile.AvatarId, "the tap saves it");
            Assert.AreEqual(3, picker.Chosen, "and rings it");
            Assert.AreSame(library.Framed(3), BigPortrait(prompt), "the big portrait follows");
            Assert.IsTrue(prompt.IsShowing, "the question stays until the name is answered");
        }

        [UnityTest]
        public IEnumerator TheNameQuestion_TheNameUnderThePortrait_FollowsWhatIsTyped()
        {
            yield return _game.Boot(freshInstall: true);
            NamePromptView prompt = Object.FindFirstObjectByType<NamePromptView>(FindObjectsInactive.Include);
            yield return GameFixture.Until(() => prompt.IsShowing, "the name question to open");
            TMP_InputField field = prompt.GetComponentInChildren<TMP_InputField>(true);
            Assert.AreEqual("Player", PlayerName(prompt), "the current name while the field is empty");

            field.text = "  Asha ";
            Assert.AreEqual("Asha", PlayerName(prompt), "the name as it would be saved");

            field.text = string.Empty;
            Assert.AreEqual("Player", PlayerName(prompt));
        }

        [UnityTest]
        public IEnumerator Profile_ThePencilOpensThePicker_ATapSavesAPortrait_BackAndDoneCloseIt()
        {
            yield return _game.Boot();
            ScreenRouter router = _game.MenuRouter;
            var library = _game.Services.Resolve<AvatarLibrary>();
            var profile = _game.Services.Resolve<IProfileService>();
            GameFixture.Press(router, "ProfileButton");
            yield return null;
            Assert.AreEqual(ScreenId.Profile, router.Current);
            ProfileView view = router.Get<ProfileView>(ScreenId.Profile);
            Assert.IsFalse(view.IsAvatarPickerOpen);

            GameFixture.Press(router, "EditAvatarButton");
            Assert.IsTrue(view.IsAvatarPickerOpen, "the pencil opens the picker");
            Assert.AreEqual(profile.AvatarId, view.AvatarPicker.Chosen, "with the player's portrait ringed");

            view.AvatarPicker.ChoiceAt(4).onClick.Invoke();
            Assert.AreEqual(4, profile.AvatarId, "a tap saves the portrait");
            Assert.AreEqual(4, view.AvatarPicker.Chosen, "and moves the ring");
            Assert.AreSame(library.Get(4), BigAvatar(view), "the big avatar follows");

            Assert.IsTrue(router.Back(), "Android back is taken");
            Assert.IsFalse(view.IsAvatarPickerOpen, "back closes the picker first");
            Assert.AreEqual(ScreenId.Profile, router.Current, "and the screen stays");
            yield return GameFixture.Until(() => !view.AvatarPicker.gameObject.activeInHierarchy, "the picker to fade out");

            GameFixture.Press(router, "AvatarRing");
            Assert.IsTrue(view.IsAvatarPickerOpen, "the big avatar opens it too");
            GameFixture.PressIn(view, "AvatarDoneButton");
            Assert.IsFalse(view.IsAvatarPickerOpen, "Done closes it");
            Assert.AreEqual(4, profile.AvatarId);
        }

        [UnityTest]
        public IEnumerator Profile_ASavedName_StaysAWeek_TheFieldTakingNoTyping()
        {
            yield return _game.Boot();
            ScreenRouter router = _game.MenuRouter;
            var profile = _game.Services.Resolve<IProfileService>();
            var text = _game.Services.Resolve<ILocalization>();
            GameFixture.Press(router, "ProfileButton");
            yield return null;
            ProfileView view = router.Get<ProfileView>(ScreenId.Profile);
            TMP_InputField field = view.GetComponentInChildren<TMP_InputField>(true);
            Assert.IsTrue(view.NameEditable, "the name answered at the start can be changed at once");

            field.text = "Asha";
            Assert.AreEqual(text.Get("ui.profile.name_week_note"), NameMessage(view), "before Save, the line says the name then stays a week");
            GameFixture.Press(router, "SaveButton");
            yield return null;

            Assert.AreEqual("Asha", profile.DisplayName);
            Assert.IsFalse(view.NameEditable, "the field takes no typing for 7 days");
            Assert.IsFalse(GameFixture.CanPress(view, "SaveButton"), "Save stays greyed");
            Assert.AreEqual(text.Format("ui.profile.name_wait_days", 7), NameMessage(view), "the line says how long");
            Assert.AreEqual(NameCheck.TooSoon, profile.TrySetDisplayName("Bikash"), "and the profile refuses a new one");

            Assert.IsFalse(view.NameMessageNudging);
            ExecuteEvents.Execute(field.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return null;
            Assert.IsFalse(field.isFocused, "a tap on the locked field opens no keyboard");
            Assert.IsTrue(view.NameMessageNudging, "it nudges the line that says how long");
            yield return GameFixture.Until(() => !view.NameMessageNudging, "the nudge to settle");
            Assert.AreEqual(text.Format("ui.profile.name_wait_days", 7), NameMessage(view), "the line still says how long");
        }

        private static Sprite Picture(AvatarPicker picker, int index)
        {
            Transform picture = picker.transform.Find("Avatar" + index + "/Picture");
            Assert.IsNotNull(picture, "choice " + index + " has a picture");
            return picture.GetComponent<Image>().sprite;
        }

        private static Sprite BigPortrait(NamePromptView prompt)
        {
            FieldInfo field = typeof(NamePromptView).GetField("_avatar", BindingFlags.NonPublic | BindingFlags.Instance);
            return ((Image)field.GetValue(prompt)).sprite;
        }

        private static string PlayerName(NamePromptView prompt)
        {
            FieldInfo field = typeof(NamePromptView).GetField("_playerName", BindingFlags.NonPublic | BindingFlags.Instance);
            return ((TMP_Text)field.GetValue(prompt)).text;
        }

        private static Sprite BigAvatar(ProfileView view)
        {
            FieldInfo field = typeof(ProfileView).GetField("_avatar", BindingFlags.NonPublic | BindingFlags.Instance);
            return ((Image)field.GetValue(view)).sprite;
        }

        private static string NameMessage(ProfileView view)
        {
            foreach (TMP_Text label in view.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == "NameMessage")
                {
                    return label.text;
                }
            }

            Assert.Fail("the Profile screen has the line under the name field");
            return null;
        }
    }
}
