using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;

namespace Callbreak.Core.Match
{
    /// <summary>
    /// The rules engine and the only mutator of match state (GDD §22). It validates commands, applies them,
    /// and appends events to an outbox that the caller drains. It has no clock and no bots: dealing, trick
    /// resolution and scoring happen inside <see cref="Start"/>/<c>Execute</c>, and timers or bots outside
    /// Core submit ordinary commands. Deterministic: the same rules, randomness and commands produce the
    /// same events. Not thread-safe; drive one engine from one thread.
    /// </summary>
    public sealed class MatchEngine
    {
        // A redeal is ~5% likely per deal; a thousand in a row means the IRandom is broken, not unlucky.
        private const int MaxRedealsPerRound = 1000;

        private readonly MatchState _state;
        private readonly IRandom _rng;
        private readonly List<MatchEvent> _outbox = new List<MatchEvent>();

        /// <summary>Creates a match in <see cref="Match.Phase.Lobby"/>. Call <see cref="Start"/> to deal round 1.</summary>
        /// <param name="rules">The rule set; fixed for the whole match.</param>
        /// <param name="rng">All randomness: the round-1 dealer (unless fixed) and every shuffle.</param>
        /// <param name="setup">Match id, seat profiles and optional first dealer.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public MatchEngine(RuleSet rules, IRandom rng, MatchSetup setup)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _state = new MatchState(
                rules,
                setup.MatchId,
                setup.Seats,
                setup.FirstDealer.HasValue,
                setup.FirstDealer ?? Seat.South);
        }

        private MatchEngine(MatchState state, IRandom rng)
        {
            _state = state;
            _rng = rng;
        }

        /// <summary>The phase waiting for input (GDD §7). Never a transient phase between calls.</summary>
        public Phase Phase => _state.Phase;

        /// <summary>
        /// The seat whose call or play turn is open. Meaningful while <see cref="CurrentTurn"/> is not
        /// <see cref="TurnId.None"/>; in the round summary it is the last trick's winner.
        /// </summary>
        public Seat CurrentSeat => _state.CurrentSeat;

        /// <summary>The open call or play turn; <see cref="TurnId.None"/> in every other phase. Increases by one per turn.</summary>
        public TurnId CurrentTurn => _state.CurrentTurn;

        /// <summary>The rule set.</summary>
        public RuleSet Rules => _state.Rules;

        /// <summary>The match id.</summary>
        public string MatchId => _state.MatchId;

        /// <summary>The current round number (0 before <see cref="Start"/>).</summary>
        public int Round => _state.Round;

        /// <summary>
        /// Rebuilds an engine from <see cref="Snapshot"/> output. Its views equal the original's and it accepts
        /// the same next command. The outbox starts empty. Future shuffles come from <paramref name="rng"/>;
        /// pass a <see cref="Pcg32Random"/> rebuilt with <see cref="Pcg32Random.FromState"/> to continue a
        /// seeded match exactly.
        /// </summary>
        /// <param name="snapshot">A snapshot taken by this schema version.</param>
        /// <param name="rules">The match's rule set; its id must match the snapshot's.</param>
        /// <param name="rng">Randomness for the rest of the match.</param>
        /// <returns>The restored engine.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> or <paramref name="rng"/> is null.</exception>
        /// <exception cref="ArgumentException">The snapshot is invalid; discard it (GDD §22).</exception>
        public static MatchEngine Restore(MatchSnapshot snapshot, RuleSet rules, IRandom rng)
        {
            if (!TryRestore(snapshot, rules, rng, out MatchEngine engine, out string error))
            {
                throw new ArgumentException(error, nameof(snapshot));
            }

            return engine;
        }

        /// <summary>Like <see cref="Restore"/>, but reports an invalid snapshot as false with a reason.</summary>
        /// <param name="snapshot">A snapshot, possibly corrupt or from another rule set.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <param name="rng">Randomness for the rest of the match.</param>
        /// <param name="engine">The restored engine, or null.</param>
        /// <param name="error">Why the snapshot was refused, or null.</param>
        /// <returns>Whether the snapshot was restored.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> or <paramref name="rng"/> is null.</exception>
        public static bool TryRestore(MatchSnapshot snapshot, RuleSet rules, IRandom rng, out MatchEngine engine, out string error)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            engine = null;
            if (!MatchSnapshots.TryRebuild(snapshot, rules, out MatchState state, out error))
            {
                return false;
            }

            engine = new MatchEngine(state, rng);
            return true;
        }

        /// <summary>
        /// Starts the match: emits <see cref="MatchStarted"/>, picks the round-1 dealer (fixed, or drawn from the
        /// randomness), deals round 1 and opens the first call turn.
        /// </summary>
        /// <exception cref="InvalidOperationException">The match is not in <see cref="Match.Phase.Lobby"/>.</exception>
        public void Start()
        {
            if (_state.Phase != Phase.Lobby)
            {
                throw new InvalidOperationException("Start is allowed once, in Lobby; the phase is " + _state.Phase + ".");
            }

            if (!_state.HasFixedFirstDealer)
            {
                _state.FirstDealer = (Seat)_rng.NextInt(SeatExtensions.Count);
            }

            Emit(new MatchStarted(_state.MatchId, _state.Rules.Id, _state.FirstDealer, _state.Profiles));
            BeginRound(1, _state.FirstDealer);
        }

        /// <summary>
        /// Places a call. Checks, in order (GDD §7): phase is Calling (<see cref="RejectReason.WrongPhase"/>);
        /// the seat is valid and holds the turn (<see cref="RejectReason.InvalidSeat"/>,
        /// <see cref="RejectReason.NotYourTurn"/>); the turn id is the open turn (<see cref="RejectReason.StaleTurn"/>);
        /// the call is within MinCall…MaxCall (<see cref="RejectReason.InvalidCall"/>). Then emits
        /// <see cref="CallPlaced"/> and opens the next call turn, or after the fourth call the first play turn.
        /// </summary>
        /// <param name="command">The call.</param>
        /// <returns>Accepted, or rejected with the first failed check; a rejection changes nothing.</returns>
        public CommandResult Execute(in PlaceCall command)
        {
            RejectReason reason = CheckTurn(Phase.Calling, command.Seat, command.TurnId);
            if (reason == RejectReason.None && !CallRules.IsValid(command.Call, _state.Rules))
            {
                reason = RejectReason.InvalidCall;
            }

            if (reason != RejectReason.None)
            {
                return CommandResult.Rejected(reason);
            }

            _state.Calls[(int)command.Seat] = command.Call;
            Emit(new CallPlaced(command.Seat, command.Call, command.IsAuto));
            Seat next = command.Seat.Next(_state.Direction);
            if (_state.Calls[(int)next] == CallRules.NotCalled)
            {
                OpenCallTurn(next);
            }
            else
            {
                // Everyone has called: the dealer's right leads the first trick (GDD §3).
                _state.Phase = Phase.Playing;
                _state.Trick = new Trick(_state.FirstSeat, _state.Direction);
                PlayOn(_state.FirstSeat);
            }

            return CommandResult.Accepted;
        }

        /// <summary>
        /// Plays a card. Checks, in order: phase is Playing; the seat is valid and holds the turn; the turn id is
        /// the open turn; the card is in hand (<see cref="RejectReason.CardNotInHand"/>) and legal
        /// (<see cref="RejectReason.IllegalCard"/>, GDD §3). Then emits <see cref="CardPlayed"/>, resolves the
        /// trick after the fourth card, scores the round after the 13th trick, and opens the next turn.
        /// </summary>
        /// <param name="command">The play.</param>
        /// <returns>Accepted, or rejected with the first failed check; a rejection changes nothing.</returns>
        public CommandResult Execute(in PlayCard command)
        {
            RejectReason reason = CheckTurn(Phase.Playing, command.Seat, command.TurnId);
            if (reason == RejectReason.None)
            {
                CardSet hand = _state.Hands[(int)command.Seat];
                if (!hand.Contains(command.Card))
                {
                    reason = RejectReason.CardNotInHand;
                }
                else if (!LegalMoves.IsLegal(command.Card, hand, _state.Trick, _state.Rules))
                {
                    reason = RejectReason.IllegalCard;
                }
            }

            if (reason != RejectReason.None)
            {
                return CommandResult.Rejected(reason);
            }

            _state.CurrentTurn = TurnId.None;
            if (ApplyPlay(command.Seat, command.Card, command.IsAuto, isForced: false, out Seat next))
            {
                PlayOn(next);
            }

            return CommandResult.Accepted;
        }

        /// <summary>
        /// Marks a seat ready to leave the round summary. Checks, in order: phase is RoundSummary; the seat is
        /// valid; the seat is not already ready (<see cref="RejectReason.AlreadyReady"/>). Emits
        /// <see cref="ReadyConfirmed"/>. When every seat is ready, deals the next round or, after the last
        /// round, ends the match. Bot seats are ready automatically; to end the summary when a countdown
        /// expires, submit this for every seat that is not ready.
        /// </summary>
        /// <param name="command">The ready signal.</param>
        /// <returns>Accepted, or rejected with the first failed check; a rejection changes nothing.</returns>
        public CommandResult Execute(in ReadyNext command)
        {
            if (_state.Phase != Phase.RoundSummary)
            {
                return CommandResult.Rejected(RejectReason.WrongPhase);
            }

            if (!command.Seat.IsValid())
            {
                return CommandResult.Rejected(RejectReason.InvalidSeat);
            }

            if (_state.Ready[(int)command.Seat])
            {
                return CommandResult.Rejected(RejectReason.AlreadyReady);
            }

            _state.Ready[(int)command.Seat] = true;
            Emit(new ReadyConfirmed(command.Seat));
            LeaveSummaryIfAllReady();
            return CommandResult.Accepted;
        }

        /// <summary>
        /// Closes the table from any phase (GDD §7 "Closed · from any state": the host closes the room, all
        /// humans leave, or the server drains). Every later command is rejected with
        /// <see cref="RejectReason.WrongPhase"/>. Emits nothing; scores so far remain in views and snapshots.
        /// </summary>
        public void Close()
        {
            _state.Phase = Phase.Closed;
            _state.CurrentTurn = TurnId.None;
        }

        /// <summary>
        /// Builds the information set of <paramref name="seat"/>: its own hand, public history, counts of the
        /// other hands. Safe to give to that seat's UI or bot (GDD §22 "ViewFor(seat)").
        /// </summary>
        /// <param name="seat">The viewing seat.</param>
        /// <returns>A new immutable view.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The seat is not defined.</exception>
        public PlayerView ViewFor(Seat seat)
        {
            if (!seat.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(seat), seat, "Seat must be 0-3.");
            }

            var handCounts = new int[SeatExtensions.Count];
            for (int i = 0; i < handCounts.Length; i++)
            {
                handCounts[i] = _state.Hands[i].Count;
            }

            return new PlayerView(
                seat,
                _state.Rules,
                _state.MatchId,
                _state.Profiles,
                _state.Phase,
                _state.Round,
                _state.Dealer,
                _state.FirstDealer,
                _state.CurrentSeat,
                _state.CurrentTurn,
                _state.Hands[(int)seat],
                _state.Calls,
                _state.Won,
                handCounts,
                _state.Ready,
                _state.Trick,
                _state.Completed,
                _state.Scores,
                _state.Standings);
        }

        /// <summary>
        /// The complete state as plain serializable data, including every hand: for the authority only
        /// (offline save, server recovery), never for clients. Allowed in any phase. Pending events are not
        /// part of it, so drain them first if they still need delivering.
        /// </summary>
        /// <returns>A new snapshot.</returns>
        public MatchSnapshot Snapshot()
        {
            return MatchSnapshots.Capture(_state);
        }

        /// <summary>
        /// Moves every pending event, oldest first, to the end of <paramref name="into"/> and clears the
        /// outbox. Route each by its <see cref="MatchEvent.Visibility"/>.
        /// </summary>
        /// <param name="into">The list to append to.</param>
        /// <returns>The number of events moved.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="into"/> is null.</exception>
        public int DrainEvents(List<MatchEvent> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            int count = _outbox.Count;
            into.AddRange(_outbox);
            _outbox.Clear();
            return count;
        }

        /// <summary>
        /// Checks every GDD §22 invariant against the live state: all 52 cards accounted for once; hand sizes;
        /// tricks won equal tricks completed; trick leaders follow winners; the current seat is next in the
        /// rule direction; the open turn is the newest turn id; calls within range and in order before any
        /// card; each total is the sum of that seat's deltas; standings sorted and consistent with the
        /// tie-breaks. For tests and fuzzing after every command.
        /// </summary>
        /// <returns>A description of the first violation, or null when every invariant holds.</returns>
        public string FindInvariantViolation()
        {
            return MatchInvariants.FindViolation(_state);
        }

        /// <summary>Throws when <see cref="FindInvariantViolation"/> finds a violation.</summary>
        /// <exception cref="InvalidOperationException">An invariant is broken, i.e. the engine has a bug.</exception>
        public void AssertInvariants()
        {
            string violation = MatchInvariants.FindViolation(_state);
            if (violation != null)
            {
                throw new InvalidOperationException("Match invariant violated: " + violation);
            }
        }

        private RejectReason CheckTurn(Phase phase, Seat seat, TurnId turnId)
        {
            if (_state.Phase != phase)
            {
                return RejectReason.WrongPhase;
            }

            if (!seat.IsValid())
            {
                return RejectReason.InvalidSeat;
            }

            if (seat != _state.CurrentSeat)
            {
                return RejectReason.NotYourTurn;
            }

            return turnId != _state.CurrentTurn ? RejectReason.StaleTurn : RejectReason.None;
        }

        private void BeginRound(int round, Seat dealer)
        {
            _state.Phase = Phase.Dealing;
            _state.Round = round;
            _state.Dealer = dealer;
            _state.ClearRound();
            Emit(new RoundStarted(round, dealer));

            Seat first = _state.FirstSeat;
            CardSet[] hands;
            int redeals = 0;
            while (true)
            {
                hands = Deck.Deal(Deck.Shuffled(_rng), first, _state.Direction);
                RedealReason reason = RedealPolicy.Check(hands, _state.Rules);
                if (reason == RedealReason.None)
                {
                    break;
                }

                if (++redeals > MaxRedealsPerRound)
                {
                    throw new InvalidOperationException("Every deal needed a redeal; the IRandom is not random.");
                }

                Emit(new Redealt(round, reason));
            }

            Seat seat = first;
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                _state.Hands[(int)seat] = hands[(int)seat];
                Emit(new HandDealt(seat, hands[(int)seat]));
                seat = seat.Next(_state.Direction);
            }

            _state.Phase = Phase.Calling;
            _state.Trick = new Trick(first, _state.Direction);
            OpenCallTurn(first);
        }

        private void OpenCallTurn(Seat seat)
        {
            _state.CurrentSeat = seat;
            _state.CurrentTurn = NextTurnId();
            Emit(new CallTurnStarted(seat, _state.CurrentTurn));
        }

        private TurnId NextTurnId()
        {
            _state.TurnCounter++;
            return new TurnId(_state.TurnCounter);
        }

        /// <summary>
        /// Opens the play turn of <paramref name="seat"/>, first making any plays the rules force: with
        /// AutoPlayLastTrick a one-card hand plays itself, with AutoPlayForcedMoves a single legal card does.
        /// Forced plays get no turn id and no PlayTurnStarted/LegalHint, only a CardPlayed with IsForced.
        /// </summary>
        private void PlayOn(Seat seat)
        {
            while (true)
            {
                CardSet hand = _state.Hands[(int)seat];
                CardSet legal = LegalMoves.Compute(hand, _state.Trick, _state.Rules);
                bool forced = (_state.Rules.AutoPlayLastTrick && hand.Count == 1)
                    || (_state.Rules.AutoPlayForcedMoves && legal.Count == 1);
                _state.CurrentSeat = seat;
                if (!forced)
                {
                    _state.CurrentTurn = NextTurnId();
                    Suit? led = _state.Trick.IsEmpty ? (Suit?)null : _state.Trick.LedSuit;
                    Emit(new PlayTurnStarted(seat, _state.CurrentTurn, led));
                    Emit(new LegalHint(seat, _state.CurrentTurn, legal));
                    return;
                }

                _state.CurrentTurn = TurnId.None;
                if (!ApplyPlay(seat, legal.Lowest(), isAuto: true, isForced: true, out seat))
                {
                    return;
                }
            }
        }

        /// <summary>Plays a validated card; returns false when the round ended (and was scored).</summary>
        private bool ApplyPlay(Seat seat, Card card, bool isAuto, bool isForced, out Seat next)
        {
            _state.Hands[(int)seat] = _state.Hands[(int)seat].Without(card);
            _state.Trick = _state.Trick.With(card);
            Emit(new CardPlayed(seat, card, isAuto, isForced));
            if (!_state.Trick.IsComplete)
            {
                next = seat.Next(_state.Direction);
                return true;
            }

            _state.Phase = Phase.TrickResolve;
            var completed = new CompletedTrick(_state.Completed.Count, _state.Trick);
            _state.Completed.Add(completed);
            _state.Won[(int)completed.Winner]++;
            _state.Trick = new Trick(completed.Winner, _state.Direction);
            Emit(new TrickCompleted(completed));
            next = completed.Winner;
            if (_state.Completed.Count == Deck.HandSize)
            {
                ScoreRound(completed.Winner);
                return false;
            }

            _state.Phase = Phase.Playing;
            return true;
        }

        private void ScoreRound(Seat lastWinner)
        {
            _state.Phase = Phase.RoundScoring;
            _state.Scores = _state.Scores.Append(_state.Calls, _state.Won, _state.Rules);
            Emit(new RoundScored(_state.Scores.Rounds[_state.Scores.Rounds.Count - 1]));

            _state.Phase = Phase.RoundSummary;
            _state.CurrentSeat = lastWinner;
            _state.CurrentTurn = TurnId.None;
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                if (_state.Profiles[i].IsBot)
                {
                    _state.Ready[i] = true;
                    Emit(new ReadyConfirmed((Seat)i));
                }
            }

            LeaveSummaryIfAllReady();
        }

        private void LeaveSummaryIfAllReady()
        {
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                if (!_state.Ready[i])
                {
                    return;
                }
            }

            if (_state.Round < _state.Rules.Rounds)
            {
                // The deal passes to the right each round (GDD §2).
                BeginRound(_state.Round + 1, _state.Dealer.Next(_state.Direction));
                return;
            }

            _state.Phase = Phase.MatchOver;
            _state.Standings = Standings.Compute(_state.Scores);
            Emit(new MatchEnded(_state.Standings));
        }

        private void Emit(MatchEvent matchEvent)
        {
            _outbox.Add(matchEvent);
        }
    }
}
