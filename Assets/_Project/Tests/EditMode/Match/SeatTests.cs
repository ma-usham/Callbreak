using System;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Match
{
    public sealed class SeatTests
    {
        [TestCase(Seat.South, Seat.East)]
        [TestCase(Seat.East, Seat.North)]
        [TestCase(Seat.North, Seat.West)]
        [TestCase(Seat.West, Seat.South)]
        public void Next_Anticlockwise_IsSeatPlusOne(Seat seat, Seat expected)
        {
            Assert.AreEqual(expected, seat.Next(Direction.Anticlockwise));
        }

        [TestCase(Seat.South, Seat.West)]
        [TestCase(Seat.West, Seat.North)]
        [TestCase(Seat.North, Seat.East)]
        [TestCase(Seat.East, Seat.South)]
        public void Next_Clockwise_IsSeatPlusThree(Seat seat, Seat expected)
        {
            Assert.AreEqual(expected, seat.Next(Direction.Clockwise));
        }

        [Test]
        public void Next_GddDealerRotation_MatchesTheRoundTable()
        {
            // GDD §2 table: dealers W, S, E, N, W; the first caller is always the dealer's right.
            Seat dealer = Seat.West;
            Seat[] expectedDealers = { Seat.West, Seat.South, Seat.East, Seat.North, Seat.West };
            Seat[] expectedFirst = { Seat.South, Seat.East, Seat.North, Seat.West, Seat.South };

            for (int round = 0; round < 5; round++)
            {
                Assert.AreEqual(expectedDealers[round], dealer, "dealer of round " + (round + 1));
                Assert.AreEqual(expectedFirst[round], dealer.Next(Direction.Anticlockwise), "first of round " + (round + 1));
                dealer = dealer.Next(Direction.Anticlockwise);
            }
        }

        [TestCase(Direction.Anticlockwise)]
        [TestCase(Direction.Clockwise)]
        public void Advance_FourSteps_ReturnsToStart(Direction direction)
        {
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual((Seat)s, ((Seat)s).Advance(direction, 4));
                Assert.AreEqual(((Seat)s).Next(direction).Next(direction), ((Seat)s).Advance(direction, 2));
            }
        }

        [TestCase(Direction.Anticlockwise)]
        [TestCase(Direction.Clockwise)]
        public void StepsTo_EveryPair_InvertsAdvance(Direction direction)
        {
            for (int from = 0; from < 4; from++)
            {
                for (int to = 0; to < 4; to++)
                {
                    int steps = ((Seat)from).StepsTo((Seat)to, direction);
                    Assert.AreEqual((Seat)to, ((Seat)from).Advance(direction, steps));
                    Assert.IsTrue(steps >= 0 && steps < 4);
                }
            }
        }

        [TestCase(Seat.South, Seat.South, 0)]
        [TestCase(Seat.East, Seat.South, 1)]
        [TestCase(Seat.North, Seat.South, 2)]
        [TestCase(Seat.West, Seat.South, 3)]
        [TestCase(Seat.South, Seat.East, 3)]
        [TestCase(Seat.North, Seat.West, 3)]
        [TestCase(Seat.South, Seat.West, 1)]
        public void RelativeTo_Viewer_BottomRightTopLeft(Seat seat, Seat viewer, int expected)
        {
            Assert.AreEqual(expected, seat.RelativeTo(viewer));
        }

        [Test]
        public void RelativeTo_NextPlayerAnticlockwise_IsAlwaysOnTheRight()
        {
            for (int s = 0; s < 4; s++)
            {
                Seat viewer = (Seat)s;
                Assert.AreEqual(1, viewer.Next(Direction.Anticlockwise).RelativeTo(viewer));
            }
        }

        [Test]
        public void IsValid_OutOfRangeValue_IsFalse()
        {
            Assert.IsTrue(Seat.West.IsValid());
            Assert.IsFalse(((Seat)4).IsValid());
            Assert.IsFalse(((Seat)255).IsValid());
        }

        [Test]
        public void Next_InvalidSeatOrDirection_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ((Seat)4).Next(Direction.Anticlockwise));
            Assert.Throws<ArgumentOutOfRangeException>(() => Seat.South.Next((Direction)7));
            Assert.Throws<ArgumentOutOfRangeException>(() => Seat.South.Advance(Direction.Clockwise, -1));
        }
    }
}
