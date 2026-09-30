using System;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>GDD §9 "Call estimator (all levels)": the worked example and every rule line.</summary>
    public sealed class CallEstimatorTests
    {
        /// <summary>The GDD §2 sample hand (bidding mockup 07).</summary>
        private const string SampleHand = "A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣";

        // Two low cards in a side suit: no honour, no void, no singleton.
        private const string LowHearts = " 3♥ 2♥";
        private const string LowDiamonds = " 3♦ 2♦";
        private const string LowClubs = " 3♣ 2♣";
        private const string LowSideSuits = LowHearts + LowDiamonds + LowClubs;

        [Test]
        public void EstimateHundredths_GddSampleHand_FourAndAHalfTricks()
        {
            // A♠ 1.0 + K♠ 1.0 + two long spades 1.0 + A♥ 1.0 + K♦ 0.5 = 4.5 (GDD §9 "Worked").
            Assert.AreEqual(450, CallEstimator.EstimateHundredths(Set(SampleHand)));
        }

        [Test]
        public void Call_GddSampleHand_Four()
        {
            // ⌊4.5 + 0.3⌋ = 4, "the call selected in mockup 07".
            Assert.AreEqual(4, CallEstimator.Call(Set(SampleHand), RuleSet.NepaliStandard));
        }

        [TestCase("A♠", 100, TestName = "EstimateHundredths_AceOfSpades_One")]
        [TestCase("K♠", 50, TestName = "EstimateHundredths_BareKingOfSpades_Half")]
        [TestCase("K♠ 2♠", 100, TestName = "EstimateHundredths_KingOfSpadesWithTwoSpades_One")]
        [TestCase("Q♠ 2♠", 0, TestName = "EstimateHundredths_QueenOfSpadesWithTwoSpades_Nothing")]
        [TestCase("Q♠ 3♠ 2♠", 75, TestName = "EstimateHundredths_QueenOfSpadesWithThreeSpades_ThreeQuarters")]
        [TestCase("A♠ K♠ Q♠", 275, TestName = "EstimateHundredths_AceKingQueenOfSpades_TwoAndThreeQuarters")]
        [TestCase("5♠ 4♠ 3♠", 0, TestName = "EstimateHundredths_ThreeLowSpades_Nothing")]
        [TestCase("5♠ 4♠ 3♠ 2♠", 50, TestName = "EstimateHundredths_FourthSpade_Half")]
        [TestCase("6♠ 5♠ 4♠ 3♠ 2♠", 100, TestName = "EstimateHundredths_FourthAndFifthSpade_One")]
        [TestCase("A♠ K♠ J♠ 9♠ 4♠", 300, TestName = "EstimateHundredths_SampleHandSpades_Three")]
        [TestCase("A♠ K♠ Q♠ J♠ 10♠ 9♠ 8♠", 475, TestName = "EstimateHundredths_SevenSpades_HonoursPlusFourLong")]
        public void EstimateHundredths_SpadeLines(string spades, int expected)
        {
            Assert.AreEqual(expected, CallEstimator.EstimateHundredths(Set(spades + LowSideSuits)));
        }

        [TestCase("A♥ 3♥ 2♥", 100, TestName = "EstimateHundredths_SideAce_One")]
        [TestCase("A♥ 6♥ 5♥ 3♥ 2♥", 100, TestName = "EstimateHundredths_SideAceInFiveCardSuit_One")]
        [TestCase("A♥ 7♥ 6♥ 5♥ 3♥ 2♥", 80, TestName = "EstimateHundredths_SideAceInSixCardSuit_PointEight")]
        [TestCase("K♥", 0, TestName = "EstimateHundredths_SideBareKing_Nothing")]
        [TestCase("K♥ 2♥", 50, TestName = "EstimateHundredths_SideKingInTwoCardSuit_Half")]
        [TestCase("K♥ 4♥ 3♥ 2♥", 50, TestName = "EstimateHundredths_SideKingInFourCardSuit_Half")]
        [TestCase("K♥ 5♥ 4♥ 3♥ 2♥", 0, TestName = "EstimateHundredths_SideKingInFiveCardSuit_Nothing")]
        [TestCase("Q♥ 2♥", 0, TestName = "EstimateHundredths_SideQueenInTwoCardSuit_Nothing")]
        [TestCase("Q♥ 3♥ 2♥", 25, TestName = "EstimateHundredths_SideQueenInThreeCardSuit_Quarter")]
        [TestCase("Q♥ 4♥ 3♥ 2♥", 25, TestName = "EstimateHundredths_SideQueenInFourCardSuit_Quarter")]
        [TestCase("Q♥ 5♥ 4♥ 3♥ 2♥", 0, TestName = "EstimateHundredths_SideQueenInFiveCardSuit_Nothing")]
        [TestCase("A♥ K♥ Q♥", 175, TestName = "EstimateHundredths_SideAceKingQueen_OneAndThreeQuarters")]
        [TestCase("A♥ K♥ Q♥ 5♥ 4♥ 3♥", 80, TestName = "EstimateHundredths_SideSixCardSuit_OnlyTheAceAtPointEight")]
        [TestCase("J♥ 10♥ 9♥", 0, TestName = "EstimateHundredths_SideNoHonour_Nothing")]
        public void EstimateHundredths_SideSuitLines(string hearts, int expected)
        {
            // No spades, so a short heart suit earns nothing for ruffing.
            Assert.AreEqual(expected, CallEstimator.EstimateHundredths(Set(hearts + LowDiamonds + LowClubs)));
        }

        [TestCase("A♦ 3♦ 2♦", TestName = "EstimateHundredths_DiamondAce_One")]
        [TestCase("A♣ 3♣ 2♣", TestName = "EstimateHundredths_ClubAce_One")]
        public void EstimateHundredths_EverySideSuit_CountsItsAce(string suit)
        {
            Assert.AreEqual(100, CallEstimator.EstimateHundredths(Set(suit + " 5♥ 4♥")));
        }

        [TestCase("2♠" + LowDiamonds + LowClubs, 80, TestName = "EstimateHundredths_VoidWithASpareSpade_PointEight")]
        [TestCase(LowDiamonds + LowClubs, 0, TestName = "EstimateHundredths_VoidWithoutSpades_Nothing")]
        [TestCase("A♠" + LowDiamonds + LowClubs, 100, TestName = "EstimateHundredths_VoidButTheOnlySpadeIsTheAce_NoBonus")]
        [TestCase("K♠" + LowDiamonds + LowClubs, 50, TestName = "EstimateHundredths_VoidButTheOnlySpadeIsTheKing_NoBonus")]
        [TestCase("A♠ 2♠" + LowDiamonds + LowClubs, 180, TestName = "EstimateHundredths_VoidWithAceAndASpareSpade_AcePlusVoid")]
        [TestCase("Q♠ 2♠" + LowDiamonds + LowClubs, 80, TestName = "EstimateHundredths_UncountedQueenIsSpare_VoidCounts")]
        [TestCase("5♠ 4♠ 3♠ 2♠" + LowDiamonds + LowClubs, 130, TestName = "EstimateHundredths_LongSpadesStaySpare_LengthPlusVoid")]
        [TestCase("2♠ 9♥" + LowDiamonds + LowClubs, 40, TestName = "EstimateHundredths_SingletonWithASpareSpade_PointFour")]
        [TestCase("9♥" + LowDiamonds + LowClubs, 0, TestName = "EstimateHundredths_SingletonWithoutSpades_Nothing")]
        [TestCase("2♠ A♥" + LowDiamonds + LowClubs, 140, TestName = "EstimateHundredths_SingletonAce_AcePlusSingleton")]
        [TestCase("2♠" + LowClubs, 80, TestName = "EstimateHundredths_TwoVoidsOneSpareSpade_OneVoidCounts")]
        [TestCase("3♠ 2♠" + LowClubs, 160, TestName = "EstimateHundredths_TwoVoidsTwoSpareSpades_BothCount")]
        [TestCase("2♠ 9♥" + LowClubs, 80, TestName = "EstimateHundredths_VoidAndSingletonOneSpareSpade_TheVoidTakesIt")]
        [TestCase("3♠ 2♠ 9♥" + LowClubs, 120, TestName = "EstimateHundredths_VoidAndSingletonTwoSpareSpades_BothCount")]
        [TestCase("4♠ 3♠ 2♠", 240, TestName = "EstimateHundredths_ThreeVoidsThreeSpareSpades_AllCount")]
        public void EstimateHundredths_ShortSuitLines(string hand, int expected)
        {
            Assert.AreEqual(expected, CallEstimator.EstimateHundredths(Set(hand)));
        }

        [Test]
        public void EstimateHundredths_EmptyHand_Zero()
        {
            Assert.AreEqual(0, CallEstimator.EstimateHundredths(CardSet.Empty));
        }

        [TestCase(0, 0)]
        [TestCase(69, 0)]
        [TestCase(70, 1)]
        [TestCase(169, 1)]
        [TestCase(170, 2)]
        [TestCase(450, 4)]
        [TestCase(469, 4)]
        [TestCase(470, 5)]
        public void RoundDown_EstimatePlusPointThree_Floored(int hundredths, int expected)
        {
            Assert.AreEqual(expected, CallEstimator.RoundDown(hundredths));
        }

        [Test]
        public void RoundDown_NegativeEstimate_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CallEstimator.RoundDown(-1));
        }

        [Test]
        public void Call_WorthlessHand_ClampedToMinCall()
        {
            CardSet hand = Set("7♥ 6♥ 5♥ 4♥ 7♦ 6♦ 5♦ 4♦ 7♣ 6♣ 5♣ 4♣ 3♣");

            Assert.AreEqual(0, CallEstimator.EstimateHundredths(hand));
            Assert.AreEqual(1, CallEstimator.Call(hand, RuleSet.NepaliStandard));
            Assert.AreEqual(2, CallEstimator.Call(hand, RuleSet.NepaliStandard.With(minCall: 2)));
        }

        [Test]
        public void Call_ThirteenSpades_ClampedToMaxCall()
        {
            CardSet hand = CardSet.OfSuit(Suit.Spades);

            // A 1.0 + K 1.0 + Q 0.75 + ten long spades 5.0 + three voids 2.4 = 10.15.
            Assert.AreEqual(1015, CallEstimator.EstimateHundredths(hand));
            Assert.AreEqual(8, CallEstimator.Call(hand, RuleSet.NepaliStandard));
            Assert.AreEqual(10, CallEstimator.Call(hand, RuleSet.NepaliStandard.With(maxCall: 13)));
        }

        [Test]
        public void Call_NullRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => CallEstimator.Call(Set(SampleHand), null));
        }
    }
}
