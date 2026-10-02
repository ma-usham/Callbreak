using System.Collections;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The seats' status pills in the real Table scene, offline (the owner, 3 Oct 2026: the pill drawn
    /// for "Bid 3 · Won 1" was too big for "3-7"). Each pill hugs its words: while the player chooses
    /// a call theirs reads "Choosing bid" and is wider than the "won-bid" pill it becomes once the
    /// call is placed, and every one-digit "won-bid", "0-3" as "3-7", fills the same short pill. Every
    /// pill holds its words clear of its round ends, its art spans it, it keeps its height and it
    /// stays centred under its name plate. Saves go to a temporary folder.
    /// </summary>
    public sealed class SeatPillTests
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
        public IEnumerator StatusPills_HugTheirWords_ChoosingBidIsWider_AndEveryOneDigitWonBidIsOneShortPill()
        {
            yield return StartOfflineMatch();
            IMatchSession session = _game.Table.Container.Resolve<IMatchSession>();
            ILocalization text = _game.Services.Resolve<ILocalization>();
            TableView table = Object.FindFirstObjectByType<TableView>();
            BiddingPanelView panel = Object.FindFirstObjectByType<BiddingPanelView>(FindObjectsInactive.Include);
            yield return GameFixture.Until(() => GameFixture.CanPress(panel, "Chip2"), "the call panel to open", TurnTimeout);

            Assert.AreEqual(text.Get(SeatStatus.ChoosingBidKey), Words(table, 0).text, "the player is choosing a call");
            for (int rel = 0; rel < TableView.SeatCount; rel++)
            {
                AssertHugs(table, rel);
            }

            Rect choosing = Pill(table, 0).rect;
            GameFixture.PressIn(panel, "Chip2");
            GameFixture.PressIn(panel, "ConfirmButton");
            yield return GameFixture.Until(() => Words(table, 0).text != text.Get(SeatStatus.ChoosingBidKey), "the player's call to show on their pill", TurnTimeout);

            int call = session.View.CallOf(session.LocalSeat);
            Assert.AreEqual(text.Format(SeatStatus.WonBidKey, 0, call), Words(table, 0).text, "the pill reads \"won-bid\"");
            Rect placed = Pill(table, 0).rect;
            Assert.Less(placed.width, choosing.width, "\"" + Words(table, 0).text + "\" sits in a shorter pill than \"Choosing bid\"");
            Assert.AreEqual(choosing.height, placed.height, 0.01f, "the pill keeps its height");
            AssertHugs(table, 0);

            // Every one-digit "won-bid", shown on the player's pill as the table shows it.
            RuleSet rules = session.Rules;
            SeatView you = table.Seat(0);
            for (int won = 0; won <= 9; won++)
            {
                for (int called = rules.MinCall; called <= Mathf.Min(9, rules.MaxCall); called++)
                {
                    you.SetStatus(text.Format(SeatStatus.WonBidKey, won, called), won >= called);
                    Assert.AreEqual(placed.width, Pill(table, 0).rect.width, 0.01f, "\"" + Words(table, 0).text + "\" fills the same pill as the call placed");
                    AssertHugs(table, 0);
                }
            }
        }

        /// <summary>
        /// Seat <paramref name="rel"/>'s pill hugs its words: they lie inside it clear of its round
        /// ends, with no more than its height of room at each side; its art spans it; and it is
        /// centred under the seat's name plate.
        /// </summary>
        private static void AssertHugs(TableView table, int rel)
        {
            RectTransform pill = Pill(table, rel);
            TMP_Text words = Words(table, rel);
            string what = "seat " + rel + "'s pill (\"" + words.text + "\")";
            words.ForceMeshUpdate();
            Bounds ink = words.textBounds;
            Rect rect = pill.rect;
            float left = pill.InverseTransformPoint(words.transform.TransformPoint(new Vector3(ink.min.x, ink.center.y, 0f))).x;
            float right = pill.InverseTransformPoint(words.transform.TransformPoint(new Vector3(ink.max.x, ink.center.y, 0f))).x;
            Assert.Greater(right - left, 0f, what + " shows words");
            Assert.GreaterOrEqual(left, rect.xMin + rect.height / 2f, what + ": the words start clear of its round end");
            Assert.LessOrEqual(right, rect.xMax - rect.height / 2f, what + ": the words end clear of its round end");
            Assert.LessOrEqual(rect.width, right - left + 2f * rect.height, what + " hugs its words");

            var face = (RectTransform)pill.Find("Face");
            Assert.IsNotNull(face, what + " has its art");
            Assert.AreEqual(rect.width, face.rect.width, 0.5f, what + ": its art spans it");

            RectTransform plate = Named(table, "SeatPlate" + rel);
            float off = plate.InverseTransformPoint(pill.TransformPoint(rect.center)).x - plate.rect.center.x;
            Assert.AreEqual(0f, off, 1f, what + " is centred under the name plate");
        }

        private static RectTransform Pill(TableView table, int rel) => Named(table, "SeatPill" + rel);

        private static TMP_Text Words(TableView table, int rel) => Named(table, "SeatStatus" + rel).GetComponent<TMP_Text>();

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

        private IEnumerator StartOfflineMatch()
        {
            yield return _game.Boot();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return _game.UntilTable();
            IMatchSession session = _game.Table.Container.Resolve<IMatchSession>();
            yield return GameFixture.Until(() => session.Status == SessionStatus.Running && session.View.Hand.Count == Deck.HandSize, "the first hand to be dealt");
        }
    }
}
