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
    /// this seat's view ever arrives. A table handler that throws never stops the session from
    /// following the host: the update is applied whole, the table is rebuilt from the view, and
    /// the exception is passed on afterwards.
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
        private readonly HandlerFaults _faults = new HandlerFaults();

        private SessionStatus _status = SessionStatus.Starting;
        private TurnTimerState _timer;
        private TableStatus _tableStatus;
        private uint _expected = 1;
        private bool _started;
        private bool _awaitingSync;
        private bool _finished;
        private bool _timerShown;
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

        /// <summary>
        /// The turn clock while a turn is open; in the round summary, the countdown to the next
        /// round. A clock the host starts later (the time left is longer than the clock: the table
        /// is still showing a deal or a finished trick) is shown from the moment it starts, so it
        /// never shows more than its length and runs out when the host's does.
        /// </summary>
        public TurnTimerState Timer => HasStarted(_timer) ? _timer : TurnTimerState.None;

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

            bool shown = Timer.IsRunning;
            if (shown != _timerShown)
            {
                // A clock the host started after a hold becomes visible now.
                _timerShown = shown;
                _faults.Raise(TimerChanged);
                _faults.Rethrow();
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
            if (_started && !_finished && View.Phase != Phase.Lobby)
            {
                RecordAbandoned();
            }

            _finished = true;
            _send(ClientMessages.LeaveTable());
            FailPending(RejectReason.WrongPhase);
            SetStatus(SessionStatus.Ended);
            try
            {
                _left?.Invoke();
            }
            catch (Exception e)
            {
                _faults.Keep(e);
            }

            _faults.Rethrow();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            FailPending(RejectReason.WrongPhase);
        }

        /// <summary>The next numbered update from the host.</summary>
        public void OnBatch(TableUpdate update)
        {
            if (!_started || _awaitingSync || update.Seq < _expected || !IsThisTable(update.View))
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

                _faults.Raise(EventReceived, e);
            }

            if (_faults.Any)
            {
                // A table handler failed on the way: whatever it missed is in the view, so the table
                // is rebuilt from it, as after a resync (GDD §17 principle 9: a resync, not a stop).
                _faults.Raise(Resynced, View);
            }

            _faults.Rethrow();
        }

        /// <summary>
        /// The whole state (join, reconnect, resync): the table is rebuilt from it at once. It may
        /// be the finished table, for a player who was reconnecting when the match ended.
        /// </summary>
        public void OnSync(TableUpdate update)
        {
            if (!_started || !IsThisTable(update.View))
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

            _faults.Raise(Resynced, View);
            _faults.Rethrow();
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

        public void OnEmote(Seat seat, int emoteId)
        {
            _faults.Raise(EmoteReceived, seat, emoteId);
            _faults.Rethrow();
        }

        /// <summary>The connection dropped; the seat is held while the app reconnects (GDD §8).</summary>
        public void OnLinkLost()
        {
            if (!IsOver)
            {
                SetStatus(SessionStatus.Reconnecting);
            }

            _faults.Rethrow();
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

        /// <summary>
        /// The room is gone (the host left, this player was removed, or reconnecting failed). Not
        /// back within the seat hold (<see cref="RoomError.ConnectionLost"/>), the player is recorded
        /// as having left, a loss, exactly as Leave is (GDD §8 L660-668, L689-690: dropping out
        /// gains nothing). When the host leaves, the match is not recorded (GDD §27 failure modes).
        /// </summary>
        public void OnRoomClosed(RoomError reason)
        {
            if (IsOver)
            {
                return;
            }

            if (reason == RoomError.ConnectionLost && !_finished && View.Phase != Phase.Lobby)
            {
                _finished = true;
                RecordAbandoned();
            }

            FailureReason = reason == RoomError.None ? RoomError.Unknown : reason;
            FailPending(RejectReason.WrongPhase);
            SetStatus(SessionStatus.Failed);
            _faults.Rethrow();
        }

        private bool IsOver => _status == SessionStatus.Ended || _status == SessionStatus.Failed;

        /// <summary>Only this match, seen from this seat, ever replaces the view.</summary>
        private bool IsThisTable(PlayerView view) => view.MatchId == View.MatchId && view.Viewer == View.Viewer;

        /// <summary>A running clock has started once no more of it is left than its length (a millisecond of rounding allowed).</summary>
        private bool HasStarted(TurnTimerState timer) => timer.IsRunning && timer.Deadline - _now <= timer.Duration + 0.001;

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
            try
            {
                for (int i = 0; i < standings.Count; i++)
                {
                    Standing standing = standings[i];
                    if (standing.Seat == LocalSeat && _stats != null)
                    {
                        _stats.RecordFinished(View.MatchId, standing, View.Scores);
                    }
                }

                if (_settings != null)
                {
                    _settings.IncrementCompletedMatches();
                }
            }
            catch (Exception e)
            {
                // A save that fails never keeps the session from reaching its end.
                _faults.Keep(e);
            }
        }

        /// <summary>GDD §8: a match left, on purpose or by not coming back in time, counts as played and lost.</summary>
        private void RecordAbandoned()
        {
            if (_stats == null)
            {
                return;
            }

            try
            {
                _stats.RecordMatch(View.MatchId, SeatExtensions.Count, View.Scores.TotalOf(LocalSeat), abandoned: true);
            }
            catch (Exception e)
            {
                _faults.Keep(e);
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
                    // The host says how long the countdown is (10 s in Quick Match, 15 s in a private
                    // room, GDD §6); a status that does not is taken to have just started.
                    double remaining = status.SummaryRemainingMs / 1000.0;
                    double length = status.TimerDurationMs > 0 ? status.TimerDurationMs / 1000.0 : remaining;
                    timer = new TurnTimerState(View.Viewer, _now + remaining, length, true);
                }
            }

            bool timerChanged = timer.IsRunning != _timer.IsRunning || timer.Seat != _timer.Seat
                || Math.Abs(timer.Deadline - _timer.Deadline) > 0.05 || Math.Abs(timer.Duration - _timer.Duration) > 0.0001;
            _timer = timer;
            _timerShown = Timer.IsRunning;
            if (!raise)
            {
                return;
            }

            if (seatsChanged)
            {
                _faults.Raise(SeatsChanged);
            }

            if (timerChanged)
            {
                _faults.Raise(TimerChanged);
            }
        }

        private void SetStatus(SessionStatus status)
        {
            if (_status == status)
            {
                return;
            }

            _status = status;
            _faults.Raise(StatusChanged);
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
