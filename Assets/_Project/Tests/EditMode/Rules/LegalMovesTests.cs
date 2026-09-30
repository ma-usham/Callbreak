using System;
using System.Collections.Generic;
using System.Linq;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Rules
{
    public sealed class LegalMovesTests
    {
        private static RuleSet Standard => RuleSet.NepaliStandard;

        private static IEnumerable<RuleSet> AllCombinations => RuleVariants.LegalityCombinations();

        // ---- GDD §5 edge cases, one test each ------------------------------------------------------

        [Test]
        public void Compute_Case1LedKingOfDiamondsHoldingFiveAndAce_OnlyAce()
        {
            CardSet hand = Set("5♦ A♦ Q♣ 3♥ 7♠");

            CardSet legal = LegalMoves.Compute(hand, Led("K♦"), Standard);

            Assert.AreEqual(Set("A♦"), legal);
        }

        [Test]
        public void Compute_Case2LedNineOfHeartsThenFourOfSpadesHoldingQueenAndThree_EitherHeart()
        {
            CardSet hand = Set("Q♥ 3♥ A♠ 7♣");

            CardSet legal = LegalMoves.Compute(hand, Led("9♥ 4♠"), Standard);

            Assert.AreEqual(Set("Q♥ 3♥"), legal);
        }

        [Test]
        public void Compute_Case3LedSevenOfClubsThenJackOfSpadesVoidInClubs_AnyCard()
        {
            CardSet hand = Set("10♠ 2♠ 8♥");

            CardSet legal = LegalMoves.Compute(hand, Led("7♣ J♠"), Standard);

            Assert.AreEqual(hand, legal);
        }

        [Test]
        public void Compute_Case4LedSixOfClubsVoidInClubsHoldingThreeOfSpades_OnlyTheSpade()
        {
            CardSet hand = Set("3♠ K♥");

            CardSet legal = LegalMoves.Compute(hand, Led("6♣"), Standard);

            Assert.AreEqual(Set("3♠"), legal);
        }

        [Test]
        public void Compute_Case5LedAceOfSpadesHoldingKingAndTwo_EitherSpade()
        {
            CardSet hand = Set("K♠ 2♠ A♥ 9♦");

            CardSet legal = LegalMoves.Compute(hand, Led("A♠"), Standard);

            Assert.AreEqual(Set("K♠ 2♠"), legal);
        }

        [Test]
        public void Compute_Case6LedFiveThenNineOfSpadesHoldingQueenAndSix_OnlyQueen()
        {
            CardSet hand = Set("Q♠ 6♠ J♥");

            CardSet legal = LegalMoves.Compute(hand, Led("5♠ 9♠"), Standard);

            Assert.AreEqual(Set("Q♠"), legal);
        }

        [Test]
        public void Compute_Fig32YouHoldAceAndEightOfHearts_OnlyAceBeatsTheQueen()
        {
            // GDD FIG 3.2: Suman (East) leads 9♥, Asha (North) plays Q♥, Bikash (West) 10♥; You (South).
            Trick trick = TrickOf(Seat.East, Direction.Anticlockwise, "9♥ Q♥ 10♥");
            CardSet hand = Set("A♥ 8♥ K♠ 5♦");

            Assert.AreEqual(Seat.South, trick.NextSeat);
            Assert.AreEqual(Set("A♥"), LegalMoves.Compute(hand, trick, Standard));
        }

        // ---- The empty trick -----------------------------------------------------------------------

        [TestCaseSource(nameof(AllCombinations))]
        public void Compute_EmptyTrick_WholeHandUnderEveryRuleCombination(RuleSet rules)
        {
            CardSet hand = Set("A♠ 4♠ J♥ 8♥ K♦ 7♣");
            var trick = new Trick(Seat.West, Direction.Anticlockwise);

            Assert.AreEqual(hand, LegalMoves.Compute(hand, trick, rules));
        }

        [Test]
        public void Compute_EmptyTrickHandOfOnlySpades_SpadesMayBeLedAnyTime()
        {
            CardSet hand = Set("A♠ 4♠");

            Assert.AreEqual(hand, LegalMoves.Compute(hand, new Trick(Seat.South, Direction.Anticlockwise), Standard));
        }

        // ---- Each legality switch, both ways --------------------------------------------------------

        [TestCase(true, "A♦")]
        [TestCase(false, "5♦ A♦")]
        public void Compute_MustBeatLedSuitSwitch_Case1(bool mustBeat, string expected)
        {
            RuleSet rules = Standard.With(mustBeatLedSuit: mustBeat);

            Assert.AreEqual(Set(expected), LegalMoves.Compute(Set("5♦ A♦ Q♣"), Led("K♦"), rules));
        }

        [TestCase(true, "Q♠")]
        [TestCase(false, "Q♠ 6♠")]
        public void Compute_MustBeatLedSuitSwitch_SpadeLead(bool mustBeat, string expected)
        {
            RuleSet rules = Standard.With(mustBeatLedSuit: mustBeat);

            Assert.AreEqual(Set(expected), LegalMoves.Compute(Set("Q♠ 6♠ J♥"), Led("5♠ 9♠"), rules));
        }

        [TestCase(true, "3♠")]
        [TestCase(false, "3♠ K♥")]
        public void Compute_MustTrumpWhenVoidSwitch_Case4(bool mustTrump, string expected)
        {
            RuleSet rules = Standard.With(mustTrumpWhenVoid: mustTrump);

            Assert.AreEqual(Set(expected), LegalMoves.Compute(Set("3♠ K♥"), Led("6♣"), rules));
        }

        [TestCase(true, "Q♠")]
        [TestCase(false, "Q♠ 2♠")]
        public void Compute_MustOvertrumpSwitch_CanOvertrump(bool mustOvertrump, string expected)
        {
            RuleSet rules = Standard.With(mustOvertrump: mustOvertrump);

            Assert.AreEqual(Set(expected), LegalMoves.Compute(Set("Q♠ 2♠ 8♥"), Led("7♣ J♠"), rules));
        }

        [TestCase(true, "10♠ 2♠ 8♥")]
        [TestCase(false, "10♠ 2♠")]
        public void Compute_MustOvertrumpSwitch_CannotOvertrump(bool mustOvertrump, string expected)
        {
            // Without the overtrump rule any spade satisfies "must trump", so the low spades stay forced.
            RuleSet rules = Standard.With(mustOvertrump: mustOvertrump);

            Assert.AreEqual(Set(expected), LegalMoves.Compute(Set("10♠ 2♠ 8♥"), Led("7♣ J♠"), rules));
        }

        [TestCase(true, "10♠ 2♠ 8♥")]
        [TestCase(false, "10♠ 2♠")]
        public void Compute_DiscardWhenCannotOvertrumpSwitch_Case3(bool discard, string expected)
        {
            RuleSet rules = Standard.With(discardWhenCannotOvertrump: discard);

            Assert.AreEqual(Set(expected), LegalMoves.Compute(Set("10♠ 2♠ 8♥"), Led("7♣ J♠"), rules));
        }

        [Test]
        public void Compute_VoidWithoutSpades_AnyCard()
        {
            CardSet hand = Set("8♥ 2♦ K♦");

            Assert.AreEqual(hand, LegalMoves.Compute(hand, Led("7♣ J♠"), Standard));
        }

        [Test]
        public void Compute_TrumpedTrickHoldingOnlyHigherLedCards_StillMayPlayEitherLedCard()
        {
            // Beating 9♥ is pointless once 4♠ is in, so the must-beat obligation no longer applies.
            CardSet hand = Set("K♥ 10♥ 2♣");

            Assert.AreEqual(Set("K♥ 10♥"), LegalMoves.Compute(hand, Led("9♥ 4♠ Q♥"), Standard));
        }

        [Test]
        public void Compute_BeatTheHighestLedCardNotTheFirst_RequiresAboveTheQueen()
        {
            CardSet hand = Set("J♥ K♥ 5♥");

            Assert.AreEqual(Set("K♥"), LegalMoves.Compute(hand, Led("9♥ Q♥ 10♥"), Standard));
        }

        // ---- Properties over random situations -----------------------------------------------------

        [TestCaseSource(nameof(AllCombinations))]
        public void Compute_RandomSituations_NeverEmptyAndSubsetOfHand(RuleSet rules)
        {
            var rng = new Pcg32Random(1000UL);
            for (int trial = 0; trial < 3000; trial++)
            {
                RuleVariants.RandomSituation(rng, out CardSet hand, out Trick trick);

                CardSet legal = LegalMoves.Compute(hand, trick, rules);

                Assert.IsFalse(legal.IsEmpty, hand + " | " + trick);
                Assert.IsTrue(legal.IsSubsetOf(hand), hand + " | " + trick);
            }
        }

        [TestCaseSource(nameof(AllCombinations))]
        public void Compute_RandomSituations_AgreesWithFlowchartOracle(RuleSet rules)
        {
            var rng = new Pcg32Random(2000UL);
            for (int trial = 0; trial < 3000; trial++)
            {
                RuleVariants.RandomSituation(rng, out CardSet hand, out Trick trick);

                CardSet expected = FlowchartOracle(hand, trick, rules);

                Assert.AreEqual(expected, LegalMoves.Compute(hand, trick, rules), hand + " | " + trick);
            }
        }

        [Test]
        public void Compute_SingleCardHand_ThatCardIsLegal()
        {
            var rng = new Pcg32Random(3000UL);
            for (int trial = 0; trial < 2000; trial++)
            {
                RuleVariants.RandomSituation(rng, out CardSet hand, out Trick trick);
                CardSet single = CardSet.Of(hand.Lowest());

                Assert.AreEqual(single, LegalMoves.Compute(single, trick, Standard));
            }
        }

        [Test]
        public void IsLegal_Case1_OnlyTheAce()
        {
            CardSet hand = Set("5♦ A♦");

            Assert.IsTrue(LegalMoves.IsLegal(C("A♦"), hand, Led("K♦"), Standard));
            Assert.IsFalse(LegalMoves.IsLegal(C("5♦"), hand, Led("K♦"), Standard));
            Assert.IsFalse(LegalMoves.IsLegal(C("Q♦"), hand, Led("K♦"), Standard));
        }

        [Test]
        public void Compute_NullRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LegalMoves.Compute(Set("A♦"), Led("K♦"), null));
            Assert.Throws<ArgumentNullException>(() => LegalMoves.Classify(Set("A♦"), Led("K♦"), null));
        }

        [Test]
        public void Compute_HotLoop_DoesNotAllocate()
        {
            CardSet hand = Set("A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣");
            Trick[] tricks =
            {
                new Trick(Seat.South, Direction.Anticlockwise),
                Led("K♦"),
                Led("9♥ 4♠"),
                Led("7♣ J♠ Q♠"),
                Led("6♣"),
                Led("5♠ 9♠"),
            };
            RuleSet rules = Standard;
            int sink = 0;

            long bytes = AllocationProbe.BytesAllocatedBy(
                () =>
                {
                    for (int i = 0; i < tricks.Length; i++)
                    {
                        sink += LegalMoves.Compute(hand, tricks[i], rules).Count;
                        sink += (int)LegalMoves.Classify(hand, tricks[i], rules).Kind;
                        sink += TrickResolver.WinningIndex(tricks[1]);
                    }
                },
                1000);

            Assert.AreEqual(0L, bytes);
            Assert.AreNotEqual(0, sink);
        }

        // ---- Obligation classification (turn prompt, GDD §11 screen 08) ----------------------------

        [Test]
        public void Classify_Leading_LeadAny()
        {
            CardSet hand = Set("A♠ 7♣");

            PlayObligation obligation = LegalMoves.Classify(hand, new Trick(Seat.South, Direction.Anticlockwise), Standard);

            Assert.AreEqual(ObligationKind.LeadAny, obligation.Kind);
            Assert.AreEqual(hand, obligation.Legal);
            Assert.IsFalse(obligation.HasCardToBeat);
        }

        [Test]
        public void Classify_Fig32_BeatTheQueenOfHearts()
        {
            Trick trick = TrickOf(Seat.East, Direction.Anticlockwise, "9♥ Q♥ 10♥");

            PlayObligation obligation = LegalMoves.Classify(Set("A♥ 8♥ K♠"), trick, Standard);

            Assert.AreEqual(ObligationKind.BeatCard, obligation.Kind);
            Assert.IsTrue(obligation.HasCardToBeat);
            Assert.AreEqual(C("Q♥"), obligation.CardToBeat);
            Assert.AreEqual(Suit.Hearts, obligation.Suit);
            Assert.AreEqual(Set("A♥"), obligation.Legal);
        }

        [Test]
        public void Classify_FollowingCannotBeat_FollowSuit()
        {
            PlayObligation obligation = LegalMoves.Classify(Set("5♦ 3♦ A♠"), Led("K♦"), Standard);

            Assert.AreEqual(ObligationKind.FollowSuit, obligation.Kind);
            Assert.AreEqual(Suit.Diamonds, obligation.Suit);
            Assert.AreEqual(Set("5♦ 3♦"), obligation.Legal);
        }

        [Test]
        public void Classify_Case2Trumped_FollowSuit()
        {
            PlayObligation obligation = LegalMoves.Classify(Set("Q♥ 3♥"), Led("9♥ 4♠"), Standard);

            Assert.AreEqual(ObligationKind.FollowSuit, obligation.Kind);
            Assert.AreEqual(Suit.Hearts, obligation.Suit);
        }

        [Test]
        public void Classify_Case5SpadeLeadCannotBeat_FollowSpades()
        {
            PlayObligation obligation = LegalMoves.Classify(Set("K♠ 2♠"), Led("A♠"), Standard);

            Assert.AreEqual(ObligationKind.FollowSuit, obligation.Kind);
            Assert.AreEqual(Suit.Spades, obligation.Suit);
        }

        [Test]
        public void Classify_Case6SpadeLeadCanBeat_BeatTheNine()
        {
            PlayObligation obligation = LegalMoves.Classify(Set("Q♠ 6♠"), Led("5♠ 9♠"), Standard);

            Assert.AreEqual(ObligationKind.BeatCard, obligation.Kind);
            Assert.AreEqual(C("9♠"), obligation.CardToBeat);
            Assert.AreEqual(Suit.Spades, obligation.Suit);
        }

        [Test]
        public void Classify_Case4VoidNoSpadeInTrick_TrumpWithAnySpade()
        {
            PlayObligation obligation = LegalMoves.Classify(Set("3♠ K♥"), Led("6♣"), Standard);

            Assert.AreEqual(ObligationKind.TrumpWithSpade, obligation.Kind);
            Assert.IsFalse(obligation.HasCardToBeat);
            Assert.AreEqual(Suit.Spades, obligation.Suit);
            Assert.AreEqual(Set("3♠"), obligation.Legal);
        }

        [Test]
        public void Classify_VoidCanOvertrump_TrumpAboveTheJack()
        {
            PlayObligation obligation = LegalMoves.Classify(Set("Q♠ 2♠ 8♥"), Led("7♣ J♠"), Standard);

            Assert.AreEqual(ObligationKind.TrumpWithSpade, obligation.Kind);
            Assert.IsTrue(obligation.HasCardToBeat);
            Assert.AreEqual(C("J♠"), obligation.CardToBeat);
            Assert.AreEqual(Set("Q♠"), obligation.Legal);
        }

        [Test]
        public void Classify_Case3VoidCannotWin_AnyCard()
        {
            CardSet hand = Set("10♠ 2♠ 8♥");

            PlayObligation obligation = LegalMoves.Classify(hand, Led("7♣ J♠"), Standard);

            Assert.AreEqual(ObligationKind.AnyCard, obligation.Kind);
            Assert.AreEqual(hand, obligation.Legal);
        }

        [Test]
        public void Classify_Case3WithoutFreeDiscard_TrumpWithAnySpade()
        {
            RuleSet rules = Standard.With(discardWhenCannotOvertrump: false);

            PlayObligation obligation = LegalMoves.Classify(Set("10♠ 2♠ 8♥"), Led("7♣ J♠"), rules);

            Assert.AreEqual(ObligationKind.TrumpWithSpade, obligation.Kind);
            Assert.IsFalse(obligation.HasCardToBeat);
            Assert.AreEqual(Set("10♠ 2♠"), obligation.Legal);
        }

        [Test]
        public void Classify_VoidNoSpades_AnyCard()
        {
            PlayObligation obligation = LegalMoves.Classify(Set("8♥ 2♦"), Led("7♣"), Standard);

            Assert.AreEqual(ObligationKind.AnyCard, obligation.Kind);
        }

        [Test]
        public void Classify_MustBeatOff_FollowSuitEvenWhenAbleToBeat()
        {
            PlayObligation obligation = LegalMoves.Classify(
                Set("5♦ A♦"), Led("K♦"), Standard.With(mustBeatLedSuit: false));

            Assert.AreEqual(ObligationKind.FollowSuit, obligation.Kind);
            Assert.AreEqual(Set("5♦ A♦"), obligation.Legal);
        }

        [Test]
        public void Classify_MustTrumpOff_AnyCard()
        {
            PlayObligation obligation = LegalMoves.Classify(
                Set("3♠ K♥"), Led("6♣"), Standard.With(mustTrumpWhenVoid: false));

            Assert.AreEqual(ObligationKind.AnyCard, obligation.Kind);
            Assert.AreEqual(Set("3♠ K♥"), obligation.Legal);
        }

        [TestCaseSource(nameof(AllCombinations))]
        public void Classify_RandomSituations_LegalMatchesComputeAndEveryKindOccurs(RuleSet rules)
        {
            var rng = new Pcg32Random(4000UL);
            var kinds = new HashSet<ObligationKind>();
            for (int trial = 0; trial < 3000; trial++)
            {
                RuleVariants.RandomSituation(rng, out CardSet hand, out Trick trick);

                PlayObligation obligation = LegalMoves.Classify(hand, trick, rules);

                Assert.AreEqual(LegalMoves.Compute(hand, trick, rules), obligation.Legal);
                kinds.Add(obligation.Kind);
                AssertObligationShape(obligation, hand, trick);
            }

            Assert.IsTrue(kinds.Contains(ObligationKind.LeadAny));
            Assert.IsTrue(kinds.Contains(ObligationKind.FollowSuit));
            Assert.IsTrue(kinds.Contains(ObligationKind.AnyCard));
            Assert.AreEqual(rules.MustBeatLedSuit, kinds.Contains(ObligationKind.BeatCard));
            Assert.AreEqual(rules.MustTrumpWhenVoid, kinds.Contains(ObligationKind.TrumpWithSpade));
        }

        private static void AssertObligationShape(PlayObligation obligation, CardSet hand, Trick trick)
        {
            switch (obligation.Kind)
            {
                case ObligationKind.LeadAny:
                    Assert.IsTrue(trick.IsEmpty);
                    Assert.AreEqual(hand, obligation.Legal);
                    break;
                case ObligationKind.BeatCard:
                    Assert.AreEqual(trick.LedSuit, obligation.Suit);
                    Assert.AreEqual(obligation.Suit, obligation.CardToBeat.Suit);
                    Assert.AreEqual(obligation.Legal, obligation.Legal & CardSet.Above(obligation.CardToBeat));
                    break;
                case ObligationKind.FollowSuit:
                    Assert.AreEqual(hand.InSuit(trick.LedSuit), obligation.Legal);
                    break;
                case ObligationKind.TrumpWithSpade:
                    Assert.IsFalse(hand.HasSuit(trick.LedSuit));
                    Assert.AreEqual(obligation.Legal, obligation.Legal.InSuit(Suit.Spades));
                    break;
                case ObligationKind.AnyCard:
                    Assert.AreEqual(hand, obligation.Legal);
                    break;
                default:
                    Assert.Fail("Unknown kind " + obligation.Kind);
                    break;
            }
        }

        /// <summary>
        /// An independent transcription of the GDD FIG 3.1 flowchart and the §3 rule cards, written
        /// with LINQ over plain lists so it shares no code with <see cref="LegalMoves"/>.
        /// </summary>
        private static CardSet FlowchartOracle(CardSet handSet, Trick trick, RuleSet rules)
        {
            List<Card> hand = Cards(handSet);
            var played = new List<Card>();
            for (int i = 0; i < trick.Count; i++)
            {
                played.Add(trick[i]);
            }

            // "Are you leading this trick?" -> play any card.
            if (played.Count == 0)
            {
                return handSet;
            }

            Suit led = played[0].Suit;
            List<Card> ledCards = hand.Where(c => c.Suit == led).ToList();

            // "Do you hold the led suit?"
            if (ledCards.Count > 0)
            {
                // "Has the trick been trumped? (a spade played on a non-spade lead)" -> any card of the led suit.
                bool trumped = led != Suit.Spades && played.Any(c => c.Suit == Suit.Spades);
                if (!rules.MustBeatLedSuit || trumped)
                {
                    return SetOf(ledCards);
                }

                // "Can you beat the highest led-suit card so far?" -> must play a higher card of the led suit.
                Rank best = played.Where(c => c.Suit == led).Max(c => c.Rank);
                List<Card> higher = ledCards.Where(c => c.Rank > best).ToList();
                return higher.Count > 0 ? SetOf(higher) : SetOf(ledCards);
            }

            // Void in the led suit. "Out of the led suit -> must play a spade if you can."
            List<Card> mySpades = hand.Where(c => c.Suit == Suit.Spades).ToList();
            if (!rules.MustTrumpWhenVoid || mySpades.Count == 0)
            {
                return handSet;
            }

            // "Do you hold a spade that beats every spade in the trick? (any spade, if none yet)"
            List<Card> trickSpades = played.Where(c => c.Suit == Suit.Spades).ToList();
            if (!rules.MustOvertrump || trickSpades.Count == 0)
            {
                return SetOf(mySpades);
            }

            Rank topSpade = trickSpades.Max(c => c.Rank);
            List<Card> beating = mySpades.Where(c => c.Rank > topSpade).ToList();
            if (beating.Count > 0)
            {
                return SetOf(beating);
            }

            // "If you can't beat the highest spade, any card is legal (false = still must play a spade)."
            return rules.DiscardWhenCannotOvertrump ? handSet : SetOf(mySpades);
        }

        private static List<Card> Cards(CardSet set)
        {
            var list = new List<Card>();
            foreach (Card card in set)
            {
                list.Add(card);
            }

            return list;
        }

        private static CardSet SetOf(IEnumerable<Card> cards)
        {
            CardSet set = CardSet.Empty;
            foreach (Card card in cards)
            {
                set = set.With(card);
            }

            return set;
        }
    }
}
