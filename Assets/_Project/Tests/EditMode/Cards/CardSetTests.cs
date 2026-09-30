using System;
using System.Collections.Generic;
using System.Linq;
using Callbreak.Core.Cards;
using Callbreak.Core.Randomness;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Cards
{
    public sealed class CardSetTests
    {
        [Test]
        public void Empty_Created_HasNoCards()
        {
            Assert.IsTrue(CardSet.Empty.IsEmpty);
            Assert.AreEqual(0, CardSet.Empty.Count);
            Assert.AreEqual(0UL, CardSet.Empty.Bits);
            Assert.AreEqual(default(CardSet), CardSet.Empty);
        }

        [Test]
        public void FullDeck_Created_HasAll52Cards()
        {
            Assert.AreEqual(52, CardSet.FullDeck.Count);
            Assert.AreEqual((1UL << 52) - 1UL, CardSet.FullDeck.Bits);
            for (int id = 0; id < 52; id++)
            {
                Assert.IsTrue(CardSet.FullDeck.Contains(Card.FromId(id)));
            }
        }

        [Test]
        public void WithAndWithout_SingleCard_SetsAndClearsItsBit()
        {
            Card aceOfSpades = Card.FromId(51);

            CardSet set = CardSet.Empty.With(aceOfSpades);

            Assert.IsTrue(set.Contains(aceOfSpades));
            Assert.AreEqual(1UL << 51, set.Bits);
            Assert.AreEqual(CardSet.Of(aceOfSpades), set);
            Assert.IsTrue(set.Without(aceOfSpades).IsEmpty);
            Assert.AreEqual(set, set.With(aceOfSpades));
        }

        [Test]
        public void Count_RandomSets_MatchesNaiveCount()
        {
            var rng = new Pcg32Random(5UL);
            for (int trial = 0; trial < 500; trial++)
            {
                ulong bits = (((ulong)rng.NextUInt32() << 32) | rng.NextUInt32()) & ((1UL << 52) - 1UL);
                int naive = 0;
                for (int i = 0; i < 52; i++)
                {
                    if ((bits & (1UL << i)) != 0UL)
                    {
                        naive++;
                    }
                }

                Assert.AreEqual(naive, CardSet.FromBits(bits).Count);
            }
        }

        [Test]
        public void OfSuit_EachSuit_HoldsExactlyItsThirteenCards()
        {
            foreach (Suit suit in new[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades })
            {
                CardSet set = CardSet.OfSuit(suit);

                Assert.AreEqual(13, set.Count);
                foreach (Card card in set)
                {
                    Assert.AreEqual(suit, card.Suit);
                }
            }

            Assert.AreEqual(0x1FFFUL << 39, CardSet.OfSuit(Suit.Spades).Bits);
        }

        [TestCase("K♦", "A♦")]
        [TestCase("A♠", "-")]
        [TestCase("9♠", "A♠ K♠ Q♠ J♠ 10♠")]
        [TestCase("2♣", "A♣ K♣ Q♣ J♣ 10♣ 9♣ 8♣ 7♣ 6♣ 5♣ 4♣ 3♣")]
        [TestCase("A♥", "-")]
        [TestCase("10♥", "A♥ K♥ Q♥ J♥")]
        public void Above_Card_IsTheHigherCardsOfItsSuitOnly(string card, string expected)
        {
            Assert.AreEqual(CardSet.Parse(expected), CardSet.Above(Card.Parse(card)));
        }

        [Test]
        public void HighestAndLowest_OneSuitSubset_AreRankExtremes()
        {
            CardSet hand = CardSet.Parse("A♠ 4♠ J♥ 8♥ K♦ 5♦ 7♣");

            Assert.AreEqual(Card.Parse("J♥"), hand.InSuit(Suit.Hearts).Highest());
            Assert.AreEqual(Card.Parse("8♥"), hand.InSuit(Suit.Hearts).Lowest());
            Assert.AreEqual(Card.Parse("A♠"), hand.Highest());
            Assert.AreEqual(Card.Parse("7♣"), hand.Lowest());
        }

        [Test]
        public void HighestAndLowest_Empty_Throw()
        {
            Assert.Throws<InvalidOperationException>(() => CardSet.Empty.Highest());
            Assert.Throws<InvalidOperationException>(() => CardSet.Empty.Lowest());
        }

        [Test]
        public void HighestAndLowest_EverySingleCard_ReturnThatCard()
        {
            for (int id = 0; id < 52; id++)
            {
                CardSet set = CardSet.Of(Card.FromId(id));
                Assert.AreEqual(id, set.Highest().Id);
                Assert.AreEqual(id, set.Lowest().Id);
            }
        }

        [Test]
        public void InSuitAndHasSuit_MixedHand_FilterBySuit()
        {
            CardSet hand = CardSet.Parse("10♠ 2♠ 8♥");

            Assert.AreEqual(CardSet.Parse("10♠ 2♠"), hand.InSuit(Suit.Spades));
            Assert.IsTrue(hand.HasSuit(Suit.Hearts));
            Assert.IsFalse(hand.HasSuit(Suit.Clubs));
            Assert.IsTrue(hand.InSuit(Suit.Diamonds).IsEmpty);
        }

        [Test]
        public void Algebra_TwoSets_UnionIntersectExcept()
        {
            CardSet a = CardSet.Parse("A♠ K♠ Q♥");
            CardSet b = CardSet.Parse("K♠ Q♥ 2♣");

            Assert.AreEqual(CardSet.Parse("A♠ K♠ Q♥ 2♣"), a.Union(b));
            Assert.AreEqual(a | b, a.Union(b));
            Assert.AreEqual(CardSet.Parse("K♠ Q♥"), a.Intersect(b));
            Assert.AreEqual(a & b, a.Intersect(b));
            Assert.AreEqual(CardSet.Parse("A♠"), a.Except(b));
            Assert.AreEqual(a - b, a.Except(b));
            Assert.IsTrue(a.Intersect(b).IsSubsetOf(a));
            Assert.IsFalse(a.IsSubsetOf(b));
            Assert.IsTrue(a.Overlaps(b));
            Assert.IsFalse(a.Except(b).Overlaps(b));
        }

        [Test]
        public void Enumeration_RandomSet_AscendingIdsAndCountMatch()
        {
            var rng = new Pcg32Random(11UL);
            for (int trial = 0; trial < 100; trial++)
            {
                ulong bits = (((ulong)rng.NextUInt32() << 32) | rng.NextUInt32()) & ((1UL << 52) - 1UL);
                CardSet set = CardSet.FromBits(bits);
                var ids = new List<int>();
                foreach (Card card in set)
                {
                    ids.Add(card.Id);
                }

                Assert.AreEqual(set.Count, ids.Count);
                CollectionAssert.AreEqual(ids.OrderBy(i => i).ToList(), ids);
                for (int k = 0; k < ids.Count; k++)
                {
                    Assert.AreEqual(ids[k], set.ElementAt(k).Id);
                }
            }
        }

        [Test]
        public void ElementAt_OutOfRange_Throws()
        {
            CardSet set = CardSet.Parse("A♠ K♠");

            Assert.Throws<ArgumentOutOfRangeException>(() => set.ElementAt(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => set.ElementAt(-1));
        }

        [Test]
        public void FromBits_BitAbove51_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CardSet.FromBits(1UL << 52));
            Assert.AreEqual(CardSet.FullDeck, CardSet.FromBits((1UL << 52) - 1UL));
        }

        [Test]
        public void ToString_Hand_HighestIdFirstAndParsesBack()
        {
            CardSet hand = CardSet.Parse("7♣ A♠ K♦ J♥");

            Assert.AreEqual("A♠ J♥ K♦ 7♣", hand.ToString());
            Assert.AreEqual(hand, CardSet.Parse(hand.ToString()));
            Assert.AreEqual("-", CardSet.Empty.ToString());
            Assert.AreEqual(CardSet.Empty, CardSet.Parse("-"));
            Assert.AreEqual(CardSet.Empty, CardSet.Parse(""));
        }

        [Test]
        public void Parse_AsciiWithCommas_ReadsAllCards()
        {
            Assert.AreEqual(CardSet.Parse("A♠ 10♥ 2♣"), CardSet.Parse("AS, 10H,2c"));
        }

        [TestCase("A♠ A♠")]
        [TestCase("A♠ ZZ")]
        [TestCase("A♠ 1")]
        public void TryParse_DuplicateOrGarbage_ReturnsFalse(string text)
        {
            Assert.IsFalse(CardSet.TryParse(text, out _));
            Assert.Throws<FormatException>(() => CardSet.Parse(text));
        }

        [Test]
        public void Operations_HotLoop_DoNotAllocate()
        {
            CardSet hand = CardSet.Parse("A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣");
            Card king = Card.Parse("K♦");
            int sink = 0;

            long bytes = AllocationProbe.BytesAllocatedBy(
                () =>
                {
                    CardSet follow = hand.InSuit(Suit.Diamonds);
                    CardSet higher = follow & CardSet.Above(king);
                    sink += higher.Count + follow.Highest().Id + hand.Lowest().Id + hand.ElementAt(3).Id;
                    sink += (hand | CardSet.OfSuit(Suit.Clubs)).Except(follow).Without(king).With(king).Count;
                    foreach (Card card in hand)
                    {
                        sink += card.Id;
                    }

                    sink += hand.Contains(king) && hand.HasSuit(Suit.Hearts) && follow.IsSubsetOf(hand) ? 1 : 0;
                },
                1000);

            Assert.AreEqual(0L, bytes);
            Assert.AreNotEqual(0, sink);
        }

        [Test]
        public void AllocationProbe_AllocatingAction_ReportsBytes()
        {
            var keep = new List<object>();

            long bytes = AllocationProbe.BytesAllocatedBy(() => keep.Add(new object()), 100);

            Assert.Greater(bytes, 0L);
        }
    }
}
