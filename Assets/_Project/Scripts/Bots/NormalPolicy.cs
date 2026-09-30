using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Bots
{
    /// <summary>
    /// The Normal play heuristics (GDD §9 "Normal play heuristics" 1–5) as a pure function of the round's
    /// public state and the acting seat's hand. <see cref="NormalStrategy"/> asks it for the viewer;
    /// <see cref="HardStrategy"/>'s rollouts ask it for every seat of a sampled deal, tens of thousands of times
    /// per decision, so it works on card masks and ids (see <see cref="Bits"/>) and never allocates.
    /// "Unseen" here is always from the acting seat's point of view: the cards neither in its hand nor played.
    /// </summary>
    internal static class NormalPolicy
    {
        /// <summary>
        /// The id of the Normal card for seat <paramref name="me"/> holding <paramref name="hand"/>, chosen from
        /// <paramref name="legal"/> (its legal cards at <paramref name="table"/>'s trick in progress).
        /// </summary>
        internal static int ChooseCard(RoundState table, int me, ulong hand, ulong legal)
        {
            if ((legal & (legal - 1UL)) == 0UL)
            {
                return Bits.Lowest(legal);
            }

            ulong unseen = Bits.Deck & ~table.Played & ~hand;
            bool needsTricks = table.Calls[me] > table.Won[me];
            if (table.Count == 0)
            {
                return Lead(table, me, hand, unseen, needsTricks);
            }

            ulong beating = Beating(legal, table.WinningId);
            return needsTricks
                ? FollowForTricks(table, hand, legal, beating, unseen)
                : FollowWithCallMade(table, hand, legal, beating, unseen);
        }

        /// <summary>The cards of <paramref name="legal"/> that would take the lead from the winning card.</summary>
        internal static ulong Beating(ulong legal, int winningId)
        {
            ulong higher = legal & Bits.Above(winningId);
            return winningId >= Bits.FirstSpade ? higher : higher | (legal & Bits.SpadeCards);
        }

        /// <summary>
        /// GDD §9 rule 2's "lowest legal card": the lowest-ranked side-suit card (ties: the longer suit, then
        /// ♣ ♦ ♥), or the lowest spade when only spades are legal.
        /// </summary>
        internal static int LowestLegal(ulong legal, ulong hand)
        {
            ulong side = legal & ~Bits.SpadeCards;
            if (side == 0UL)
            {
                return Bits.Lowest(legal);
            }

            int pick = -1;
            int pickRank = 0;
            int pickLength = 0;
            for (int suit = 0; suit < Bits.Spades; suit++)
            {
                ulong cards = side & Bits.OfSuit(suit);
                if (cards == 0UL)
                {
                    continue;
                }

                int low = Bits.Lowest(cards);
                int rank = Bits.RankOf(low);
                int length = Bits.Count(hand & Bits.OfSuit(suit));
                if (pick < 0 || rank < pickRank || (rank == pickRank && length > pickLength))
                {
                    pick = low;
                    pickRank = rank;
                    pickLength = length;
                }
            }

            return pick;
        }

        /// <summary>
        /// GDD §9 rule 4's discard when the trick cannot be won: the lowest card of the longest side suit,
        /// passing over suits whose lowest card is itself the top remaining card (ties: the lower card, then
        /// ♣ ♦ ♥); the lowest spade when only spades are legal. With one legal suit, its lowest card.
        /// </summary>
        internal static int Discard(ulong legal, ulong hand, ulong unseen)
        {
            ulong side = legal & ~Bits.SpadeCards;
            if (side == 0UL)
            {
                return Bits.Lowest(legal);
            }

            int pick = -1;
            int pickKey = 0;
            for (int suit = 0; suit < Bits.Spades; suit++)
            {
                ulong cards = side & Bits.OfSuit(suit);
                if (cards == 0UL)
                {
                    continue;
                }

                // Not a winner first, then the longer suit, then the lower card.
                int low = Bits.Lowest(cards);
                int key = (IsTop(low, unseen) ? 0 : 1 << 10) | (Bits.Count(hand & Bits.OfSuit(suit)) << 5) | (Card.PerSuit - Bits.RankOf(low));
                if (pick < 0 || key > pickKey)
                {
                    pick = low;
                    pickKey = key;
                }
            }

            return pick;
        }

        /// <summary>Whether no unseen card of the suit outranks the card <paramref name="id"/>.</summary>
        internal static bool IsTop(int id, ulong unseen)
        {
            return (unseen & Bits.Above(id)) == 0UL;
        }

        /// <summary>
        /// Rule 3, on every lead: cash the top remaining card of a side suit no opponent can trump (the suit
        /// with the most cards still out, so it is least likely to be trumped), else the top remaining spade.
        /// Without such a card: while tricks are needed, the lowest card of a side suit no opponent can trump
        /// (ties: the shorter suit, heading for a void), else the lowest spade; once the call is made, rule 2's
        /// lowest legal card.
        /// </summary>
        private static int Lead(RoundState table, int me, ulong hand, ulong unseen, bool needsTricks)
        {
            int winner = -1;
            int winnerOutstanding = -1;
            int low = -1;
            int lowRank = 0;
            int lowLength = 0;
            for (int suit = 0; suit < Bits.Spades; suit++)
            {
                ulong suitCards = Bits.OfSuit(suit);
                ulong mine = hand & suitCards;
                if (mine == 0UL || OpponentMayTrump(table, me, suitCards, unseen))
                {
                    continue;
                }

                int top = Bits.Highest(mine);
                int outstanding = Bits.Count(unseen & suitCards);
                if (IsTop(top, unseen) && outstanding > winnerOutstanding)
                {
                    winner = top;
                    winnerOutstanding = outstanding;
                }

                int lowest = Bits.Lowest(mine);
                int rank = Bits.RankOf(lowest);
                int length = Bits.Count(mine);
                if (low < 0 || rank < lowRank || (rank == lowRank && length < lowLength))
                {
                    low = lowest;
                    lowRank = rank;
                    lowLength = length;
                }
            }

            if (winner >= 0)
            {
                return winner;
            }

            ulong spades = hand & Bits.SpadeCards;
            if (spades != 0UL)
            {
                int topSpade = Bits.Highest(spades);
                if (IsTop(topSpade, unseen))
                {
                    return topSpade;
                }
            }

            if (!needsTricks)
            {
                return LowestLegal(hand, hand);
            }

            if (low >= 0)
            {
                return low;
            }

            return spades == 0UL ? LowestLegal(hand, hand) : Bits.Lowest(spades);
        }

        /// <summary>
        /// Rules 1 and 4 while the call is not yet made: last to play, win as cheaply as possible; following
        /// suit earlier, play high only with the top remaining card (and only if no later seat is known to be
        /// able to trump it), else low; void, trump with the lowest spade that wins; unable to win, discard.
        /// </summary>
        private static int FollowForTricks(RoundState table, ulong hand, ulong legal, ulong beating, ulong unseen)
        {
            if (beating == 0UL)
            {
                return Discard(legal, hand, unseen);
            }

            ulong ledCards = Bits.OfSuit(table.LedSuit);
            if (table.Count == Trick.Size - 1 || (hand & ledCards) == 0UL)
            {
                return Cheapest(beating);
            }

            int top = Bits.Highest(legal);
            if ((beating & (1UL << top)) != 0UL
                && IsTop(top, unseen)
                && (table.LedSuit == Bits.Spades || !LaterSeatMayTrump(table, ledCards, unseen)))
            {
                return top;
            }

            return Bits.Lowest(legal);
        }

        /// <summary>
        /// Rule 2 once the call is made: the lowest legal card, unless the trick is going to an opponent who
        /// still needs tricks and the cheapest winning card is sure to hold it (last to play, or no later seat
        /// can beat it).
        /// </summary>
        private static int FollowWithCallMade(RoundState table, ulong hand, ulong legal, ulong beating, ulong unseen)
        {
            if (beating != 0UL && table.Calls[table.WinningSeat] > table.Won[table.WinningSeat])
            {
                int cheapest = Cheapest(beating);
                if (table.Count == Trick.Size - 1 || IsSecure(table, cheapest, unseen))
                {
                    return cheapest;
                }
            }

            return LowestLegal(legal, hand);
        }

        /// <summary>The cheapest of some winning cards: a side-suit card before a spade, then the lowest rank.</summary>
        private static int Cheapest(ulong winners)
        {
            ulong side = winners & ~Bits.SpadeCards;
            return side == 0UL ? Bits.Lowest(winners) : LowestLegal(side, side);
        }

        /// <summary>Whether no seat still to play can beat the card <paramref name="id"/> as far as the actor knows.</summary>
        private static bool IsSecure(RoundState table, int id, ulong unseen)
        {
            ulong above = Bits.Above(id);
            ulong suitCards = Bits.OfSuit(Bits.SuitOf(id));
            bool isTrump = id >= Bits.FirstSpade;
            for (int position = table.Count + 1; position < Trick.Size; position++)
            {
                ulong possible = unseen & ~table.Excluded[table.SeatAt(position)];
                if ((possible & above) != 0UL)
                {
                    return false;
                }

                if (!isTrump && (possible & suitCards) == 0UL && (possible & Bits.SpadeCards) != 0UL)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether a seat still to play is known void in the led suit and may hold a spade.</summary>
        private static bool LaterSeatMayTrump(RoundState table, ulong ledCards, ulong unseen)
        {
            for (int position = table.Count + 1; position < Trick.Size; position++)
            {
                ulong possible = unseen & ~table.Excluded[table.SeatAt(position)];
                if ((possible & ledCards) == 0UL && (possible & Bits.SpadeCards) != 0UL)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>GDD §9 rule 3: an opponent is void in the suit and may still hold spades.</summary>
        private static bool OpponentMayTrump(RoundState table, int me, ulong suitCards, ulong unseen)
        {
            for (int seat = 0; seat < SeatExtensions.Count; seat++)
            {
                if (seat == me)
                {
                    continue;
                }

                ulong possible = unseen & ~table.Excluded[seat];
                if ((possible & suitCards) == 0UL && (possible & Bits.SpadeCards) != 0UL)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
