using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Bots
{
    /// <summary>
    /// The public state of a round in progress as plain integers and card masks (see <see cref="Bits"/>): cards
    /// played, calls and tricks won, what each seat has shown it cannot hold, and the trick in progress.
    /// <see cref="CardTracker"/> fills one from a view and shows it through Core types; Hard's rollouts advance
    /// one card by card with <see cref="Play"/>. Its fields are read directly by the rollout code, which runs
    /// tens of thousands of times per decision.
    /// <para>
    /// <see cref="Legal"/> and the inferences in <see cref="Play"/> restate <c>LegalMoves</c> for masks; tests
    /// hold them to it for every combination of the rule switches. A strategy's own card always comes from
    /// <c>view.LegalCards()</c>; this copy of the rules only steers rollouts.
    /// </para>
    /// </summary>
    internal sealed class RoundState
    {
        private const int SeatMask = SeatExtensions.Count - 1;

        /// <summary>Per seat: cards it has shown it cannot hold.</summary>
        internal readonly ulong[] Excluded = new ulong[SeatExtensions.Count];

        /// <summary>Per seat: cards it has played this round.</summary>
        internal readonly ulong[] PlayedBy = new ulong[SeatExtensions.Count];

        /// <summary>Per seat: its call, or <see cref="CallRules.NotCalled"/>.</summary>
        internal readonly int[] Calls = new int[SeatExtensions.Count];

        /// <summary>Per seat: tricks won this round.</summary>
        internal readonly int[] Won = new int[SeatExtensions.Count];

        /// <summary>Per seat: cards in hand.</summary>
        internal readonly int[] HandCounts = new int[SeatExtensions.Count];

        /// <summary>The ids of the cards in the trick in progress, in play order.</summary>
        internal readonly int[] TrickIds = new int[Trick.Size];

        /// <summary>The rule set the switches below were read from.</summary>
        internal readonly RuleSet Rules;

        /// <summary>
        /// How far <c>Seat.Next(direction)</c> moves in seat numbers (1 or 3), read from <c>Seat.Next</c> itself
        /// so the direction is not decided here.
        /// </summary>
        internal readonly int Step;

        /// <summary><see cref="RuleSet.MustBeatLedSuit"/>.</summary>
        internal readonly bool MustBeat;

        /// <summary><see cref="RuleSet.MustTrumpWhenVoid"/>.</summary>
        internal readonly bool MustTrump;

        /// <summary><see cref="RuleSet.MustOvertrump"/>.</summary>
        internal readonly bool MustOvertrump;

        /// <summary><see cref="RuleSet.DiscardWhenCannotOvertrump"/>.</summary>
        internal readonly bool FreeDiscard;

        /// <summary>Every card played this round, including the trick in progress.</summary>
        internal ulong Played;

        /// <summary>Tricks completed this round.</summary>
        internal int Completed;

        /// <summary>The seat that leads (or led) the trick in progress.</summary>
        internal int Leader;

        /// <summary>Cards in the trick in progress, 0–3.</summary>
        internal int Count;

        /// <summary>The led suit; meaningful while <see cref="Count"/> is not 0.</summary>
        internal int LedSuit;

        /// <summary>The card winning the trick in progress; meaningful while <see cref="Count"/> is not 0.</summary>
        internal int WinningId;

        /// <summary>The seat that played <see cref="WinningId"/>.</summary>
        internal int WinningSeat;

        internal RoundState(RuleSet rules)
        {
            Rules = rules;
            Step = (int)Seat.South.Next(rules.Direction);
            MustBeat = rules.MustBeatLedSuit;
            MustTrump = rules.MustTrumpWhenVoid;
            MustOvertrump = rules.MustOvertrump;
            FreeDiscard = rules.DiscardWhenCannotOvertrump;
        }

        /// <summary>The seat that plays the card at <paramref name="position"/> of the trick in progress.</summary>
        internal int SeatAt(int position)
        {
            return (Leader + (position * Step)) & SeatMask;
        }

        /// <summary>Makes this state equal to <paramref name="source"/>, which was built for the same rules.</summary>
        internal void CopyFrom(RoundState source)
        {
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                Excluded[s] = source.Excluded[s];
                PlayedBy[s] = source.PlayedBy[s];
                Calls[s] = source.Calls[s];
                Won[s] = source.Won[s];
                HandCounts[s] = source.HandCounts[s];
                TrickIds[s] = source.TrickIds[s];
            }

            Played = source.Played;
            Completed = source.Completed;
            Leader = source.Leader;
            Count = source.Count;
            LedSuit = source.LedSuit;
            WinningId = source.WinningId;
            WinningSeat = source.WinningSeat;
        }

        /// <summary>Starts an empty trick led by <paramref name="leader"/>.</summary>
        internal void Lead(int leader)
        {
            Leader = leader;
            Count = 0;
        }

        /// <summary>
        /// The cards <paramref name="hand"/> may play to the trick in progress: <c>LegalMoves.Compute</c> on
        /// masks. While no spade is in the trick the winning card is the best card of the led suit; once one is,
        /// the winning card is the best spade.
        /// </summary>
        internal ulong Legal(ulong hand)
        {
            if (Count == 0)
            {
                return hand;
            }

            bool spadeWinning = WinningId >= Bits.FirstSpade;
            ulong follow = hand & Bits.OfSuit(LedSuit);
            if (follow != 0UL)
            {
                if (MustBeat && (LedSuit == Bits.Spades || !spadeWinning))
                {
                    ulong higher = follow & Bits.Above(WinningId);
                    if (higher != 0UL)
                    {
                        return higher;
                    }
                }

                return follow;
            }

            ulong spades = hand & Bits.SpadeCards;
            if (MustTrump && spades != 0UL)
            {
                if (!(spadeWinning && MustOvertrump))
                {
                    return spades;
                }

                ulong winners = spades & Bits.Above(WinningId);
                if (winners != 0UL)
                {
                    return winners;
                }

                if (!FreeDiscard)
                {
                    return spades;
                }
            }

            return hand;
        }

        /// <summary>
        /// Advances the round by one legal play, as the engine would: notes what the play proves about the rest of
        /// the seat's hand (the negation of the <see cref="Legal"/> branches), and after the fourth card credits
        /// the winner, who leads the next trick.
        /// </summary>
        internal void Play(int seat, int id)
        {
            int suit = Bits.SuitOf(id);
            if (Count == 0)
            {
                LedSuit = suit;
                WinningId = id;
                WinningSeat = seat;
            }
            else
            {
                bool spadeWinning = WinningId >= Bits.FirstSpade;
                if (suit == LedSuit)
                {
                    // Followed without beating the best card although it had to beat it if it could.
                    if (MustBeat && id < WinningId && (LedSuit == Bits.Spades || !spadeWinning))
                    {
                        Excluded[seat] |= Bits.Above(WinningId);
                    }
                }
                else
                {
                    ulong excluded = Bits.OfSuit(LedSuit);
                    if (MustTrump)
                    {
                        bool hadToOvertrump = MustOvertrump && spadeWinning;
                        if (suit == Bits.Spades)
                        {
                            if (hadToOvertrump && id < WinningId)
                            {
                                excluded |= Bits.Above(WinningId);
                            }
                        }
                        else if (hadToOvertrump && FreeDiscard)
                        {
                            excluded |= Bits.Above(WinningId);
                        }
                        else
                        {
                            excluded |= Bits.SpadeCards;
                        }
                    }

                    Excluded[seat] |= excluded;
                }

                // Spade ids are above every other id, so a spade beats any side-suit winner by this comparison too.
                if (id > WinningId && (suit == Bits.Spades || suit == LedSuit))
                {
                    WinningId = id;
                    WinningSeat = seat;
                }
            }

            ulong card = 1UL << id;
            Played |= card;
            PlayedBy[seat] |= card;
            HandCounts[seat]--;
            TrickIds[Count] = id;
            Count++;
            if (Count == Trick.Size)
            {
                Won[WinningSeat]++;
                Completed++;
                Leader = WinningSeat;
                Count = 0;
            }
        }
    }
}
