#if UNITY_EDITOR
using System.Collections;
using Callbreak.Application.Economy;
using Callbreak.Infrastructure.Platform;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The banner in the Editor (ADR 071) in the real Boot, Menu and Table scenes: with the Editor's
    /// switch on, a game played in the Editor shows the drawn test banner where a phone shows AdMob's,
    /// in the main menu's bottom-left corner and in the table's top-right corner, and both make room for
    /// it. The tests themselves boot without it, whatever the switch says, so the table and the menu
    /// look as they always did to every other test. Saves go to a temporary folder.
    /// </summary>
    public sealed class EditorTestBannerFlowTests
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
        public IEnumerator WithTheSwitchOn_TheMenuAndTheTable_MakeRoom_AndShowTheTestBanner()
        {
            yield return _game.Boot(editorTestBanner: true);
            MainMenuView menu = Object.FindFirstObjectByType<MainMenuView>();
            Assert.IsTrue(menu.HasBannerRoom, "the menu makes room");
            yield return GameFixture.Until(() => OnScreen() != null, "the test banner on the menu");
            Assert.IsTrue(menu.TryGetBannerSlot(out BannerSlot menuSlot));
            Rect place = OnScreen().Place;
            Assert.AreEqual(menuSlot.X, place.x, "in the slot's left corner");
            Assert.AreEqual(menuSlot.Y + menuSlot.Height, place.yMax, "on its bottom edge");

            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return _game.UntilTable();
            TableView table = Object.FindFirstObjectByType<TableView>();
            Assert.IsTrue(table.HasBannerRoom, "the table makes room");
            yield return GameFixture.Until(() => OnScreen() != null && table.TryGetBannerSlot(out _), "the test banner on the table");
            Assert.IsTrue(table.TryGetBannerSlot(out BannerSlot tableSlot));
            place = OnScreen().Place;
            Assert.AreEqual(tableSlot.Y, place.y, "on the slot's top edge");
            Assert.AreEqual(tableSlot.X + tableSlot.Width, place.xMax, "in its right corner");
        }

        [UnityTest]
        public IEnumerator TheTests_BootWithoutIt_WhateverTheSwitchSays()
        {
            using (EditorTestBanner.While(true))
            {
                yield return _game.Boot();
            }

            MainMenuView menu = Object.FindFirstObjectByType<MainMenuView>();
            Assert.IsFalse(menu.HasBannerRoom, "the menu as it always was");
            yield return null;
            Assert.IsNull(OnScreen(), "no test banner");
        }

        /// <summary>The test banner on the screen, or null.</summary>
        private static EditorTestBannerView OnScreen()
        {
            foreach (EditorTestBannerView view in Object.FindObjectsByType<EditorTestBannerView>(FindObjectsSortMode.None))
            {
                if (view.IsOnScreen)
                {
                    return view;
                }
            }

            return null;
        }
    }
}
#endif
