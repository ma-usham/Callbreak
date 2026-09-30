using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>
    /// The rollouts' own copy of the rules (card masks instead of Core types) must agree with Core exactly:
    /// legal cards with <see cref="LegalMoves"/>, trick winners with <see cref="TrickResolver"/>, seat order
    /// with <c>Seat.Next</c>, and the mask helpers with <see cref="CardSet"/>.
    /// </summary>
    public sealed class RoundStateTests
    {
        [Test]
        public void Legal_RandomSituationsUnderEveryLegalityCombination_EqualsLegalMoves()
        {
            var rng = new Pcg32Random(2024UL);
            int compared = 0;
            foreach (RuleSet legality in RuleVariants.LegalityCombinations())
            {
                for (int i = 0; i < 1500; i++)
                {
                    RuleVariants.RandomSituation(rng, out CardSet hand, out Trick trick);
                    RuleSet rules = legality.With(direction: trick.Direction);
                    RoundState state = StateAfter(rules, trick);

                    CardSet legal = CardSet.FromBits(state.Legal(hand.Bits));

                    Assert.AreEqual(LegalMoves.Compute(hand, trick, rules), legal, rules + "\nhand " + hand + "\ntrick " + trick);
                    compared++;
                }
            }

            Assert.AreEqual(16 * 1500, compared);
        }

        [Test]
        public void Play_RandomTricks_WinnerAndWinningCardEqualTrickResolver()
        {
            var rng = new Pcg32Random(77UL);
            for (int i = 0; i < 3000; i++)
            {
                Card[] deck = Deck.Shuffled(rng);
                var leader = (Seat)rng.NextInt(4);
                Direction direction = rng.NextInt(2) == 0 ? Direction.Anticlockwise : Direction.Clockwise;
                var trick = new Trick(leader, direction);
                var state = new RoundState(RuleSet.NepaliStandard.With(direction: direction));
                state.Lead((int)leader);
                for (int k = 0; k < 3; k++)
                {
                    trick = trick.With(deck[k]);
                    state.Play((int)trick.SeatAt(k), deck[k].Id);

                    Assert.AreEqual(TrickResolver.WinningCard(trick).Id, state.WinningId);
                    Assert.AreEqual((int)TrickResolver.Winner(trick), state.WinningSeat);
                    Assert.AreEqual((int)trick.NextSeat, state.SeatAt(state.Count));
                    Assert.AreEqual((int)trick.LedSuit, state.LedSuit);
                }

                trick = trick.With(deck[3]);
                state.Play((int)trick.SeatAt(3), deck[3].Id);
                var winner = (int)TrickResolver.Winner(trick);

                Assert.AreEqual(1, state.Completed);
                Assert.AreEqual(0, state.Count);
                Assert.AreEqual(winner, state.Leader, "the winner leads the next trick");
                Assert.AreEqual(1, state.Won[winner]);
                Assert.AreEqual(trick.Cards.Bits, state.Played);
            }
        }

        [TestCase(Direction.Anticlockwise)]
        [TestCase(Direction.Clockwise)]
        public void SeatAt_EveryLeaderAndPosition_FollowsSeatNext(Direction direction)
        {
            var state = new RoundState(RuleSet.NepaliStandard.With(direction: direction));
            for (int leader = 0; leader < 4; leader++)
            {
                state.Lead(leader);
                for (int position = 0; position < 4; position++)
                {
                    Assert.AreEqual((int)((Seat)leader).Advance(direction, position), state.SeatAt(position));
                }
            }
        }

        [Test]
        public void Bits_RandomMasks_AgreeWithCardSet()
        {
            var rng = new Pcg32Random(5UL);
            for (int i = 0; i < 2000; i++)
            {
                CardSet set = CardSet.Empty;
                int size = 1 + rng.NextInt(20);
                for (int k = 0; k < size; k++)
                {
                    set = set.With(Card.FromId(rng.NextInt(52)));
                }

                Card any = set.ElementAt(rng.NextInt(set.Count));

                Assert.AreEqual(set.Count, Bits.Count(set.Bits));
                Assert.AreEqual(set.Lowest().Id, Bits.Lowest(set.Bits));
                Assert.AreEqual(set.Highest().Id, Bits.Highest(set.Bits));
                Assert.AreEqual(CardSet.Above(any).Bits, Bits.Above(any.Id));
                Assert.AreEqual((int)any.Suit, Bits.SuitOf(any.Id));
                Assert.AreEqual((int)any.Rank - 2, Bits.RankOf(any.Id));
                Assert.AreEqual(CardSet.OfSuit(any.Suit).Bits, Bits.OfSuit((int)any.Suit));
            }

            Assert.AreEqual(CardSet.FullDeck.Bits, Bits.Deck);
            Assert.AreEqual(CardSet.OfSuit(Suit.Spades).Bits, Bits.SpadeCards);
            Assert.AreEqual(new Card(Suit.Spades, Rank.Two).Id, Bits.FirstSpade);
        }

        private static RoundState StateAfter(RuleSet rules, Trick trick)
        {
            var state = new RoundState(rules);
            state.Lead((int)trick.Leader);
            for (int i = 0; i < trick.Count; i++)
            {
                state.Play((int)trick.SeatAt(i), trick[i].Id);
            }

            return state;
        }
    }
}
