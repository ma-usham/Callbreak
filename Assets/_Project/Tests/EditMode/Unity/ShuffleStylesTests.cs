using System.Collections.Generic;
using Callbreak.Presentation.Table;
using Callbreak.Presentation.Table.Playback;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The shuffles before a deal (the owner, 6 Oct 2026: "add some random shuffle", then "add other 2 kind"; ADR 039): six
    /// styles, picked at random and never twice in a row. Each lasts as long as the riffle did,
    /// sounds once, and leaves the 52 cards squared in the middle of the table in a new order, the
    /// top card drawn last, ready to be dealt; also when it is cut short. They use the table
    /// prefab's card views, so they live outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class ShuffleStylesTests
    {
        private const string TablePrefab = "Assets/_Project/Prefabs/UI/Screens/Table.prefab";
        private const float Frame = 1f / 60f;

        private static readonly ShuffleStyle[] Styles = { ShuffleStyle.Riffle, ShuffleStyle.Overhand, ShuffleStyle.Cut, ShuffleStyle.Wash, ShuffleStyle.Fan, ShuffleStyle.Pinwheel };

        private GameObject _table;
        private TableView _view;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefab);
            Assert.IsNotNull(prefab, TablePrefab);
            _table = Object.Instantiate(prefab);
            _view = _table.GetComponentInChildren<TableView>(true);
            Assert.IsNotNull(_view, "the table prefab has its view");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_table);
        }

        [Test]
        public void TheStyles_AreTheRiffleOverhandCutWashFanAndPinwheel()
        {
            CollectionAssert.AreEqual(Styles, ShuffleStyles.All);
            Assert.IsInstanceOf<ShuffleAnimation>(ShuffleStyles.Create(ShuffleStyle.Riffle, NewDeck(), null, null), "the riffle is the one the game had");
        }

        [TestCaseSource(nameof(Styles))]
        public void EveryStyle_TakesAsLongAsTheRiffle_SoundsOnce_AndEndsSquaredInANewOrder(ShuffleStyle style)
        {
            DeckPile deck = NewDeck();
            List<CardView> before = Cards(deck);
            int sounds = 0;
            IEventAnimation shuffle = ShuffleStyles.Create(style, deck, () => sounds++, new System.Random(7));
            Assert.AreEqual(ShuffleAnimation.Seconds, shuffle.Duration, "every style fills the time the deal waits for");

            shuffle.Begin();
            int frames = 1;
            while (!shuffle.Tick(Frame))
            {
                frames++;
                Assert.Less(frames, 200, style + " ends");
            }

            Assert.AreEqual(ShuffleAnimation.Seconds, frames * Frame, Frame * 1.5f, style + " lasts as long as the riffle");
            Assert.AreEqual(1, sounds, style + " sounds once");
            AssertSquaredInANewOrder(deck, before, style);
        }

        [TestCaseSource(nameof(Styles))]
        public void EveryStyle_CutShort_LeavesTheDeckSquaredInItsNewOrder(ShuffleStyle style)
        {
            DeckPile deck = NewDeck();
            List<CardView> before = Cards(deck);
            IEventAnimation shuffle = ShuffleStyles.Create(style, deck, null, new System.Random(3));

            shuffle.Begin();
            for (int i = 0; i < 30; i++)
            {
                shuffle.Tick(Frame);
            }

            shuffle.Finish();
            shuffle.Finish();
            AssertSquaredInANewOrder(deck, before, style);
        }

        [TestCaseSource(nameof(Styles))]
        public void EveryStyle_FinishedBeforeItBegins_StillSquaresTheDeck(ShuffleStyle style)
        {
            DeckPile deck = NewDeck();
            List<CardView> before = Cards(deck);

            ShuffleStyles.Create(style, deck, null, new System.Random(5)).Finish();

            AssertSquaredInANewOrder(deck, before, style);
        }

        [Test]
        public void TheNextStyle_IsNeverTheOneBefore_AndInTimeEveryStyleShows()
        {
            var random = new System.Random(11);
            var seen = new HashSet<ShuffleStyle>();
            ShuffleStyle? last = null;
            for (int deal = 0; deal < 400; deal++)
            {
                ShuffleStyle next = ShuffleStyles.Next(last, random);
                Assert.AreNotEqual(last, next, "deal " + deal + " shuffles another way than the deal before");
                seen.Add(next);
                last = next;
            }

            CollectionAssert.AreEquivalent(Styles, seen);
        }

        [Test]
        public void TheFirstStyle_CanBeAnyOfThem()
        {
            var random = new System.Random(2);
            var seen = new HashSet<ShuffleStyle>();
            for (int match = 0; match < 200; match++)
            {
                seen.Add(ShuffleStyles.Next(null, random));
            }

            CollectionAssert.AreEquivalent(Styles, seen);
        }

        private DeckPile NewDeck()
        {
            return new DeckPile(_view.Cards, _view.Trick.Area, null, 52, 0.7f);
        }

        private static List<CardView> Cards(DeckPile deck)
        {
            var cards = new List<CardView>(deck.Count);
            for (int i = 0; i < deck.Count; i++)
            {
                cards.Add(deck[i]);
            }

            return cards;
        }

        private static void AssertSquaredInANewOrder(DeckPile deck, List<CardView> before, ShuffleStyle style)
        {
            List<CardView> after = Cards(deck);
            Assert.AreEqual(52, after.Count, style + ": every card is still in the deck");
            CollectionAssert.AreEquivalent(before, after, style + ": the same 52 cards");
            CollectionAssert.AreNotEqual(before, after, style + ": in a new order");
            int sibling = -1;
            for (int i = 0; i < deck.Count; i++)
            {
                CardPlacement rest = deck.Resting(i);
                CardPlacement placement = deck[i].Placement;
                Assert.AreEqual(rest.Position.x, placement.Position.x, 0.01f, style + ": card " + i + " squared (x)");
                Assert.AreEqual(rest.Position.y, placement.Position.y, 0.01f, style + ": card " + i + " squared (y)");
                Assert.AreEqual(rest.Rotation, placement.Rotation, 0.01f, style + ": card " + i + " squared (turn)");
                int index = deck[i].Rect.GetSiblingIndex();
                Assert.Greater(index, sibling, style + ": card " + i + " draws over the one below it");
                sibling = index;
            }
        }
    }
}
