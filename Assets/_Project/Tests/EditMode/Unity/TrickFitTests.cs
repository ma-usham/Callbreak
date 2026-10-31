using System.Collections.Generic;
using System.IO;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The table as other Callbreak games lay it (the owner, 30 Oct 2026: "in my the thrown cards get
    /// small", "here is how other games card are shown same size evven after throwing"; ADR 062, in
    /// place of ADR 059's and 060's trick). On the table prefab laid out on a 16:9, 20:9, 21:9 and 4:3
    /// canvas, with and without its room for the banner ad (ADR 051, 057): a trick card is the hand's
    /// card, the four meet in a cross in the middle, the side ones lying sideways towards their
    /// players; the top one is below the top seat and its full fan, the side ones clear the side seats
    /// and their fans, the bottom one is above the hand with a card lifted, and none is under the turn
    /// prompt. The hand keeps its gentle arc (ADR 021), centred under the trick and clear of the local
    /// seat and the emote button. Its cards are a fifth of the screen's height, their corner index still
    /// readable (the owner, 30 Oct 2026: "way too big"). The prefab is the built one, so these live
    /// outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class TrickFitTests
    {
        private const string TablePrefab = "Assets/_Project/Prefabs/UI/Screens/Table.prefab";
        private const string CardLibrary = "Assets/_Project/Config/Cards/CardSprites.asset";
        private const string Deck = "classic-crimson";
        private const string FacesFolder = "Assets/_Project/Art/Cards/Faces";

        /// <summary>A face's corner index lies in these columns of its texture, left of a court card's frame.</summary>
        private const int IndexLeft = 12;
        private const int IndexRight = 64;

        /// <summary>Canvas units the smallest rank (the 10's) stood at 28% of the screen's height, less a little.</summary>
        private const float MinRank = 18f;

        /// <summary>Canvas units of cloth that show between a trick card and what is next to it.</summary>
        private const float Clear = 5f;

        /// <summary>The widest prompt in English: a name as long as a profile takes, of the widest letter, wins the trick.</summary>
        private const string LongestPrompt = "WWWWWWWWWWWWWWWW wins the trick";

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
        public void AFullTrick_OfTheHandsCards_MeetsInTheMiddle_ClearOfTheSeatsThePromptAndTheHand(float width, float height, bool bannerRoom)
        {
            Open(width, height);
            _view.SetBannerRoom(bannerRoom);
            Rect[] trick = Deal();

            // The hand's card, upright at the top and the bottom, lying sideways at the sides with
            // its top towards the player who played it (seats: 0 bottom, 1 right, 2 top, 3 left).
            Vector2[] towards = { Vector2.up, Vector2.right, Vector2.up, Vector2.left };
            Vector2 handCard = SizeOf(Find("HandLayer").GetComponentInChildren<CardView>(false));
            for (int seat = 0; seat < TrickAreaView.Slots; seat++)
            {
                CardView card = _view.Trick.CardAt(seat);
                Assert.AreEqual(_view.Hand.CardScale, card.Placement.Scale, 1e-4f, $"seat {seat}'s card keeps the hand's size");
                Assert.AreEqual(handCard.x, SizeOf(card).x, 0.5f, "on the screen too");
                Assert.AreEqual(handCard.y, SizeOf(card).y, 0.5f);
                Assert.AreEqual(1f, Vector2.Dot(card.transform.up, towards[seat]), 1e-3f, $"seat {seat}'s card points at its player");
            }

            // A cross: every card reaches the middle.
            Vector2 middle = Box(_view.Trick.Area).center;
            foreach (Rect card in trick)
            {
                Assert.IsTrue(Rect.MinMaxRect(card.xMin - 0.5f, card.yMin - 0.5f, card.xMax + 0.5f, card.yMax + 0.5f).Contains(middle), "the cards meet in the middle");
            }

            Rect top = trick[2];
            foreach (Rect piece in new[] { Box(Find("SeatPlate2")), Box(Find("SeatPill2")), Box(Find("SeatRing2")), Box(Find("SeatTimerRing2")), Fan(2) })
            {
                Assert.GreaterOrEqual(piece.yMin - top.yMax, Clear, "the top trick card is below the top seat, its fan included");
            }

            foreach (int side in new[] { 1, 3 })
            {
                foreach (Rect piece in new[] { Box(Find($"SeatPlate{side}")), Box(Find($"SeatPill{side}")), Box(Find($"SeatRing{side}")), Box(Find($"SeatTimerRing{side}")), Fan(side) })
                {
                    for (int seat = 0; seat < TrickAreaView.Slots; seat++)
                    {
                        Assert.IsFalse(trick[seat].Overlaps(piece), $"the trick card of seat {seat} clears seat {side} and its fan");
                    }
                }
            }

            Rect prompt = Prompt();
            for (int seat = 0; seat < TrickAreaView.Slots; seat++)
            {
                Assert.IsFalse(trick[seat].Overlaps(prompt), $"the trick card of seat {seat} is clear of the turn prompt");
            }

            Rect bottom = trick[0];
            Assert.GreaterOrEqual(bottom.yMin - HandTop(), Clear, "the bottom trick card is above the hand, a card lifted");
            Assert.GreaterOrEqual(prompt.yMin - HandTop(), Clear, "and so is the prompt");

            Rect canvas = Box(_canvas.transform);
            foreach (Rect piece in new[] { trick[0], trick[1], trick[2], trick[3], prompt })
            {
                Assert.IsTrue(canvas.Contains(piece.min) && canvas.Contains(piece.max), "on the screen");
            }

            foreach (Rect piece in new[] { Box(Find("SeatPill1")), Box(Find("SeatPlate1")), Fan(1), Box(Find("EmoteButton")) })
            {
                Assert.IsFalse(prompt.Overlaps(piece), "the prompt clears the right seat, its fan and the emote button");
            }
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2400f, 1080f)]
        [TestCase(2520f, 1080f)]
        [TestCase(1920f, 1440f)]
        public void TheHand_IsAGentleArcUnderTheTrick_ClearOfTheLocalSeatAndTheEmoteButton(float width, float height)
        {
            Open(width, height);
            Deal();

            // Left to right: the end cards lean outwards, 5° on a 16:9 screen (ADR 021) and a little
            // more where the hand spreads wider; the middle one stands upright.
            var cards = new List<CardView>(HandCards());
            cards.Sort((a, b) => a.Placement.Position.x.CompareTo(b.Placement.Position.x));
            Assert.That(cards[0].Placement.Rotation, Is.InRange(4.5f, 7f), "the first card leans left, gently");
            Assert.AreEqual(-cards[0].Placement.Rotation, cards[12].Placement.Rotation, 0.01f, "the last card leans right as much");
            Assert.AreEqual(0f, cards[6].Placement.Rotation, 1e-3f, "the middle card stands upright");

            Rect row = Union(cards);
            Rect canvas = Box(_canvas.transform);
            Assert.GreaterOrEqual(row.xMin, canvas.xMin, "on the screen");
            Assert.LessOrEqual(row.xMax, canvas.xMax);
            Assert.AreEqual(Box(_view.Trick.Area).center.x, Box(cards[6].transform).center.x, 1f, "under the trick");
            foreach (string piece in new[] { "SeatPlate0", "SeatPill0", "SeatRing0", "SeatTimerRing0", "EmoteButton" })
            {
                Rect box = Box(Find(piece));
                foreach (CardView card in cards)
                {
                    Assert.IsFalse(Overlaps(card, box), "the hand clears " + piece);
                }
            }
        }

        [Test]
        public void TheTablesCards_AreAFifthOfTheScreen_TheirCornerIndexStillReads()
        {
            Open(2400f, 1080f);
            Deal();
            Vector2 card = SizeOf(Find("HandLayer").GetComponentInChildren<CardView>(false));
            Assert.That(card.y / Box(_canvas.transform).height, Is.InRange(0.19f, 0.22f), "a card is a fifth of the screen's height");

            // So the corner index grew in the art (ADR 062's addendum): every face's rank, the first ink
            // from the top of its corner, stands MinRank canvas units or more at the table's size.
            foreach (string path in Directory.GetFiles(FacesFolder, "Card_*.png"))
            {
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)), path);
                    Assert.GreaterOrEqual(RankHeight(texture) * card.y / texture.height, MinRank, path + "'s rank reads at the table");
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
        }

        /// <summary>Rows of the first ink from the top of a face's corner index: its rank.</summary>
        private static int RankHeight(Texture2D face)
        {
            Color32[] pixels = face.GetPixels32();
            int first = -1;
            for (int y = 0; y < face.height / 2; y++)
            {
                int row = (face.height - 1 - y) * face.width;
                bool ink = false;
                for (int x = IndexLeft; x < IndexRight && !ink; x++)
                {
                    ink = pixels[row + x].a > 128;
                }

                if (ink && first < 0)
                {
                    first = y;
                }
                else if (!ink && first >= 0)
                {
                    return y - first;
                }
            }

            return 0;
        }

        /// <summary>
        /// The table as a trick is won: four cards in the trick, a full fan at each opponent, the
        /// player's full hand with A♥ lifted and the longest prompt. Returns the trick cards' boxes.
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
            _view.Hand.SetHighlights(CardSet.Of(Card.Parse("A♥")), CardSet.Empty, false);

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
            _view.Prompt.Show(LongestPrompt);
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

        /// <summary>Whether a card, turned as it is, covers any of a box (its own corners, not the box round them).</summary>
        private static bool Overlaps(CardView card, Rect box)
        {
            // Separating axes: the box's two and the card's two.
            var corners = new Vector3[4];
            card.Rect.GetWorldCorners(corners);
            var boxCorners = new Vector2[] { box.min, new Vector2(box.xMin, box.yMax), box.max, new Vector2(box.xMax, box.yMin) };
            var axes = new Vector2[] { Vector2.right, Vector2.up, corners[1] - corners[0], corners[3] - corners[0] };
            foreach (Vector2 axis in axes)
            {
                float cardMin = float.MaxValue, cardMax = float.MinValue, boxMin = float.MaxValue, boxMax = float.MinValue;
                foreach (Vector3 corner in corners)
                {
                    float d = Vector2.Dot(corner, axis);
                    cardMin = Mathf.Min(cardMin, d);
                    cardMax = Mathf.Max(cardMax, d);
                }

                foreach (Vector2 corner in boxCorners)
                {
                    float d = Vector2.Dot(corner, axis);
                    boxMin = Mathf.Min(boxMin, d);
                    boxMax = Mathf.Max(boxMax, d);
                }

                if (cardMax <= boxMin || boxMax <= cardMin)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>A card's own width and height on the canvas, however it is turned.</summary>
        private static Vector2 SizeOf(CardView card)
        {
            var corners = new Vector3[4];
            card.Rect.GetWorldCorners(corners);
            return new Vector2(Vector3.Distance(corners[0], corners[3]), Vector3.Distance(corners[0], corners[1]));
        }

        private CardView[] HandCards()
        {
            CardView[] cards = Find("HandLayer").GetComponentsInChildren<CardView>(false);
            Assert.AreEqual(Core.Cards.Deck.HandSize, cards.Length, "a full hand");
            return cards;
        }

        /// <summary>The highest point of the hand's cards, the lifted one included.</summary>
        private float HandTop()
        {
            return Union(HandCards()).yMax;
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
