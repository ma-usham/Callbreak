using System.Collections;
using Callbreak.Application.Profile;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using TMPro;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// A display name may be in any script (GDD §11 screen 14) while the game's own fonts are baked
    /// with Latin only: the device's fonts have to fill in, or the name is a row of empty boxes.
    /// </summary>
    public sealed class PlayerNameTextTests
    {
        private const string Devanagari = "आशा थापा";
        private const string Accented = "René Åsa";

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
        public IEnumerator Name_WithAccents_HasAGlyphForEveryLetterOnTheMainMenu()
        {
            yield return _game.Boot();
            RequireSystemFonts();

            yield return ShowsEveryLetter(Accented);
        }

        [UnityTest]
        public IEnumerator Name_InDevanagari_HasAGlyphForEveryLetterOnTheMainMenu()
        {
            yield return _game.Boot();
            RequireSystemFonts();
            bool devanagari = false;
            foreach (TMP_FontAsset font in _game.Services.Resolve<SystemFontFallbacks>().Added)
            {
                devanagari |= font.HasCharacter('क', false, true);
            }

            if (!devanagari)
            {
                Assert.Ignore("This machine has no Devanagari system font the game looks for.");
            }

            yield return ShowsEveryLetter(Devanagari);
        }

        private void RequireSystemFonts()
        {
            if (_game.Services.Resolve<SystemFontFallbacks>().Added.Count == 0)
            {
                Assert.Ignore("This machine has none of the system fonts the game looks for.");
            }
        }

        private IEnumerator ShowsEveryLetter(string name)
        {
            Assert.AreEqual(NameCheck.Ok, _game.Services.Resolve<IProfileService>().TrySetDisplayName(name));
            yield return null;

            TMP_Text label = null;
            foreach (TMP_Text text in _game.MenuRouter.Get<UIScreen>(ScreenId.MainMenu).GetComponentsInChildren<TMP_Text>(false))
            {
                if (text.text == name)
                {
                    label = text;
                }
            }

            Assert.IsNotNull(label, "the main menu shows the player's name");
            label.ForceMeshUpdate();
            foreach (char c in name)
            {
                Assert.IsTrue(c == ' ' || label.font.HasCharacter(c, true, true), $"a glyph for U+{(int)c:X4}");
            }

            Assert.AreEqual(name.Length, label.textInfo.characterCount, "nothing was dropped from the name");
        }
    }
}
