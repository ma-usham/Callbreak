using System;
using System.Collections.Generic;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;

namespace Callbreak.Core.Match
{
    /// <summary>
    /// Re-runs a logged match (GDD §22 "Determinism &amp; replay": the server logs {matchId, seed, commands[]}
    /// and a replay must equal the recorded event stream). Feeds the same commands to a fresh engine seeded
    /// the same way and returns every event it emits, in order.
    /// </summary>
    public static class MatchReplay
    {
        /// <summary>Replays a match that ran on <c>new Pcg32Random(seed)</c>.</summary>
        /// <param name="rules">The match's rule set.</param>
        /// <param name="seed">The seed of the original <see cref="Pcg32Random"/> (default stream).</param>
        /// <param name="setup">The original setup.</param>
        /// <param name="commands">Every command submitted, in order, including rejected ones.</param>
        /// <returns>All events, starting with MatchStarted.</returns>
        public static List<MatchEvent> Run(RuleSet rules, ulong seed, MatchSetup setup, IReadOnlyList<RecordedCommand> commands)
        {
            return Run(rules, new Pcg32Random(seed), setup, commands);
        }

        /// <summary>Replays a match on the given randomness, which must start where the original's started.</summary>
        /// <param name="rules">The match's rule set.</param>
        /// <param name="rng">Randomness identical to the original's.</param>
        /// <param name="setup">The original setup.</param>
        /// <param name="commands">Every command submitted, in order, including rejected ones.</param>
        /// <returns>All events, starting with MatchStarted.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static List<MatchEvent> Run(RuleSet rules, IRandom rng, MatchSetup setup, IReadOnlyList<RecordedCommand> commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            var engine = new MatchEngine(rules, rng, setup);
            var events = new List<MatchEvent>();
            engine.Start();
            engine.DrainEvents(events);
            for (int i = 0; i < commands.Count; i++)
            {
                commands[i].ExecuteOn(engine);
                engine.DrainEvents(events);
            }

            return events;
        }
    }
}
