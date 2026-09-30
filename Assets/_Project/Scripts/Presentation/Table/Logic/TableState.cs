using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using EnginePhase = Callbreak.Core.Match.Phase;

namespace Callbreak.Presentation.Table
{
    /// <summary>What the table is showing, as far as the played events go.</summary>
    public enum TablePhase
    {
        Idle,
        Dealing,
        Calling,
        Playing,
        RoundEnded,
        MatchEnded,
    }

    /// <summary>
    /// The table as the player sees it right now: the state after the last event that has been
    /// <em>played</em>, which runs behind the session's model while animations catch up (GDD §23
    /// "the model updates immediately and the view catches up"). Events are applied one at a time
    /// by the event player; a resync replaces everything from a <see cref="PlayerView"/>. Holds only
    /// what the local seat may know: its own hand, and counts for the others.
    /// </summary>
    public sealed class TableState
    {
        private readonly int[] _calls = new int[SeatExtensions.Count];
        private readonly bool[] _autoCalls = new bool[SeatExtensions.Count];
        private readonly int[] _won = new int[SeatExtensions.Count];
        private readonly int[] _handCounts = new int[SeatExtensions.Count];
        private readonly bool[] _ready = new bool[SeatExtensions.Count];
        private readonly int[] _totals = new int[SeatExtensions.Count];
        private IReadOnlyList<Standing> _standings = Array.Empty<Standing>();

        public TableState(Seat localSeat, RuleSet rules)
        {
            if (!localSeat.IsValid())
            {
                throw new ArgumentOutOfRangeException(nameof(localSeat), localSeat, "Seat must be 0-3.");
            }

            LocalSeat = localSeat;
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            ClearRound();
        }

        public Seat LocalSeat { get; }

        public RuleSet Rules { get; }

        public TablePhase Phase { get; private set; }

        /// <summary>1 to <see cref="RuleSet.Rounds"/>; 0 before the first round starts.</summary>
        public int Round { get; private set; }

        public Seat Dealer { get; private set; }

        /// <summary>The local seat's cards.</summary>
        public CardSet Hand { get; private set; }

        /// <summary>A call or play turn is open and its event has been played.</summary>
        public bool HasTurn { get; private set; }

        /// <summary>Whose turn it is while <see cref="HasTurn"/>.</summary>
        public Seat TurnSeat { get; private set; }

        public TurnId Turn { get; private set; }

        public bool IsLocalCallTurn => HasTurn && Phase == TablePhase.Calling && TurnSeat == LocalSeat;

        public bool IsLocalPlayTurn => HasTurn && Phase == TablePhase.Playing && TurnSeat == LocalSeat;

        /// <summary>The cards the local seat may play; empty unless it is its play turn.</summary>
        public CardSet Legal { get; private set; }

        /// <summary>
        /// The trick on the table. Once its TrickCompleted event has played (the four cards were
        /// shown and swept to the winner) the table is empty again and the trick is
        /// <see cref="LastTrick"/>.
        /// </summary>
        public Trick Trick { get; private set; }

        /// <summary>This round has a finished trick (for the peek at the previous trick).</summary>
        public bool HasLastTrick { get; private set; }

        /// <summary>The trick finished last this round; meaningful while <see cref="HasLastTrick"/>.</summary>
        public CompletedTrick LastTrick { get; private set; }

        /// <summary>Tricks finished this round ("Trick 6 / 13" shows this plus one).</summary>
        public int TricksCompleted { get; private set; }

        /// <summary>The round that was just scored, while <see cref="Phase"/> is RoundEnded or later.</summary>
        public RoundResult LastRoundResult { get; private set; }

        /// <summary>Four standings once the match is over; empty before.</summary>
        public IReadOnlyList<Standing> FinalStandings => _standings;

        /// <summary>
        /// Every round whose RoundScored event has been played, in order: what the scorecard shows
        /// (GDD §11 screen 16). It never runs ahead of what the player has seen on the table.
        /// </summary>
        public ScoreSheet Scores { get; private set; } = ScoreSheet.Empty;

        /// <summary>All four seats have called this round.</summary>
        public bool AllCalled
        {
            get
            {
                for (int i = 0; i < _calls.Length; i++)
                {
                    if (_calls[i] == CallRules.NotCalled)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public int CallOf(Seat seat) => _calls[(int)seat];

        public bool HasCalled(Seat seat) => _calls[(int)seat] != CallRules.NotCalled;

        public bool IsAutoCall(Seat seat) => _autoCalls[(int)seat];

        public int WonBy(Seat seat) => _won[(int)seat];

        public int HandCountOf(Seat seat) => _handCounts[(int)seat];

        public bool IsReady(Seat seat) => _ready[(int)seat];

        /// <summary>Match total in tenths after the last scored round.</summary>
        public int TotalOf(Seat seat) => _totals[(int)seat];

        /// <summary>The prompt obligation while it is the local seat's play turn.</summary>
        public bool TryGetObligation(out PlayObligation obligation)
        {
            if (!IsLocalPlayTurn)
            {
                obligation = default;
                return false;
            }

            Trick trick = Trick.IsComplete ? new Trick(LocalSeat, Rules.Direction) : Trick;
            obligation = LegalMoves.Classify(Hand, trick, Rules);
            return true;
        }

        /// <summary>Applies the next event in order. Events for other seats' private data are ignored.</summary>
        public void Apply(MatchEvent e)
        {
            switch (e)
            {
                case MatchStarted _:
                    Round = 0;
                    Scores = ScoreSheet.Empty;
                    Array.Clear(_totals, 0, _totals.Length);
                    _standings = Array.Empty<Standing>();
                    LastRoundResult = null;
                    ClearRound();
                    Phase = TablePhase.Idle;
                    break;

                case RoundStarted started:
                    ClearRound();
                    Round = started.Round;
                    Dealer = started.Dealer;
                    Phase = TablePhase.Dealing;
                    break;

                case HandDealt dealt:
                    if (dealt.Seat == LocalSeat)
                    {
                        Hand = dealt.Hand;
                        for (int i = 0; i < _handCounts.Length; i++)
                        {
                            _handCounts[i] = Deck.HandSize;
                        }
                    }

                    break;

                case CallTurnStarted callTurn:
                    Phase = TablePhase.Calling;
                    OpenTurn(callTurn.Seat, callTurn.TurnId);
                    break;

                case CallPlaced placed:
                    _calls[(int)placed.Seat] = placed.Call;
                    _autoCalls[(int)placed.Seat] = placed.IsAuto;
                    CloseTurn();
                    break;

                case PlayTurnStarted playTurn:
                    Phase = TablePhase.Playing;
                    if (Trick.IsComplete || Trick.IsEmpty)
                    {
                        Trick = new Trick(playTurn.Seat, Rules.Direction);
                    }

                    OpenTurn(playTurn.Seat, playTurn.TurnId);
                    Legal = playTurn.Seat == LocalSeat ? LegalMoves.Compute(Hand, Trick, Rules) : CardSet.Empty;
                    break;

                case LegalHint hint:
                    if (hint.Seat == LocalSeat && IsLocalPlayTurn && hint.TurnId == Turn)
                    {
                        Legal = hint.Legal;
                    }

                    break;

                case CardPlayed played:
                    Phase = TablePhase.Playing;
                    if (Trick.IsComplete || Trick.IsEmpty)
                    {
                        Trick = new Trick(played.Seat, Rules.Direction);
                    }

                    Trick = Trick.With(played.Card);
                    if (_handCounts[(int)played.Seat] > 0)
                    {
                        _handCounts[(int)played.Seat]--;
                    }

                    if (played.Seat == LocalSeat)
                    {
                        Hand = Hand.Without(played.Card);
                    }

                    CloseTurn();
                    break;

                case TrickCompleted completed:
                    Trick = default;
                    LastTrick = completed.Completed;
                    HasLastTrick = true;
                    _won[(int)completed.Winner]++;
                    TricksCompleted = completed.TrickIndex + 1;
                    CloseTurn();
                    break;

                case RoundScored scored:
                    LastRoundResult = scored.Result;
                    if (scored.Result.Round == Scores.Rounds.Count + 1)
                    {
                        Scores = Scores.Append(scored.Result);
                    }

                    for (int i = 0; i < _totals.Length; i++)
                    {
                        _totals[i] = scored.Result[(Seat)i].TotalTenths;
                    }

                    Trick = default;
                    CloseTurn();
                    Phase = TablePhase.RoundEnded;
                    break;

                case ReadyConfirmed ready:
                    _ready[(int)ready.Seat] = true;
                    break;

                case MatchEnded ended:
                    _standings = ended.Standings;
                    CloseTurn();
                    Phase = TablePhase.MatchEnded;
                    break;
            }
        }

        /// <summary>Replaces everything with the session's view (resume, reconnect, resync).</summary>
        public void Reset(PlayerView view)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            ClearRound();
            Round = view.Round;
            Dealer = view.Dealer;
            Hand = view.Hand;
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                var seat = (Seat)i;
                _calls[i] = view.CallOf(seat);
                _won[i] = view.TricksWonBy(seat);
                _handCounts[i] = view.HandCountOf(seat);
                _ready[i] = view.IsReady(seat);
                _totals[i] = view.TotalOf(seat);
            }

            Trick = view.CurrentTrick;
            TricksCompleted = view.TrickIndex;
            HasLastTrick = view.TryGetPreviousTrick(out CompletedTrick previous);
            LastTrick = previous;
            _standings = view.FinalStandings;
            Scores = view.Scores;
            LastRoundResult = view.History.Count > 0 ? view.History[view.History.Count - 1] : null;
            switch (view.Phase)
            {
                case EnginePhase.Lobby:
                    Phase = TablePhase.Idle;
                    break;
                case EnginePhase.Dealing:
                    Phase = TablePhase.Dealing;
                    break;
                case EnginePhase.Calling:
                    Phase = TablePhase.Calling;
                    break;
                case EnginePhase.Playing:
                case EnginePhase.TrickResolve:
                    Phase = TablePhase.Playing;
                    break;
                case EnginePhase.RoundScoring:
                case EnginePhase.RoundSummary:
                    Phase = TablePhase.RoundEnded;
                    break;
                default:
                    Phase = TablePhase.MatchEnded;
                    break;
            }

            if (!view.CurrentTurn.IsNone && (Phase == TablePhase.Calling || Phase == TablePhase.Playing))
            {
                OpenTurn(view.CurrentSeat, view.CurrentTurn);
                Legal = view.LegalCards();
            }
        }

        private void OpenTurn(Seat seat, TurnId turn)
        {
            HasTurn = true;
            TurnSeat = seat;
            Turn = turn;
            Legal = CardSet.Empty;
        }

        private void CloseTurn()
        {
            HasTurn = false;
            Turn = TurnId.None;
            Legal = CardSet.Empty;
        }

        private void ClearRound()
        {
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                _calls[i] = CallRules.NotCalled;
                _autoCalls[i] = false;
                _won[i] = 0;
                _handCounts[i] = 0;
                _ready[i] = false;
            }

            Hand = CardSet.Empty;
            Trick = default;
            HasLastTrick = false;
            LastTrick = default;
            TricksCompleted = 0;
            CloseTurn();
        }
    }
}
