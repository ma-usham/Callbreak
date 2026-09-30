using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>
    /// Drives an engine the way a session would: submits commands, drains events, records every command
    /// for replay and asserts the GDD §22 invariants after each one. Policies choose calls and cards from
    /// the acting seat's PlayerView only.
    /// </summary>
    public sealed class MatchDriver
    {
        private readonly Pcg32Random _policyRng;
        private int _lastTurnSeen;

        public MatchDriver(MatchEngine engine, bool randomPolicy, ulong policySeed = 1UL)
        {
            Engine = engine;
            RandomPolicy = randomPolicy;
            _policyRng = new Pcg32Random(policySeed, 99UL);
        }

        public MatchEngine Engine { get; }

        public bool RandomPolicy { get; }

        public List<MatchEvent> Events { get; } = new List<MatchEvent>();

        public List<RecordedCommand> Commands { get; } = new List<RecordedCommand>();

        public void Start()
        {
            Engine.Start();
            AfterCommand();
        }

        public CommandResult Submit(RecordedCommand command)
        {
            Commands.Add(command);
            CommandResult result = command.ExecuteOn(Engine);
            AfterCommand();
            return result;
        }

        /// <summary>Makes one legal move for whoever must act; returns false once the match is over.</summary>
        public bool Step()
        {
            switch (Engine.Phase)
            {
                case Phase.Calling:
                {
                    PlayerView view = Engine.ViewFor(Engine.CurrentSeat);
                    int call = RandomPolicy
                        ? view.Rules.MinCall + _policyRng.NextInt(view.Rules.MaxCall - view.Rules.MinCall + 1)
                        : view.Rules.MinCall;
                    Accept(RecordedCommand.Of(new PlaceCall(view.Viewer, call, view.CurrentTurn, isAuto: RandomPolicy && _policyRng.NextInt(5) == 0)));
                    return true;
                }

                case Phase.Playing:
                {
                    PlayerView view = Engine.ViewFor(Engine.CurrentSeat);
                    CardSet legal = view.LegalCards();
                    Card card = RandomPolicy ? legal.ElementAt(_policyRng.NextInt(legal.Count)) : legal.Lowest();
                    Accept(RecordedCommand.Of(new PlayCard(view.Viewer, card, view.CurrentTurn, isAuto: RandomPolicy && _policyRng.NextInt(7) == 0)));
                    return true;
                }

                case Phase.RoundSummary:
                    for (int s = 0; s < 4; s++)
                    {
                        if (!Engine.ViewFor((Seat)s).IsReady((Seat)s) && Engine.Phase == Phase.RoundSummary)
                        {
                            Accept(RecordedCommand.Of(new ReadyNext((Seat)s)));
                        }
                    }

                    return true;

                case Phase.MatchOver:
                case Phase.Closed:
                    return false;

                default:
                    throw new InvalidOperationException("Engine is waiting in " + Engine.Phase + ".");
            }
        }

        public void PlayToEnd()
        {
            int guard = 0;
            while (Step())
            {
                if (++guard > 5000)
                {
                    Assert.Fail("Match did not finish.");
                }
            }
        }

        private void Accept(RecordedCommand command)
        {
            CommandResult result = Submit(command);
            Assert.IsTrue(result.IsAccepted, command + " -> " + result);
        }

        private void AfterCommand()
        {
            Engine.DrainEvents(Events);
            string violation = Engine.FindInvariantViolation();
            Assert.IsNull(violation, violation);
            if (!Engine.CurrentTurn.IsNone)
            {
                Assert.GreaterOrEqual(Engine.CurrentTurn.Value, _lastTurnSeen, "turn ids never go back");
                _lastTurnSeen = Engine.CurrentTurn.Value;
            }
        }
    }
}
