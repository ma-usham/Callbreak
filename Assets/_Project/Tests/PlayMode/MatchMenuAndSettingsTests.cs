using System.Collections;
using System.Collections.Generic;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
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
    /// The pages that open over a game (GDD §11 screens 11 and 13), in the real scenes. The match
    /// menu of an online game says "Online game", inside its pill, and its first button reads "Back
    /// to Game": an online game cannot pause. Offline it says the match is paused and offers "Resume
    /// Game". Settings covers the screen it opens from, the gear that opened it included, and has no
    /// gear of its own, wherever it opens from. Saves go to a temporary folder.
    /// </summary>
    public sealed class MatchMenuAndSettingsTests
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
        public IEnumerator MatchMenu_Online_SaysOnlineGame_AndItsFirstButtonGoesBackToTheGame()
        {
            var online = new ScriptedOnlineTable();
            yield return online.Open(_game);
            ScreenRouter router = _game.TableRouter;
            TableView table = Object.FindFirstObjectByType<TableView>();

            GameFixture.PressIn(table, "HomeButton");
            Assert.AreEqual(ScreenId.MatchMenu, router.Current);
            UIScreen menu = router.Get<UIScreen>(ScreenId.MatchMenu);
            yield return Settled(menu);

            TMP_Text mode = Label(menu, "ModeLabel");
            TMP_Text first = Label(menu, "ResumeLabel");
            Assert.AreEqual(Text(MatchMenuText.ModeOnlineKey), mode.text, "the tag says the game is online");
            Assert.AreEqual(Text(MatchMenuText.BackToGameKey), first.text, "an online game is not paused: the button goes back to it");
            Assert.AreNotEqual(Text(MatchMenuText.ResumeKey), first.text);
            Assert.IsFalse(Named(menu, "PausedNote").gameObject.activeInHierarchy, "nothing says the game is paused");
            AssertInkInside(mode, Named(menu, "ModePill"), "the mode tag");
            AssertInkInside(first, Named(menu, "ResumeButton"), "\"Back to Game\"");

            GameFixture.Press(router, "ResumeButton");
            Assert.AreEqual(ScreenId.Gameplay, router.Current, "the button goes back to the game");
            Assert.AreEqual(SessionStatus.Running, online.Session.Status);
        }

        [UnityTest]
        public IEnumerator MatchMenu_Offline_SaysPaused_AndOffersResumeGame()
        {
            yield return StartOfflineMatch();
            ScreenRouter router = _game.TableRouter;
            TableView table = Object.FindFirstObjectByType<TableView>();

            GameFixture.PressIn(table, "HomeButton");
            Assert.AreEqual(ScreenId.MatchMenu, router.Current);
            UIScreen menu = router.Get<UIScreen>(ScreenId.MatchMenu);
            yield return Settled(menu);

            TMP_Text mode = Label(menu, "ModeLabel");
            TMP_Text first = Label(menu, "ResumeLabel");
            Assert.AreEqual(Text(MatchMenuText.ModeOfflineKey), mode.text);
            Assert.AreEqual(Text(MatchMenuText.ResumeKey), first.text);
            Assert.IsTrue(Named(menu, "PausedNote").gameObject.activeInHierarchy, "\"Your match is paused.\"");
            AssertInkInside(mode, Named(menu, "ModePill"), "the mode tag");
            AssertInkInside(first, Named(menu, "ResumeButton"), "\"Resume Game\"");
        }

        [UnityTest]
        public IEnumerator Settings_OverTheTable_CoversTheGearThatOpenedIt_AndHasNoGearOfItsOwn()
        {
            yield return StartOfflineMatch();
            ScreenRouter router = _game.TableRouter;
            TableView table = Object.FindFirstObjectByType<TableView>();

            // The table's gear stays where it is under the page: the page must hide it.
            yield return OpenSettingsFrom(router, table, "the table");

            // Over the scorecard (the round and match results have the same gear and open the same page).
            GameFixture.PressIn(table, "ScoresButton");
            Assert.AreEqual(ScreenId.Scorecard, router.Current);
            UIScreen scorecard = router.Get<UIScreen>(ScreenId.Scorecard);
            yield return Settled(scorecard);
            yield return OpenSettingsFrom(router, scorecard, "the scorecard");
        }

        [UnityTest]
        public IEnumerator Settings_FromTheMenuScreens_HasNoGearOfItsOwn()
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true)));
            ScreenRouter router = _game.MenuRouter;
            Assert.AreEqual(ScreenId.MainMenu, router.Current);
            yield return Settled(router.Get<UIScreen>(ScreenId.MainMenu));
            yield return OpenSettingsFrom(router, router.Get<UIScreen>(ScreenId.MainMenu), "the main menu");

            yield return Open(router, "PlayOfflineButton", ScreenId.OfflineSetup);
            yield return OpenSettingsFrom(router, router.Get<UIScreen>(ScreenId.OfflineSetup), "Offline setup");
            Assert.IsTrue(router.Back());
            yield return Settled(router.Get<UIScreen>(ScreenId.MainMenu));

            yield return Open(router, "PlayWithFriendsButton", ScreenId.PlayWithFriends);
            yield return OpenSettingsFrom(router, router.Get<UIScreen>(ScreenId.PlayWithFriends), "Play with Friends");
            foreach ((string button, ScreenId screen) in new[] { ("CreateButton", ScreenId.CreateRoom), ("JoinButton", ScreenId.JoinRoom) })
            {
                yield return Open(router, button, screen);
                yield return OpenSettingsFrom(router, router.Get<UIScreen>(screen), screen.ToString());
                Assert.IsTrue(router.Back());
                yield return Settled(router.Get<UIScreen>(ScreenId.PlayWithFriends));
            }
        }

        private static IEnumerator Open(ScreenRouter router, string button, ScreenId screen)
        {
            GameFixture.Press(router, button);
            Assert.AreEqual(screen, router.Current, button + " opens " + screen);
            yield return Settled(router.Get<UIScreen>(screen));
        }

        /// <summary>
        /// Taps the gear on <paramref name="from"/> and checks the page that opens: no gear of its
        /// own, and nothing of <paramref name="from"/>'s gear to see or to touch. Then goes back.
        /// </summary>
        private static IEnumerator OpenSettingsFrom(ScreenRouter router, Component from, string where)
        {
            ScreenId before = router.Current;
            RectTransform gear = ActiveButton(from, "SettingsButton");
            Assert.IsNotNull(gear, where + " has a gear");
            Vector2 onTheGear = CentreOf(gear);

            GameFixture.PressIn(from, "SettingsButton");
            Assert.AreEqual(ScreenId.Settings, router.Current, "the gear on " + where + " opens Settings");
            UIScreen page = router.Get<UIScreen>(ScreenId.Settings);
            yield return Settled(page);

            // A screen of the router is put away under the page; the table is not one, and stays.
            bool routed = from is UIScreen screen && router.Get<UIScreen>(screen.Id) == screen;
            if (routed)
            {
                yield return GameFixture.Until(() => !from.gameObject.activeInHierarchy, where + " to be put away under Settings");
            }

            // Mockup 11 draws the header's gear on Settings too; there it opens nothing, and it reads as
            // the gear of the screen underneath showing through.
            Assert.IsFalse(Named(page, "SettingsButton").gameObject.activeInHierarchy, "Settings (from " + where + ") shows no gear");
            Assert.IsNull(ActiveButton(page, "SettingsButton"));

            GameObject hit = TopHit(onTheGear);
            Assert.IsNotNull(hit, "a touch where the gear was reaches something");
            Assert.IsTrue(hit.transform.IsChildOf(page.transform), "a touch where " + where + "'s gear was lands on Settings, not on " + hit.name);

            if (!routed)
            {
                // The table stays on the screen under the page: the page's own picture has to hide it.
                Assert.IsTrue(gear.gameObject.activeInHierarchy, "the table's gear is still there, under the page");
                Image background = Named(page, "Background").GetComponent<Image>();
                Assert.IsTrue(background != null && background.isActiveAndEnabled, "Settings has a background");
                Assert.GreaterOrEqual(background.color.a * page.CanvasGroup.alpha, 0.999f, "an opaque one");
                Assert.IsTrue(Covers(background.rectTransform, gear), "it lies over " + where + "'s gear");
                Graphic look = gear.GetComponentInChildren<Graphic>();
                Assert.Greater(SortingOrderOf(background), SortingOrderOf(look), "and is drawn after it");
            }

            Assert.IsTrue(router.Back());
            Assert.AreEqual(before, router.Current);
            UIScreen back = router.Get<UIScreen>(before);
            if (back != null)
            {
                yield return Settled(back);
            }
        }

        /// <summary>Waits for the screen's transition to end: fully shown, in place, at its size.</summary>
        private static IEnumerator Settled(UIScreen screen)
        {
            yield return GameFixture.Until(
                () => screen.CanvasGroup.alpha >= 0.999f
                    && Mathf.Abs(screen.SlideTarget.anchoredPosition.x) < 0.01f
                    && Mathf.Abs(screen.SlideTarget.localScale.x - 1f) < 0.0001f,
                screen.Id + " to finish its transition");
        }

        /// <summary>The label's letters lie inside the box, clear of its rounded ends (half its height each side).</summary>
        private static void AssertInkInside(TMP_Text label, RectTransform box, string what)
        {
            label.ForceMeshUpdate();
            Bounds ink = label.textBounds;
            Assert.Greater(ink.size.x, 0f, what + " shows text");
            float left = box.InverseTransformPoint(label.transform.TransformPoint(new Vector3(ink.min.x, ink.center.y, 0f))).x;
            float right = box.InverseTransformPoint(label.transform.TransformPoint(new Vector3(ink.max.x, ink.center.y, 0f))).x;
            Rect rect = box.rect;
            float end = rect.height * 0.5f;
            Assert.GreaterOrEqual(left, rect.xMin + end, what + " (\"" + label.text + "\") starts inside its " + box.name);
            Assert.LessOrEqual(right, rect.xMax - end, what + " (\"" + label.text + "\") ends inside its " + box.name);
        }

        /// <summary>The sorting order the graphic is drawn with: its nearest canvas that sorts on its own.</summary>
        private static int SortingOrderOf(Graphic graphic)
        {
            Canvas canvas = graphic.canvas;
            while (canvas != null && !canvas.isRootCanvas && !canvas.overrideSorting)
            {
                canvas = canvas.transform.parent.GetComponentInParent<Canvas>();
            }

            Assert.IsNotNull(canvas, graphic.name + " is on a canvas");
            return canvas.sortingOrder;
        }

        private static bool Covers(RectTransform over, RectTransform under)
        {
            var a = new Vector3[4];
            var b = new Vector3[4];
            over.GetWorldCorners(a);
            under.GetWorldCorners(b);
            return a[0].x <= b[0].x && a[0].y <= b[0].y && a[2].x >= b[2].x && a[2].y >= b[2].y;
        }

        private static Vector2 CentreOf(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        /// <summary>The object a touch at that screen point would reach, as the event system decides it.</summary>
        private static GameObject TopHit(Vector2 screenPoint)
        {
            var pointer = new PointerEventData(EventSystem.current) { position = screenPoint };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        private static RectTransform ActiveButton(Component root, string name)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(false))
            {
                if (button.name == name)
                {
                    return (RectTransform)button.transform;
                }
            }

            return null;
        }

        private static RectTransform Named(Component root, string name)
        {
            foreach (RectTransform child in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            Assert.Fail(root.name + " has no " + name);
            return null;
        }

        private static TMP_Text Label(Component root, string name) => Named(root, name).GetComponent<TMP_Text>();

        private string Text(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private IEnumerator StartOfflineMatch()
        {
            yield return _game.Boot();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return _game.UntilTable();
            IMatchSession session = _game.Table.Container.Resolve<IMatchSession>();
            yield return GameFixture.Until(() => session.Status == SessionStatus.Running && session.View.Hand.Count == 13, "the first hand to be dealt");
        }
    }
}
