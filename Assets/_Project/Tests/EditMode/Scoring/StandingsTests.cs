using System;
using System.Collections.Generic;
using System.Linq;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Scoring
{
    public sealed class StandingsTests
    {
        [Test]
        public void Compute_GddMockupMatch_RanksOneToFour()
        {
            // GDD §4 "Match & final standings": 21.3, 16.1, 12.0, 6.1 -> ranks 1-4.
            IReadOnlyList<Standing> standings = Standings.Compute(ScoreSheetTests.MockupMatch());

            AssertStanding(standings[0], Seat.South, 1, 213, false);
            AssertStanding(standings[1], Seat.North, 2, 161, false);
            AssertStanding(standings[2], Seat.East, 3, 120, false);
            AssertStanding(standings[3], Seat.West, 4, 61, false);
        }

        [Test]
        public void Compute_DistinctTotals_HigherTotalRanksFirst()
        {
            IReadOnlyList<Standing> standings = Standings.Compute(
                new[] { 61, 213, 120, 161 }, new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 0 });

            CollectionAssert.AreEqual(
                new[] { Seat.East, Seat.West, Seat.North, Seat.South },
                standings.Select(s => s.Seat).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, standings.Select(s => s.Rank).ToArray());
        }

        [Test]
        public void Compute_TotalsTied_MoreRoundsMetWins()
        {
            IReadOnlyList<Standing> standings = Standings.Compute(
                new[] { 100, 100, 50, 20 }, new[] { 4, 5, 3, 2 }, new[] { 40, 30, 20, 10 });

            AssertStanding(standings[0], Seat.East, 1, 100, false);
            AssertStanding(standings[1], Seat.South, 2, 100, false);
        }

        [Test]
        public void Compute_TotalsAndRoundsMetTied_HigherBestRoundWins()
        {
            IReadOnlyList<Standing> standings = Standings.Compute(
                new[] { 100, 100, 50, 20 }, new[] { 4, 4, 3, 2 }, new[] { 30, 40, 20, 10 });

            AssertStanding(standings[0], Seat.East, 1, 100, false);
            AssertStanding(standings[1], Seat.South, 2, 100, false);
        }

        [Test]
        public void Compute_StillTiedAtTheTop_TwoFirstsThenThird()
        {
            // GDD §4 tie rule 4: "two players 1st, next 3rd".
            IReadOnlyList<Standing> standings = Standings.Compute(
                new[] { 100, 100, 50, 20 }, new[] { 4, 4, 3, 2 }, new[] { 30, 30, 20, 10 });

            AssertStanding(standings[0], Seat.South, 1, 100, true);
            AssertStanding(standings[1], Seat.East, 1, 100, true);
            AssertStanding(standings[2], Seat.North, 3, 50, false);
            AssertStanding(standings[3], Seat.West, 4, 20, false);
        }

        [Test]
        public void Compute_TieInTheMiddle_SharedSecondThenFourth()
        {
            IReadOnlyList<Standing> standings = Standings.Compute(
                new[] { 100, 50, 50, 20 }, new[] { 4, 3, 3, 2 }, new[] { 30, 20, 20, 10 });

            CollectionAssert.AreEqual(new[] { 1, 2, 2, 4 }, standings.Select(s => s.Rank).ToArray());
            CollectionAssert.AreEqual(new[] { false, true, true, false }, standings.Select(s => s.IsShared).ToArray());
        }

        [Test]
        public void Compute_AllFourTied_AllFirstAndShared()
        {
            IReadOnlyList<Standing> standings = Standings.Compute(
                new[] { 30, 30, 30, 30 }, new[] { 1, 1, 1, 1 }, new[] { 30, 30, 30, 30 });

            Assert.IsTrue(standings.All(s => s.Rank == 1 && s.IsShared));
            CollectionAssert.AreEqual(
                new[] { Seat.South, Seat.East, Seat.North, Seat.West },
                standings.Select(s => s.Seat).ToArray());
        }

        [Test]
        public void Compute_NegativeTotals_StillOrderedByTotal()
        {
            IReadOnlyList<Standing> standings = Standings.Compute(
                new[] { -30, -80, 10, -10 }, new[] { 0, 0, 1, 0 }, new[] { -30, -80, 10, -10 });

            CollectionAssert.AreEqual(
                new[] { Seat.North, Seat.West, Seat.South, Seat.East },
                standings.Select(s => s.Seat).ToArray());
        }

        [Test]
        public void Compute_EmptySheet_EveryoneSharesFirst()
        {
            IReadOnlyList<Standing> standings = Standings.Compute(ScoreSheet.Empty);

            Assert.AreEqual(4, standings.Count);
            Assert.IsTrue(standings.All(s => s.Rank == 1 && s.IsShared && s.TotalTenths == 0));
        }

        [Test]
        public void Compute_Standing_CarriesTieBreakInputs()
        {
            IReadOnlyList<Standing> standings = Standings.Compute(ScoreSheetTests.MockupMatch());

            Standing you = standings.Single(s => s.Seat == Seat.South);
            Assert.AreEqual(5, you.RoundsMet);
            Assert.AreEqual(50, you.BestRoundTenths);
        }

        [Test]
        public void Compare_OrdersByTheThreeTieBreaksInTurn()
        {
            var high = new Standing(Seat.South, 1, false, 100, 4, 30);
            var moreMet = new Standing(Seat.East, 1, false, 100, 5, 10);
            var betterBest = new Standing(Seat.North, 1, false, 100, 4, 40);
            var same = new Standing(Seat.West, 1, false, 100, 4, 30);

            Assert.Less(Standings.Compare(moreMet, high), 0);
            Assert.Less(Standings.Compare(betterBest, high), 0);
            Assert.AreEqual(0, Standings.Compare(same, high));
            Assert.Greater(Standings.Compare(new Standing(Seat.West, 1, false, 99, 9, 99), high), 0);
        }

        [Test]
        public void Compute_WrongLengths_Throw()
        {
            Assert.Throws<ArgumentException>(() => Standings.Compute(new[] { 1, 2, 3 }, new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 0 }));
            Assert.Throws<ArgumentNullException>(() => Standings.Compute((ScoreSheet)null));
        }

        private static void AssertStanding(Standing standing, Seat seat, int rank, int total, bool shared)
        {
            Assert.AreEqual(seat, standing.Seat, "seat");
            Assert.AreEqual(rank, standing.Rank, "rank of " + seat);
            Assert.AreEqual(total, standing.TotalTenths, "total of " + seat);
            Assert.AreEqual(shared, standing.IsShared, "shared flag of " + seat);
        }
    }
}
