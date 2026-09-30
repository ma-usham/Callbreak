using System;
using System.Collections.Generic;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;

namespace Callbreak.Core.Cards
{
    /// <summary>
    /// Shuffling and dealing (GDD §2 "Dealing &amp; the dealer"). Both steps are pure functions of their
    /// inputs, so a seeded <see cref="IRandom"/> reproduces every deal of a match.
    /// </summary>
    public static class Deck
    {
        /// <summary>Cards in the deck.</summary>
        public const int Size = Card.Count;

        /// <summary>Cards per hand, which is also the number of tricks in a round.</summary>
        public const int HandSize = Size / SeatExtensions.Count;

        /// <summary>
        /// Returns a uniformly shuffled deck. Starts from ids 0–51 in order and runs Fisher–Yates from the
        /// top: for i = 51 down to 1, swap position i with <c>rng.NextInt(i + 1)</c>. Exactly 51 draws, so
        /// the draw sequence is part of the replay contract (GDD §22 "Determinism &amp; replay").
        /// </summary>
        /// <param name="rng">The match's randomness.</param>
        /// <returns>A new array of 52 distinct cards, top of the deck first.</returns>
        public static Card[] Shuffled(IRandom rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            var order = new Card[Size];
            for (int id = 0; id < Size; id++)
            {
                order[id] = Card.FromId(id);
            }

            for (int i = Size - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                if ((uint)j > (uint)i)
                {
                    throw new InvalidOperationException("IRandom.NextInt returned a value outside its bound.");
                }

                Card swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }

            return order;
        }

        /// <summary>
        /// Deals <paramref name="order"/> one card at a time, the first card to <paramref name="firstSeat"/>
        /// and then round the table in <paramref name="direction"/> (GDD §2: "13 cards to each player, one
        /// at a time, starting on the dealer's right").
        /// </summary>
        /// <param name="order">52 distinct cards, top of the deck first.</param>
        /// <param name="firstSeat">The seat that receives the first card (the dealer's right).</param>
        /// <param name="direction">The rule set's direction.</param>
        /// <returns>Four hands indexed by seat number.</returns>
        /// <exception cref="ArgumentException"><paramref name="order"/> is not 52 distinct cards.</exception>
        public static CardSet[] Deal(IReadOnlyList<Card> order, Seat firstSeat, Direction direction)
        {
            if (order == null)
            {
                throw new ArgumentNullException(nameof(order));
            }

            if (order.Count != Size)
            {
                throw new ArgumentException("A deal needs exactly 52 cards.", nameof(order));
            }

            var hands = new CardSet[SeatExtensions.Count];
            CardSet dealt = CardSet.Empty;
            Seat seat = firstSeat;
            for (int i = 0; i < Size; i++)
            {
                Card card = order[i];
                if (dealt.Contains(card))
                {
                    throw new ArgumentException("Card " + card + " appears twice in the deal.", nameof(order));
                }

                dealt = dealt.With(card);
                hands[(int)seat] = hands[(int)seat].With(card);
                seat = seat.Next(direction);
            }

            return hands;
        }
    }
}
