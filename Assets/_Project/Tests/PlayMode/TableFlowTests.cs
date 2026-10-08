using System;
using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.UI;
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
        public IEnumerator OfflineMatch_CallWithTheChipsThenPlayATrickByThrowingACard()
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

            // A touch on the hand before it takes input only looks at a card: wait until the turn's
            // own playback has ended and the hand is live.
            PlayedTable shown = _game.Table.Container.Resolve<PlayedTable>();
            HandView hand = UnityEngine.Object.FindFirstObjectByType<HandView>();
            yield return GameFixture.Until(() => shown.State.TryGetObligation(out _) && hand.Interactable, "the table to show my first turn to play", TurnTimeout);
            Card card = session.View.LegalCards().Lowest();

            // ADR 022: a tap plays nothing; the card is thrown.
            Swipe(hand, card, 0f);
            yield return null;
            Assert.IsTrue(session.View.Hand.Contains(card) && hand.ViewOf(card) != null, "a tapped card stays in the hand");
            Swipe(hand, card, 260f);

            yield return GameFixture.Until(() => session.View.CompletedTricks.Count >= 1, "the first trick to finish", TurnTimeout);
            Assert.IsFalse(session.View.Hand.Contains(card), "the thrown card left the hand");
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
            GameFixture.Press(router, "CloseButton");
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

        [UnityTest]
        public IEnumerator Overlay_OverTheTable_TakesEveryTouch_NothingUnderItCanBeTapped()
        {
            yield return StartOfflineMatch();
            ScreenRouter router = _game.TableRouter;
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();
            HandView hand = UnityEngine.Object.FindFirstObjectByType<HandView>();
            yield return GameFixture.Until(() => hand.Count == 13, "the hand to be laid out", TurnTimeout);
            Vector2 onTheHand = CentreOf((RectTransform)hand.transform);
            GameObject before = TopHit(onTheHand);
            Assert.IsNotNull(before, "the hand takes touches");
            Assert.IsTrue(before.transform.IsChildOf(table.transform), "with nothing over it, a touch there reaches the table (" + before.name + ")");

            // Online a match runs on under these pages: a touch between their buttons must not
            // play a card or press anything on the table (GDD 11 screens 09-11, 16).
            foreach ((string button, ScreenId screen) in new[] { ("ScoresButton", ScreenId.Scorecard), ("SettingsButton", ScreenId.Settings) })
            {
                GameFixture.PressIn(table, button);
                yield return null;
                Assert.AreEqual(screen, router.Current);

                GameObject hit = TopHit(onTheHand);
                Assert.IsNotNull(hit, "the " + screen + " page takes the touch");
                Assert.IsTrue(
                    hit.transform.IsChildOf(router.Get<UIScreen>(screen).transform),
                    "a touch over the hand lands on the " + screen + " page, not on " + hit.name + " under it");

                Assert.IsTrue(router.Back());
                yield return null;
                Assert.AreEqual(ScreenId.Gameplay, router.Current);
            }
        }

        [UnityTest]
        public IEnumerator TapsInTheMiddleAndOnTheAvatars_ShowNoEarlierTrick_AndChangeNothing()
        {
            yield return StartOfflineMatch();
            IMatchSession session = Session();
            BiddingPanelView panel = UnityEngine.Object.FindFirstObjectByType<BiddingPanelView>(FindObjectsInactive.Include);
            yield return GameFixture.Until(() => GameFixture.CanPress(panel, "Chip2"), "the call panel to open", TurnTimeout);
            GameFixture.PressIn(panel, "Chip2");
            GameFixture.PressIn(panel, "ConfirmButton");

            // A quiet table with a finished trick behind it: my turn after the first trick, which
            // the bots wait for.
            PlayedTable shown = _game.Table.Container.Resolve<PlayedTable>();
            HandView hand = UnityEngine.Object.FindFirstObjectByType<HandView>();
            yield return GameFixture.Until(() => shown.State.IsLocalPlayTurn && hand.Interactable, "the table to show my first turn to play", TurnTimeout);
            Swipe(hand, session.View.LegalCards().Lowest(), 260f);
            yield return GameFixture.Until(
                () => shown.State.TricksCompleted >= 1 && shown.State.IsLocalPlayTurn && hand.Interactable, "my turn after the first trick", TurnTimeout);

            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();
            CardViewPool pool = table.Cards;
            int rented = pool.Created - pool.Free;
            int lying = table.Trick.Count;

            // The owner, 5 Oct 2026 (ADR 038): a tap in the middle of the table shows no earlier trick.
            GameObject middle = TapAt(CentreOf(table.Trick.Area));
            Assert.IsTrue(middle.transform.IsChildOf(table.Trick.Area), "a tap in the middle reaches the trick area's catcher, not " + middle.name);
            yield return null;
            Assert.AreEqual(rented, pool.Created - pool.Free, "a tap in the middle of the table shows no earlier trick");
            Assert.AreEqual(lying, table.Trick.Count, "a tap in the middle of the table leaves the trick as it lies");

            // A tap on an opponent's avatar reaches that seat, never the trick area, and changes
            // nothing: nobody can be muted (the owner, the same day), so nothing dims and no card shows.
            for (int rel = 1; rel < TableView.SeatCount; rel++)
            {
                SeatView seat = table.Seat(rel);
                Image[] images = seat.Anchor.GetComponentsInChildren<Image>();
                var alphas = new float[images.Length];
                for (int i = 0; i < images.Length; i++)
                {
                    alphas[i] = images[i].color.a;
                }

                GameObject reached = TapAt(CentreOf(seat.Anchor));
                Assert.IsTrue(reached.transform.IsChildOf(seat.transform), "a tap on seat " + rel + "'s avatar reaches that seat, not " + reached.name);
                yield return null;
                for (int i = 0; i < images.Length; i++)
                {
                    Assert.AreEqual(alphas[i], images[i].color.a, "a tap on seat " + rel + "'s avatar dims nothing (" + images[i].name + ")");
                }
            }

            Assert.AreEqual(rented, pool.Created - pool.Free, "a tap on an avatar shows no card");
            Assert.AreEqual(lying, table.Trick.Count, "a tap on an avatar leaves the trick as it lies");
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

        /// <summary>
        /// A tap at that screen point, as a finger's: the event system picks what the touch reaches,
        /// and the first object from there up that takes a click is clicked. Returns that object.
        /// </summary>
        private static GameObject TapAt(Vector2 screenPoint)
        {
            GameObject hit = TopHit(screenPoint);
            Assert.IsNotNull(hit, "something takes a touch at " + screenPoint);
            GameObject clicked = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            Assert.IsNotNull(clicked, "a touch on " + hit.name + " reaches something that takes a click");
            var pointer = new PointerEventData(EventSystem.current) { position = screenPoint, button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(clicked, pointer, ExecuteEvents.pointerClickHandler);
            return clicked;
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

        /// <summary>
        /// A touch on the visible top of a card in the hand, pulled up <paramref name="pull"/> canvas
        /// units before it lets go, as the pointer events deliver it: 0 is a tap.
        /// </summary>
        private static void Swipe(HandView hand, Card card, float pull)
        {
            CardView view = hand.ViewOf(card);
            Assert.IsNotNull(view, "the hand shows " + card);
            var corners = new Vector3[4];
            ((RectTransform)view.transform).GetWorldCorners(corners);
            Vector2 position = (corners[1] + corners[2]) * 0.5f;
            position.y -= 30f;
            Vector2 end = position + new Vector2(0f, pull * view.transform.lossyScale.y);
            hand.OnPointerDown(Pointer(position, position));
            if (pull > 0f)
            {
                hand.OnDrag(Pointer(Vector2.Lerp(position, end, 0.25f), position));
                hand.OnDrag(Pointer(end, position));
            }

            hand.OnPointerUp(Pointer(end, position));
        }

        private static PointerEventData Pointer(Vector2 position, Vector2 pressPosition)
        {
            return new PointerEventData(EventSystem.current)
            {
                position = position,
                pressPosition = pressPosition,
                button = PointerEventData.InputButton.Left,
            };
        }
    }
}
