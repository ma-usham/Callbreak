using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Rules
{
    public sealed class RedealPolicyTests
    {
        // South holds all 13 hearts (no spade); every hand holds an honour.
        private static CardSet[] SpadelessDeal => new[]
        {
            CardSet.OfSuit(Suit.Hearts),
            CardSet.OfSuit(Suit.Spades).Except(Set("2♠ 3♠ 4♠")).Union(Set("2♣ 3♣ 4♣")),
            CardSet.OfSuit(Suit.Diamonds).Except(Set("2♦")).Union(Set("2♠")),
            CardSet.OfSuit(Suit.Clubs).Except(Set("2♣ 3♣ 4♣")).Union(Set("3♠ 4♠ 2♦")),
        };

        // Every hand holds a spade; East and West hold nothing above 10.
        private static CardSet[] HonourlessDeal => new[]
        {
            Set("A♠ K♠ Q♠ J♠ A♥ K♥ Q♥ J♥ A♦ K♦ Q♦ J♦ A♣"),
            Set("10♠ 9♥ 8♥ 7♥ 6♥ 5♥ 4♥ 3♥ 2♥ 10♥ 9♦ 8♦ 7♦"),
            Set("9♠ 8♠ 7♠ K♣ Q♣ J♣ 10♣ 9♣ 8♣ 7♣ 6♦ 5♦ 4♦"),
            Set("6♠ 5♠ 4♠ 3♠ 2♠ 6♣ 5♣ 4♣ 3♣ 2♣ 10♦ 3♦ 2♦"),
        };

        // South and West hold no spade and East holds no honour, all in the same deal.
        private static CardSet[] BothConditionsDeal => new[]
        {
            CardSet.OfSuit(Suit.Hearts),
            Set("10♠ 9♠ 8♠ 7♠ 6♠ 5♠ 4♠ 3♠ 2♠ 5♦ 4♦ 3♦ 2♦"),
            Set("A♠ K♠ Q♠ J♠ A♦ K♦ Q♦ J♦ 10♦ 9♦ 8♦ 7♦ 6♦"),
            CardSet.OfSuit(Suit.Clubs),
        };

        [Test]
        public void Fixtures_Built_AreFullDealsOf13Each()
        {
            AssertFullDeal(SpadelessDeal);
            AssertFullDeal(HonourlessDeal);
            AssertFullDeal(BothConditionsDeal);
        }

        [Test]
        public void Check_HandWithoutSpadeAndRuleOn_NoSpade()
        {
            Assert.AreEqual(RedealReason.NoSpade, RedealPolicy.Check(SpadelessDeal, RuleSet.NepaliStandard));
        }

        [Test]
        public void Check_HandWithoutSpadeAndRuleOff_None()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(redealIfNoSpade: false);

            Assert.AreEqual(RedealReason.None, RedealPolicy.Check(SpadelessDeal, rules));
        }

        [Test]
        public void Check_HandWithoutHonourAndRuleOn_NoHonour()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(redealIfNoHonour: true);

            Assert.AreEqual(RedealReason.NoHonour, RedealPolicy.Check(HonourlessDeal, rules));
        }

        [Test]
        public void Check_HandWithoutHonourAndRuleOffByDefault_None()
        {
            Assert.AreEqual(RedealReason.None, RedealPolicy.Check(HonourlessDeal, RuleSet.NepaliStandard));
        }

        [Test]
        public void Check_BothConditionsAndBothRulesOn_NoSpadeTakesPrecedence()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(redealIfNoHonour: true);

            Assert.AreEqual(RedealReason.NoSpade, RedealPolicy.Check(BothConditionsDeal, rules));
        }

        [Test]
        public void Check_BothConditionsOnlyHonourRuleOn_NoHonour()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(redealIfNoSpade: false, redealIfNoHonour: true);

            Assert.AreEqual(RedealReason.NoHonour, RedealPolicy.Check(BothConditionsDeal, rules));
        }

        [Test]
        public void Check_BothConditionsBothRulesOff_None()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(redealIfNoSpade: false, redealIfNoHonour: false);

            Assert.AreEqual(RedealReason.None, RedealPolicy.Check(BothConditionsDeal, rules));
        }

        [Test]
        public void Check_OrdinaryShuffledDeal_None()
        {
            CardSet[] deal = Deck.Deal(Deck.Shuffled(new Pcg32Random(12UL)), Seat.South, Direction.Anticlockwise);
            foreach (CardSet hand in deal)
            {
                Assert.IsTrue(hand.HasSuit(Suit.Spades) && RedealPolicy.HasHonour(hand), "fixture: seed 12 deals ordinary hands");
            }

            Assert.AreEqual(RedealReason.None, RedealPolicy.Check(deal, RuleSet.NepaliStandard.With(redealIfNoHonour: true)));
        }

        [TestCase("J♣", true)]
        [TestCase("A♥", true)]
        [TestCase("10♠ 9♠ 2♥", false)]
        [TestCase("-", false)]
        public void HasHonour_AnyHand_TrueOnlyWithACardAboveTen(string hand, bool expected)
        {
            Assert.AreEqual(expected, RedealPolicy.HasHonour(Set(hand)));
        }

        private static void AssertFullDeal(CardSet[] deal)
        {
            CardSet all = CardSet.Empty;
            foreach (CardSet hand in deal)
            {
                Assert.AreEqual(13, hand.Count);
                Assert.IsFalse(all.Overlaps(hand));
                all |= hand;
            }

            Assert.AreEqual(CardSet.FullDeck, all);
        }
    }
}
