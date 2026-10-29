using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The trick fits on the table (the owner, 30 Oct 2026: "while playing the card gets overlap.
    /// either reduce the card size or make move the player card quite below and move the top player
    /// quite upside so that the card fits"; ADR 060). Since the table is seen from straight above
    /// (ADR 059) a trick card is drawn full height. On the table prefab laid out on a 16:9, 20:9, 21:9
    /// and 4:3 canvas, with and without its room for the banner ad (ADR 051, 057), the four cards of a
    /// trick lie apart, none over another; the top one is below the top seat (its name plate, status
    /// pill, avatar and a full fan of 13 backs), the side ones clear the side seats, and the bottom
    /// one is above the turn prompt and the player's full hand. The prefab is the built one, so these
    /// live outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class TrickFitTests
    {
        private const string TablePrefab = "Assets/_Project/Prefabs/UI/Screens/Table.prefab";
        private const string CardLibrary = "Assets/_Project/Config/Cards/CardSprites.asset";
        private const string Deck = "classic-crimson";

        /// <summary>Canvas units of cloth that show between a trick card and what is next to it.</summary>
        private const float Clear = 5f;

        private static readonly string[] Trick = { "J♥", "9♥", "Q♥", "10♥" };

        private GameObject _canvas;
        private TableView _view;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
        }

        [TestCase(1920f, 1080f, false)]
        [TestCase(1920f, 1080f, true)]
        [TestCase(2400f, 1080f, false)]
        [TestCase(2400f, 1080f, true)]
        [TestCase(2520f, 1080f, false)]
        [TestCase(2520f, 1080f, true)]
        [TestCase(1920f, 1440f, false)]
        [TestCase(1920f, 1440f, true)]
        public void AFullTrick_LiesApart_ClearOfTheSeatsThePromptAndTheHand(float width, float height, bool bannerRoom)
        {
            Open(width, height);
            _view.SetBannerRoom(bannerRoom);
            Rect[] trick = Deal();

            for (int a = 0; a < trick.Length; a++)
            {
                for (int b = a + 1; b < trick.Length; b++)
                {
                    Assert.IsFalse(trick[a].Overlaps(trick[b]), $"the trick cards of seats {a} and {b} lie apart");
                }
            }

            // Seats by relative position: 0 bottom (you), 1 right, 2 top, 3 left.
            Rect top = trick[2];
            foreach (Rect piece in new[] { Box(Find("SeatPlate2")), Box(Find("SeatPill2")), Box(Find("SeatRing2")), Fan(2) })
            {
                Assert.GreaterOrEqual(piece.yMin - top.yMax, Clear, "the top trick card is below the top seat, its fan included");
            }

            foreach (int side in new[] { 1, 3 })
            {
                foreach (Rect piece in new[] { Box(Find($"SeatPlate{side}")), Box(Find($"SeatPill{side}")), Box(Find($"SeatRing{side}")), Fan(side) })
                {
                    Assert.IsFalse(trick[side].Overlaps(piece), $"the trick card of seat {side} clears its seat");
                }
            }

            Rect bottom = trick[0];
            Assert.GreaterOrEqual(bottom.yMin - Prompt().yMax, Clear, "the bottom trick card is above the turn prompt");
            Assert.GreaterOrEqual(bottom.yMin - HandTop(), Clear, "and above the hand");

            Rect canvas = Box(_canvas.transform);
            foreach (Rect card in trick)
            {
                Assert.IsTrue(canvas.Contains(card.min) && canvas.Contains(card.max), "on the screen");
            }
        }

        /// <summary>
        /// The table as a trick is won: four cards in the trick, a full fan at each opponent, the
        /// player's full hand at rest and the prompt saying who won. Returns the trick cards' boxes.
        /// </summary>
        private Rect[] Deal()
        {
            var sprites = AssetDatabase.LoadAssetAtPath<CardSpriteProvider>(CardLibrary);
            Assert.IsNotNull(sprites, CardLibrary);
            for (int seat = 1; seat < TableView.SeatCount; seat++)
            {
                _view.Seat(seat).Fan.SetBack(sprites.Back(Deck));
                _view.Seat(seat).Fan.SetCount(Core.Cards.Deck.HandSize);
            }

            var hand = new List<Card>();
            foreach (string card in "A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ 7♣ 3♣ K♦ 5♦ 2♦".Split(' '))
            {
                hand.Add(Card.Parse(card));
            }

            _view.Hand.SetDeck(sprites, Deck);
            _view.Hand.SetCards(hand, false);

            var boxes = new Rect[TrickAreaView.Slots];
            for (int seat = 0; seat < TrickAreaView.Slots; seat++)
            {
                Card card = Card.Parse(Trick[seat]);
                CardView view = _view.Cards.Rent(_view.Trick.Area);
                view.ShowFace(card, sprites.Face(Deck, card), sprites.Base(Deck));
                _view.Trick.Adopt(seat, view);
                _view.Trick.Settle(seat);
            }

            _view.Prompt.ReduceMotion = true;
            _view.Prompt.Show("Tip: you must beat the highest heart if you can.");
            Canvas.ForceUpdateCanvases();
            for (int seat = 0; seat < TrickAreaView.Slots; seat++)
            {
                boxes[seat] = Box(_view.Trick.CardAt(seat).transform);
            }

            return boxes;
        }

        /// <summary>The table prefab under a world-space canvas of the given size, as the capture tool lays it out.</summary>
        private void Open(float width, float height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefab);
            Assert.IsNotNull(prefab, TablePrefab);
            _canvas = new GameObject("TestCanvas", typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(width, height);
            GameObject table = Object.Instantiate(prefab, _canvas.transform, false);
            _view = table.GetComponentInChildren<TableView>(true);
            Assert.IsNotNull(_view, "the table prefab has its view");
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>What a seat's fan of backs covers.</summary>
        private Rect Fan(int seat)
        {
            OpponentHandView fan = _view.Seat(seat).Fan;
            Assert.AreEqual(Core.Cards.Deck.HandSize, fan.Count, "a full fan");
            return Union(fan.Area.GetComponentsInChildren<CardView>(false));
        }

        /// <summary>The highest point of the hand's cards.</summary>
        private float HandTop()
        {
            CardView[] cards = Find("HandLayer").GetComponentsInChildren<CardView>(false);
            Assert.AreEqual(Core.Cards.Deck.HandSize, cards.Length, "a full hand");
            return Union(cards).yMax;
        }

        /// <summary>The turn prompt's pill, at its full size (its pop-in does not run in Edit mode).</summary>
        private Rect Prompt()
        {
            RectTransform pill = Find("Prompt").Find("Pill") as RectTransform;
            Assert.IsNotNull(pill, "the prompt has its pill");
            Assert.IsTrue(pill.gameObject.activeInHierarchy, "the prompt shows");
            pill.localScale = Vector3.one;
            return Box(pill);
        }

        private RectTransform Find(string name)
        {
            foreach (RectTransform t in _canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            Assert.Fail("the table has " + name);
            return null;
        }

        private static Rect Union(IReadOnlyList<CardView> cards)
        {
            Assert.Greater(cards.Count, 0);
            Rect union = Box(cards[0].transform);
            foreach (CardView card in cards)
            {
                Rect box = Box(card.transform);
                union = Rect.MinMaxRect(Mathf.Min(union.xMin, box.xMin), Mathf.Min(union.yMin, box.yMin), Mathf.Max(union.xMax, box.xMax), Mathf.Max(union.yMax, box.yMax));
            }

            return union;
        }

        /// <summary>What a piece covers on the canvas (a turned card: the box round its corners).</summary>
        private static Rect Box(Transform transform)
        {
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            float xMin = corners[0].x, yMin = corners[0].y, xMax = corners[0].x, yMax = corners[0].y;
            foreach (Vector3 corner in corners)
            {
                xMin = Mathf.Min(xMin, corner.x);
                yMin = Mathf.Min(yMin, corner.y);
                xMax = Mathf.Max(xMax, corner.x);
                yMax = Mathf.Max(yMax, corner.y);
            }

            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }
    }
}
