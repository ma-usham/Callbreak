using System;
using System.Collections;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The real Table scene with an offline match against the real bots, driven through the real
    /// views: the call panel's chips, taps on the hand, the HUD buttons and the match menu
    /// (GDD §11 screens 07, 08, 13 and 16). Saves go to a temporary folder.
    /// </summary>
    public sealed class TableFlowTests
    {
        private const float TurnTimeout = 40f;

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
        public IEnumerator OfflineMatch_CallWithTheChipsThenPlayATrickByTappingACard()
        {
            yield return StartOfflineMatch();
            IMatchSession session = Session();

            // The table shows a turn only after the deal and the others' calls have played out,
            // which is later than the session knows it: wait for what the player sees.
            BiddingPanelView panel = UnityEngine.Object.FindFirstObjectByType<BiddingPanelView>(FindObjectsInactive.Include);
            yield return GameFixture.Until(() => GameFixture.CanPress(panel, "Chip2"), "the call panel to open", TurnTimeout);
            Assert.IsFalse(GameFixture.CanPress(panel, "ConfirmButton"), "Confirm waits for a chip");
            GameFixture.PressIn(panel, "Chip2");
            GameFixture.PressIn(panel, "ConfirmButton");

            PlayedTable shown = _game.Table.Container.Resolve<PlayedTable>();
            yield return GameFixture.Until(() => shown.State.TryGetObligation(out _), "the table to show my first turn to play", TurnTimeout);
            Card card = session.View.LegalCards().Lowest();
            Tap(UnityEngine.Object.FindFirstObjectByType<HandView>(), card);

            yield return GameFixture.Until(() => session.View.CompletedTricks.Count >= 1, "the first trick to finish", TurnTimeout);
            Assert.IsFalse(session.View.Hand.Contains(card), "the tapped card left the hand");
            Assert.AreEqual(12, session.View.Hand.Count);
            Assert.AreEqual(ScreenId.Gameplay, _game.TableRouter.Current, "nothing covers the table during play");
        }

        [UnityTest]
        public IEnumerator MatchMenu_PausesTheMatch_AndLeavingKeepsItForResume()
        {
            yield return StartOfflineMatch();
            IMatchSession session = Session();
            ScreenRouter router = _game.TableRouter;
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();

            GameFixture.PressIn(table, "HomeButton");
            Assert.AreEqual(ScreenId.MatchMenu, router.Current);
            Assert.IsTrue(session.IsPaused, "GDD 11 screen 13: an offline match pauses under the menu");
            GameFixture.Press(router, "ResumeButton");
            Assert.AreEqual(ScreenId.Gameplay, router.Current);
            Assert.IsFalse(session.IsPaused);

            GameFixture.PressIn(table, "HomeButton");
            GameFixture.Press(router, "LeaveButton");
            yield return null;
            _game.PressDialog(confirm: true);
            yield return _game.UntilMenu();
            Assert.IsTrue(_game.Services.Resolve<IOfflineMatchStore>().HasSave, "leaving keeps the match for later");

            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            yield return null;
            Assert.IsTrue(_game.Dialogs.IsOpen, "\"Resume your match?\"");
            _game.PressDialog(confirm: true);
            yield return _game.UntilTable();
            IMatchSession resumed = Session();
            yield return GameFixture.Until(() => resumed.Status == SessionStatus.Running, "the resumed match to run");
            Assert.AreEqual(13, resumed.View.Hand.Count, "the same hand is back");
            Assert.AreEqual(1, resumed.View.Round);
        }

        [UnityTest]
        public IEnumerator Overlays_ScorecardSettingsAndRulesOpenOverTheTableAndCloseAgain()
        {
            yield return StartOfflineMatch();
            IMatchSession session = Session();
            ScreenRouter router = _game.TableRouter;
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();

            GameFixture.PressIn(table, "ScoresButton");
            Assert.AreEqual(ScreenId.Scorecard, router.Current);
            Assert.IsTrue(session.IsPaused, "an offline match waits while a screen covers the table");
            Assert.IsTrue(router.Back());
            Assert.AreEqual(ScreenId.Gameplay, router.Current);
            Assert.IsFalse(session.IsPaused);

            GameFixture.PressIn(table, "SettingsButton");
            Assert.AreEqual(ScreenId.Settings, router.Current);
            GameFixture.Press(router, "HowToPlayButton");
            Assert.AreEqual(ScreenId.HowToPlay, router.Current);
            Assert.IsTrue(router.Back());
            Assert.AreEqual(ScreenId.Settings, router.Current);
            Assert.IsTrue(router.Back());
            yield return null;

            Assert.AreEqual(ScreenId.Gameplay, router.Current);
            Assert.AreEqual(1, router.Depth);
            Assert.IsFalse(session.IsPaused);
        }

        private IEnumerator StartOfflineMatch()
        {
            yield return _game.Boot();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return _game.UntilTable();
            IMatchSession session = Session();
            yield return GameFixture.Until(() => session.Status == SessionStatus.Running && session.View.Hand.Count == 13, "the first hand to be dealt");
        }

        private IMatchSession Session() => _game.Table.Container.Resolve<IMatchSession>();

        /// <summary>A tap on the visible top of a card in the hand, as the pointer events deliver it.</summary>
        private static void Tap(HandView hand, Card card)
        {
            CardView view = hand.ViewOf(card);
            Assert.IsNotNull(view, "the hand shows " + card);
            var corners = new Vector3[4];
            ((RectTransform)view.transform).GetWorldCorners(corners);
            Vector2 position = (corners[1] + corners[2]) * 0.5f;
            position.y -= 30f;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = position,
                pressPosition = position,
                button = PointerEventData.InputButton.Left,
            };
            hand.OnPointerDown(pointer);
            hand.OnPointerUp(pointer);
        }
    }
}
