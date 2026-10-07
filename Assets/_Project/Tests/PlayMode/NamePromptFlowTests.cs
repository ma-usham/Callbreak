using System.Collections;
using System.Reflection;
using Callbreak.Application.Profile;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
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
    /// ADR 022: the game asks the player's name when it starts, until one is chosen, and the main
    /// menu then shows it. ADR 043: on the screen of the owner's mockup 22, with the name field at
    /// the top, where a phone's keyboard leaves it in sight; the keyboard waits for a tap on the
    /// field, its Done only puts it away, and Save Changes answers. The real Boot and Menu scenes,
    /// with saves in a temporary folder.
    /// </summary>
    public sealed class NamePromptFlowTests
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
        public IEnumerator FirstLaunch_AsksTheName_ThenTheRules_ThenTheMainMenuShowsIt()
        {
            yield return _game.Boot(freshInstall: true);
            NamePromptView prompt = Prompt();
            yield return GameFixture.Until(() => prompt.IsShowing, "the name question to open");
            ScreenRouter router = _game.MenuRouter;
            HowToPlayView rules = router.Get<HowToPlayView>(ScreenId.HowToPlay);

            Button next = Save(prompt);
            Assert.IsFalse(next.interactable, "Save Changes waits for a name");
            yield return null;
            Assert.AreEqual(ScreenId.HowToPlay, router.Current, "the app opens on How to Play's background and logo, not the main menu");
            Assert.IsFalse(rules.RulesVisible, "the rules do not show behind the name question");
            Assert.IsTrue(_game.Menu.Container.Resolve<NamePromptPresenter>().HandleBack(), "Android back cannot close the question");
            Assert.IsTrue(prompt.IsShowing);

            TMP_InputField field = prompt.GetComponentInChildren<TMP_InputField>(true);
            field.text = "Ra";
            Assert.IsFalse(next.interactable, "a name of two characters cannot be used");
            Assert.AreEqual(Message("ui.error.name_too_short"), MessageText(prompt), "the line says why");

            field.text = "Asha";
            Assert.IsTrue(next.interactable, "a good name can be used");
            next.onClick.Invoke();
            yield return GameFixture.Until(() => !prompt.IsShowing, "the question to close");

            IProfileService profile = _game.Services.Resolve<IProfileService>();
            Assert.AreEqual("Asha", profile.DisplayName);
            Assert.IsTrue(profile.HasChosenName);
            Assert.AreEqual("Asha", MainMenuName(), "the main menu shows the chosen name");
            Assert.IsFalse(_game.Menu.Container.Resolve<NamePromptPresenter>().HandleBack(), "back reaches the menu again");
            Assert.AreEqual(ScreenId.HowToPlay, router.Current);
            Assert.IsTrue(rules.RulesVisible, "the rules appear once the name is answered");
            Assert.IsTrue(DimShowing(rules), "the first time, the rules sit over the dimmed background, as the name question did");
            Assert.IsFalse(GameFixture.CanPress(rules, "BackButton"), "no back button the first time");
            Assert.IsFalse(GameFixture.CanPress(rules, "SettingsButton"), "no settings gear the first time");

            GameFixture.Press(router, "GotItButton");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, router.Current, "the main menu comes after the rules");
            Assert.IsTrue(_game.Settings.HowToPlaySeen);

            // From Settings later, How to Play has its usual look again.
            _game.PressDialog(confirm: false);
            yield return null;
            GameFixture.Press(router, "SettingsButton");
            yield return null;
            GameFixture.Press(router, "HowToPlayButton");
            yield return null;
            Assert.AreEqual(ScreenId.HowToPlay, router.Current);
            Assert.IsFalse(DimShowing(rules), "no dim once the rules have been seen");
            Assert.IsTrue(GameFixture.CanPress(rules, "BackButton") && GameFixture.CanPress(rules, "SettingsButton"), "the back button and the gear are back");
        }

        private static bool DimShowing(HowToPlayView rules)
        {
            foreach (Transform child in rules.GetComponentsInChildren<Transform>(false))
            {
                if (child.name == "Dim")
                {
                    return true;
                }
            }

            return false;
        }

        [UnityTest]
        public IEnumerator TheField_SitsAboveASidewaysKeyboard_AndOnlyATallerOneLiftsTheScreen()
        {
            // A phone held sideways: the keyboard covers 60% of the screen. The field is at the top
            // of the panel (the owner, 7 Oct 2026), so it stays in sight and nothing moves.
            yield return _game.Boot(freshInstall: true);
            NamePromptView prompt = Prompt();
            yield return GameFixture.Until(() => prompt.IsShowing, "the name question to open");
            var field = (RectTransform)prompt.GetComponentInChildren<TMP_InputField>(true).transform;
            float keyboard = Screen.height * 0.6f;
            Assert.Greater(Edge(field, 0), keyboard, "at rest the field is above the keyboard");

            prompt.KeyboardHeight = () => keyboard;
            yield return null;
            yield return null;
            Assert.AreEqual(0f, prompt.Lift, "so the screen stays where it is");

            // A taller keyboard still never hides the field: the screen rises until it is above it.
            float tall = Screen.height * 0.8f;
            prompt.KeyboardHeight = () => tall;
            yield return GameFixture.Until(() => prompt.Lift > 0f && Edge(field, 0) > tall, "the screen to rise until the field is above the keyboard");
            Assert.Less(Edge(field, 1), Screen.height, "the field stays on the screen");

            prompt.KeyboardHeight = () => 0f;
            yield return GameFixture.Until(() => prompt.Lift == 0f, "the screen to settle back once the keyboard has gone");
        }

        [UnityTest]
        public IEnumerator TheKeyboard_WaitsForATapOnTheField_ItsDoneOnlyPutsItAway_AndSaveAnswers()
        {
            yield return _game.Boot(freshInstall: true);
            NamePromptView prompt = Prompt();
            yield return GameFixture.Until(() => prompt.IsShowing, "the name question to open");
            TMP_InputField field = prompt.GetComponentInChildren<TMP_InputField>(true);
            yield return null;
            yield return null;
            Assert.IsFalse(field.isFocused, "the keyboard waits for a tap on the field (the owner, 7 Oct 2026)");

            ExecuteEvents.Execute(field.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            yield return GameFixture.Until(() => field.isFocused, "the field to take the keyboard at the tap");

            field.text = "Asha";
            field.onSubmit.Invoke(field.text);
            yield return null;
            IProfileService profile = _game.Services.Resolve<IProfileService>();
            Assert.IsFalse(field.isFocused, "the keyboard's Done puts the keyboard away");
            Assert.IsTrue(prompt.IsShowing, "and the question stays");
            Assert.IsFalse(profile.HasChosenName, "nothing is saved yet");

            Save(prompt).onClick.Invoke();
            yield return GameFixture.Until(() => !prompt.IsShowing, "Save Changes to answer the question");
            Assert.AreEqual("Asha", profile.DisplayName);
        }

        [UnityTest]
        public IEnumerator TheNameField_DrawsItsOwnCaret_AlsoOnAPhone()
        {
            // The owner, 3 Oct 2026: no cursor in the name field on an Android phone. TMP_InputField
            // draws its caret only while it edits in place; a phone does that only with the
            // keyboard's own input box hidden, and otherwise leaves the cursor to that box.
            yield return _game.Boot(freshInstall: true);
            NamePromptView prompt = Prompt();
            yield return GameFixture.Until(() => prompt.IsShowing, "the name question to open");
            TMP_InputField field = prompt.GetComponentInChildren<TMP_InputField>(true);

            Assert.IsTrue(field.shouldHideMobileInput, "the field edits in place");
            Assert.IsTrue(HidesMobileInputOnAPhone(field), "on a phone too: the keyboard's own box is hidden and the field draws the caret");
            Assert.AreEqual(1f, field.caretColor.a, 1e-3f, "the caret is opaque");
            Assert.GreaterOrEqual(field.caretWidth, 2, "the caret is at least 2 wide");

            field.ActivateInputField();   // the player taps the field
            yield return GameFixture.Until(() => field.isFocused, "the field to take the keyboard at the tap");
            TMP_SelectionCaret caret = field.textViewport.GetComponentInChildren<TMP_SelectionCaret>(true);
            Assert.IsNotNull(caret, "TMP's caret is under the text area");
            Assert.IsTrue(caret.gameObject.activeInHierarchy, "the caret is shown while the field has focus");
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

        [UnityTest]
        public IEnumerator APlayerWithAName_IsNotAsked()
        {
            yield return _game.Boot();
            yield return null;

            Assert.IsFalse(Prompt().IsShowing, "the name was answered before");
        }

        private NamePromptView Prompt()
        {
            NamePromptView prompt = Object.FindFirstObjectByType<NamePromptView>(FindObjectsInactive.Include);
            Assert.IsNotNull(prompt, "the Menu scene has the name question");
            return prompt;
        }

        private static Button Save(NamePromptView prompt)
        {
            foreach (Button button in prompt.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "SaveButton")
                {
                    return button;
                }
            }

            Assert.Fail("the question has a Save Changes button");
            return null;
        }

        private static string MessageText(NamePromptView prompt)
        {
            foreach (TMP_Text text in prompt.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "NameMessage")
                {
                    return text.text;
                }
            }

            return null;
        }

        private string Message(string key) => _game.Services.Resolve<Callbreak.Application.Ports.ILocalization>().Get(key);

        private string MainMenuName()
        {
            MainMenuView menu = Object.FindFirstObjectByType<MainMenuView>(FindObjectsInactive.Include);
            Assert.IsNotNull(menu, "the main menu exists");
            var so = typeof(MainMenuView).GetField("_playerNameText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return ((TMP_Text)so.GetValue(menu)).text;
        }
    }
}
