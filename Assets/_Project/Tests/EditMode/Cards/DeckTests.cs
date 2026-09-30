using System;
using System.Collections.Generic;
using System.Linq;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Cards
{
    public sealed class DeckTests
    {
        [Test]
        public void Shuffled_AnySeed_IsAPermutationOfTheDeck()
        {
            for (ulong seed = 0; seed < 50; seed++)
            {
                Card[] order = Deck.Shuffled(new Pcg32Random(seed));

                Assert.AreEqual(52, order.Length);
                Assert.AreEqual(52, order.Select(c => c.Id).Distinct().Count());
            }
        }

        [Test]
        public void Shuffled_SameSeed_SameOrder()
        {
            CollectionAssert.AreEqual(Deck.Shuffled(new Pcg32Random(77UL)), Deck.Shuffled(new Pcg32Random(77UL)));
            CollectionAssert.AreNotEqual(Deck.Shuffled(new Pcg32Random(77UL)), Deck.Shuffled(new Pcg32Random(78UL)));
        }

        [Test]
        public void Shuffled_FisherYates_Draws51TimesWithShrinkingBounds()
        {
            var recorder = new RecordingRandom(new Pcg32Random(3UL));

            Deck.Shuffled(recorder);

            CollectionAssert.AreEqual(Enumerable.Range(2, 51).Reverse().ToList(), recorder.Bounds);
        }

        [Test]
        public void Shuffled_AllZeroDraws_ProducesTheDocumentedPermutation()
        {
            // With j = 0 every time, position i swaps with position 0 for i = 51..1.
            Card[] order = Deck.Shuffled(new ConstantRandom(0));

            Assert.AreEqual(1, order[0].Id);
            Assert.AreEqual(0, order[51].Id);
            for (int i = 1; i < 51; i++)
            {
                Assert.AreEqual(i + 1, order[i].Id);
            }
        }

        [Test]
        public void Deal_IdentityOrder_OneCardAtATimeFromTheFirstSeat()
        {
            Card[] order = Enumerable.Range(0, 52).Select(Card.FromId).ToArray();

            CardSet[] hands = Deck.Deal(order, Seat.East, Direction.Anticlockwise);

            // East gets cards 0, 4, 8, ...; North 1, 5, ...; West 2, 6, ...; South 3, 7, ...
            AssertHand(hands[(int)Seat.East], 0);
            AssertHand(hands[(int)Seat.North], 1);
            AssertHand(hands[(int)Seat.West], 2);
            AssertHand(hands[(int)Seat.South], 3);
        }

        [Test]
        public void Deal_Clockwise_GoesTheOtherWayRound()
        {
            Card[] order = Enumerable.Range(0, 52).Select(Card.FromId).ToArray();

            CardSet[] hands = Deck.Deal(order, Seat.East, Direction.Clockwise);

            AssertHand(hands[(int)Seat.East], 0);
            AssertHand(hands[(int)Seat.South], 1);
            AssertHand(hands[(int)Seat.West], 2);
            AssertHand(hands[(int)Seat.North], 3);
        }

        [Test]
        public void Deal_ShuffledDeck_ThirteenEachAndNothingLost()
        {
            CardSet[] hands = Deck.Deal(Deck.Shuffled(new Pcg32Random(9UL)), Seat.South, Direction.Anticlockwise);

            CardSet all = CardSet.Empty;
            foreach (CardSet hand in hands)
            {
                Assert.AreEqual(13, hand.Count);
                Assert.IsFalse(all.Overlaps(hand));
                all |= hand;
            }

            Assert.AreEqual(CardSet.FullDeck, all);
        }

        [Test]
        public void Deal_WrongSizeOrDuplicate_Throws()
        {
            Card[] tooShort = Enumerable.Range(0, 51).Select(Card.FromId).ToArray();
            Card[] duplicate = Enumerable.Range(0, 52).Select(i => Card.FromId(i == 51 ? 0 : i)).ToArray();

            Assert.Throws<ArgumentException>(() => Deck.Deal(tooShort, Seat.South, Direction.Anticlockwise));
            Assert.Throws<ArgumentException>(() => Deck.Deal(duplicate, Seat.South, Direction.Anticlockwise));
        }

        private static void AssertHand(CardSet hand, int firstId)
        {
            var expected = new List<int>();
            for (int id = firstId; id < 52; id += 4)
            {
                expected.Add(id);
            }

            var actual = new List<int>();
            foreach (Card card in hand)
            {
                actual.Add(card.Id);
            }

            CollectionAssert.AreEqual(expected, actual);
        }

        private sealed class RecordingRandom : IRandom
        {
            private readonly IRandom _inner;

            public RecordingRandom(IRandom inner)
            {
                _inner = inner;
            }

            public List<int> Bounds { get; } = new List<int>();

            public int NextInt(int maxExclusive)
            {
                Bounds.Add(maxExclusive);
                return _inner.NextInt(maxExclusive);
            }
        }

        private sealed class ConstantRandom : IRandom
        {
            private readonly int _value;

            public ConstantRandom(int value)
            {
                _value = value;
            }

            public int NextInt(int maxExclusive)
            {
                return _value;
            }
        }
    }
}
