using System;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Scoring
{
    public sealed class ScoreSheetTests
    {
        // Seats as in GDD FIG 2.1: You = South, Suman = East, Asha = North, Bikash = West.
        private static readonly int[][] MockupCalls =
        {
            new[] { 4, 3, 3, 3 },
            new[] { 5, 2, 3, 2 },
            new[] { 4, 2, 4, 2 },
            new[] { 4, 3, 3, 2 },
            new[] { 4, 2, 3, 3 },
        };

        private static readonly int[][] MockupWon =
        {
            new[] { 5, 3, 3, 2 },
            new[] { 5, 2, 4, 2 },
            new[] { 4, 2, 4, 3 },
            new[] { 5, 3, 3, 2 },
            new[] { 5, 2, 3, 3 },
        };

        private static RuleSet Rules => RuleSet.NepaliStandard;

        public static ScoreSheet MockupMatch()
        {
            ScoreSheet sheet = ScoreSheet.Empty;
            for (int round = 0; round < 5; round++)
            {
                sheet = sheet.Append(MockupCalls[round], MockupWon[round], RuleSet.NepaliStandard);
            }

            return sheet;
        }

        [Test]
        public void Append_GddWorkedExampleRound1_DeltasAndTotals()
        {
            // GDD §4: You 4/5 -> +4.1, Asha 3/3 -> +3.0, Bikash 3/2 -> -3.0, Suman 3/3 -> +3.0.
            ScoreSheet sheet = ScoreSheet.Empty.Append(new[] { 4, 3, 3, 3 }, new[] { 5, 3, 3, 2 }, Rules);
            RoundResult round = sheet.Rounds[0];

            Assert.AreEqual(1, round.Round);
            Assert.AreEqual(41, round[Seat.South].DeltaTenths);
            Assert.AreEqual(30, round[Seat.North].DeltaTenths);
            Assert.AreEqual(-30, round[Seat.West].DeltaTenths);
            Assert.AreEqual(30, round[Seat.East].DeltaTenths);
            Assert.AreEqual(41, sheet.TotalOf(Seat.South));
            Assert.AreEqual(-30, sheet.TotalOf(Seat.West));
            Assert.IsTrue(round[Seat.South].Met);
            Assert.IsFalse(round[Seat.West].Met);
            Assert.AreEqual(4, round[Seat.South].Call);
            Assert.AreEqual(5, round[Seat.South].Won);
            Assert.AreEqual(Seat.West, round[Seat.West].Seat);
        }

        [Test]
        public void Append_MockupFiveRounds_TotalsMatchGddFinalStandings()
        {
            ScoreSheet sheet = MockupMatch();

            Assert.AreEqual(5, sheet.Rounds.Count);
            Assert.AreEqual(213, sheet.TotalOf(Seat.South));
            Assert.AreEqual(161, sheet.TotalOf(Seat.North));
            Assert.AreEqual(120, sheet.TotalOf(Seat.East));
            Assert.AreEqual(61, sheet.TotalOf(Seat.West));
            int[] youByRound = { 41, 50, 40, 41, 41 };
            int[] bikashTotals = { -30, -10, 11, 31, 61 };
            for (int r = 0; r < 5; r++)
            {
                Assert.AreEqual(youByRound[r], sheet.Rounds[r][Seat.South].DeltaTenths);
                Assert.AreEqual(bikashTotals[r], sheet.Rounds[r][Seat.West].TotalTenths);
                Assert.AreEqual(r + 1, sheet.Rounds[r].Round);
            }
        }

        [Test]
        public void TieBreakInputs_MockupMatch_RoundsMetAndBestRound()
        {
            ScoreSheet sheet = MockupMatch();

            Assert.AreEqual(5, sheet.RoundsMetBy(Seat.South));
            Assert.AreEqual(4, sheet.RoundsMetBy(Seat.West));
            Assert.AreEqual(50, sheet.BestRoundOf(Seat.South));
            Assert.AreEqual(40, sheet.BestRoundOf(Seat.North));
            Assert.AreEqual(30, sheet.BestRoundOf(Seat.West));
        }

        [Test]
        public void Empty_NoRounds_ZeroEverything()
        {
            Assert.AreEqual(0, ScoreSheet.Empty.Rounds.Count);
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(0, ScoreSheet.Empty.TotalOf((Seat)s));
                Assert.AreEqual(0, ScoreSheet.Empty.RoundsMetBy((Seat)s));
                Assert.AreEqual(0, ScoreSheet.Empty.BestRoundOf((Seat)s));
            }
        }

        [TestCase(new[] { 5, 3, 3, 3 })]
        [TestCase(new[] { 5, 3, 3, 1 })]
        [TestCase(new[] { 0, 0, 0, 0 })]
        public void Append_TricksNotSummingTo13_Throws(int[] won)
        {
            Assert.Throws<ArgumentException>(() => ScoreSheet.Empty.Append(new[] { 4, 3, 3, 3 }, won, Rules));
        }

        [Test]
        public void Append_WrongLengthOrInvalidCall_Throws()
        {
            Assert.Throws<ArgumentException>(() => ScoreSheet.Empty.Append(new[] { 4, 3, 3 }, new[] { 5, 3, 3, 2 }, Rules));
            Assert.Throws<ArgumentException>(() => ScoreSheet.Empty.Append(new[] { 4, 3, 3, 3 }, new[] { 5, 3, 5 }, Rules));
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreSheet.Empty.Append(new[] { 0, 3, 3, 3 }, new[] { 5, 3, 3, 2 }, Rules));
            Assert.Throws<ArgumentNullException>(() => ScoreSheet.Empty.Append(null, new[] { 5, 3, 3, 2 }, Rules));
        }

        [Test]
        public void Append_OriginalSheet_IsNotModified()
        {
            ScoreSheet one = ScoreSheet.Empty.Append(MockupCalls[0], MockupWon[0], Rules);

            ScoreSheet two = one.Append(MockupCalls[1], MockupWon[1], Rules);

            Assert.AreEqual(1, one.Rounds.Count);
            Assert.AreEqual(2, two.Rounds.Count);
            Assert.AreEqual(41, one.TotalOf(Seat.South));
        }

        [Test]
        public void Append_RandomRounds_EachTotalEqualsSumOfDeltas()
        {
            var rng = new Pcg32Random(8UL);
            ScoreSheet sheet = ScoreSheet.Empty;
            var sums = new int[4];
            for (int round = 0; round < 10; round++)
            {
                int[] calls = new int[4];
                int[] won = new int[4];
                int left = 13;
                for (int s = 0; s < 4; s++)
                {
                    calls[s] = 1 + rng.NextInt(8);
                    won[s] = s == 3 ? left : rng.NextInt(left + 1);
                    left -= won[s];
                }

                sheet = sheet.Append(calls, won, Rules);
                for (int s = 0; s < 4; s++)
                {
                    sums[s] += sheet.Rounds[round][(Seat)s].DeltaTenths;
                    Assert.AreEqual(sums[s], sheet.TotalOf((Seat)s));
                    Assert.AreEqual(sums[s], sheet.Rounds[round][(Seat)s].TotalTenths);
                }
            }
        }

        [Test]
        public void Equals_SameRounds_EqualSheets()
        {
            Assert.AreEqual(MockupMatch(), MockupMatch());
            Assert.AreEqual(MockupMatch().GetHashCode(), MockupMatch().GetHashCode());
            Assert.AreNotEqual(MockupMatch(), ScoreSheet.Empty);
            Assert.AreEqual(MockupMatch().Rounds[2], MockupMatch().Rounds[2]);
        }

        [Test]
        public void Rounds_ExposedList_CannotBeCastBackToAMutableArray()
        {
            ScoreSheet sheet = MockupMatch();

            Assert.IsFalse(sheet.Rounds is RoundResult[]);
        }
    }
}
