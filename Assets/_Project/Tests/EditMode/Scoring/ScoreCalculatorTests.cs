using System;
using System.Collections.Generic;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Scoring
{
    public sealed class ScoreCalculatorTests
    {
        // GDD §4 "Scoring reference — every call × tricks won", transcribed value by value in tenths.
        // Row = call 1..8, column = tricks won 0..13. This is golden data, not a formula.
        private static readonly int[][] GoldenGrid =
        {
            new[] { -10, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22 },
            new[] { -20, -20, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31 },
            new[] { -30, -30, -30, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40 },
            new[] { -40, -40, -40, -40, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49 },
            new[] { -50, -50, -50, -50, -50, 50, 51, 52, 53, 54, 55, 56, 57, 58 },
            new[] { -60, -60, -60, -60, -60, -60, 60, 61, 62, 63, 64, 65, 66, 67 },
            new[] { -70, -70, -70, -70, -70, -70, -70, 70, 71, 72, 73, 74, 75, 76 },
            new[] { -80, -80, -80, -80, -80, -80, -80, -80, 80, 81, 82, 83, 84, 85 },
        };

        private static IEnumerable<TestCaseData> GoldenCells()
        {
            for (int call = 1; call <= 8; call++)
            {
                for (int won = 0; won <= 13; won++)
                {
                    yield return new TestCaseData(call, won, GoldenGrid[call - 1][won])
                        .SetName("RoundScore_GoldenGridCall" + call + "Won" + won + "_" + GoldenGrid[call - 1][won]);
                }
            }
        }

        [TestCaseSource(nameof(GoldenCells))]
        public void RoundScore_GoldenGrid_MatchesGddTable(int call, int won, int expectedTenths)
        {
            Assert.AreEqual(expectedTenths, ScoreCalculator.RoundScore(call, won, RuleSet.NepaliStandard));
        }

        [Test]
        public void GoldenGrid_Shape_Is8By14()
        {
            Assert.AreEqual(8, GoldenGrid.Length);
            foreach (int[] row in GoldenGrid)
            {
                Assert.AreEqual(14, row.Length);
            }
        }

        [TestCase(4, 5, 41)]
        [TestCase(3, 3, 30)]
        [TestCase(3, 2, -30)]
        [TestCase(3, 13, 40)]
        [TestCase(8, 13, 85)]
        [TestCase(1, 0, -10)]
        public void RoundScore_GddExamples_Tenths(int call, int won, int expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.RoundScore(call, won, RuleSet.NepaliStandard));
        }

        [Test]
        public void RoundScore_Case7CallEightWonThirteen_85WithoutBonusAnd130With()
        {
            // GDD §5 edge case 7: scores 8 + 0.5 = 8.5 (tenths 85). With BigCallBonus, 13.0 instead.
            Assert.AreEqual(85, ScoreCalculator.RoundScore(8, 13, RuleSet.NepaliStandard));
            Assert.AreEqual(130, ScoreCalculator.RoundScore(8, 13, RuleSet.NepaliStandard.With(bigCallBonus: true)));
        }

        [TestCase(false, 8, 8, 80)]
        [TestCase(true, 8, 8, 130)]
        [TestCase(false, 8, 7, -80)]
        [TestCase(true, 8, 7, -80)]
        [TestCase(true, 7, 13, 76)]
        [TestCase(false, 7, 13, 76)]
        public void RoundScore_BigCallBonusSwitch_OnlyMadeCallsOfEightOrMore(bool bonus, int call, int won, int expected)
        {
            RuleSet rules = RuleSet.NepaliStandard.With(bigCallBonus: bonus);

            Assert.AreEqual(expected, ScoreCalculator.RoundScore(call, won, rules));
        }

        [Test]
        public void RoundScore_BigCallBonusWithHigherMaxCall_AppliesToNineToo()
        {
            RuleSet rules = RuleSet.NepaliStandard.With(maxCall: 13, bigCallBonus: true);

            Assert.AreEqual(130, ScoreCalculator.RoundScore(9, 9, rules));
            Assert.AreEqual(130, ScoreCalculator.RoundScore(13, 13, rules));
            Assert.AreEqual(-130, ScoreCalculator.RoundScore(13, 12, rules));
        }

        [TestCase(0, 4, 5, 40)]
        [TestCase(0, 3, 13, 30)]
        [TestCase(5, 4, 6, 50)]
        [TestCase(10, 3, 13, 130)]
        [TestCase(10, 3, 2, -30)]
        public void RoundScore_OvertrickTenths_ScalesOnlyExtraTricks(int overtrick, int call, int won, int expected)
        {
            RuleSet rules = RuleSet.NepaliStandard.With(overtrickTenths: overtrick);

            Assert.AreEqual(expected, ScoreCalculator.RoundScore(call, won, rules));
        }

        [TestCase(0, 5)]
        [TestCase(14, 5)]
        [TestCase(4, -1)]
        [TestCase(4, 14)]
        public void RoundScore_OutOfRangeArguments_Throw(int call, int won)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreCalculator.RoundScore(call, won, RuleSet.NepaliStandard));
        }

        [Test]
        public void RoundScore_NullRules_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => ScoreCalculator.RoundScore(4, 5, null));
        }

        [TestCase(4, 4, true)]
        [TestCase(4, 13, true)]
        [TestCase(4, 3, false)]
        public void IsMet_WonAtLeastCall_True(int call, int won, bool expected)
        {
            Assert.AreEqual(expected, ScoreCalculator.IsMet(call, won));
        }
    }
}
