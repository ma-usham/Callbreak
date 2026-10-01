using System.Collections;
using Callbreak.Application.Profile;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// ADR 022: the game asks the player's name when it starts, until one is chosen, and the main
    /// menu then shows it. The real Boot and Menu scenes, with saves in a temporary folder.
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

            Button next = Continue(prompt);
            Assert.IsFalse(next.interactable, "Continue waits for a name");
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

        private static Button Continue(NamePromptView prompt)
        {
            foreach (Button button in prompt.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "ContinueButton")
                {
                    return button;
                }
            }

            Assert.Fail("the question has a Continue button");
            return null;
        }

        private static string MessageText(NamePromptView prompt)
        {
            foreach (TMP_Text text in prompt.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.name == "Message")
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
