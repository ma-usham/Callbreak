using System;
using System.Diagnostics;
using System.Globalization;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>
    /// The GDD §9 Bot Arena: "target: one Hard bot wins ≥ 35% against three Normal bots". Every arena plays the
    /// same seeded matches (the deals depend only on the seed, not on the bots), with the hero seat rotating
    /// through the four seats; a win is finishing first, alone or shared. The numbers are written to the test
    /// output. Slow: run with <c>--filter "TestCategory=Slow"</c>.
    /// </summary>
    [Explicit("Arena of 1,000 matches per test; run with --filter \"TestCategory=Slow\"")]
    [Category("Slow")]
    public sealed class BotArenaTests
    {
        private const int Matches = 1000;
        private const ulong FirstSeed = 1000UL;

        [Test]
        public void Arena_OneHardAgainstThreeNormal_WinsAtLeastThirtyFivePercent()
        {
            Result result = Run("Hard vs 3 Normal", new HardStrategy(), new NormalStrategy());

            Assert.GreaterOrEqual(result.WinRate, 0.35, "GDD §9 target");
        }

        [Test]
        public void Arena_FourNormal_EachWinsAboutAQuarter()
        {
            Result result = Run("Normal vs 3 Normal (baseline)", new NormalStrategy(), new NormalStrategy());

            Assert.That(result.WinRate, Is.InRange(0.20, 0.30));
        }

        [Test]
        public void Arena_OneHardWithEstimatorCallsAgainstThreeNormal_CardPlayAloneIsASmallEdge()
        {
            // What the Monte-Carlo card play is worth without the calibrated calls.
            var estimatorCalls = new HardStrategy(HardStrategy.DefaultSamples, HardStrategy.SpoilPercent, 0, HardStrategy.DefaultHintTries, 0);

            Result result = Run("Hard with estimator calls vs 3 Normal", estimatorCalls, new NormalStrategy());

            Assert.Greater(result.WinRate, 0.25);
        }

        [Test]
        public void Arena_OneNormalAgainstThreeEasy_WinsMoreThanItsShare()
        {
            Result result = Run("Normal vs 3 Easy", new NormalStrategy(), new EasyStrategy());

            Assert.Greater(result.WinRate, 0.35);
        }

        [Test]
        public void Arena_OneHardAgainstThreeEasy_WinsMoreThanNormalDoes()
        {
            Result result = Run("Hard vs 3 Easy", new HardStrategy(), new EasyStrategy());

            Assert.Greater(result.WinRate, 0.45);
        }

        [Test]
        public void Timing_HardestHardDecisions_WellInsideTheBudget()
        {
            // GDD §9: "≤ 40 ms per decision on a mid-range phone". The costliest card decision is the first lead
            // with thirteen cards none of which are interchangeable; the call uses four times the samples.
            PlayerView lead = BotViews.For(Seat.South, "A♠ Q♠ 10♠ 8♠ K♥ J♥ 9♥ 7♥ A♦ Q♦ 10♦ K♣ J♣").Build();
            PlayerView call = BotViews.For(Seat.South, "A♠ Q♠ 10♠ 8♠ K♥ J♥ 9♥ 7♥ A♦ Q♦ 10♦ K♣ J♣").Calls(3, 4, 2, -1).Calling().Build();
            var hard = new HardStrategy();
            var normal = new NormalStrategy();
            var into = new Card[13];
            Assert.AreEqual(13, HardStrategy.Candidates(new CardTracker(lead), lead.LegalCards(), into));

            double hardCard = MeanMilliseconds(() => hard.ChooseCard(lead, new Pcg32Random(7UL)), 100);
            double hardCall = MeanMilliseconds(() => hard.ChooseCall(call, new Pcg32Random(7UL)), 100);
            double normalCard = MeanMilliseconds(() => normal.ChooseCard(lead, new Pcg32Random(7UL)), 20000);

            TestContext.Out.WriteLine("Hard first lead, 13 candidates x 64 samples: " + hardCard.ToString("0.000", CultureInfo.InvariantCulture) + " ms");
            TestContext.Out.WriteLine("Hard call, 256 samples: " + hardCall.ToString("0.000", CultureInfo.InvariantCulture) + " ms");
            TestContext.Out.WriteLine("Normal first lead: " + normalCard.ToString("0.0000", CultureInfo.InvariantCulture) + " ms");
            Assert.Less(hardCard, 20.0, "aim: well under 20 ms on a desktop");
            Assert.Less(hardCall, 20.0);
            Assert.Less(normalCard, 2.0, "GDD §9 Normal budget");
        }

        private static double MeanMilliseconds(Action decide, int repeats)
        {
            for (int i = 0; i < 5; i++)
            {
                decide();
            }

            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < repeats; i++)
            {
                decide();
            }

            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency / repeats;
        }

        private static Result Run(string name, IBotStrategy hero, IBotStrategy field)
        {
            var timings = new DecisionTimings();
            var result = new Result();
            for (int match = 0; match < Matches; match++)
            {
                var heroSeat = (Seat)(match % 4);
                var seats = new IBotStrategy[4];
                for (int s = 0; s < 4; s++)
                {
                    seats[s] = s == (int)heroSeat ? hero : field;
                }

                PlayerView final = BotMatches.Play(RuleSet.NepaliStandard, FirstSeed + (ulong)match, seats, timings);
                result.Add(final, heroSeat);
            }

            TestContext.Out.WriteLine(name + ", " + Matches + " matches: " + result.Describe());
            TestContext.Out.WriteLine("  " + timings.Describe(hero.Level));
            if (field.Level != hero.Level)
            {
                TestContext.Out.WriteLine("  " + timings.Describe(field.Level));
            }

            return result;
        }

        /// <summary>The hero seat's wins, calls and scores over an arena, and the field's for comparison.</summary>
        private sealed class Result
        {
            private int _matches;
            private int _wins;
            private int _heroRounds;
            private int _heroMade;
            private long _heroCalls;
            private long _heroTricks;
            private long _heroTenths;
            private int _fieldRounds;
            private int _fieldMade;
            private long _fieldCalls;
            private long _fieldTenths;

            public double WinRate => (double)_wins / _matches;

            public void Add(PlayerView final, Seat hero)
            {
                _matches++;
                if (BotMatches.Won(final.FinalStandings, hero))
                {
                    _wins++;
                }

                for (int r = 0; r < final.History.Count; r++)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        SeatRoundResult line = final.History[r][(Seat)s];
                        if ((Seat)s == hero)
                        {
                            _heroRounds++;
                            _heroMade += line.Met ? 1 : 0;
                            _heroCalls += line.Call;
                            _heroTricks += line.Won;
                            _heroTenths += line.DeltaTenths;
                        }
                        else
                        {
                            _fieldRounds++;
                            _fieldMade += line.Met ? 1 : 0;
                            _fieldCalls += line.Call;
                            _fieldTenths += line.DeltaTenths;
                        }
                    }
                }
            }

            public string Describe()
            {
                return "hero wins " + _wins + " (" + Number(100.0 * _wins / _matches, "0.0") + "%)"
                    + ", calls " + Number((double)_heroCalls / _heroRounds, "0.00")
                    + ", makes " + Number(100.0 * _heroMade / _heroRounds, "0.0") + "%"
                    + ", tricks " + Number((double)_heroTricks / _heroRounds, "0.00")
                    + ", round score " + Number(_heroTenths / 10.0 / _heroRounds, "0.00")
                    + " | field calls " + Number((double)_fieldCalls / _fieldRounds, "0.00")
                    + ", makes " + Number(100.0 * _fieldMade / _fieldRounds, "0.0") + "%"
                    + ", round score " + Number(_fieldTenths / 10.0 / _fieldRounds, "0.00");
            }

            private static string Number(double value, string format)
            {
                return value.ToString(format, CultureInfo.InvariantCulture);
            }
        }
    }
}
