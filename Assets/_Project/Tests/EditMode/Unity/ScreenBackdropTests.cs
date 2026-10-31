using Callbreak.Presentation.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// One background for every menu screen, and the table behind the pages over a match (the owner,
    /// 31 Oct 2026; ADR 058's addendum): no screen the routers swap carries a picture of its own, so a
    /// screen change no longer swaps and slides the picture. The main menu holds the one picture the
    /// Menu scene lifts out to stand behind all its screens (<see cref="MenuBackdrop"/>). The pages that
    /// open over a match have a dim that the table's scope turns on, and none until then: Settings and
    /// How to Play open over the menus too. The loading curtain and the name prompt, each on a canvas
    /// of its own over whatever is under it, keep theirs. The prefabs are the built ones, so these live
    /// outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class ScreenBackdropTests
    {
        private const string Screens = "Assets/_Project/Prefabs/UI/Screens/";

        [TestCase("PlayWithFriends")]
        [TestCase("CreateRoom")]
        [TestCase("JoinRoom")]
        [TestCase("PrivateLobby")]
        [TestCase("Matchmaking")]
        [TestCase("OfflineSetup")]
        [TestCase("Settings")]
        [TestCase("HowToPlay")]
        [TestCase("Profile")]
        [TestCase("Shop")]
        [TestCase("EarnCoins")]
        [TestCase("PlayOnline")]
        [TestCase("DailyRewards")]
        [TestCase("RoundResults")]
        [TestCase("MatchResults")]
        [TestCase("Scorecard")]
        public void ARoutedScreen_HasNoPictureOfItsOwn(string screen)
        {
            Assert.IsNull(Load(screen).GetComponentInChildren<EquippedBackground>(true), screen + " shows the scene's background, not a copy of its own");
        }

        [Test]
        public void TheMainMenu_HoldsTheMenusOneBackground()
        {
            Assert.AreEqual(1, Load("MainMenu").GetComponentsInChildren<EquippedBackground>(true).Length);
        }

        [TestCase("SceneLoading")]
        [TestCase("NamePrompt")]
        public void AScreenOnACanvasOfItsOwn_KeepsItsPicture(string screen)
        {
            Assert.AreEqual(1, Load(screen).GetComponentsInChildren<EquippedBackground>(true).Length);
        }

        [TestCase("Scorecard")]
        [TestCase("RoundResults")]
        [TestCase("MatchResults")]
        [TestCase("Settings")]
        [TestCase("HowToPlay")]
        public void APageOverTheMatch_HasADimTheTableTurnsOn(string screen)
        {
            var over = Load(screen).GetComponent<OverTheTable>();
            Assert.IsNotNull(over, screen + " knows how to stand over the table");
            Assert.IsFalse(over.Shown, "no dim until the table's scope asks for it (the menus show none)");

            Image dim = over.Dim;
            Assert.IsNotNull(dim, screen + " has its dim");
            Image menuDim = System.Array.Find(Load("MatchMenu").GetComponentsInChildren<Image>(true), image => image.name == "Dim");
            Assert.IsNotNull(menuDim, "the match menu has its dim");
            Assert.AreEqual(menuDim.color, dim.color, "the match menu's dim, so the table keeps its look between them");
            Assert.Less(dim.color.a, 1f, "the table shows through it");
            Assert.IsTrue(dim.raycastTarget, "it keeps taps off the table");
            Assert.AreEqual(0, dim.transform.GetSiblingIndex(), "under everything the page draws");
            var rect = dim.rectTransform;
            Assert.AreEqual(Vector2.zero, rect.anchorMin);
            Assert.AreEqual(Vector2.one, rect.anchorMax);
            Assert.That(rect.offsetMin.x, Is.LessThan(0f), "past the screen's edges");
            Assert.That(rect.offsetMax.x, Is.GreaterThan(0f), "past the screen's edges");
        }

        private static GameObject Load(string screen)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Screens + screen + ".prefab");
            Assert.IsNotNull(prefab, Screens + screen + ".prefab");
            return prefab;
        }
    }
}
