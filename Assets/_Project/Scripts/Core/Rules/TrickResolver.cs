using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;

namespace Callbreak.Core.Rules
{
    /// <summary>
    /// Who wins a trick (GDD §4 "Who wins a trick"): if any spade was played the highest spade wins;
    /// otherwise the highest card of the led suit. Cards of other suits never win. Works on partial tricks
    /// too, where it names the card currently winning.
    /// </summary>
    public static class TrickResolver
    {
        /// <summary>The card currently winning <paramref name="trick"/>.</summary>
        /// <param name="trick">A trick with at least one card.</param>
        /// <returns>The winning card.</returns>
        /// <exception cref="InvalidOperationException">The trick is empty.</exception>
        public static Card WinningCard(in Trick trick)
        {
            if (trick.IsEmpty)
            {
                throw new InvalidOperationException("An empty trick has no winner.");
            }

            CardSet played = trick.Cards;
            CardSet spades = played.InSuit(Suit.Spades);
            return spades.IsEmpty ? played.InSuit(trick.LedSuit).Highest() : spades.Highest();
        }

        /// <summary>Position in play order (0 = lead) of the winning card.</summary>
        /// <param name="trick">A trick with at least one card.</param>
        /// <returns>0–3.</returns>
        /// <exception cref="InvalidOperationException">The trick is empty.</exception>
        public static int WinningIndex(in Trick trick)
        {
            Card winning = WinningCard(trick);
            for (int i = 0; i < trick.Count; i++)
            {
                if (trick[i] == winning)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("The winning card is missing from its trick.");
        }

        /// <summary>The seat that played the winning card; it leads the next trick (GDD §3).</summary>
        /// <param name="trick">A trick with at least one card.</param>
        /// <returns>The winning seat.</returns>
        /// <exception cref="InvalidOperationException">The trick is empty.</exception>
        public static Seat Winner(in Trick trick)
        {
            return trick.SeatAt(WinningIndex(trick));
        }
    }
}
