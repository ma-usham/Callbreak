using System;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>GDD §9 "Easy": "estimate − 1 (min 1), ±1 random"; "random legal card, biased 60% toward the lowest".</summary>
    public sealed class EasyStrategyTests
    {
        /// <summary>The GDD §2 sample hand: the estimator calls 4.</summary>
        private const string SampleHand = "A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣";

        private const string WorthlessHand = "7♥ 6♥ 5♥ 4♥ 7♦ 6♦ 5♦ 4♦ 7♣ 6♣ 5♣ 4♣ 3♣";

        [Test]
        public void Level_Easy()
        {
            Assert.AreEqual(BotLevel.Easy, new EasyStrategy().Level);
        }

        [TestCase(0, 2)]
        [TestCase(1, 3)]
        [TestCase(2, 4)]
        public void ChooseCall_SampleHand_EstimateMinusOnePlusOrMinusOne(int draw, int expectedCall)
        {
            PlayerView view = CallTurn(SampleHand, RuleSet.NepaliStandard);
            var rng = new SequenceRandom(new[] { draw });

            Assert.AreEqual(expectedCall, new EasyStrategy().ChooseCall(view, rng));
            Assert.AreEqual(1, rng.Draws);
        }

        [TestCase(0, 1)]
        [TestCase(1, 1)]
        [TestCase(2, 2)]
        public void ChooseCall_WorthlessHand_NeverBelowMinCall(int draw, int expectedCall)
        {
            // Estimate 0 − 1 is raised to MinCall 1 first; the random step cannot take it below.
            PlayerView view = CallTurn(WorthlessHand, RuleSet.NepaliStandard);

            Assert.AreEqual(expectedCall, new EasyStrategy().ChooseCall(view, new SequenceRandom(new[] { draw })));
        }

        [Test]
        public void ChooseCall_EstimateAboveMaxCall_ClampedToMaxCall()
        {
            // Estimate 4 − 1 + 1 = 4 with calls capped at 3.
            PlayerView view = CallTurn(SampleHand, RuleSet.NepaliStandard.With(maxCall: 3));

            Assert.AreEqual(3, new EasyStrategy().ChooseCall(view, new SequenceRandom(new[] { 2 })));
        }

        [Test]
        public void ChooseCall_ManySeeds_EachOfTheThreeCallsAboutAThirdOfTheTime()
        {
            PlayerView view = CallTurn(SampleHand, RuleSet.NepaliStandard);
            var easy = new EasyStrategy();
            var rng = new Pcg32Random(8UL);
            var counts = new int[9];

            for (int i = 0; i < 3000; i++)
            {
                counts[easy.ChooseCall(view, rng)]++;
            }

            Assert.AreEqual(3000, counts[2] + counts[3] + counts[4]);
            Assert.That(counts[2], Is.InRange(900, 1100));
            Assert.That(counts[3], Is.InRange(900, 1100));
            Assert.That(counts[4], Is.InRange(900, 1100));
        }

        [TestCase(0)]
        [TestCase(59)]
        public void ChooseCard_RollBelowSixty_TheLowestLegalCard(int roll)
        {
            var rng = new SequenceRandom(new[] { roll });

            Card card = new EasyStrategy().ChooseCard(LeadWithSampleHand(), rng);

            // The lowest-ranked side-suit card; spades are kept.
            Assert.AreEqual(C("5♦"), card);
            Assert.AreEqual(1, rng.Draws);
        }

        [TestCase(60, 0, "7♣")]
        [TestCase(99, 12, "A♠")]
        [TestCase(75, 5, "8♥")]
        public void ChooseCard_RollOfSixtyOrMore_AUniformlyRandomLegalCard(int roll, int index, string expected)
        {
            // Legal cards in id order: 7♣ Q♣ 5♦ 10♦ K♦ 8♥ J♥ A♥ 4♠ 9♠ J♠ K♠ A♠.
            var rng = new SequenceRandom(new[] { roll, index });

            Assert.AreEqual(C(expected), new EasyStrategy().ChooseCard(LeadWithSampleHand(), rng));
            Assert.AreEqual(2, rng.Draws);
        }

        [Test]
        public void ChooseCard_ManySeeds_LowestCardSixtyPercentPlusItsRandomShare()
        {
            PlayerView view = LeadWithSampleHand();
            var easy = new EasyStrategy();
            var rng = new Pcg32Random(3UL);
            int lowest = 0;
            CardSet seen = CardSet.Empty;
            const int Plays = 10000;

            for (int i = 0; i < Plays; i++)
            {
                Card card = easy.ChooseCard(view, rng);
                Assert.IsTrue(view.LegalCards().Contains(card));
                seen = seen.With(card);
                if (card == C("5♦"))
                {
                    lowest++;
                }
            }

            // 60% + 40% / 13 legal cards = 63.1%.
            Assert.That(lowest, Is.InRange(6100, 6500));
            Assert.AreEqual(view.LegalCards(), seen, "every legal card is played sometimes");
        }

        [Test]
        public void ChooseCard_OneLegalCard_ReturnedWithoutDrawing()
        {
            // Only the A♥ beats the K♥.
            PlayerView view = BotViews.For(Seat.East, "A♥ 8♥ 3♥ K♠ 7♠ 2♠ Q♦ 9♦ 5♦ A♣ J♣ 6♣ 2♣")
                .Current(Seat.South, "K♥")
                .Build();
            var rng = new SequenceRandom(new int[0]);

            Assert.AreEqual(C("A♥"), new EasyStrategy().ChooseCard(view, rng));
            Assert.AreEqual(0, rng.Draws);
        }

        [Test]
        public void ChooseCard_SameSeed_SameCards()
        {
            PlayerView view = LeadWithSampleHand();
            var easy = new EasyStrategy();
            var first = new Pcg32Random(77UL);
            var second = new Pcg32Random(77UL);

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(easy.ChooseCard(view, first), easy.ChooseCard(view, second));
            }
        }

        [Test]
        public void ChooseCard_NotTheViewersPlayTurn_Throws()
        {
            PlayerView view = CallTurn(SampleHand, RuleSet.NepaliStandard);

            Assert.Throws<InvalidOperationException>(() => new EasyStrategy().ChooseCard(view, new Pcg32Random(1UL)));
        }

        [Test]
        public void ChooseCallAndCard_NullArguments_Throw()
        {
            var easy = new EasyStrategy();
            PlayerView view = LeadWithSampleHand();

            Assert.Throws<ArgumentNullException>(() => easy.ChooseCall(null, new Pcg32Random(1UL)));
            Assert.Throws<ArgumentNullException>(() => easy.ChooseCall(view, null));
            Assert.Throws<ArgumentNullException>(() => easy.ChooseCard(null, new Pcg32Random(1UL)));
            Assert.Throws<ArgumentNullException>(() => easy.ChooseCard(view, null));
        }

        private static PlayerView CallTurn(string hand, RuleSet rules)
        {
            return BotViews.For(Seat.South, hand).Rules(rules).Calls(-1, -1, -1, -1).Calling().Build();
        }

        private static PlayerView LeadWithSampleHand()
        {
            return BotViews.For(Seat.South, SampleHand).Build();
        }
    }
}
