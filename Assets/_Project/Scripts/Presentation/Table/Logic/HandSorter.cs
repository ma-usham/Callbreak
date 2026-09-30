using System;
using System.Collections.Generic;
using Callbreak.Application.Settings;
using Callbreak.Core.Cards;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The order of the local hand, left to right (GDD §2 "auto-sort"; screen 08 "Sort: toggles the
    /// suit order"): suits ♠ ♥ ♦ ♣ by default or ♠ ♥ ♣ ♦ so colours alternate, and within a suit
    /// the highest card first, as the mockups draw it (A♠ K♠ 9♠ … 7♣ 3♣).
    /// </summary>
    public static class HandSorter
    {
        private static readonly Suit[] BySuit = { Suit.Spades, Suit.Hearts, Suit.Diamonds, Suit.Clubs };
        private static readonly Suit[] Alternating = { Suit.Spades, Suit.Hearts, Suit.Clubs, Suit.Diamonds };

        /// <summary>The suits in display order for <paramref name="order"/>.</summary>
        public static IReadOnlyList<Suit> SuitOrder(HandSortOrder order)
        {
            return order == HandSortOrder.AlternatingColours ? Alternating : BySuit;
        }

        /// <summary>Clears <paramref name="into"/> and fills it with <paramref name="hand"/> in display order.</summary>
        public static void Sort(CardSet hand, HandSortOrder order, List<Card> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            into.Clear();
            Suit[] suits = order == HandSortOrder.AlternatingColours ? Alternating : BySuit;
            for (int s = 0; s < suits.Length; s++)
            {
                for (int rank = (int)Rank.Ace; rank >= (int)Rank.Two; rank--)
                {
                    var card = new Card(suits[s], (Rank)rank);
                    if (hand.Contains(card))
                    {
                        into.Add(card);
                    }
                }
            }
        }

        /// <summary>The other order: what the Sort button switches to.</summary>
        public static HandSortOrder Toggle(HandSortOrder order)
        {
            return order == HandSortOrder.BySuit ? HandSortOrder.AlternatingColours : HandSortOrder.BySuit;
        }
    }
}
