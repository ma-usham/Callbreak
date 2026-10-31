using System.Collections;
using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// One background behind every menu screen (the owner, 31 Oct 2026: "cant we like use the one bg for
    /// all so that it wont be noticed"; ADR 058's addendum), in the real scenes: the Menu scene shows a
    /// single picture, part of no screen and behind all of them, which stays where it is, at full
    /// strength, while the screens change over it, and shows the theme the player equips. Saves go to a
    /// temporary folder.
    /// </summary>
    public sealed class MenuBackdropTests
    {
        private const string Tihar = "tihar-lights";

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
        public IEnumerator TheMenus_ShowOneBackground_BehindEveryScreen_StillWhileTheyChange()
        {
            yield return _game.Boot();
            ScreenRouter router = _game.MenuRouter;
            UIScreen menu = router.Get<UIScreen>(ScreenId.MainMenu);
            Transform screens = menu.transform.parent;

            EquippedBackground[] pictures = screens.GetComponentsInChildren<EquippedBackground>(true);
            Assert.AreEqual(1, pictures.Length, "one picture for all the menu screens");
            EquippedBackground backdrop = pictures[0];
            Assert.IsNull(backdrop.GetComponentInParent<UIScreen>(true), "part of no screen");
            Assert.AreSame(screens, backdrop.transform.parent, "beside the screens");
            Assert.AreEqual(0, backdrop.transform.GetSiblingIndex(), "behind all of them");
            Assert.IsTrue(backdrop.isActiveAndEnabled);
            Rect before = WorldRect(backdrop);
            Assert.IsTrue(Covers(before, WorldRect((RectTransform)screens)), "it fills the screen");

            GameFixture.Press(router, "ShopButton");
            for (int frame = 0; frame < 6; frame++)
            {
                yield return null;
                Assert.AreEqual(before, WorldRect(backdrop), "it stays where it is while the screens change");
                Assert.AreEqual(1f, Strength(backdrop), "at full strength");
            }

            Assert.IsTrue(router.Back());
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(before, WorldRect(backdrop));
            Assert.AreEqual(1f, Strength(backdrop));
        }

        [UnityTest]
        public IEnumerator TheBackground_ShowsTheThemeThePlayerEquips()
        {
            yield return _game.Boot(seed: saves =>
            {
                var data = new CosmeticsData();
                data.boughtThemeIds.Add(Tihar);
                saves.Save(CosmeticsService.SaveKey, data);
            });
            EquippedBackground backdrop = _game.MenuRouter.Get<UIScreen>(ScreenId.MainMenu).transform.parent.GetComponentInChildren<EquippedBackground>(true);

            _game.Services.Resolve<ICosmeticsShop>().Equip(CosmeticKind.Theme, Tihar);

            Assert.AreSame(backdrop.Library.BackgroundPicture(Tihar, out _), backdrop.Shown);
        }

        /// <summary>How strongly it is drawn: the alpha of every canvas group above it.</summary>
        private static float Strength(Component picture)
        {
            float alpha = 1f;
            foreach (CanvasGroup group in picture.GetComponentsInParent<CanvasGroup>(true))
            {
                alpha *= group.alpha;
                if (group.ignoreParentGroups)
                {
                    break;
                }
            }

            return alpha;
        }

        private static Rect WorldRect(Component picture) => WorldRect((RectTransform)picture.transform);

        private static Rect WorldRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static bool Covers(Rect over, Rect under) =>
            over.xMin <= under.xMin + 0.01f && over.yMin <= under.yMin + 0.01f && over.xMax >= under.xMax - 0.01f && over.yMax >= under.yMax - 0.01f;
    }
}
