using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Bots
{
    /// <summary>
    /// The Normal play heuristics (GDD §9 "Normal play heuristics" 1–5) as a pure function of the round's
    /// public knowledge and the acting seat's hand. <see cref="NormalStrategy"/> asks it for the viewer;
    /// <see cref="HardStrategy"/>'s rollouts ask it for every seat of a sampled deal, so it never allocates.
    /// "Unseen" here is always from the acting seat's point of view: the cards neither in its hand nor played.
    /// </summary>
    internal static class NormalPolicy
    {
        /// <summary>
        /// The Normal card for <paramref name="me"/> holding <paramref name="hand"/> at <paramref name="table"/>'s
        /// current trick. Always one of <c>LegalMoves.Compute(hand, trick, rules)</c>.
        /// </summary>
        internal static Card ChooseCard(CardTracker table, Seat me, CardSet hand)
        {
            Trick trick = table.CurrentTrick;
            CardSet legal = LegalMoves.Compute(hand, trick, table.Rules);
            if (legal.Count == 1)
            {
                return legal.Lowest();
            }

            CardSet unseen = CardSet.FullDeck - table.Played - hand;
            bool needsTricks = table.TricksNeededBy(me) > 0;
            if (trick.IsEmpty)
            {
                return Lead(table, me, hand, unseen, needsTricks);
            }

            CardSet beating = Beating(legal, TrickResolver.WinningCard(trick));
            return needsTricks
                ? FollowForTricks(table, trick, hand, legal, beating, unseen)
                : FollowWithCallMade(table, trick, hand, legal, beating, unseen);
        }

        /// <summary>The cards of <paramref name="legal"/> that would take the lead from <paramref name="winning"/>.</summary>
        internal static CardSet Beating(CardSet legal, Card winning)
        {
            CardSet higher = legal & CardSet.Above(winning);
            return winning.IsTrump ? higher : higher | legal.InSuit(Suit.Spades);
        }

        /// <summary>
        /// GDD §9 rule 2's "lowest legal card": the lowest-ranked side-suit card (ties: the longer suit, then
        /// ♣ ♦ ♥), or the lowest spade when only spades are legal.
        /// </summary>
        internal static Card LowestLegal(CardSet legal, CardSet hand)
        {
            CardSet side = legal - CardSet.OfSuit(Suit.Spades);
            if (side.IsEmpty)
            {
                return legal.Lowest();
            }

            bool found = false;
            Card pick = default;
            int pickLength = 0;
            for (int s = 0; s < (int)Suit.Spades; s++)
            {
                var suit = (Suit)s;
                CardSet cards = side.InSuit(suit);
                if (cards.IsEmpty)
                {
                    continue;
                }

                Card low = cards.Lowest();
                int length = hand.InSuit(suit).Count;
                if (!found || low.Rank < pick.Rank || (low.Rank == pick.Rank && length > pickLength))
                {
                    found = true;
                    pick = low;
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
        internal static Card Discard(CardSet legal, CardSet hand, CardSet unseen)
        {
            CardSet side = legal - CardSet.OfSuit(Suit.Spades);
            if (side.IsEmpty)
            {
                return legal.Lowest();
            }

            bool found = false;
            Card pick = default;
            int pickKey = 0;
            for (int s = 0; s < (int)Suit.Spades; s++)
            {
                var suit = (Suit)s;
                CardSet cards = side.InSuit(suit);
                if (cards.IsEmpty)
                {
                    continue;
                }

                // Not a winner first, then the longer suit, then the lower card.
                Card low = cards.Lowest();
                int key = (IsTop(low, unseen) ? 0 : 1 << 10) | (hand.InSuit(suit).Count << 5) | (15 - (int)low.Rank);
                if (!found || key > pickKey)
                {
                    found = true;
                    pick = low;
                    pickKey = key;
                }
            }

            return pick;
        }

        /// <summary>Whether no unseen card of the suit outranks <paramref name="card"/>.</summary>
        internal static bool IsTop(Card card, CardSet unseen)
        {
            return !unseen.Overlaps(CardSet.Above(card));
        }

        /// <summary>
        /// Rule 3, on every lead: cash the top remaining card of a side suit no opponent can trump (the suit
        /// with the most cards still out, so it is least likely to be trumped), else the top remaining spade.
        /// Without such a card: while tricks are needed, the lowest card of a side suit no opponent can trump
        /// (ties: the shorter suit, heading for a void), else the lowest spade; once the call is made, rule 2's
        /// lowest legal card.
        /// </summary>
        private static Card Lead(CardTracker table, Seat me, CardSet hand, CardSet unseen, bool needsTricks)
        {
            bool haveWinner = false;
            Card winner = default;
            int winnerOutstanding = -1;
            bool haveLow = false;
            Card low = default;
            int lowLength = 0;
            for (int s = 0; s < (int)Suit.Spades; s++)
            {
                var suit = (Suit)s;
                CardSet mine = hand.InSuit(suit);
                if (mine.IsEmpty || OpponentMayTrump(table, me, suit, unseen))
                {
                    continue;
                }

                Card top = mine.Highest();
                int outstanding = unseen.InSuit(suit).Count;
                if (IsTop(top, unseen) && outstanding > winnerOutstanding)
                {
                    haveWinner = true;
                    winner = top;
                    winnerOutstanding = outstanding;
                }

                Card lowest = mine.Lowest();
                if (!haveLow || lowest.Rank < low.Rank || (lowest.Rank == low.Rank && mine.Count < lowLength))
                {
                    haveLow = true;
                    low = lowest;
                    lowLength = mine.Count;
                }
            }

            if (haveWinner)
            {
                return winner;
            }

            CardSet spades = hand.InSuit(Suit.Spades);
            if (!spades.IsEmpty && IsTop(spades.Highest(), unseen))
            {
                return spades.Highest();
            }

            if (!needsTricks)
            {
                return LowestLegal(hand, hand);
            }

            if (haveLow)
            {
                return low;
            }

            return spades.IsEmpty ? LowestLegal(hand, hand) : spades.Lowest();
        }

        /// <summary>
        /// Rules 1 and 4 while the call is not yet made: last to play, win as cheaply as possible; following
        /// suit earlier, play high only with the top remaining card (and only if no later seat is known to be
        /// able to trump it), else low; void, trump with the lowest spade that wins; unable to win, discard.
        /// </summary>
        private static Card FollowForTricks(CardTracker table, Trick trick, CardSet hand, CardSet legal, CardSet beating, CardSet unseen)
        {
            if (beating.IsEmpty)
            {
                return Discard(legal, hand, unseen);
            }

            Suit led = trick.LedSuit;
            if (trick.Count == Trick.Size - 1 || !hand.HasSuit(led))
            {
                return Cheapest(beating);
            }

            Card top = legal.Highest();
            if (beating.Contains(top)
                && IsTop(top, unseen)
                && (led == Suit.Spades || !LaterSeatMayTrump(table, trick, led, unseen)))
            {
                return top;
            }

            return legal.Lowest();
        }

        /// <summary>
        /// Rule 2 once the call is made: the lowest legal card, unless the trick is going to an opponent who
        /// still needs tricks and the cheapest winning card is sure to hold it (last to play, or no later seat
        /// can beat it).
        /// </summary>
        private static Card FollowWithCallMade(CardTracker table, Trick trick, CardSet hand, CardSet legal, CardSet beating, CardSet unseen)
        {
            if (!beating.IsEmpty && table.TricksNeededBy(TrickResolver.Winner(trick)) > 0)
            {
                Card cheapest = Cheapest(beating);
                if (trick.Count == Trick.Size - 1 || IsSecure(table, trick, cheapest, unseen))
                {
                    return cheapest;
                }
            }

            return LowestLegal(legal, hand);
        }

        /// <summary>The cheapest of some winning cards: a side-suit card before a spade, then the lowest rank.</summary>
        private static Card Cheapest(CardSet winners)
        {
            CardSet side = winners - CardSet.OfSuit(Suit.Spades);
            return side.IsEmpty ? winners.Lowest() : LowestLegal(side, side);
        }

        /// <summary>Whether no seat still to play can beat <paramref name="card"/> as far as the actor knows.</summary>
        private static bool IsSecure(CardTracker table, Trick trick, Card card, CardSet unseen)
        {
            for (int i = trick.Count + 1; i < Trick.Size; i++)
            {
                CardSet possible = unseen - table.CannotHold(trick.SeatAt(i));
                if (possible.Overlaps(CardSet.Above(card)))
                {
                    return false;
                }

                if (!card.IsTrump && !possible.HasSuit(card.Suit) && possible.HasSuit(Suit.Spades))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Whether a seat still to play is known void in <paramref name="led"/> and may hold a spade.</summary>
        private static bool LaterSeatMayTrump(CardTracker table, Trick trick, Suit led, CardSet unseen)
        {
            for (int i = trick.Count + 1; i < Trick.Size; i++)
            {
                CardSet possible = unseen - table.CannotHold(trick.SeatAt(i));
                if (!possible.HasSuit(led) && possible.HasSuit(Suit.Spades))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>GDD §9 rule 3: an opponent is void in <paramref name="suit"/> and may still hold spades.</summary>
        private static bool OpponentMayTrump(CardTracker table, Seat me, Suit suit, CardSet unseen)
        {
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                if (s == (int)me)
                {
                    continue;
                }

                CardSet possible = unseen - table.CannotHold((Seat)s);
                if (!possible.HasSuit(suit) && possible.HasSuit(Suit.Spades))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
