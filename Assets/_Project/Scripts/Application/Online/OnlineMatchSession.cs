using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// A match played through a host (GDD §17 principle 4, §25-26): the table sees the same
    /// <see cref="IMatchSession"/> as offline, but every move is an intent the host decides, and
    /// everything shown comes from the host's numbered updates. A gap in the numbers asks for the
    /// whole state again, and the table rebuilds from it. It holds no hidden information: only
    /// this seat's view ever arrives.
    /// </summary>
    public sealed class OnlineMatchSession : IMatchSession
    {
        private const double ResyncRetrySeconds = 3.0;

        private readonly Action<byte[]> _send;
        private readonly Action _left;
        private readonly IStatsService _stats;
        private readonly ISettingsService _settings;
        private readonly SessionSeat[] _seats = new SessionSeat[SeatExtensions.Count];
        private readonly List<PendingIntent> _pending = new List<PendingIntent>();

        private SessionStatus _status = SessionStatus.Starting;
        private TurnTimerState _timer;
        private TableStatus _tableStatus;
        private uint _expected = 1;
        private bool _started;
        private bool _awaitingSync;
        private bool _finished;
        private double _now;
        private double _resyncAskedAt;

        /// <param name="send">Sends a payload to the host.</param>
        /// <param name="left">Called once this player has left the match on purpose, so the room can be left too.</param>
        public OnlineMatchSession(
            MatchMode mode,
            PlayerView initialView,
            TableStatus initialStatus,
            Action<byte[]> send,
            IStatsService stats,
            ISettingsService settings,
            Action left = null)
        {
            _left = left;
            Mode = mode;
            View = initialView ?? throw new ArgumentNullException(nameof(initialView));
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _stats = stats;
            _settings = settings;
            ApplyStatus(initialStatus, raise: false);
        }

        public event Action<MatchEvent> EventReceived;

        public event Action<PlayerView> Resynced;

        public event Action SeatsChanged;

        public event Action StatusChanged;

        public event Action TimerChanged;

        public event Action<Seat, int> EmoteReceived;

        public MatchMode Mode { get; }

        public Seat LocalSeat => View.Viewer;

        public RuleSet Rules => View.Rules;

        public IReadOnlyList<SessionSeat> Seats => _seats;

        public PlayerView View { get; private set; }

        public SessionStatus Status => _status;

        /// <summary>The turn clock while a turn is open; in the round summary, the countdown to the next round.</summary>
        public TurnTimerState Timer => _timer;

        public bool CanPause => false;

        public bool IsPaused => false;

        public string MatchId => View.MatchId;

        public void Start()
        {
            if (_started)
            {
                return;
            }

            // The host answers "table ready" with the whole state, whatever has happened so far.
            _started = true;
            _awaitingSync = true;
            _resyncAskedAt = _now;
            _send(ClientMessages.TableReady());
        }

        public void Tick(double now)
        {
            _now = now;
            if (_started && _awaitingSync && !IsOver && _status != SessionStatus.Reconnecting && now - _resyncAskedAt >= ResyncRetrySeconds)
            {
                RequestResync();
            }
        }

        public Task<CommandResult> PlaceCallAsync(int call, CancellationToken ct)
        {
            if (!CanAct() || View.Phase != Phase.Calling)
            {
                return Task.FromResult(CommandResult.Rejected(RejectReason.WrongPhase));
            }

            TurnId turn = View.CurrentTurn;
            Task<CommandResult> task = Track(turn, isCall: true);
            _send(ClientMessages.PlaceCall(call, turn));
            return task;
        }

        public Task<CommandResult> PlayCardAsync(Card card, CancellationToken ct)
        {
            if (!CanAct() || View.Phase != Phase.Playing)
            {
                return Task.FromResult(CommandResult.Rejected(RejectReason.WrongPhase));
            }

            TurnId turn = View.CurrentTurn;
            Task<CommandResult> task = Track(turn, isCall: false);
            _send(ClientMessages.PlayCard(card, turn));
            return task;
        }

        public Task ReadyNextAsync(CancellationToken ct)
        {
            if (CanAct())
            {
                _send(ClientMessages.ReadyNext());
            }

            return Task.CompletedTask;
        }

        public Task SendEmoteAsync(int emoteId, CancellationToken ct)
        {
            if (CanAct())
            {
                _send(ClientMessages.Emote(emoteId));
            }

            return Task.CompletedTask;
        }

        /// <summary>"I'm back" (GDD §8): tells the host this player is present again.</summary>
        public void ReportPresence()
        {
            if (CanAct())
            {
                _send(ClientMessages.Presence());
            }
        }

        /// <summary>Online play never pauses (GDD §11 screen 13).</summary>
        public void Pause()
        {
        }

        public void Resume()
        {
        }

        public Task LeaveAsync(CancellationToken ct)
        {
            if (IsOver)
            {
                return Task.CompletedTask;
            }

            // GDD §8: leaving on purpose costs the match.
            if (_started && !_finished && View.Phase != Phase.Lobby && _stats != null)
            {
                _stats.RecordMatch(View.MatchId, SeatExtensions.Count, View.Scores.TotalOf(LocalSeat), abandoned: true);
            }

            _finished = true;
            _send(ClientMessages.LeaveTable());
            FailPending(RejectReason.WrongPhase);
            SetStatus(SessionStatus.Ended);
            _left?.Invoke();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            FailPending(RejectReason.WrongPhase);
        }

        /// <summary>The next numbered update from the host.</summary>
        public void OnBatch(TableUpdate update)
        {
            if (!_started || _awaitingSync || update.Seq < _expected)
            {
                return;
            }

            if (update.Seq > _expected)
            {
                // GDD §26: a gap means something was missed; ask for the whole state.
                RequestResync();
                return;
            }

            _expected = update.Seq + 1;
            View = update.View;
            ApplyStatus(update.Status, raise: true);
            if (_status == SessionStatus.Starting || _status == SessionStatus.Reconnecting)
            {
                SetStatus(SessionStatus.Running);
            }

            for (int i = 0; i < update.Events.Count; i++)
            {
                MatchEvent e = update.Events[i];
                Settle(e);
                if (e is MatchEnded ended)
                {
                    Record(ended.Standings);
                    SetStatus(SessionStatus.Ended);
                }

                EventReceived?.Invoke(e);
            }
        }

        /// <summary>The whole state (join, reconnect, resync): the table is rebuilt from it at once.</summary>
        public void OnSync(TableUpdate update)
        {
            if (!_started)
            {
                return;
            }

            _awaitingSync = false;
            _expected = update.Seq + 1;
            View = update.View;
            ApplyStatus(update.Status, raise: true);
            FailPending(RejectReason.StaleTurn);
            if (!IsOver)
            {
                if (View.Phase == Phase.MatchOver)
                {
                    // The match ended while this player was away: the standings are in the view.
                    Record(View.FinalStandings);
                    SetStatus(SessionStatus.Ended);
                }
                else
                {
                    SetStatus(SessionStatus.Running);
                }
            }

            Resynced?.Invoke(View);
        }

        public void OnIntentRejected(TurnId turn, RejectReason reason)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Turn == turn)
                {
                    PendingIntent intent = _pending[i];
                    _pending.RemoveAt(i);
                    intent.Completion.TrySetResult(CommandResult.Rejected(reason));
                }
            }
        }

        public void OnEmote(Seat seat, int emoteId) => EmoteReceived?.Invoke(seat, emoteId);

        /// <summary>The connection dropped; the seat is held while the app reconnects (GDD §8).</summary>
        public void OnLinkLost()
        {
            if (!IsOver)
            {
                SetStatus(SessionStatus.Reconnecting);
            }
        }

        /// <summary>The connection is back: ask for the whole state.</summary>
        public void OnRejoined()
        {
            if (IsOver || !_started)
            {
                return;
            }

            _awaitingSync = true;
            _resyncAskedAt = _now;
            _send(ClientMessages.TableReady());
        }

        /// <summary>Why the session failed (the table says so before it returns to the menu); None otherwise.</summary>
        public RoomError FailureReason { get; private set; }

        /// <summary>The room is gone (the host left, this player was removed, or reconnecting failed).</summary>
        public void OnRoomClosed(RoomError reason)
        {
            if (IsOver)
            {
                return;
            }

            FailureReason = reason == RoomError.None ? RoomError.Unknown : reason;
            FailPending(RejectReason.WrongPhase);
            SetStatus(SessionStatus.Failed);
        }

        private bool IsOver => _status == SessionStatus.Ended || _status == SessionStatus.Failed;

        private bool CanAct() => _started && !_awaitingSync && _status == SessionStatus.Running;

        private void RequestResync()
        {
            _awaitingSync = true;
            _resyncAskedAt = _now;
            _send(ClientMessages.RequestResync(_expected - 1));
        }

        private Task<CommandResult> Track(TurnId turn, bool isCall)
        {
            var completion = new TaskCompletionSource<CommandResult>();
            _pending.Add(new PendingIntent(turn, isCall, completion));
            return completion.Task;
        }

        /// <summary>This seat's own call or card arriving back is the host's acceptance of the intent.</summary>
        private void Settle(MatchEvent e)
        {
            bool isCall;
            if (e is CallPlaced call && call.Seat == LocalSeat)
            {
                isCall = true;
            }
            else if (e is CardPlayed played && played.Seat == LocalSeat)
            {
                isCall = false;
            }
            else
            {
                return;
            }

            for (int i = 0; i < _pending.Count; i++)
            {
                if (_pending[i].IsCall == isCall)
                {
                    PendingIntent intent = _pending[i];
                    _pending.RemoveAt(i);
                    intent.Completion.TrySetResult(CommandResult.Accepted);
                    return;
                }
            }
        }

        private void FailPending(RejectReason reason)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            PendingIntent[] all = _pending.ToArray();
            _pending.Clear();
            foreach (PendingIntent intent in all)
            {
                intent.Completion.TrySetResult(CommandResult.Rejected(reason));
            }
        }

        /// <summary>Records the finished match once (GDD §11 screen 10: stats change exactly once per match).</summary>
        private void Record(IReadOnlyList<Standing> standings)
        {
            if (_finished)
            {
                return;
            }

            _finished = true;
            for (int i = 0; i < standings.Count; i++)
            {
                Standing standing = standings[i];
                if (standing.Seat == LocalSeat && _stats != null)
                {
                    _stats.RecordMatch(View.MatchId, standing.Rank, standing.TotalTenths, abandoned: false);
                }
            }

            if (_settings != null)
            {
                _settings.IncrementCompletedMatches();
            }
        }

        private void ApplyStatus(TableStatus status, bool raise)
        {
            TableStatus previous = _tableStatus;
            _tableStatus = status;
            bool seatsChanged = previous == null;
            for (int s = 0; s < _seats.Length; s++)
            {
                var seat = (Seat)s;
                SeatProfile profile = View.Seats[s];
                SeatConnection connection = status != null ? status.ConnectionOf(seat) : SeatConnection.Connected;
                SessionSeat current = _seats[s];
                if (current != null && current.Connection == connection && current.Name == profile.Name
                    && current.IsBot == profile.IsBot && current.DeckThemeId == profile.DeckThemeId)
                {
                    continue;
                }

                int.TryParse(profile.AvatarId, out int avatar);
                _seats[s] = new SessionSeat(seat, profile.Name, avatar, profile.DeckThemeId, profile.IsBot, seat == View.Viewer, connection);
                seatsChanged = true;
            }

            TurnTimerState timer = TurnTimerState.None;
            if (status != null)
            {
                if (status.HasTimer)
                {
                    timer = new TurnTimerState(status.TimerSeat, _now + (status.TimerRemainingMs / 1000.0), status.TimerDurationMs / 1000.0, true);
                }
                else if (status.SummaryRemainingMs >= 0)
                {
                    timer = new TurnTimerState(View.Viewer, _now + (status.SummaryRemainingMs / 1000.0), Protocol.SummarySeconds, true);
                }
            }

            bool timerChanged = timer.IsRunning != _timer.IsRunning || timer.Seat != _timer.Seat
                || Math.Abs(timer.Deadline - _timer.Deadline) > 0.05 || Math.Abs(timer.Duration - _timer.Duration) > 0.0001;
            _timer = timer;
            if (!raise)
            {
                return;
            }

            if (seatsChanged)
            {
                SeatsChanged?.Invoke();
            }

            if (timerChanged)
            {
                TimerChanged?.Invoke();
            }
        }

        private void SetStatus(SessionStatus status)
        {
            if (_status == status)
            {
                return;
            }

            _status = status;
            StatusChanged?.Invoke();
        }

        private readonly struct PendingIntent
        {
            public PendingIntent(TurnId turn, bool isCall, TaskCompletionSource<CommandResult> completion)
            {
                Turn = turn;
                IsCall = isCall;
                Completion = completion;
            }

            public TurnId Turn { get; }

            public bool IsCall { get; }

            public TaskCompletionSource<CommandResult> Completion { get; }
        }
    }
}
