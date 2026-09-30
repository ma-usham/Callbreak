using System;
using System.Linq;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using NUnit.Framework;
using static Callbreak.Tests.EditMode.Support.Notation;

namespace Callbreak.Tests.EditMode.Rules
{
    public sealed class TrickResolverTests
    {
        [Test]
        public void Winner_GddHeartsExample_HighestHeartWins()
        {
            // GDD §4: led ♥, 9♥ Q♥ 10♥ A♥ -> A♥ (highest heart).
            Trick trick = TrickOf(Seat.South, Direction.Anticlockwise, "9♥ Q♥ 10♥ A♥");

            Assert.AreEqual(C("A♥"), TrickResolver.WinningCard(trick));
            Assert.AreEqual(3, TrickResolver.WinningIndex(trick));
            Assert.AreEqual(Seat.West, TrickResolver.Winner(trick));
        }

        [Test]
        public void Winner_GddDiamondsExample_OnlySpadeWins()
        {
            // GDD §4: led ♦, K♦ 3♠ A♦ 5♣ -> 3♠ (only spade).
            Trick trick = TrickOf(Seat.South, Direction.Anticlockwise, "K♦ 3♠ A♦ 5♣");

            Assert.AreEqual(C("3♠"), TrickResolver.WinningCard(trick));
            Assert.AreEqual(Seat.East, TrickResolver.Winner(trick));
        }

        [Test]
        public void Winner_GddClubsExample_HighestSpadeWins()
        {
            // GDD §4: led ♣, 7♣ J♠ Q♠ 2♥ -> Q♠ (highest spade).
            Trick trick = TrickOf(Seat.South, Direction.Anticlockwise, "7♣ J♠ Q♠ 2♥");

            Assert.AreEqual(C("Q♠"), TrickResolver.WinningCard(trick));
            Assert.AreEqual(Seat.North, TrickResolver.Winner(trick));
        }

        [Test]
        public void Winner_OffSuitAces_NeverWin()
        {
            Trick trick = Led("2♣ A♥ A♦ K♥");

            Assert.AreEqual(C("2♣"), TrickResolver.WinningCard(trick));
            Assert.AreEqual(Seat.South, TrickResolver.Winner(trick));
        }

        [Test]
        public void Winner_SpadeLead_HighestSpadeWins()
        {
            Trick trick = Led("5♠ 9♠ A♥ 6♠");

            Assert.AreEqual(C("9♠"), TrickResolver.WinningCard(trick));
        }

        [Test]
        public void Winner_TwoOfSpadesAgainstAceOfHearts_SpadeWins()
        {
            // GDD §2: "2♠ beats A♥".
            Trick trick = Led("A♥ 2♠");

            Assert.AreEqual(C("2♠"), TrickResolver.WinningCard(trick));
        }

        [Test]
        public void Winner_PartialTrick_IsTheCurrentLeader()
        {
            Assert.AreEqual(Seat.South, TrickResolver.Winner(Led("Q♥")));
            Assert.AreEqual(Seat.East, TrickResolver.Winner(Led("Q♥ K♥")));
            Assert.AreEqual(Seat.East, TrickResolver.Winner(Led("Q♥ K♥ J♥")));
        }

        [Test]
        public void Winner_Clockwise_MapsIndexToTheRightSeat()
        {
            Trick trick = TrickOf(Seat.South, Direction.Clockwise, "9♥ A♥ 10♥ Q♥");

            Assert.AreEqual(Seat.West, TrickResolver.Winner(trick));
        }

        [Test]
        public void Winner_EmptyTrick_Throws()
        {
            var empty = new Trick(Seat.South, Direction.Anticlockwise);

            Assert.Throws<InvalidOperationException>(() => TrickResolver.Winner(empty));
            Assert.Throws<InvalidOperationException>(() => TrickResolver.WinningIndex(empty));
        }

        [Test]
        public void Winner_RandomCompleteTricks_MatchesTheGddRuleOracle()
        {
            var rng = new Pcg32Random(31UL);
            for (int trial = 0; trial < 5000; trial++)
            {
                Card[] deck = Deck.Shuffled(rng);
                var leader = (Seat)rng.NextInt(4);
                var trick = new Trick(leader, Direction.Anticlockwise);
                for (int i = 0; i < 4; i++)
                {
                    trick = trick.With(deck[i]);
                }

                // Oracle: highest spade if any spade was played, else highest card of the led suit.
                Card[] played = deck.Take(4).ToArray();
                Card expected = played.Any(c => c.Suit == Suit.Spades)
                    ? played.Where(c => c.Suit == Suit.Spades).OrderByDescending(c => c.Rank).First()
                    : played.Where(c => c.Suit == played[0].Suit).OrderByDescending(c => c.Rank).First();

                Assert.AreEqual(expected, TrickResolver.WinningCard(trick), trick.ToString());
                Assert.AreEqual(trick.SeatAt(Array.IndexOf(played, expected)), TrickResolver.Winner(trick));
            }
        }

        [Test]
        public void CompletedTrick_FourCards_RecordsIndexAndWinner()
        {
            Trick trick = TrickOf(Seat.South, Direction.Anticlockwise, "K♦ 3♠ A♦ 5♣");

            var completed = new CompletedTrick(4, trick);

            Assert.AreEqual(4, completed.Index);
            Assert.AreEqual(trick, completed.Trick);
            Assert.AreEqual(Seat.South, completed.Leader);
            Assert.AreEqual(Seat.East, completed.Winner);
            Assert.AreEqual(C("3♠"), completed.WinningCard);
        }

        [Test]
        public void CompletedTrick_IncompleteTrickOrBadIndex_Throws()
        {
            Assert.Throws<ArgumentException>(() => new CompletedTrick(0, Led("K♦ 3♠")));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CompletedTrick(13, Led("K♦ 3♠ A♦ 5♣")));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CompletedTrick(-1, Led("K♦ 3♠ A♦ 5♣")));
        }
    }
}
