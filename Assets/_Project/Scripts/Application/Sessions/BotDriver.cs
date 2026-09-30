using System.Collections.Generic;
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
    /// </summary>
    public sealed class BotDriver
    {
        private readonly Dictionary<Seat, IBotStrategy> _bots;
        private readonly IBotStrategy _standIn;
        private readonly IRandom _decisions;
        private readonly IRandom _pace;
        private readonly BotTiming _timing;

        private TurnId _scheduledTurn = TurnId.None;
        private double _dueAt;
        private double _holdUntil;

        /// <param name="bots">The bot seats and their strategies.</param>
        /// <param name="standIn">Plays for people who are away or timed out (GDD §8: the Normal choice).</param>
        /// <param name="decisions">Randomness for the strategies' choices.</param>
        /// <param name="pace">Randomness for think times, kept apart so pacing never changes the game.</param>
        public BotDriver(IReadOnlyDictionary<Seat, IBotStrategy> bots, IBotStrategy standIn, IRandom decisions, IRandom pace, BotTiming timing)
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
        }

        public bool IsBot(Seat seat) => _bots.ContainsKey(seat);

        /// <summary>A seat's person has gone (left, or away for good): a bot plays it from now on.</summary>
        public void TakeOver(Seat seat, IBotStrategy strategy)
        {
            _bots[seat] = strategy ?? _standIn;
            _scheduledTurn = TurnId.None;
        }

        /// <summary>The seat's person is back (reconnected, or tapped "I'm back"): bots stop playing it.</summary>
        public void Release(Seat seat)
        {
            if (_bots.Remove(seat))
            {
                _scheduledTurn = TurnId.None;
            }
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

        /// <summary>Tells the driver what the engine just emitted, so it can pace itself.</summary>
        public void Observe(MatchEvent e, double now)
        {
            switch (e)
            {
                case TrickCompleted _:
                    Hold(now + _timing.TrickDisplaySeconds);
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
                _dueAt = System.Math.Max(now, _holdUntil) + ThinkTime(engine.ViewFor(seat));
                return false;
            }

            if (now < _dueAt || now < _holdUntil)
            {
                return false;
            }

            result = Decide(engine, seat, strategy, isAuto: false);
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
    }
}
