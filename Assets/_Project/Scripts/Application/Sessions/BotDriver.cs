using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;

namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// Plays the bot seats of a match (GDD §9 "bots use the same path as people"): when a bot's
    /// turn opens it waits a human-looking think time, asks its strategy with only that seat's
    /// <see cref="PlayerView"/>, and submits an ordinary command that the engine validates like any
    /// other. It can also act for a human seat (a timed-out turn online), marking the command
    /// auto. Runs wherever the engine runs: offline on the device, online on the server.
    /// <para>
    /// Given a <see cref="IWorker"/>, a Hard bot thinks off the main thread (GDD §9: up to 40 ms
    /// a decision, on a worker; §20): its decision starts with the think delay, against the
    /// immutable view and a generator of its own, and is played on the main thread once both are
    /// over. Without one every decision is made on the spot, as tests and replays want.
    /// </para>
    /// </summary>
    public sealed class BotDriver
    {
        private readonly Dictionary<Seat, IBotStrategy> _bots;
        private readonly IBotStrategy _standIn;
        private readonly IRandom _decisions;
        private readonly IRandom _pace;
        private readonly BotTiming _timing;
        private readonly IWorker _worker;

        private TurnId _scheduledTurn = TurnId.None;
        private double _dueAt;
        private double _holdUntil;
        private Thinking _thinking;

        /// <param name="bots">The bot seats and their strategies.</param>
        /// <param name="standIn">Plays for people who are away or timed out (GDD §8: the Normal choice).</param>
        /// <param name="decisions">Randomness for the strategies' choices.</param>
        /// <param name="pace">Randomness for think times, kept apart so pacing never changes the game.</param>
        /// <param name="worker">Where Hard bots think (<see cref="ThreadPool"/> in the app); null decides every move on the spot.</param>
        public BotDriver(IReadOnlyDictionary<Seat, IBotStrategy> bots, IBotStrategy standIn, IRandom decisions, IRandom pace, BotTiming timing, IWorker worker = null)
        {
            _bots = new Dictionary<Seat, IBotStrategy>();
            foreach (KeyValuePair<Seat, IBotStrategy> pair in bots)
            {
                _bots[pair.Key] = pair.Value;
            }

            _standIn = standIn;
            _decisions = decisions;
            _pace = pace;
            _timing = timing;
            _worker = worker;
        }

        /// <summary>Runs a bot's decision away from the main thread; its result is read back on the main thread.</summary>
        public interface IWorker
        {
            Task<T> Run<T>(Func<T> decision);
        }

        /// <summary>The thread pool (GDD §20: Task.Run against an immutable PlayerView).</summary>
        public static IWorker ThreadPool { get; } = new ThreadPoolWorker();

        public bool IsBot(Seat seat) => _bots.ContainsKey(seat);

        /// <summary>
        /// A seat's person has gone (dropped, Away, or left): a bot plays it from now on. A turn's
        /// think time is drawn once, when a bot first has that turn; taking over or releasing any
        /// seat never draws it again, so people coming and going cannot hold a bot's move back.
        /// </summary>
        public void TakeOver(Seat seat, IBotStrategy strategy)
        {
            _bots[seat] = strategy ?? _standIn;
        }

        /// <summary>The seat's person is back (reconnected, or tapped "I'm back"): bots stop playing it.</summary>
        public void Release(Seat seat)
        {
            _bots.Remove(seat);
        }

        /// <summary>Holds every bot until <paramref name="until"/> (a trick on display, the deal animation).</summary>
        public void Hold(double until)
        {
            if (until > _holdUntil)
            {
                _holdUntil = until;
            }
        }

        /// <summary>Moves pending deadlines forward after a pause.</summary>
        public void Shift(double seconds)
        {
            _dueAt += seconds;
            _holdUntil += seconds;
        }

        /// <summary>
        /// Tells the driver what the engine just emitted, so it can pace itself as the table shows
        /// it: a played card flies to the trick first, so the next bot starts thinking once it has
        /// landed, and a finished trick's pause starts then too (ADR 055).
        /// </summary>
        public void Observe(MatchEvent e, double now)
        {
            switch (e)
            {
                case CardPlayed _:
                    Hold(now + _timing.CardFlightSeconds);
                    break;
                case TrickCompleted _:
                    Hold(now + _timing.CardFlightSeconds + _timing.TrickDisplaySeconds);
                    break;
                case RoundStarted _:
                    Hold(now + _timing.DealSeconds);
                    break;
            }
        }

        /// <summary>
        /// Acts for the current seat if it is a bot and its think time has passed. Returns true
        /// when a command was submitted (its result in <paramref name="result"/>).
        /// </summary>
        public bool TryAct(MatchEngine engine, double now, out CommandResult result)
        {
            result = CommandResult.Accepted;
            if (engine.CurrentTurn.IsNone || (engine.Phase != Phase.Calling && engine.Phase != Phase.Playing))
            {
                _scheduledTurn = TurnId.None;
                return false;
            }

            Seat seat = engine.CurrentSeat;
            if (!_bots.TryGetValue(seat, out IBotStrategy strategy))
            {
                return false;
            }

            if (_scheduledTurn != engine.CurrentTurn)
            {
                _scheduledTurn = engine.CurrentTurn;
                PlayerView view = engine.ViewFor(seat);
                _dueAt = Math.Max(now, _holdUntil) + ThinkTime(view);
                _thinking = StartThinking(engine.Phase, seat, strategy, view);
                return false;
            }

            if (now < _dueAt || now < _holdUntil)
            {
                return false;
            }

            Thinking thinking = _thinking;
            if (thinking != null && thinking.Seat == seat && thinking.Strategy == strategy)
            {
                if (!thinking.IsDone)
                {
                    // The think time is over but the decision is not: the move waits for it.
                    return false;
                }

                result = Submit(engine, seat, thinking);
            }
            else
            {
                result = Decide(engine, seat, strategy, isAuto: false);
            }

            _thinking = null;
            _scheduledTurn = TurnId.None;
            return true;
        }

        /// <summary>Acts at once for a person's seat (a timed-out turn), marking the command auto.</summary>
        public CommandResult ActFor(MatchEngine engine, Seat seat)
        {
            return Decide(engine, seat, _standIn, isAuto: true);
        }

        private CommandResult Decide(MatchEngine engine, Seat seat, IBotStrategy strategy, bool isAuto)
        {
            PlayerView view = engine.ViewFor(seat);
            if (engine.Phase == Phase.Calling)
            {
                int call = CallRules.Clamp(strategy.ChooseCall(view, _decisions), view.Rules);
                return engine.Execute(new PlaceCall(seat, call, engine.CurrentTurn, isAuto));
            }

            Card card = strategy.ChooseCard(view, _decisions);
            if (!view.LegalCards().Contains(card))
            {
                // A strategy must never do this; fall back to a legal card rather than stall the table.
                card = view.LegalCards().Lowest();
            }

            return engine.Execute(new PlayCard(seat, card, engine.CurrentTurn, isAuto));
        }

        /// <summary>
        /// A Hard bot starts deciding when its think delay starts, on the worker. The view is
        /// immutable, and the decision gets a generator of its own, seeded here on the main thread
        /// in turn order, so the move is the same whichever thread makes it and whenever it ends.
        /// </summary>
        private Thinking StartThinking(Phase phase, Seat seat, IBotStrategy strategy, PlayerView view)
        {
            if (_worker == null || strategy.Level != BotLevel.Hard || (phase != Phase.Calling && phase != Phase.Playing))
            {
                return null;
            }

            ulong seed = ((ulong)(uint)_decisions.NextInt(int.MaxValue) << 32) | (uint)_decisions.NextInt(int.MaxValue);
            IRandom rng = new Pcg32Random(seed);
            return phase == Phase.Calling
                ? new Thinking(seat, strategy, _worker.Run(() => strategy.ChooseCall(view, rng)), null)
                : new Thinking(seat, strategy, null, _worker.Run(() => strategy.ChooseCard(view, rng)));
        }

        /// <summary>Plays a finished decision; what the strategy threw is thrown here, on the main thread, as if it had decided here.</summary>
        private CommandResult Submit(MatchEngine engine, Seat seat, Thinking thinking)
        {
            PlayerView view = engine.ViewFor(seat);
            if (engine.Phase == Phase.Calling && thinking.Call != null)
            {
                int call = CallRules.Clamp(thinking.Call.GetAwaiter().GetResult(), view.Rules);
                return engine.Execute(new PlaceCall(seat, call, engine.CurrentTurn, false));
            }

            if (engine.Phase == Phase.Playing && thinking.Card != null)
            {
                Card card = thinking.Card.GetAwaiter().GetResult();
                if (!view.LegalCards().Contains(card))
                {
                    card = view.LegalCards().Lowest();
                }

                return engine.Execute(new PlayCard(seat, card, engine.CurrentTurn, false));
            }

            return Decide(engine, seat, thinking.Strategy, isAuto: false);
        }

        private double ThinkTime(PlayerView view)
        {
            if (view.Phase == Phase.Playing && view.LegalCards().Count == 1)
            {
                return _timing.ForcedSeconds;
            }

            double span = _timing.ThinkMaxSeconds - _timing.ThinkMinSeconds;
            if (span <= 0)
            {
                return _timing.ThinkMinSeconds;
            }

            return _timing.ThinkMinSeconds + span * (_pace.NextInt(1000) / 1000.0);
        }

        /// <summary>A Hard bot's decision under way for the scheduled turn: a call or a card.</summary>
        private sealed class Thinking
        {
            public Thinking(Seat seat, IBotStrategy strategy, Task<int> call, Task<Card> card)
            {
                Seat = seat;
                Strategy = strategy;
                Call = call;
                Card = card;
            }

            public Seat Seat { get; }

            public IBotStrategy Strategy { get; }

            public Task<int> Call { get; }

            public Task<Card> Card { get; }

            public bool IsDone => Call != null ? Call.IsCompleted : Card.IsCompleted;
        }

        private sealed class ThreadPoolWorker : IWorker
        {
            public Task<T> Run<T>(Func<T> decision) => Task.Run(decision);
        }
    }
}
