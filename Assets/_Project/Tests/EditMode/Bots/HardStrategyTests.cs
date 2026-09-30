using System;
using System.Collections.Generic;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>
    /// GDD §9 "Hard": Monte-Carlo sampling of hidden hands with Normal-policy rollouts, fixed seed per decision
    /// so replays are reproducible.
    /// </summary>
    public sealed class HardStrategyTests
    {
        private const string SampleHand = "A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣";

        [Test]
        public void Defaults_SixtyFourSamplesPerCardAndFourTimesAsManyPerCall()
        {
            var hard = new HardStrategy();

            Assert.AreEqual(BotLevel.Hard, hard.Level);
            Assert.AreEqual(64, hard.Samples);
            Assert.AreEqual(256, hard.CallSamples);
            Assert.AreEqual(128, new HardStrategy(128).Samples);
            Assert.AreEqual(512, new HardStrategy(128).CallSamples);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(HardStrategy.MaxSamples + 1)]
        public void Constructor_SamplesOutOfRange_Throws(int samples)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HardStrategy(samples));
        }

        // ---- Determinism (GDD §9 "the seed is fixed per decision, so replays are reproducible") ---------

        [Test]
        public void ChooseCard_SameViewAndSeed_SameCardAtEveryDecisionOfAMatch()
        {
            var hard = new HardStrategy(12);
            var seats = new IBotStrategy[] { hard, new NormalStrategy(), new EasyStrategy(), new NormalStrategy() };
            int compared = 0;
            BotMatches.Play(RuleSet.NepaliStandard.With(rounds: 2), 31UL, seats, null, (engine, view) =>
            {
                if (view.Viewer != Seat.South)
                {
                    return;
                }

                ulong seed = 1000UL + (ulong)compared;
                Card first = hard.ChooseCard(view, new Pcg32Random(seed));
                Card second = hard.ChooseCard(view, new Pcg32Random(seed));
                Assert.AreEqual(first, second);
                Assert.IsTrue(view.LegalCards().Contains(first));
                compared++;
            });

            Assert.AreEqual(24, compared, "12 free tricks a round");
        }

        [Test]
        public void ChooseCall_SameViewAndSeed_SameCallInRange()
        {
            PlayerView view = CallTurn(SampleHand);
            var hard = new HardStrategy();

            for (ulong seed = 1; seed <= 2; seed++)
            {
                int first = hard.ChooseCall(view, new Pcg32Random(seed));
                int second = hard.ChooseCall(view, new Pcg32Random(seed));

                Assert.AreEqual(first, second);
                Assert.That(first, Is.InRange(3, 6), "the GDD sample hand is worth about 4.5 tricks");
            }
        }

        // ---- Calls -------------------------------------------------------------------------------------

        [Test]
        public void ChooseCall_NoCallSamples_TheEstimatorsCall()
        {
            var estimatorOnly = new HardStrategy(64, HardStrategy.SpoilPercent, 0, 1, 0);
            var rng = new SequenceRandom(new int[0]);

            Assert.AreEqual(4, estimatorOnly.ChooseCall(CallTurn(SampleHand), rng));
            Assert.AreEqual(0, rng.Draws);
        }

        [Test]
        public void ChooseCall_RoundAlreadyUnderWay_TheEstimatorsCall()
        {
            PlayerView view = BotViews.For(Seat.East, SampleHand).Current(Seat.South, "2♥").Build();

            Assert.AreEqual(4, new HardStrategy().ChooseCall(view, new Pcg32Random(1UL)));
        }

        [Test]
        public void ChooseCall_WorthlessAndUnbeatableHands_LowestAndHighCalls()
        {
            var hard = new HardStrategy();
            PlayerView worthless = CallTurn("7♥ 6♥ 5♥ 4♥ 7♦ 6♦ 5♦ 4♦ 7♣ 6♣ 5♣ 4♣ 3♣");
            PlayerView unbeatable = CallTurn("A♠ K♠ Q♠ J♠ 10♠ 9♠ 8♠ 7♠ A♥ A♦ A♣ K♣ Q♣");

            Assert.AreEqual(1, hard.ChooseCall(worthless, new Pcg32Random(2UL)));
            Assert.AreEqual(8, hard.ChooseCall(unbeatable, new Pcg32Random(2UL)), "eight top spades are eight tricks; MaxCall is 8");
        }

        [Test]
        public void BestCall_AlwaysTheSameTricks_CallsThem()
        {
            var tricks = new int[14];
            tricks[4] = 100;

            Assert.AreEqual(4, HardStrategy.BestCall(tricks, RuleSet.NepaliStandard, 0));
        }

        [Test]
        public void BestCall_HigherCallMadeOnlyHalfTheTime_StaysLow()
        {
            // Calling 4: 50 × 4.0 − 50 × 4.0 = 0. Calling 3: always made, 3.0 or 3.1.
            var tricks = new int[14];
            tricks[3] = 50;
            tricks[4] = 50;

            Assert.AreEqual(3, HardStrategy.BestCall(tricks, RuleSet.NepaliStandard, 0));
        }

        [Test]
        public void BestCall_HigherCallMadeNineTimesInTen_RaisesBecauseTheGainOutweighsTheRisk()
        {
            // Calling 4: 90 × 4.0 − 10 × 4.0 = 320. Calling 3: 10 × 3.0 + 90 × 3.1 = 309.
            var tricks = new int[14];
            tricks[3] = 10;
            tricks[4] = 90;

            Assert.AreEqual(4, HardStrategy.BestCall(tricks, RuleSet.NepaliStandard, 0));
        }

        [Test]
        public void BestCall_CallEdgeCreditsOneMoreTrick_TipsACloseCall()
        {
            // Made 85% of the time, calling 4 scores 280 against 308.5 for calling 3. With one rollout in three
            // worth a trick more, the 4 is made 90% of the time and scores more.
            var tricks = new int[14];
            tricks[3] = 15;
            tricks[4] = 85;

            Assert.AreEqual(3, HardStrategy.BestCall(tricks, RuleSet.NepaliStandard, 0));
            Assert.AreEqual(4, HardStrategy.BestCall(tricks, RuleSet.NepaliStandard, HardStrategy.CallEdgePercent));
        }

        [Test]
        public void BestCall_NoTricksToBeHad_TheLowestCall()
        {
            var noTricks = new int[14];
            noTricks[0] = 10;

            Assert.AreEqual(1, HardStrategy.BestCall(noTricks, RuleSet.NepaliStandard, 0));
            Assert.AreEqual(2, HardStrategy.BestCall(noTricks, RuleSet.NepaliStandard.With(minCall: 2), 0));
            Assert.AreEqual(1, HardStrategy.BestCall(new int[14], RuleSet.NepaliStandard, 0), "equal totals go to the lower call");
        }

        [Test]
        public void BestCall_BigCallBonus_WorthARiskierEight()
        {
            // 8 made 70% of the time: 560 − 240 = 320 without the bonus, 910 − 240 = 670 with it; calling 7 scores 707.
            var tricks = new int[14];
            tricks[7] = 30;
            tricks[8] = 70;
            RuleSet bonus = RuleSet.NepaliStandard.With(bigCallBonus: true);

            Assert.AreEqual(7, HardStrategy.BestCall(tricks, RuleSet.NepaliStandard, 0));
            Assert.AreEqual(7, HardStrategy.BestCall(tricks, bonus, 0));
            tricks[7] = 20;
            tricks[8] = 80;
            Assert.AreEqual(7, HardStrategy.BestCall(tricks, RuleSet.NepaliStandard, 0), "8 at 80%: 640 − 160 = 480 < 708");
            Assert.AreEqual(8, HardStrategy.BestCall(tricks, bonus, 0), "8 at 80% with the bonus: 1040 − 160 = 880 > 708");
        }

        // ---- Card play ---------------------------------------------------------------------------------

        [Test]
        public void ChooseCard_OneLegalCard_ReturnedWithoutDrawing()
        {
            PlayerView view = BotViews.For(Seat.East, "A♥ 8♥ 3♥ K♠ 7♠ 2♠ Q♦ 9♦ 5♦ A♣ J♣ 6♣ 2♣")
                .Current(Seat.South, "K♥")
                .Build();
            var rng = new SequenceRandom(new int[0]);

            Assert.AreEqual(C("A♥"), new HardStrategy().ChooseCard(view, rng));
            Assert.AreEqual(0, rng.Draws);
        }

        [Test]
        public void ChooseCard_OnlyInterchangeableCardsLegal_ReturnsTheLowestWithoutDrawing()
        {
            // The Q♥ and K♥ both beat the J♥ and nothing ranks between them: there is nothing to decide.
            PlayerView view = BotViews.For(Seat.East, "K♥ Q♥ 3♥ K♠ 7♠ 2♠ Q♦ 9♦ 5♦ A♣ J♣ 6♣ 2♣")
                .Current(Seat.South, "J♥")
                .Build();
            var rng = new SequenceRandom(new int[0]);

            Assert.AreEqual(C("Q♥"), new HardStrategy().ChooseCard(view, rng));
            Assert.AreEqual(0, rng.Draws);
        }

        [Test]
        public void ChooseCard_AnySeed_ALegalCard()
        {
            PlayerView view = BotViews.For(Seat.South, SampleHand).Build();
            var hard = new HardStrategy(8);

            for (ulong seed = 0; seed < 12; seed++)
            {
                Assert.IsTrue(view.LegalCards().Contains(hard.ChooseCard(view, new Pcg32Random(seed))));
            }
        }

        [Test]
        public void ChooseCard_HandCountsDoNotAddUp_PlaysTheNormalCard()
        {
            PlayerView sound = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♦ 9♦ 6♦ 2♦ Q♥ 7♥ 3♥ J♣ 8♣ 5♣").Build();
            PlayerView malformed = BotViews.WithHandCounts(sound, 13, 12, 13, 13);
            var rng = new SequenceRandom(new int[0]);

            Assert.AreEqual(C("A♦"), new HardStrategy().ChooseCard(malformed, rng));
            Assert.AreEqual(0, rng.Draws);
        }

        [Test]
        public void ChooseCard_ACallIsMissing_PlaysTheNormalCard()
        {
            PlayerView view = BotViews.For(Seat.South, "K♠ 8♠ 4♠ A♦ 9♦ 6♦ 2♦ Q♥ 7♥ 3♥ J♣ 8♣ 5♣").Calls(3, 3, -1, 3).Build();
            var rng = new SequenceRandom(new int[0]);

            Assert.AreEqual(C("A♦"), new HardStrategy().ChooseCard(view, rng));
            Assert.AreEqual(0, rng.Draws);
        }

        [Test]
        public void ChooseCard_ImpossibleHistory_StillALegalCard()
        {
            // All three opponents "showed out" of hearts while hearts are still unseen: the contradictory
            // exclusions are dropped rather than the bot giving up.
            PlayerView view = BotViews.For(Seat.South, "K♥ 9♥ A♠ 7♠ 4♠ K♦ 5♦ 2♦ A♣ 9♣ 6♣ 2♣")
                .Trick(Seat.South, "2♥ 3♣ 4♣ 5♣")
                .Build();

            Card card = new HardStrategy(16).ChooseCard(view, new Pcg32Random(6UL));

            Assert.IsTrue(view.LegalCards().Contains(card));
        }

        [Test]
        public void ChooseCard_NotTheViewersPlayTurn_Throws()
        {
            Assert.Throws<InvalidOperationException>(() => new HardStrategy().ChooseCard(CallTurn(SampleHand), new Pcg32Random(1UL)));
        }

        [Test]
        public void ChooseCallAndCard_NullArguments_Throw()
        {
            var hard = new HardStrategy();
            PlayerView view = BotViews.For(Seat.South, SampleHand).Build();

            Assert.Throws<ArgumentNullException>(() => hard.ChooseCall(null, new Pcg32Random(1UL)));
            Assert.Throws<ArgumentNullException>(() => hard.ChooseCall(view, null));
            Assert.Throws<ArgumentNullException>(() => hard.ChooseCard(null, new Pcg32Random(1UL)));
            Assert.Throws<ArgumentNullException>(() => hard.ChooseCard(view, null));
        }

        // ---- Candidates --------------------------------------------------------------------------------

        [Test]
        public void Candidates_NeighbouringCards_OneCandidatePerGroupCheapestFirst()
        {
            // Hearts 9 8 7 are interchangeable, the Q♥ is not (10♥ and J♥ are out); 3♦ and 2♦ are one group.
            PlayerView view = BotViews.For(Seat.South, "A♠ K♠ 5♠ Q♥ 9♥ 8♥ 7♥ 3♦ 2♦ K♣ 9♣ 6♣ 4♣").Build();
            var into = new Card[13];

            int count = HardStrategy.Candidates(new CardTracker(view), view.LegalCards(), into);

            var candidates = new List<Card>();
            for (int i = 0; i < count; i++)
            {
                candidates.Add(into[i]);
            }

            CollectionAssert.AreEqual(
                new[] { C("2♦"), C("4♣"), C("6♣"), C("7♥"), C("9♣"), C("Q♥"), C("K♣"), C("5♠"), C("K♠") },
                candidates);
        }

        [Test]
        public void Candidates_CardBetweenWasPlayedInAnEarlierTrick_StillOneGroup()
        {
            // The 8♥ went in the first trick, so the 9♥ and 7♥ are now neighbours.
            PlayerView view = BotViews.For(Seat.South, "9♥ 7♥ A♠ K♠ 5♠ 3♦ K♣ 9♣ 6♣ 4♣ 3♣ 2♣")
                .Trick(Seat.South, "A♥ 8♥ 2♥ 3♥")
                .Build();
            var into = new Card[12];

            int count = HardStrategy.Candidates(new CardTracker(view), view.Hand.InSuit(Suit.Hearts), into);

            Assert.AreEqual(1, count);
            Assert.AreEqual(C("7♥"), into[0]);
        }

        [Test]
        public void Candidates_CardBetweenIsInTheCurrentTrick_SeparateCandidates()
        {
            // Without the must-beat rule the 5♥ loses to the 6♥ on the table and the 7♥ beats it.
            PlayerView view = BotViews.For(Seat.East, "7♥ 5♥ A♠ K♠ 5♠ 3♦ 2♦ K♣ 9♣ 6♣ 4♣ 3♣ 2♣")
                .Rules(RuleSet.NepaliStandard.With(mustBeatLedSuit: false))
                .Current(Seat.South, "6♥")
                .Build();
            var into = new Card[13];

            int count = HardStrategy.Candidates(new CardTracker(view), view.LegalCards(), into);

            Assert.AreEqual(2, count);
            Assert.AreEqual(C("5♥"), into[0]);
            Assert.AreEqual(C("7♥"), into[1]);
        }

        // ---- Calls as hints ----------------------------------------------------------------------------

        [Test]
        public void CallMismatch_SampledHandsAgainstTheCalls_CountsTricksBeyondOneOff()
        {
            // East called 5. A hand the estimator would call 1 is three tricks beyond the ±1 allowance.
            PlayerView view = BotViews.For(Seat.South, SampleHand).Calls(3, 5, -1, 1).Build();
            var tracker = new CardTracker(view);
            var hands = new CardSet[4];
            hands[(int)Seat.South] = view.Hand;
            hands[(int)Seat.East] = Set("7♥ 6♥ 5♥ 4♥ 7♦ 6♦ 4♦ 3♦ 6♣ 5♣ 4♣ 3♣ 2♣");
            hands[(int)Seat.North] = Set("Q♠ 10♠ 8♠ 7♠ 6♠ K♥ Q♥ 10♥ A♦ Q♦ A♣ K♣ J♣");
            hands[(int)Seat.West] = Set("5♠ 3♠ 2♠ 9♥ 3♥ 2♥ J♦ 9♦ 8♦ 2♦ 10♣ 9♣ 8♣");

            Assert.AreEqual(3, HardStrategy.CallMismatch(tracker, hands));

            hands[(int)Seat.East] = hands[(int)Seat.North];
            hands[(int)Seat.North] = Set("7♥ 6♥ 5♥ 4♥ 7♦ 6♦ 4♦ 3♦ 6♣ 5♣ 4♣ 3♣ 2♣");
            Assert.AreEqual(0, HardStrategy.CallMismatch(tracker, hands), "the strong hand fits the 5; North has not called");
        }

        // ---- Rollouts ----------------------------------------------------------------------------------

        [Test]
        public void Rollout_TrueHandsAndTheNormalCard_EndsExactlyLikeTheRealGame()
        {
            // Four Normal bots play. At every decision a rollout is given the real hands and the card Normal is
            // about to play; it must finish the round with the very tricks the real game then produces.
            var normal = new NormalStrategy();
            var seats = new IBotStrategy[] { normal, normal, normal, normal };
            var predictions = new List<int[]>();
            PlayerView final = BotMatches.Play(RuleSet.NepaliStandard.With(rounds: 3), 58UL, seats, null, (engine, view) =>
            {
                var rollout = new Rollout(new CardTracker(view), 0);
                rollout.Run(BotMatches.TrueHands(engine), normal.ChooseCard(view, new Pcg32Random(1UL)));
                predictions.Add(new[]
                {
                    view.Round,
                    rollout.TricksWonBy(Seat.South),
                    rollout.TricksWonBy(Seat.East),
                    rollout.TricksWonBy(Seat.North),
                    rollout.TricksWonBy(Seat.West),
                });
            });

            Assert.AreEqual(3 * 48, predictions.Count);
            foreach (int[] prediction in predictions)
            {
                RoundResult real = final.History[prediction[0] - 1];
                for (int s = 0; s < 4; s++)
                {
                    Assert.AreEqual(real[(Seat)s].Won, prediction[s + 1], "round " + prediction[0] + " seat " + (Seat)s);
                }
            }
        }

        [Test]
        public void ChooseCard_FourTimesTheSamples_AllocatesNothingMore()
        {
            // Only the per-decision buffers are allocated; the rollouts themselves allocate nothing.
            PlayerView view = BotViews.For(Seat.South, SampleHand).Build();
            var few = new HardStrategy(2);
            var many = new HardStrategy(8);

            long fewBytes = AllocationProbe.BytesAllocatedBy(() => few.ChooseCard(view, new Pcg32Random(3UL)), 4);
            long manyBytes = AllocationProbe.BytesAllocatedBy(() => many.ChooseCard(view, new Pcg32Random(3UL)), 4);

            Assert.AreEqual(fewBytes, manyBytes);
            Assert.Less(fewBytes / 4, 4096L, "bytes per decision");
        }

        [Test]
        public void ChooseCall_FourTimesTheSamples_AllocatesNothingMore()
        {
            PlayerView view = CallTurn(SampleHand);
            var few = new HardStrategy(2);
            var many = new HardStrategy(8);

            long fewBytes = AllocationProbe.BytesAllocatedBy(() => few.ChooseCall(view, new Pcg32Random(3UL)), 4);
            long manyBytes = AllocationProbe.BytesAllocatedBy(() => many.ChooseCall(view, new Pcg32Random(3UL)), 4);

            Assert.AreEqual(fewBytes, manyBytes);
            Assert.Less(fewBytes / 4, 4096L, "bytes per decision");
        }

        private static PlayerView CallTurn(string hand)
        {
            return BotViews.For(Seat.South, hand).Calls(-1, -1, -1, -1).Calling().Build();
        }
    }
}
