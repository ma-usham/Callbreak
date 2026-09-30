using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;

namespace Callbreak.Core.Views
{
    /// <summary>
    /// Everything one seat is allowed to know (GDD §22 "PlayerView: own hand, public calls, cards played,
    /// current trick, scores and hand counts"; §26 information matrix). The only thing the UI and bots
    /// receive. Immutable and self-contained (no reference back to the engine), so it is safe to hand to a
    /// worker thread. Other seats' hands exist here only as counts, so no member can reveal them.
    /// </summary>
    public sealed class PlayerView : IEquatable<PlayerView>
    {
        private readonly ReadOnlyCollection<SeatProfile> _seats;
        private readonly int[] _calls;
        private readonly int[] _tricksWon;
        private readonly int[] _handCounts;
        private readonly bool[] _ready;
        private readonly ReadOnlyCollection<CompletedTrick> _completedTricks;
        private readonly ReadOnlyCollection<Standing> _finalStandings;

        /// <summary>
        /// Creates a view. The engine builds these with <c>MatchEngine.ViewFor</c>; the constructor is public
        /// so a client can rebuild one from a network snapshot. Lists are copied; per-seat lists are indexed by
        /// seat number.
        /// </summary>
        /// <param name="viewer">The seat whose information set this is.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <param name="matchId">The match id.</param>
        /// <param name="seats">Four seat profiles.</param>
        /// <param name="phase">The engine phase.</param>
        /// <param name="round">The round number (0 before the match starts).</param>
        /// <param name="dealer">This round's dealer.</param>
        /// <param name="firstDealer">The round-1 dealer.</param>
        /// <param name="currentSeat">The seat whose turn is open (see <see cref="CurrentSeat"/>).</param>
        /// <param name="currentTurn">The open turn, or <see cref="TurnId.None"/>.</param>
        /// <param name="hand">The viewer's own cards.</param>
        /// <param name="calls">Each seat's call, or <see cref="CallRules.NotCalled"/>.</param>
        /// <param name="tricksWon">Tricks each seat has won this round.</param>
        /// <param name="handCounts">Cards each seat holds.</param>
        /// <param name="ready">Each seat's ready flag in the round summary.</param>
        /// <param name="currentTrick">The trick in progress.</param>
        /// <param name="completedTricks">This round's finished tricks, in order.</param>
        /// <param name="scores">Every scored round.</param>
        /// <param name="finalStandings">Four standings once the match is over, otherwise empty.</param>
        /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
        /// <exception cref="ArgumentException">A list has the wrong length, or the hand does not match its count.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A seat is not defined.</exception>
        public PlayerView(
            Seat viewer,
            RuleSet rules,
            string matchId,
            IReadOnlyList<SeatProfile> seats,
            Phase phase,
            int round,
            Seat dealer,
            Seat firstDealer,
            Seat currentSeat,
            TurnId currentTurn,
            CardSet hand,
            IReadOnlyList<int> calls,
            IReadOnlyList<int> tricksWon,
            IReadOnlyList<int> handCounts,
            IReadOnlyList<bool> ready,
            Trick currentTrick,
            IReadOnlyList<CompletedTrick> completedTricks,
            ScoreSheet scores,
            IReadOnlyList<Standing> finalStandings)
        {
            RequireSeat(viewer, nameof(viewer));
            RequireSeat(dealer, nameof(dealer));
            RequireSeat(firstDealer, nameof(firstDealer));
            RequireSeat(currentSeat, nameof(currentSeat));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            MatchId = matchId ?? throw new ArgumentNullException(nameof(matchId));
            Scores = scores ?? throw new ArgumentNullException(nameof(scores));
            _seats = new ReadOnlyCollection<SeatProfile>(CopyPerSeat(seats, nameof(seats)));
            _calls = CopyPerSeat(calls, nameof(calls));
            _tricksWon = CopyPerSeat(tricksWon, nameof(tricksWon));
            _handCounts = CopyPerSeat(handCounts, nameof(handCounts));
            _ready = CopyPerSeat(ready, nameof(ready));
            _completedTricks = new ReadOnlyCollection<CompletedTrick>(Copy(completedTricks, nameof(completedTricks)));
            _finalStandings = new ReadOnlyCollection<Standing>(Copy(finalStandings, nameof(finalStandings)));
            if (_completedTricks.Count > Deck.HandSize)
            {
                throw new ArgumentException("A round has at most 13 tricks.", nameof(completedTricks));
            }

            if (_finalStandings.Count != 0 && _finalStandings.Count != SeatExtensions.Count)
            {
                throw new ArgumentException("Standings are empty or one per seat.", nameof(finalStandings));
            }

            if (hand.Count != _handCounts[(int)viewer])
            {
                throw new ArgumentException("The viewer's hand does not match its hand count.", nameof(hand));
            }

            Viewer = viewer;
            Phase = phase;
            Round = round;
            Dealer = dealer;
            FirstDealer = firstDealer;
            CurrentSeat = currentSeat;
            CurrentTurn = currentTurn;
            Hand = hand;
            CurrentTrick = currentTrick;
        }

        /// <summary>The seat whose information set this is.</summary>
        public Seat Viewer { get; }

        /// <summary>The match's rule set.</summary>
        public RuleSet Rules { get; }

        /// <summary>The match id.</summary>
        public string MatchId { get; }

        /// <summary>The four seat profiles, indexed by seat number.</summary>
        public IReadOnlyList<SeatProfile> Seats => _seats;

        /// <summary>The engine phase when the view was taken.</summary>
        public Phase Phase { get; }

        /// <summary>The round number, 1 to <see cref="RuleSet.Rounds"/> (0 before the match starts).</summary>
        public int Round { get; }

        /// <summary>
        /// Index of the trick in progress, 0–12 ("Trick 1 / 13" is 0); 13 once the round's tricks are done.
        /// Equals the number of completed tricks this round.
        /// </summary>
        public int TrickIndex => _completedTricks.Count;

        /// <summary>This round's dealer.</summary>
        public Seat Dealer { get; }

        /// <summary>The round-1 dealer.</summary>
        public Seat FirstDealer { get; }

        /// <summary>
        /// The seat whose call or play turn is open. Meaningful while <see cref="CurrentTurn"/> is not
        /// <see cref="TurnId.None"/>; in the round summary it is the last trick's winner.
        /// </summary>
        public Seat CurrentSeat { get; }

        /// <summary>The open turn's id, or <see cref="TurnId.None"/> when no call or play is awaited.</summary>
        public TurnId CurrentTurn { get; }

        /// <summary>True when the viewer must call or play now.</summary>
        public bool IsMyTurn => !CurrentTurn.IsNone
            && CurrentSeat == Viewer
            && (Phase == Phase.Calling || Phase == Phase.Playing);

        /// <summary>The viewer's own cards: the only hand in this view.</summary>
        public CardSet Hand { get; }

        /// <summary>The trick in progress (leader and cards so far).</summary>
        public Trick CurrentTrick { get; }

        /// <summary>This round's finished tricks in order, with leaders and winners (public history).</summary>
        public IReadOnlyList<CompletedTrick> CompletedTricks => _completedTricks;

        /// <summary>Every scored round (calls, tricks won, deltas and totals per seat).</summary>
        public ScoreSheet Scores { get; }

        /// <summary>Per-round history, the same as <c>Scores.Rounds</c>.</summary>
        public IReadOnlyList<RoundResult> History => Scores.Rounds;

        /// <summary>Four standings once the match is over; empty before.</summary>
        public IReadOnlyList<Standing> FinalStandings => _finalStandings;

        /// <summary>A seat's call this round, or <see cref="CallRules.NotCalled"/> (−1) before it calls.</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The call.</returns>
        public int CallOf(Seat seat)
        {
            return _calls[Index(seat)];
        }

        /// <summary>Whether a seat has called this round.</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>True once its call is placed.</returns>
        public bool HasCalled(Seat seat)
        {
            return _calls[Index(seat)] != CallRules.NotCalled;
        }

        /// <summary>Tricks a seat has won this round ("Bid 3 · Won 1").</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The count.</returns>
        public int TricksWonBy(Seat seat)
        {
            return _tricksWon[Index(seat)];
        }

        /// <summary>How many cards a seat holds. The only information about other hands (GDD §26).</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The count.</returns>
        public int HandCountOf(Seat seat)
        {
            return _handCounts[Index(seat)];
        }

        /// <summary>Whether a seat is ready to leave the round summary.</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The ready flag.</returns>
        public bool IsReady(Seat seat)
        {
            return _ready[Index(seat)];
        }

        /// <summary>A seat's match total in tenths.</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The total.</returns>
        public int TotalOf(Seat seat)
        {
            return Scores.TotalOf(seat);
        }

        /// <summary>The last finished trick of this round, for the "peek" (GDD §11 screen 08).</summary>
        /// <param name="previous">The trick, or default when none has finished this round.</param>
        /// <returns>Whether a trick has finished this round.</returns>
        public bool TryGetPreviousTrick(out CompletedTrick previous)
        {
            if (_completedTricks.Count == 0)
            {
                previous = default;
                return false;
            }

            previous = _completedTricks[_completedTricks.Count - 1];
            return true;
        }

        /// <summary>The viewer's legal cards when it is the viewer's play turn; otherwise empty.</summary>
        /// <returns>The cards to highlight.</returns>
        public CardSet LegalCards()
        {
            return IsMyPlayTurn() ? LegalMoves.Compute(Hand, CurrentTrick, Rules) : CardSet.Empty;
        }

        /// <summary>The turn-prompt obligation when it is the viewer's play turn (GDD §11 screen 08).</summary>
        /// <param name="obligation">The obligation, or default when it is not the viewer's play turn.</param>
        /// <returns>Whether the viewer is to play now.</returns>
        public bool TryGetObligation(out PlayObligation obligation)
        {
            if (!IsMyPlayTurn())
            {
                obligation = default;
                return false;
            }

            obligation = LegalMoves.Classify(Hand, CurrentTrick, Rules);
            return true;
        }

        /// <inheritdoc />
        public bool Equals(PlayerView other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other != null
                && Viewer == other.Viewer
                && Rules.Equals(other.Rules)
                && string.Equals(MatchId, other.MatchId, StringComparison.Ordinal)
                && Phase == other.Phase
                && Round == other.Round
                && Dealer == other.Dealer
                && FirstDealer == other.FirstDealer
                && CurrentSeat == other.CurrentSeat
                && CurrentTurn == other.CurrentTurn
                && Hand == other.Hand
                && CurrentTrick == other.CurrentTrick
                && Scores.Equals(other.Scores)
                && SameItems(_seats, other._seats)
                && SameItems(_calls, other._calls)
                && SameItems(_tricksWon, other._tricksWon)
                && SameItems(_handCounts, other._handCounts)
                && SameItems(_ready, other._ready)
                && SameItems(_completedTricks, other._completedTricks)
                && SameItems(_finalStandings, other._finalStandings);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as PlayerView);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Viewer;
                hash = (hash * 397) ^ (int)Phase;
                hash = (hash * 397) ^ Round;
                hash = (hash * 397) ^ CurrentTurn.Value;
                hash = (hash * 397) ^ Hand.GetHashCode();
                hash = (hash * 397) ^ CurrentTrick.GetHashCode();
                return (hash * 397) ^ _completedTricks.Count;
            }
        }

        /// <summary>A multi-line, culture-invariant dump for logs and test failures.</summary>
        /// <returns>The view's text.</returns>
        public override string ToString()
        {
            var text = new StringBuilder(400);
            text.Append("PlayerView viewer=").Append(Viewer.ToString())
                .Append(" phase=").Append(Phase.ToString())
                .Append(" round=").Append(Round.ToString(CultureInfo.InvariantCulture))
                .Append(" trick=").Append(TrickIndex.ToString(CultureInfo.InvariantCulture))
                .Append(" dealer=").Append(Dealer.ToString())
                .Append(" current=").Append(CurrentSeat.ToString())
                .Append(" turn=").Append(CurrentTurn.ToString())
                .Append('\n').Append("hand=").Append(Hand.ToString())
                .Append('\n');
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                text.Append((Seat)i).Append(": call=")
                    .Append(_calls[i] == CallRules.NotCalled ? "-" : _calls[i].ToString(CultureInfo.InvariantCulture))
                    .Append(" won=").Append(_tricksWon[i].ToString(CultureInfo.InvariantCulture))
                    .Append(" cards=").Append(_handCounts[i].ToString(CultureInfo.InvariantCulture))
                    .Append(" ready=").Append(_ready[i] ? '1' : '0')
                    .Append(" total=").Append(Scores.TotalOf((Seat)i).ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
            }

            text.Append("trick=").Append(CurrentTrick.ToString());
            for (int i = 0; i < _completedTricks.Count; i++)
            {
                text.Append('\n').Append(_completedTricks[i].ToString());
            }

            return text.ToString();
        }

        private bool IsMyPlayTurn()
        {
            return Phase == Phase.Playing && !CurrentTurn.IsNone && CurrentSeat == Viewer;
        }

        private static int Index(Seat seat)
        {
            RequireSeat(seat, nameof(seat));
            return (int)seat;
        }

        private static void RequireSeat(Seat seat, string name)
        {
            if (!seat.IsValid())
            {
                throw new ArgumentOutOfRangeException(name, seat, "Seat must be 0-3.");
            }
        }

        private static T[] CopyPerSeat<T>(IReadOnlyList<T> source, string name)
        {
            T[] copy = Copy(source, name);
            if (copy.Length != SeatExtensions.Count)
            {
                throw new ArgumentException("Expected one entry per seat.", name);
            }

            return copy;
        }

        private static T[] Copy<T>(IReadOnlyList<T> source, string name)
        {
            if (source == null)
            {
                throw new ArgumentNullException(name);
            }

            var copy = new T[source.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static bool SameItems<T>(IReadOnlyList<T> a, IReadOnlyList<T> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int i = 0; i < a.Count; i++)
            {
                if (!comparer.Equals(a[i], b[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
