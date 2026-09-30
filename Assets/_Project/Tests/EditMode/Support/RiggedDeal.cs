using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>
    /// Makes the real engine deal chosen hands: inverts Deck.Shuffled's Fisher–Yates to find the draws that
    /// produce a given deck order, and orders the deck so Deck.Deal hands out the chosen cards. Use with a
    /// fixed first dealer so the first draws are the shuffle.
    /// </summary>
    public static class RiggedDeal
    {
        /// <summary>Script for one deal of <paramref name="hands"/> (indexed by seat), dealt by <paramref name="dealer"/>.</summary>
        public static int[] DrawsFor(CardSet[] hands, Seat dealer, Direction direction)
        {
            return ShuffleDrawsFor(OrderFor(hands, dealer.Next(direction), direction));
        }

        /// <summary>An IRandom that deals these hands in round 1 (after any extra leading draws), then plays on seeded.</summary>
        public static SequenceRandom Random(CardSet[] hands, Seat dealer, Direction direction, ulong fallbackSeed = 7UL)
        {
            return new SequenceRandom(DrawsFor(hands, dealer, direction), fallbackSeed);
        }

        /// <summary>Concatenates several deal scripts, e.g. a spadeless deal followed by a good one.</summary>
        public static SequenceRandom Sequence(ulong fallbackSeed, params int[][] scripts)
        {
            var all = new List<int>();
            foreach (int[] script in scripts)
            {
                all.AddRange(script);
            }

            return new SequenceRandom(all, fallbackSeed);
        }

        public static Card[] OrderFor(CardSet[] hands, Seat firstSeat, Direction direction)
        {
            var queues = new Queue<Card>[4];
            CardSet all = CardSet.Empty;
            for (int s = 0; s < 4; s++)
            {
                if (hands[s].Count != 13 || all.Overlaps(hands[s]))
                {
                    throw new ArgumentException("Rigged hands must be four disjoint sets of 13.");
                }

                all |= hands[s];
                queues[s] = new Queue<Card>();
                foreach (Card card in hands[s])
                {
                    queues[s].Enqueue(card);
                }
            }

            var order = new Card[52];
            Seat seat = firstSeat;
            for (int k = 0; k < 52; k++)
            {
                order[k] = queues[(int)seat].Dequeue();
                seat = seat.Next(direction);
            }

            return order;
        }

        public static int[] ShuffleDrawsFor(IReadOnlyList<Card> order)
        {
            var deck = new int[52];
            for (int i = 0; i < 52; i++)
            {
                deck[i] = i;
            }

            var draws = new int[51];
            for (int i = 51; i > 0; i--)
            {
                int j = Array.IndexOf(deck, order[i].Id, 0, i + 1);
                draws[51 - i] = j;
                int swap = deck[i];
                deck[i] = deck[j];
                deck[j] = swap;
            }

            return draws;
        }
    }
}
