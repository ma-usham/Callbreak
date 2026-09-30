using System;
using System.Collections.Generic;
using Callbreak.Application.Sessions;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// The authority of one online table (GDD §24-26): it owns the engine, the bots, the turn clock
    /// and every seat's presence, validates each intent against the seat it came from, and sends
    /// every seat only its own numbered updates. It never reads a wall clock: time comes in through
    /// <see cref="Tick"/> and the calls that carry <c>now</c>. Transport-free, so the same class
    /// runs on the host's phone today and on a dedicated server later.
    /// </summary>
    public sealed class TableHost
    {
        /// <summary>The six quick phrases of GDD §8.</summary>
        public const int EmoteCount = 6;

        private const int MaxBotMovesPerTick = 8;

        private readonly MatchEngine _engine;
        private readonly BotDriver _bots;
        private readonly BotTiming _timing;
        private readonly TableClocks _clocks;
        private readonly SeatState[] _seats = new SeatState[SeatExtensions.Count];
        private readonly List<MatchEvent> _outbox = new List<MatchEvent>();
        private readonly List<MatchEvent> _visible = new List<MatchEvent>();

        private bool _started;
        private bool _over;
        private bool _clockRunning;
        private TurnId _clockTurn = TurnId.None;
        private Seat _clockSeat;
        private int _clockSeconds;
        private double _clockDeadline;
        private double _holdUntil;
        private bool _summaryOpen;
        private double _summaryDeadline;

        public TableHost(TableHostSetup setup, IBotFactory botFactory)
        {
            if (setup == null)
            {
                throw new ArgumentNullException(nameof(setup));
            }

            if (botFactory == null)
            {
                throw new ArgumentNullException(nameof(botFactory));
            }

            _timing = setup.Timing;
            _clocks = setup.Clocks;
            _engine = new MatchEngine(setup.Rules, new Pcg32Random(setup.Seed), new MatchSetup(setup.MatchId, setup.Seats));

            var botSeats = new Dictionary<Seat, IBotStrategy>();
            for (int s = 0; s < _seats.Length; s++)
            {
                _seats[s] = new SeatState(setup.Humans[s]);
                if (!setup.Humans[s])
                {
                    botSeats[(Seat)s] = botFactory.Create(setup.FillLevel);
                }
            }

            _bots = new BotDriver(botSeats, botFactory.Create(BotLevel.Normal), new Pcg32Random(setup.Seed, 11), new Pcg32Random(setup.Seed, 13), _timing);
        }

        /// <summary>A payload for the member in a seat. The room routes it to that member's connection.</summary>
        public event Action<Seat, byte[]> Outbound;

        /// <summary>Raised once, when the last round has been scored and the standings are final.</summary>
        public event Action Ended;

        public string MatchId => _engine.MatchId;

        public bool IsStarted => _started;

        public bool IsOver => _over;

        public PlayerView ViewFor(Seat seat) => _engine.ViewFor(seat);

        public bool IsHuman(Seat seat) => _seats[(int)seat].Human && !_seats[(int)seat].Gone;

        /// <summary>Deals the first hand. Call once every member's table is listening.</summary>
        public void Start(double now)
        {
            if (_started)
            {
                return;
            }

            _started = true;
            _engine.Start();
            Flush(now);
        }

        /// <summary>Advances everything time drives: held seats, the turn clock, bots and the summary countdown.</summary>
        public void Tick(double now)
        {
            if (!_started || _over)
            {
                return;
            }

            for (int s = 0; s < _seats.Length; s++)
            {
                SeatState seat = _seats[s];
                if (seat.Presence == SeatConnection.Reconnecting && now >= seat.DisconnectedAt + Protocol.SeatHoldSeconds)
                {
                    // GDD §8: not back after 90 s, the seat is a bot for the rest of the match.
                    seat.Gone = true;
                    seat.Presence = SeatConnection.BotControlled;
                    Flush(now);
                }
            }

            if (_clockRunning && now >= _clockDeadline + Protocol.TurnGraceSeconds)
            {
                TimeOut(now);
            }

            for (int guard = 0; guard < MaxBotMovesPerTick; guard++)
            {
                if (!_bots.TryAct(_engine, now, out CommandResult result) || !result.IsAccepted)
                {
                    break;
                }

                Flush(now);
            }

            if (_summaryOpen && _engine.Phase == Phase.RoundSummary && now >= _summaryDeadline)
            {
                _summaryOpen = false;
                for (int s = 0; s < _seats.Length; s++)
                {
                    _engine.Execute(new ReadyNext((Seat)s));
                }

                Flush(now);
            }
        }

        /// <summary>
        /// Handles one table intent from the member in <paramref name="seat"/> (the room has already
        /// mapped the sender to its seat, GDD §25). <paramref name="reader"/> is positioned after
        /// the kind byte.
        /// </summary>
        public void Handle(Seat seat, ClientMessageKind kind, WireReader reader, double now)
        {
            SeatState state = _seats[(int)seat];
            if (!state.Human || state.Gone)
            {
                return;
            }

            // GDD §8: any intent from the owner ends Away.
            bool statusChanged = false;
            if (state.Presence == SeatConnection.Away)
            {
                state.Presence = SeatConnection.Connected;
                state.ConsecutiveTimeouts = 0;
                _bots.Release(seat);
                statusChanged = true;
            }

            switch (kind)
            {
                case ClientMessageKind.PlaceCall:
                {
                    int call = reader.Byte();
                    TurnId turn = WireCodec.ReadTurnId(reader);
                    Submit(seat, turn, _engine.Execute(new PlaceCall(seat, call, turn)), now, statusChanged);
                    return;
                }

                case ClientMessageKind.PlayCard:
                {
                    Card card = WireCodec.ReadCard(reader);
                    TurnId turn = WireCodec.ReadTurnId(reader);
                    Submit(seat, turn, _engine.Execute(new PlayCard(seat, card, turn)), now, statusChanged);
                    return;
                }

                case ClientMessageKind.ReadyNext:
                    if (_engine.Execute(new ReadyNext(seat)).IsAccepted || statusChanged)
                    {
                        Flush(now);
                    }

                    return;

                case ClientMessageKind.Emote:
                    ShowEmote(seat, reader.Byte(), now);
                    break;

                case ClientMessageKind.TableReady:
                case ClientMessageKind.RequestResync:
                    if (statusChanged)
                    {
                        Flush(now);
                        statusChanged = false;
                    }

                    SendSync(seat, now);
                    break;

                case ClientMessageKind.LeaveTable:
                    Leave(seat, now);
                    return;
            }

            if (statusChanged)
            {
                Flush(now);
            }
        }

        /// <summary>The member's connection dropped or came back (GDD §8: the seat is held, a bot covers its turns).</summary>
        public void SetConnected(Seat seat, bool connected, double now)
        {
            SeatState state = _seats[(int)seat];
            if (!state.Human || state.Gone)
            {
                return;
            }

            if (!connected)
            {
                if (state.Presence == SeatConnection.Reconnecting)
                {
                    return;
                }

                state.Presence = SeatConnection.Reconnecting;
                state.DisconnectedAt = now;
                _bots.TakeOver(seat, null);
            }
            else
            {
                if (state.Presence == SeatConnection.Connected)
                {
                    return;
                }

                state.Presence = SeatConnection.Connected;
                state.ConsecutiveTimeouts = 0;
                _bots.Release(seat);
            }

            Flush(now);
        }

        /// <summary>The member left on purpose: a bot plays the seat to the end (GDD §8).</summary>
        public void Leave(Seat seat, double now)
        {
            SeatState state = _seats[(int)seat];
            if (!state.Human || state.Gone)
            {
                return;
            }

            state.Gone = true;
            state.Presence = SeatConnection.Left;
            _bots.TakeOver(seat, null);
            Flush(now);
        }

        /// <summary>Sends the seat its whole state (join, reconnect, resync).</summary>
        public void SendSync(Seat seat, double now)
        {
            SeatState state = _seats[(int)seat];
            var update = new TableUpdate(state.Seq, null, _engine.ViewFor(seat), StatusAt(now));
            Outbound?.Invoke(seat, HostMessages.SyncState(update));
        }

        /// <summary>The table's clocks and presences as of <paramref name="now"/>.</summary>
        public TableStatus StatusAt(double now)
        {
            int remaining = TableStatus.NoClock;
            int duration = 0;
            Seat timerSeat = _engine.CurrentSeat;
            if (_clockRunning)
            {
                double left = Math.Max(0.0, Math.Min(_clockDeadline - now, _clockSeconds));
                remaining = (int)Math.Round(left * 1000.0);
                duration = _clockSeconds * 1000;
                timerSeat = _clockSeat;
            }

            int summary = TableStatus.NoClock;
            if (_summaryOpen && _engine.Phase == Phase.RoundSummary)
            {
                // No turn is open in the summary, so the duration is free to say how long the countdown is.
                summary = (int)Math.Round(Math.Max(0.0, _summaryDeadline - now) * 1000.0);
                duration = (int)Math.Round(_clocks.SummarySeconds * 1000.0);
            }

            var connections = new SeatConnection[_seats.Length];
            for (int s = 0; s < _seats.Length; s++)
            {
                connections[s] = _seats[s].Human ? _seats[s].Presence : SeatConnection.Connected;
            }

            return new TableStatus(timerSeat, remaining, duration, summary, connections);
        }

        private void Submit(Seat seat, TurnId turn, CommandResult result, double now, bool statusChanged)
        {
            if (result.IsAccepted)
            {
                _seats[(int)seat].ConsecutiveTimeouts = 0;
                Flush(now);
                return;
            }

            Outbound?.Invoke(seat, HostMessages.IntentRejected(turn, result.Reason));
            if (statusChanged)
            {
                Flush(now);
            }
        }

        /// <summary>GDD §8: the clock ran out. The Normal bot's choice is played, marked auto; twice in a row and the seat is Away.</summary>
        private void TimeOut(double now)
        {
            Seat seat = _clockSeat;
            _clockRunning = false;
            SeatState state = _seats[(int)seat];
            _bots.ActFor(_engine, seat);
            state.ConsecutiveTimeouts++;
            if (state.ConsecutiveTimeouts >= Protocol.TimeoutsBeforeAway && state.Presence == SeatConnection.Connected)
            {
                state.Presence = SeatConnection.Away;
                _bots.TakeOver(seat, null);
            }

            Flush(now);
        }

        private void ShowEmote(Seat seat, int emoteId, double now)
        {
            SeatState state = _seats[(int)seat];
            if (emoteId >= EmoteCount
                || now - state.LastEmoteAt < Protocol.EmoteIntervalSeconds
                || state.EmotesThisRound >= Protocol.EmotesPerRound)
            {
                return;
            }

            state.LastEmoteAt = now;
            state.EmotesThisRound++;
            byte[] payload = HostMessages.EmoteShown(seat, emoteId);
            for (int s = 0; s < _seats.Length; s++)
            {
                if (_seats[s].Human && !_seats[s].Gone)
                {
                    Outbound?.Invoke((Seat)s, payload);
                }
            }
        }

        /// <summary>
        /// Takes everything the engine emitted, paces the table from it, and sends each seat its
        /// own numbered update: the events it may see, its view and the table status.
        /// </summary>
        private void Flush(double now)
        {
            _outbox.Clear();
            bool ended = false;
            int examined = 0;
            do
            {
                _engine.DrainEvents(_outbox);
                for (; examined < _outbox.Count; examined++)
                {
                    MatchEvent e = _outbox[examined];
                    _bots.Observe(e, now);
                    switch (e)
                    {
                        case TrickCompleted _:
                            _holdUntil = Math.Max(_holdUntil, now + _timing.TrickDisplaySeconds);
                            break;
                        case RoundStarted _:
                            _holdUntil = Math.Max(_holdUntil, now + _timing.DealSeconds);
                            for (int s = 0; s < _seats.Length; s++)
                            {
                                _seats[s].EmotesThisRound = 0;
                            }

                            break;
                        case RoundScored _:
                            _summaryOpen = true;
                            _summaryDeadline = now + _clocks.SummarySeconds;
                            break;
                        case MatchEnded _:
                            ended = true;
                            break;
                    }
                }
            }
            while (ReadyAbsentSeats());

            RefreshClock(now);
            TableStatus status = StatusAt(now);
            for (int s = 0; s < _seats.Length; s++)
            {
                SeatState state = _seats[s];
                if (!state.Human || state.Gone)
                {
                    continue;
                }

                var seat = (Seat)s;
                _visible.Clear();
                for (int i = 0; i < _outbox.Count; i++)
                {
                    if (_outbox[i].Visibility.IsVisibleTo(seat))
                    {
                        _visible.Add(_outbox[i]);
                    }
                }

                state.Seq++;
                var update = new TableUpdate(state.Seq, _visible.ToArray(), _engine.ViewFor(seat), status);
                Outbound?.Invoke(seat, HostMessages.TableBatch(update));
            }

            if (ended && !_over)
            {
                _over = true;
                _clockRunning = false;
                Ended?.Invoke();
            }
        }

        /// <summary>
        /// In the round summary nobody waits for a seat whose person is not there: it is readied at
        /// once. Returns true when that produced more events to drain.
        /// </summary>
        private bool ReadyAbsentSeats()
        {
            if (_engine.Phase != Phase.RoundSummary)
            {
                return false;
            }

            bool any = false;
            for (int s = 0; s < _seats.Length; s++)
            {
                SeatState state = _seats[s];
                if (state.Human && state.Presence != SeatConnection.Connected && _engine.Phase == Phase.RoundSummary
                    && _engine.Execute(new ReadyNext((Seat)s)).IsAccepted)
                {
                    any = true;
                }
            }

            return any;
        }

        /// <summary>
        /// Runs the turn clock while a present person holds the turn; bots pace themselves. A call
        /// turn and a play turn each have their own length (GDD §6: 20 s and 15 s in Quick Match).
        /// </summary>
        private void RefreshClock(double now)
        {
            TurnId turn = _engine.CurrentTurn;
            bool open = !turn.IsNone && (_engine.Phase == Phase.Calling || _engine.Phase == Phase.Playing);
            int seconds = _engine.Phase == Phase.Calling ? _clocks.CallSeconds : _clocks.PlaySeconds;
            if (!open || seconds <= 0)
            {
                _clockRunning = false;
                _clockTurn = TurnId.None;
                return;
            }

            Seat seat = _engine.CurrentSeat;
            SeatState state = _seats[(int)seat];
            if (!state.Human || state.Presence != SeatConnection.Connected)
            {
                _clockRunning = false;
                _clockTurn = TurnId.None;
                return;
            }

            if (_clockRunning && _clockTurn == turn && _clockSeat == seat)
            {
                return;
            }

            _clockRunning = true;
            _clockTurn = turn;
            _clockSeat = seat;
            _clockSeconds = seconds;
            _clockDeadline = Math.Max(now, _holdUntil) + seconds;
        }

        private sealed class SeatState
        {
            public SeatState(bool human)
            {
                Human = human;
            }

            public bool Human { get; }

            /// <summary>The person is gone for good (left, or held too long); a bot owns the seat.</summary>
            public bool Gone { get; set; }

            public SeatConnection Presence { get; set; } = SeatConnection.Connected;

            public int ConsecutiveTimeouts { get; set; }

            public double DisconnectedAt { get; set; }

            public uint Seq { get; set; }

            public double LastEmoteAt { get; set; } = double.NegativeInfinity;

            public int EmotesThisRound { get; set; }
        }
    }
}
