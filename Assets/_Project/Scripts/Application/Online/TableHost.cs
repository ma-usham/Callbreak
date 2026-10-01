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

        /// <summary>
        /// How long a table takes to show the end of a round before its results open, so that the
        /// round results' countdown (GDD §6: 10 s in Quick Match, 15 s in a private room) starts only
        /// then and players see all of it: the last card's flight 0.26 s, the finished trick 0.9 s and
        /// its sweep 0.35 s, then the forced last trick, four cards of a 0.35 s beat and a 0.26 s
        /// flight each, and 1.25 s for that trick: 0.26 + 0.9 + 0.35 + 4 × (0.35 + 0.26) + 1.25 = 5.2 s.
        /// </summary>
        public const double RoundEndPlaybackSeconds = 5.2;

        private const int MaxBotMovesPerTick = 8;

        private readonly MatchEngine _engine;
        private readonly RoundSeededRandom _shuffle;
        private readonly BotDriver _bots;
        private readonly BotChatter _chatter;
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

            // GDD §22: each round is dealt from a seed of its own, drawn when that round begins.
            _shuffle = new RoundSeededRandom(setup.Seeds, () => _engine.Round);
            _engine = new MatchEngine(setup.Rules, _shuffle, new MatchSetup(setup.MatchId, setup.Seats));

            var botSeats = new Dictionary<Seat, IBotStrategy>();
            for (int s = 0; s < _seats.Length; s++)
            {
                _seats[s] = new SeatState(setup.Humans[s]);
                if (!setup.Humans[s])
                {
                    botSeats[(Seat)s] = botFactory.Create(setup.FillLevel);
                }
            }

            // The bots' choices and think times are seen by everyone, so their generators owe
            // nothing to any shuffle: timing a bot tells nobody anything about a deal.
            _bots = new BotDriver(botSeats, botFactory.Create(BotLevel.Normal), new Pcg32Random(setup.Seeds()), new Pcg32Random(setup.Seeds()), _timing);

            // What the bots say tells nobody anything either, and its generator comes from the match
            // id, so the table draws its seeds as it did before the bots spoke (ADR 026).
            if (setup.BotsChat)
            {
                _chatter = new BotChatter(seat => !_seats[(int)seat].Human, new Pcg32Random(StableHash(setup.MatchId)), setup.Rules.Rounds);
            }
        }

        /// <summary>A payload for the member in a seat. The room routes it to that member's connection.</summary>
        public event Action<Seat, byte[]> Outbound;

        /// <summary>Raised once, when the last round has been scored and the standings are final.</summary>
        public event Action Ended;

        public string MatchId => _engine.MatchId;

        /// <summary>Every shuffle seed drawn so far, for tests that prove they stay here: none ever leaves the host (GDD §22).</summary>
        internal IReadOnlyList<ulong> ShuffleSeeds => _shuffle.Seeds;

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

            if (_chatter != null && _chatter.TryTake(now, out Seat speaker, out int phrase))
            {
                ShowEmote(speaker, phrase, now);
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

            // The whole message is read before anything changes: one that cannot be read throws here,
            // the room drops it, and the seat is as it was (an Away seat stays Away, its bot playing).
            int call = 0;
            int emote = 0;
            Card card = default;
            TurnId turn = TurnId.None;
            switch (kind)
            {
                case ClientMessageKind.PlaceCall:
                    call = reader.Byte();
                    turn = WireCodec.ReadTurnId(reader);
                    break;

                case ClientMessageKind.PlayCard:
                    card = WireCodec.ReadCard(reader);
                    turn = WireCodec.ReadTurnId(reader);
                    break;

                case ClientMessageKind.Emote:
                    emote = reader.Byte();
                    break;

                case ClientMessageKind.ReadyNext:
                case ClientMessageKind.TableReady:
                case ClientMessageKind.RequestResync:
                case ClientMessageKind.Presence:
                case ClientMessageKind.LeaveTable:
                    break;

                default:
                    // Not a table intent of this protocol, so not an intent at all.
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
                    Submit(seat, turn, _engine.Execute(new PlaceCall(seat, call, turn)), now, statusChanged);
                    return;

                case ClientMessageKind.PlayCard:
                    Submit(seat, turn, _engine.Execute(new PlayCard(seat, card, turn)), now, statusChanged);
                    return;

                case ClientMessageKind.ReadyNext:
                    if (_engine.Execute(new ReadyNext(seat)).IsAccepted || statusChanged)
                    {
                        Flush(now);
                    }

                    return;

                case ClientMessageKind.Emote:
                    if (ShowEmote(seat, emote, now))
                    {
                        _chatter?.Heard(seat, emote, now);
                    }

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
                if (!_bots.IsBot(seat))
                {
                    _bots.TakeOver(seat, null);
                }
            }
            else
            {
                if (state.Presence == SeatConnection.Connected)
                {
                    return;
                }

                state.Presence = SeatConnection.Connected;
                state.ConsecutiveTimeouts = 0;
                if (HoldsOpenTurn(seat))
                {
                    // GDD §8: a returning player takes control from the next turn. The bot covering
                    // this one finishes it on the think time it already has, and no clock starts, so
                    // dropping and coming back never buys time (the person's own move still counts
                    // if it arrives first).
                    state.BotFinishes = _engine.CurrentTurn;
                }
                else
                {
                    _bots.Release(seat);
                }
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

        /// <summary>
        /// The table's clocks and presences as of <paramref name="now"/>. The time left is the true
        /// time left: a turn that opened while a deal or a finished trick is still shown reports more
        /// than its length, because its clock only starts when that hold ends (GDD §8: clients only
        /// display the server's clock, so theirs runs out when this one does).
        /// </summary>
        public TableStatus StatusAt(double now)
        {
            int remaining = TableStatus.NoClock;
            int duration = 0;
            Seat timerSeat = _engine.CurrentSeat;
            if (_clockRunning)
            {
                double left = Math.Max(0.0, _clockDeadline - now);
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

        /// <summary>Sends a seat's quick phrase to everyone at the table, within the limits (GDD §8); false when it was held back.</summary>
        private bool ShowEmote(Seat seat, int emoteId, double now)
        {
            SeatState state = _seats[(int)seat];
            if (emoteId >= EmoteCount
                || now - state.LastEmoteAt < Protocol.EmoteIntervalSeconds
                || state.EmotesThisRound >= Protocol.EmotesPerRound)
            {
                return false;
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

            return true;
        }

        /// <summary>FNV-1a over the text: the same match id always gives the same seed.</summary>
        private static ulong StableHash(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 1099511628211UL;
            }

            return hash;
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
                    _chatter?.Observe(e, now);
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
                            // The countdown runs once every table has shown the round's end; the
                            // people still to say ready are readied when it runs out (Tick).
                            _summaryOpen = true;
                            _summaryDeadline = now + (_clocks.SummarySeconds > 0.0 ? RoundEndPlaybackSeconds + _clocks.SummarySeconds : 0.0);
                            break;
                        case MatchEnded _:
                            ended = true;
                            break;
                    }
                }
            }
            while (ReadyAbsentSeats());

            ReleaseReturnedSeats();
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

        /// <summary>A player who came back during their own turn gets the seat back once the bot has finished that turn.</summary>
        private void ReleaseReturnedSeats()
        {
            for (int s = 0; s < _seats.Length; s++)
            {
                SeatState state = _seats[s];
                if (state.BotFinishes.IsNone || state.BotFinishes == _engine.CurrentTurn)
                {
                    continue;
                }

                state.BotFinishes = TurnId.None;
                if (!state.Gone && state.Presence == SeatConnection.Connected)
                {
                    _bots.Release((Seat)s);
                }
            }
        }

        private bool HoldsOpenTurn(Seat seat) =>
            !_engine.CurrentTurn.IsNone && _engine.CurrentSeat == seat
            && (_engine.Phase == Phase.Calling || _engine.Phase == Phase.Playing);

        /// <summary>
        /// Runs the turn clock while a present person holds the turn; bots pace themselves, and so
        /// does a bot finishing the turn of someone who came back during it. A call turn and a play
        /// turn each have their own length (GDD §6: 20 s and 15 s in Quick Match).
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
            if (!state.Human || state.Presence != SeatConnection.Connected || _bots.IsBot(seat))
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

            /// <summary>The open turn a bot finishes although the person came back during it; None otherwise.</summary>
            public TurnId BotFinishes { get; set; } = TurnId.None;

            public int ConsecutiveTimeouts { get; set; }

            public double DisconnectedAt { get; set; }

            public uint Seq { get; set; }

            public double LastEmoteAt { get; set; } = double.NegativeInfinity;

            public int EmotesThisRound { get; set; }
        }
    }
}
