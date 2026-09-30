using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Rules
{
    public sealed class TrickTests
    {
        [Test]
        public void NewTrick_NoCards_IsEmptyAndLeaderIsNext()
        {
            var trick = new Trick(Seat.North, Direction.Anticlockwise);

            Assert.IsTrue(trick.IsEmpty);
            Assert.IsFalse(trick.IsComplete);
            Assert.AreEqual(0, trick.Count);
            Assert.AreEqual(Seat.North, trick.Leader);
            Assert.AreEqual(Seat.North, trick.NextSeat);
            Assert.IsFalse(trick.HasSpade);
            Assert.IsFalse(trick.IsTrumped);
            Assert.AreEqual(CardSet.Empty, trick.Cards);
            Assert.Throws<InvalidOperationException>(() => { Suit unused = trick.LedSuit; });
        }

        [Test]
        public void With_FourCards_KeepsPlayOrderAndSeats()
        {
            Trick trick = TrickOf(Seat.West, Direction.Anticlockwise, "9♥ Q♥ 10♥ A♥");

            Assert.IsTrue(trick.IsComplete);
            Assert.AreEqual(C("9♥"), trick[0]);
            Assert.AreEqual(C("A♥"), trick[3]);
            Assert.AreEqual(Seat.West, trick.SeatAt(0));
            Assert.AreEqual(Seat.South, trick.SeatAt(1));
            Assert.AreEqual(Seat.East, trick.SeatAt(2));
            Assert.AreEqual(Seat.North, trick.SeatAt(3));
            Assert.AreEqual(Suit.Hearts, trick.LedSuit);
            Assert.AreEqual(Set("9♥ Q♥ 10♥ A♥"), trick.Cards);
        }

        [Test]
        public void SeatAt_Clockwise_GoesTheOtherWay()
        {
            Trick trick = TrickOf(Seat.West, Direction.Clockwise, "9♥ Q♥");

            Assert.AreEqual(Seat.North, trick.SeatAt(1));
            Assert.AreEqual(Seat.East, trick.NextSeat);
        }

        [Test]
        public void TryGetCardOf_PlayedAndUnplayedSeats_ReportsCorrectly()
        {
            Trick trick = TrickOf(Seat.East, Direction.Anticlockwise, "7♣ J♠");

            Assert.IsTrue(trick.TryGetCardOf(Seat.East, out Card eastCard));
            Assert.AreEqual(C("7♣"), eastCard);
            Assert.IsTrue(trick.TryGetCardOf(Seat.North, out Card northCard));
            Assert.AreEqual(C("J♠"), northCard);
            Assert.IsFalse(trick.TryGetCardOf(Seat.West, out _));
            Assert.IsFalse(trick.TryGetCardOf(Seat.South, out _));
        }

        [Test]
        public void IsTrumped_SpadeOnNonSpadeLead_True()
        {
            Trick trick = TrickOf(Seat.South, Direction.Anticlockwise, "9♥ 4♠");

            Assert.IsTrue(trick.HasSpade);
            Assert.IsTrue(trick.IsTrumped);
        }

        [Test]
        public void IsTrumped_SpadeLead_FalseAlthoughItHasSpades()
        {
            Trick trick = TrickOf(Seat.South, Direction.Anticlockwise, "5♠ 9♠");

            Assert.IsTrue(trick.HasSpade);
            Assert.IsFalse(trick.IsTrumped);
        }

        [Test]
        public void With_FifthCardOrRepeatedCard_Throws()
        {
            Trick full = TrickOf(Seat.South, Direction.Anticlockwise, "9♥ Q♥ 10♥ A♥");
            Trick partial = TrickOf(Seat.South, Direction.Anticlockwise, "9♥");

            Assert.Throws<InvalidOperationException>(() => full.With(C("2♣")));
            Assert.Throws<ArgumentException>(() => partial.With(C("9♥")));
        }

        [Test]
        public void Indexer_OutOfRange_Throws()
        {
            Trick trick = TrickOf(Seat.South, Direction.Anticlockwise, "9♥");

            Assert.Throws<ArgumentOutOfRangeException>(() => { Card unused = trick[1]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { Card unused = trick[-1]; });
        }

        [Test]
        public void Constructor_InvalidLeaderOrDirection_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Trick((Seat)4, Direction.Anticlockwise));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Trick(Seat.South, (Direction)2));
        }

        [Test]
        public void Equality_SameCardsSameLeader_Equal()
        {
            Trick a = TrickOf(Seat.South, Direction.Anticlockwise, "9♥ Q♥");
            Trick b = TrickOf(Seat.South, Direction.Anticlockwise, "9♥ Q♥");
            Trick otherOrder = TrickOf(Seat.South, Direction.Anticlockwise, "Q♥ 9♥");
            Trick otherLeader = TrickOf(Seat.East, Direction.Anticlockwise, "9♥ Q♥");

            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.IsTrue(a != otherOrder);
            Assert.IsTrue(a != otherLeader);
        }

        [Test]
        public void ToString_PartialTrick_LeaderAndCardsInOrder()
        {
            Assert.AreEqual("West: 9♥ Q♥", TrickOf(Seat.West, Direction.Anticlockwise, "9♥ Q♥").ToString());
            Assert.AreEqual("West: -", new Trick(Seat.West, Direction.Anticlockwise).ToString());
        }
    }
}
