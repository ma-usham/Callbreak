using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;

namespace Callbreak.Bots
{
    /// <summary>
    /// What one seat can know about the round in progress (GDD §9 "CardTracker: seen cards · voids · calls vs
    /// won"; Normal "Memory: played cards, inferred voids"; Hard "full card tracking"). It is rebuilt from a
    /// <see cref="PlayerView"/> for every decision, so strategies stay stateless (GDD §9 rule 5: a void lasts
    /// for the rest of the round because every later view still holds the trick that revealed it).
    /// <para>
    /// Besides the viewer's hand, it holds only public facts: the cards played this round, the calls and tricks
    /// won, and for every seat the cards it <see cref="CannotHold"/>. Those are read off each play with the same
    /// legality rules the engine applies (<see cref="LegalMoves"/>): a seat that does not follow suit is void in
    /// it; one that follows without beating the best card when it had to beat holds nothing higher in that
    /// suit; one that is void and neither trumps nor overtrumps when it had to holds no (higher) spade. A seat's
    /// <see cref="PossibleCards"/> are the unseen cards it is not excluded from.
    /// </para>
    /// </summary>
    public sealed class CardTracker
    {
        private readonly CardSet[] _cannotHold = new CardSet[SeatExtensions.Count];
        private readonly CardSet[] _playedBy = new CardSet[SeatExtensions.Count];
        private readonly int[] _calls = new int[SeatExtensions.Count];
        private readonly int[] _won = new int[SeatExtensions.Count];
        private readonly int[] _handCounts = new int[SeatExtensions.Count];
        private CardSet _hand;
        private CardSet _played;
        private Trick _trick;
        private int _trickIndex;

        /// <summary>Rebuilds the round's knowledge from what <paramref name="view"/> shows its viewer.</summary>
        /// <param name="view">Any view; before the first card the tracker simply knows the viewer's hand.</param>
        /// <exception cref="ArgumentNullException"><paramref name="view"/> is null.</exception>
        public CardTracker(PlayerView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            Rules = view.Rules;
            Viewer = view.Viewer;
            _hand = view.Hand;
            IReadOnlyList<CompletedTrick> completed = view.CompletedTricks;
            for (int i = 0; i < completed.Count; i++)
            {
                Replay(completed[i].Trick);
            }

            Replay(view.CurrentTrick);
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                // The view is authoritative for counts; the replay above only infers what seats cannot hold.
                _calls[s] = view.CallOf((Seat)s);
                _won[s] = view.TricksWonBy((Seat)s);
                _handCounts[s] = view.HandCountOf((Seat)s);
            }

            _trick = view.CurrentTrick;
            _trickIndex = completed.Count;
        }

        private CardTracker(CardTracker source)
        {
            Rules = source.Rules;
            Viewer = source.Viewer;
            CopyFrom(source);
        }

        /// <summary>The seat whose knowledge this is.</summary>
        public Seat Viewer { get; }

        /// <summary>The match's rule set; the inferences follow its legality switches.</summary>
        public RuleSet Rules { get; }

        /// <summary>The viewer's cards.</summary>
        public CardSet Hand => _hand;

        /// <summary>Every card played this round: completed tricks and the trick in progress.</summary>
        public CardSet Played => _played;

        /// <summary>
        /// The cards the viewer has not seen: neither in its hand nor played. Exactly the other three hands.
        /// </summary>
        public CardSet Unseen => CardSet.FullDeck - _hand - _played;

        /// <summary>The trick in progress.</summary>
        public Trick CurrentTrick => _trick;

        /// <summary>Tricks completed this round (the index of the trick in progress, 0–12).</summary>
        public int TrickIndex => _trickIndex;

        /// <summary>A seat's call this round, or <see cref="CallRules.NotCalled"/>.</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The call.</returns>
        public int CallOf(Seat seat)
        {
            return _calls[Index(seat)];
        }

        /// <summary>Tricks a seat has won this round.</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The count.</returns>
        public int TricksWonBy(Seat seat)
        {
            return _won[Index(seat)];
        }

        /// <summary>
        /// Tricks a seat still needs to meet its call ("calls vs won"); 0 once met, and 0 before it has called.
        /// </summary>
        /// <param name="seat">Any seat.</param>
        /// <returns><c>max(0, call − won)</c>.</returns>
        public int TricksNeededBy(Seat seat)
        {
            int index = Index(seat);
            int call = _calls[index];
            return call == CallRules.NotCalled || _won[index] >= call ? 0 : call - _won[index];
        }

        /// <summary>How many cards a seat holds.</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The count.</returns>
        public int HandCountOf(Seat seat)
        {
            return _handCounts[Index(seat)];
        }

        /// <summary>The cards a seat has played this round.</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>Its played cards.</returns>
        public CardSet PlayedBy(Seat seat)
        {
            return _playedBy[Index(seat)];
        }

        /// <summary>
        /// Cards a seat has shown it cannot hold: whole suits it failed to follow (GDD §9 rule 5), led-suit cards
        /// above a card it had to beat and did not, and spades (or spades above the best one played) when it was
        /// void and did not trump (or overtrump) although the rules required it.
        /// </summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The excluded cards (they may include cards already played).</returns>
        public CardSet CannotHold(Seat seat)
        {
            return _cannotHold[Index(seat)];
        }

        /// <summary>
        /// The cards a seat may still hold as far as the viewer knows: its own hand for the viewer, otherwise
        /// the <see cref="Unseen"/> cards the seat is not excluded from.
        /// </summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The possible cards.</returns>
        public CardSet PossibleCards(Seat seat)
        {
            int index = Index(seat);
            return seat == Viewer ? _hand : Unseen - _cannotHold[index];
        }

        /// <summary>
        /// Whether a seat is known to hold no card of <paramref name="suit"/>: it failed to follow the suit, or
        /// none of the suit's unseen cards can be in its hand.
        /// </summary>
        /// <param name="seat">Any seat.</param>
        /// <param name="suit">The suit.</param>
        /// <returns>True when the seat is void in the suit.</returns>
        public bool IsVoid(Seat seat, Suit suit)
        {
            return !PossibleCards(seat).HasSuit(suit);
        }

        /// <summary>Whether a seat may still hold a spade to trump with (GDD §9 rule 3 "if they still hold spades").</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>False once the seat is known to have no spades.</returns>
        public bool MayHoldSpades(Seat seat)
        {
            return PossibleCards(seat).HasSuit(Suit.Spades);
        }

        /// <summary>
        /// Whether <paramref name="card"/> outranks every unseen card of its suit ("the top remaining card of the
        /// suit", GDD §9 rules 1 and 3). The viewer's own higher cards do not count against it.
        /// </summary>
        /// <param name="card">Any card, normally one in the viewer's hand.</param>
        /// <returns>True when no other hand can hold a higher card of the suit.</returns>
        public bool IsTopRemaining(Card card)
        {
            return !Unseen.Overlaps(CardSet.Above(card));
        }

        /// <summary>A copy that can be advanced independently (a Monte-Carlo rollout).</summary>
        internal CardTracker Clone()
        {
            return new CardTracker(this);
        }

        /// <summary>Makes this tracker equal to <paramref name="source"/>, which must share its rules and viewer.</summary>
        internal void CopyFrom(CardTracker source)
        {
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                _cannotHold[s] = source._cannotHold[s];
                _playedBy[s] = source._playedBy[s];
                _calls[s] = source._calls[s];
                _won[s] = source._won[s];
                _handCounts[s] = source._handCounts[s];
            }

            _hand = source._hand;
            _played = source._played;
            _trick = source._trick;
            _trickIndex = source._trickIndex;
        }

        /// <summary>Sets a seat's call for a rollout that tries out calls.</summary>
        internal void SetCall(Seat seat, int call)
        {
            _calls[(int)seat] = call;
        }

        /// <summary>
        /// Advances the round by one play, as the engine would: records what the play reveals, and after the
        /// fourth card credits the winner, who leads the next trick. Used by rollouts; the caller guarantees the
        /// play is legal.
        /// </summary>
        internal void Record(Seat seat, Card card)
        {
            Infer(seat, card, _trick);
            _played = _played.With(card);
            _playedBy[(int)seat] = _playedBy[(int)seat].With(card);
            _handCounts[(int)seat]--;
            if (seat == Viewer)
            {
                _hand = _hand.Without(card);
            }

            _trick = _trick.With(card);
            if (_trick.IsComplete)
            {
                Seat winner = TrickResolver.Winner(_trick);
                _won[(int)winner]++;
                _trickIndex++;
                _trick = new Trick(winner, _trick.Direction);
            }
        }

        private void Replay(Trick trick)
        {
            var partial = new Trick(trick.Leader, trick.Direction);
            for (int i = 0; i < trick.Count; i++)
            {
                Seat seat = trick.SeatAt(i);
                Card card = trick[i];
                Infer(seat, card, partial);
                _played = _played.With(card);
                _playedBy[(int)seat] = _playedBy[(int)seat].With(card);
                partial = partial.With(card);
            }
        }

        /// <summary>
        /// What playing <paramref name="card"/> to <paramref name="before"/> proves about the rest of the seat's
        /// hand, given that the play was legal (the exact negation of the <see cref="LegalMoves"/> branches).
        /// </summary>
        private void Infer(Seat seat, Card card, in Trick before)
        {
            if (before.IsEmpty)
            {
                return;
            }

            Suit led = before.LedSuit;
            CardSet trickCards = before.Cards;
            CardSet excluded = CardSet.Empty;
            if (card.Suit == led)
            {
                if (Rules.MustBeatLedSuit && !before.IsTrumped)
                {
                    Card best = trickCards.InSuit(led).Highest();
                    if (!CardSet.Above(best).Contains(card))
                    {
                        excluded = CardSet.Above(best);
                    }
                }
            }
            else
            {
                excluded = CardSet.OfSuit(led);
                if (Rules.MustTrumpWhenVoid)
                {
                    CardSet playedSpades = trickCards.InSuit(Suit.Spades);
                    bool mustOvertrump = Rules.MustOvertrump && !playedSpades.IsEmpty;
                    if (card.IsTrump)
                    {
                        if (mustOvertrump && !CardSet.Above(playedSpades.Highest()).Contains(card))
                        {
                            excluded |= CardSet.Above(playedSpades.Highest());
                        }
                    }
                    else if (mustOvertrump && Rules.DiscardWhenCannotOvertrump)
                    {
                        excluded |= CardSet.Above(playedSpades.Highest());
                    }
                    else
                    {
                        excluded |= CardSet.OfSuit(Suit.Spades);
                    }
                }
            }

            _cannotHold[(int)seat] |= excluded;
        }

        private static int Index(Seat seat)
        {
            if (!seat.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(seat), seat, "Seat must be 0-3.");
            }

            return (int)seat;
        }
    }
}
