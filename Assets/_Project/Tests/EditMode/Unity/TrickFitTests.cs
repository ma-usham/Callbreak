using System.Collections.Generic;
using System.IO;
using Callbreak.Application.Economy;
using Callbreak.Application.Cosmetics;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The table as the owner's mock-ups lay it (2 Nov 2026: "when throwing a card make it a bit small
    /// so that it feels like card is being thrown like in real", "move our cards a bit up and remove
    /// the tips/suggestion box"; ADR 070, in place of ADR 062's full-size cross). On the table prefab
    /// laid out on a 16:9, 20:9, 21:9 and 4:3 canvas, with and without its room for the banner ad
    /// (ADR 051, 057, 087): a trick card is four fifths of the hand's card; the four lie apart in a
    /// "+" round the middle, the top and the bottom one upright, the side ones lying sideways towards
    /// their players (ADR 075); the top one is below the top seat and its full fan, the side ones clear the
    /// side seats and their fans, and the bottom one is above the hand with a card lifted. There is
    /// no turn prompt. The hand keeps its gentle arc (ADR 021), centred under the trick, its whole
    /// middle card on the screen, clear of the local seat and the emote button. Its cards are a fifth
    /// of the screen's height, their corner index still readable (the owner, 30 Oct 2026: "way too
    /// big"), in every deck of the shop (Figma's 512 × 712 decks since ADR 068). The prefab is the
    /// built one, so these live outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class TrickFitTests
    {
        private const string TablePrefab = "Assets/_Project/Prefabs/UI/Screens/Table.prefab";
        private const string CardLibrary = "Assets/_Project/Config/Cards/CardSprites.asset";
        private const string Deck = CosmeticCatalog.Decks.Default;

        /// <summary>The see-through texels round every card picture (ADR 073), outside the card.</summary>
        private const int Frame = 2;

        /// <summary>
        /// A face's corner index lies in these columns of its texture (the card 278 px wide inside its
        /// see-through frame), inside the frame lines and left of a court card's frame, below its top
        /// frame lines.
        /// </summary>
        private const int IndexLeft = Frame + 8;
        private const int IndexRight = Frame + 36;
        private const int IndexTop = Frame + 8;

        /// <summary>Ink is this much darker than the paper above the index (luminance, 0-255).</summary>
        private const float InkContrast = 80f;

        /// <summary>
        /// Canvas units the smallest rank stands at the table, less a little: Figma's decks' 10s stand
        /// 17.0 (Kathmandu Royal, Mithila Ink) to 17.6 (Classic) (ADR 068; the old art's stood 19.3,
        /// with its index enlarged on 30 Oct 2026).
        /// </summary>
        private const float MinRank = 16.9f;

        /// <summary>Canvas units of cloth that show between a trick card and what is next to it.</summary>
        private const float Clear = 5f;

        /// <summary>A thrown card's size, of the hand card's.</summary>
        private const float TrickShare = 0.8f;

        /// <summary>How far a side card is turned towards its player, in degrees: it lies sideways (ADR 075).</summary>
        private const float SideLean = 90f;

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
        public void AFullTrick_OfSmallerCards_LiesApartRoundTheMiddle_ClearOfTheSeatsAndTheHand(float width, float height, bool bannerRoom)
        {
            Open(width, height);
            _view.SetBannerRoom(bannerRoom, height > 1080f ? new BannerSize(600, 94) : new BannerSize(840, 131));
            Rect[] trick = Deal();

            // Four fifths of the hand's card, upright at the top and the bottom, lying sideways
            // with its top towards the player who played it at the sides (seats: 0 bottom, 1 right,
            // 2 top, 3 left; a positive angle turns anticlockwise, so the left card's top points left).
            float[] leans = { 0f, -SideLean, 0f, SideLean };
            Vector2 handCard = SizeOf(Find("HandLayer").GetComponentInChildren<CardView>(false));
            for (int seat = 0; seat < TrickAreaView.Slots; seat++)
            {
                CardView card = _view.Trick.CardAt(seat);
                Assert.AreEqual(TrickShare * _view.Hand.CardScale, card.Placement.Scale, 1e-4f, $"seat {seat}'s card is smaller than the hand's");
                Assert.AreEqual(TrickShare * handCard.x, SizeOf(card).x, 0.5f, "on the screen too");
                Assert.AreEqual(TrickShare * handCard.y, SizeOf(card).y, 0.5f);
                Assert.AreEqual(leans[seat], Vector2.SignedAngle(Vector2.up, card.transform.up), 0.01f, $"seat {seat}'s card leans towards its player");
            }

            // A "+" round the middle: the top and the bottom card one above the other, the side ones
            // level with the middle, no card over another, and all of them close together.
            Vector2 middle = Box(_view.Trick.Area).center;
            Assert.AreEqual(middle.x, trick[0].center.x, 0.5f, "the bottom card stands under the middle");
            Assert.AreEqual(middle.x, trick[2].center.x, 0.5f, "the top card stands over the middle");
            Assert.AreEqual(middle.y, trick[1].center.y, 0.5f, "the right card is level with the middle");
            Assert.AreEqual(middle.y, trick[3].center.y, 0.5f, "the left card is level with the middle");
            Assert.Greater(trick[2].yMin, trick[0].yMax, "the top card is clear of the bottom one");
            foreach (int side in new[] { 1, 3 })
            {
                foreach (int column in new[] { 0, 2 })
                {
                    Assert.IsFalse(Overlaps(_view.Trick.CardAt(side), trick[column]), $"the side card of seat {side} is clear of the card of seat {column}");
                }
            }

            Rect cross = Union(new[] { _view.Trick.CardAt(0), _view.Trick.CardAt(1), _view.Trick.CardAt(2), _view.Trick.CardAt(3) });
            Assert.LessOrEqual(cross.width, TrickShare * (handCard.x + (2f * handCard.y)) + 20f, "the four lie close together");
            Assert.LessOrEqual(cross.height, 2.2f * TrickShare * handCard.y);

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

            Rect bottom = trick[0];
            Assert.GreaterOrEqual(bottom.yMin - HandTop(), Clear, "the bottom trick card is above the hand, a card lifted");

            Rect canvas = Box(_canvas.transform);
            foreach (Rect piece in trick)
            {
                Assert.IsTrue(canvas.Contains(piece.min) && canvas.Contains(piece.max), "on the screen");
            }

            Assert.IsFalse(Has("Prompt"), "no turn prompt on the table");
        }

        [TestCase(1920f, 1080f, false)]
        [TestCase(1920f, 1080f, true)]
        [TestCase(2400f, 1080f, false)]
        [TestCase(2400f, 1080f, true)]
        [TestCase(2520f, 1080f, false)]
        [TestCase(2520f, 1080f, true)]
        [TestCase(1920f, 1440f, false)]
        [TestCase(1920f, 1440f, true)]
        public void TheTopSeat_HasItsNameAndCallLeftOfItsAvatar_AndStandsWhereItsNameDid(float width, float height, bool bannerRoom)
        {
            Open(width, height);
            _view.SetBannerRoom(bannerRoom, height > 1080f ? new BannerSize(600, 94) : new BannerSize(840, 131));
            Deal();
            RectTransform seat = Find("Seat2");
            Rect timer = Box(Find("SeatTimerRing2"));
            Rect plate = Box(Find("SeatPlate2"));
            Rect pill = Box(Find("SeatPill2"));

            // The name above the call, left of the avatar's timer ring, the name level with the ring's
            // top and the call centred under it as on the other seats; nothing of the seat stands above
            // the ring (ADR 072).
            Assert.LessOrEqual(plate.xMax, timer.xMin, "the name plate stands left of the avatar");
            Assert.Greater(plate.xMax, timer.xMin - 20f, "close to it");
            Assert.AreEqual(plate.center.x, pill.center.x, 0.5f, "the call's pill centred under the name plate");
            Assert.GreaterOrEqual(plate.yMin, pill.yMax - 0.5f, "the name above the call");
            Assert.AreEqual(timer.yMax, plate.yMax, 1f, "the name level with the top of the avatar");
            Assert.GreaterOrEqual(timer.yMax, plate.yMax - 0.5f, "the avatar stands as high as the seat goes");

            // Its widest words stay centred under the name, clear of the avatar.
            foreach (string words in new[] { "Calling…", "Waiting", "13-13" })
            {
                _view.Seat(2).SetStatus(words, false);
                Canvas.ForceUpdateCanvases();
                Rect said = Box(Find("SeatPill2"));
                Assert.AreEqual(plate.center.x, said.center.x, 0.5f, words + " centred under the name");
                Assert.LessOrEqual(said.xMax, timer.xMin, words + " clear of the avatar");
            }

            // The seconds disc and speech box stand right of the avatar; the auto-play bubble is
            // left of the name, clear of the others and of the fan.
            Rect disc = Box(FindIn(seat, "SeatTimerDisc2"));
            Rect chat = Box(FindIn(seat, "Chat"));
            Rect bubble = Box(FindIn(seat, "Bubble"));
            Assert.GreaterOrEqual(disc.center.x, timer.center.x, "the seconds disc on the avatar's right");
            Assert.GreaterOrEqual(chat.xMin, disc.xMax - 0.5f, "the speech box right of the seconds disc");
            Assert.LessOrEqual(bubble.xMax, Mathf.Min(plate.xMin, pill.xMin), "the bubble left of the name and the call");
            Rect canvas = Box(_canvas.transform);
            foreach (Rect piece in new[] { plate, pill, disc, chat, bubble })
            {
                Assert.IsTrue(canvas.Contains(piece.min) && canvas.Contains(piece.max), "on the screen");
                foreach (CardView card in _view.Seat(2).Fan.Area.GetComponentsInChildren<CardView>(false))
                {
                    Assert.IsFalse(Overlaps(card, piece), "clear of the fan");
                }
            }

            foreach (Rect piece in new[] { plate, pill, disc, chat })
            {
                Assert.IsFalse(piece.Overlaps(bubble), "clear of the bubble");
            }

            Assert.IsFalse(chat.Overlaps(disc), "the speech box clear of the disc");
            if (_view.HasBannerRoom)
            {
                Rect slot = Box(Find("BannerSlot"));
                float bannerWidth = height > 1080f ? 600f : 840f;
                float bannerHeight = height > 1080f ? 94f : 131f;
                Rect banner = Rect.MinMaxRect(slot.xMax - bannerWidth, slot.yMax - bannerHeight, slot.xMax, slot.yMax);
                foreach (Rect piece in new[] { plate, pill, timer, disc, chat, bubble, Fan(2) })
                {
                    Assert.IsFalse(piece.Overlaps(banner), "the top seat clears the banner rectangle");
                }
            }

            foreach (string corner in new[] { "Logo", "RoundPill", "HomeButton", "ScoresButton", "SettingsButton" })
            {
                foreach (Rect piece in new[] { plate, pill, bubble })
                {
                    Assert.IsFalse(piece.Overlaps(Box(Find(corner))), "clear of " + corner);
                }
            }
        }

        [TestCase(1920f, 1080f, 840, 131, false)]
        [TestCase(1920f, 1080f, 960, 150, false)]
        [TestCase(2400f, 1080f, 840, 131, true)]
        [TestCase(2400f, 1080f, 960, 150, true)]
        [TestCase(1920f, 1440f, 600, 94, true)]
        public void BannerRoom_KeepsTheNormalTopSpeechLayout_AndHidesTheAdWhenItCannotFit(float width, float height, int bannerWidth, int bannerHeight, bool expectedRoom)
        {
            Open(width, height);
            Vector2 home = Find("Seat2").anchoredPosition;
            _view.SetBannerRoom(true, new BannerSize(bannerWidth, bannerHeight));
            Assert.AreEqual(expectedRoom, _view.HasBannerRoom);
            Deal();
            RectTransform chat = FindIn(Find("Seat2"), "Chat");
            Assert.GreaterOrEqual(Box(chat).xMin, Box(Find("SeatTimerDisc2")).xMax - 0.5f, "the normal speech box stays right of the avatar's seconds disc");
            if (!expectedRoom)
            {
                Assert.AreEqual(home, Find("Seat2").anchoredPosition, "the narrow-phone fallback leaves the player at home");
                Assert.IsFalse(Find("BannerSlot").gameObject.activeSelf);
                return;
            }

            Rect slot = Box(Find("BannerSlot"));
            Rect banner = Rect.MinMaxRect(slot.xMax - bannerWidth, slot.yMax - bannerHeight, slot.xMax, slot.yMax);
            Assert.IsFalse(Box(chat).Overlaps(banner), "the normal speech box clears the banner");
            Assert.IsFalse(Box(FindIn(chat, "Tail")).Overlaps(banner), "its original tail clears the banner");
            Assert.IsFalse(Fan(2).Overlaps(banner), "all 13 cards clear the banner");
        }

        [Test]
        public void SeatBubbles_StartHidden_WithoutASampleCallNumber()
        {
            Open(1920f, 1080f);
            for (int seat = 0; seat < TableView.SeatCount; seat++)
            {
                RectTransform bubble = FindIn(Find("Seat" + seat), "Bubble");
                Assert.IsFalse(bubble.gameObject.activeSelf);
                var view = new SerializedObject(_view.Seat(seat));
                var text = new SerializedObject(view.FindProperty("_bubbleText").objectReferenceValue);
                Assert.IsEmpty(text.FindProperty("m_text").stringValue, "a call is already shown in the status pill");
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
            Assert.GreaterOrEqual(Box(cards[6].transform).yMin, canvas.yMin, "the whole middle card shows");
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

            // About a quarter of the screen's height: the owner asked for the cards "by small amount"
            // larger than a fifth twice (3 Nov 2026, ADR 083; 4 Nov 2026, ADR 086), having found 9% larger
            // than the shared card "way too big" (ADR 062).
            Vector2 card = SizeOf(Find("HandLayer").GetComponentInChildren<CardView>(false));
            Assert.That(card.y / Box(_canvas.transform).height, Is.InRange(0.22f, 0.26f), "a card is about a quarter of the screen's height");

            // Every face's rank, the first ink from the top of its corner, stands MinRank canvas units
            // or more at the table's size, in every deck.
            var sprites = AssetDatabase.LoadAssetAtPath<CardSpriteProvider>(CardLibrary);
            foreach (CosmeticItem deck in CosmeticCatalog.Of(CosmeticKind.Deck))
            {
                for (int id = 0; id < Card.Count; id++)
                {
                    string path = AssetDatabase.GetAssetPath(sprites.Face(deck.Id, Card.FromId(id)));
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    try
                    {
                        Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)), path);
                        Assert.GreaterOrEqual(RankHeight(texture) * card.y / (texture.height - (2 * Frame)), MinRank, path + "'s rank reads at the table");
                    }
                    finally
                    {
                        Object.DestroyImmediate(texture);
                    }
                }
            }
        }

        /// <summary>Rows of the first ink from the top of a face's corner index: its rank.</summary>
        private static int RankHeight(Texture2D face)
        {
            Color32[] pixels = face.GetPixels32();
            float paper = PaperAbove(pixels, face);
            int first = -1;
            for (int y = IndexTop; y < face.height / 2; y++)
            {
                int row = (face.height - 1 - y) * face.width;
                bool ink = false;
                for (int x = IndexLeft; x < IndexRight && !ink; x++)
                {
                    Color32 p = pixels[row + x];
                    ink = p.a > 128 && Luminance(p) < paper - InkContrast;
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

        /// <summary>The paper's luminance in the margin above a face's corner index (its median).</summary>
        private static float PaperAbove(Color32[] pixels, Texture2D face)
        {
            var values = new List<float>();
            for (int y = IndexTop; y < IndexTop + 12; y++)
            {
                for (int x = IndexLeft; x < IndexRight; x++)
                {
                    values.Add(Luminance(pixels[((face.height - 1 - y) * face.width) + x]));
                }
            }

            values.Sort();
            return values[values.Count / 2];
        }

        private static float Luminance(Color32 p) => (0.2126f * p.r) + (0.7152f * p.g) + (0.0722f * p.b);

        /// <summary>
        /// The table as a trick is won: four cards in the trick, a full fan at each opponent and the
        /// player's full hand with A♥ lifted. Returns the trick cards' boxes.
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

        private bool Has(string name)
        {
            foreach (RectTransform t in _canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private static RectTransform FindIn(Transform root, string name)
        {
            foreach (RectTransform t in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            Assert.Fail(root.name + " has " + name);
            return null;
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
