using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The order of the local hand, left to right: ♠ ♥ ♣ ♦, so the colours alternate, and within a
    /// suit the highest card first, as the mockups draw it (A♠ K♠ 9♠ … 5♦ 2♦). It is the only order:
    /// the owner chose it in place of the GDD's Sort button and its two orders (ADR 021).
    /// </summary>
    public static class HandSorter
    {
        private static readonly Suit[] Suits = { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds };

        /// <summary>Clears <paramref name="into"/> and fills it with <paramref name="hand"/> in display order.</summary>
        public static void Sort(CardSet hand, List<Card> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            for (int s = 0; s < Suits.Length; s++)
            {
                for (int rank = (int)Rank.Ace; rank >= (int)Rank.Two; rank--)
                {
                    var card = new Card(Suits[s], (Rank)rank);
                    if (hand.Contains(card))
                    {
                        into.Add(card);
                    }
                }
            }
        }
    }
}
