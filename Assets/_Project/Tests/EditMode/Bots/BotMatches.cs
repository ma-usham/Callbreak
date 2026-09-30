using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Bots
{
    /// <summary>
    /// Plays whole matches with a strategy in every seat, driving the engine exactly as Docs/API/Core.md
    /// "Drive a match" does but asking the strategies. After every command it asserts the command was
    /// accepted and every GDD §22 invariant holds; every call must be in range and every card legal.
    /// </summary>
    public static class BotMatches
    {
        /// <summary>Plays a match to the end and returns South's final view (standings and score sheet).</summary>
        /// <param name="rules">The rule set.</param>
        /// <param name="seed">Seeds the deal (engine) and, on another stream, the bots' decisions.</param>
        /// <param name="seats">The strategy for each seat, by seat number.</param>
        /// <param name="timings">Collects decision times per level, if given.</param>
        /// <param name="beforeCard">Called before each card decision with the engine and the acting view, if given.</param>
        public static PlayerView Play(
            RuleSet rules,
            ulong seed,
            IReadOnlyList<IBotStrategy> seats,
            DecisionTimings timings = null,
            Action<MatchEngine, PlayerView> beforeCard = null)
        {
            var engine = new MatchEngine(rules, new Pcg32Random(seed), TestSetups.AllBots(matchId: "bots-" + seed));
            var decisions = new Pcg32Random(seed, 17UL);
            var events = new List<MatchEvent>();
            engine.Start();
            engine.AssertInvariants();
            int guard = 0;
            while (engine.Phase != Phase.MatchOver)
            {
                if (++guard > 5000)
                {
                    Assert.Fail("Match " + seed + " did not finish.");
                }

                engine.DrainEvents(events);
                events.Clear();
                PlayerView view = engine.ViewFor(engine.CurrentSeat);
                IBotStrategy bot = seats[(int)view.Viewer];
                if (engine.Phase == Phase.Calling)
                {
                    long start = Stopwatch.GetTimestamp();
                    int call = bot.ChooseCall(view, decisions);
                    timings?.AddCall(bot.Level, Stopwatch.GetTimestamp() - start);
                    Assert.IsTrue(CallRules.IsValid(call, rules), bot.Level + " called " + call + " in match " + seed);
                    Accept(engine.Execute(new PlaceCall(view.Viewer, call, view.CurrentTurn)), seed);
                }
                else if (engine.Phase == Phase.Playing)
                {
                    beforeCard?.Invoke(engine, view);
                    long start = Stopwatch.GetTimestamp();
                    Card card = bot.ChooseCard(view, decisions);
                    timings?.AddCard(bot.Level, Stopwatch.GetTimestamp() - start);
                    Assert.IsTrue(view.LegalCards().Contains(card), bot.Level + " chose illegal " + card + "\n" + view);
                    Accept(engine.Execute(new PlayCard(view.Viewer, card, view.CurrentTurn)), seed);
                }
                else if (engine.Phase == Phase.RoundSummary)
                {
                    for (int s = 0; s < 4; s++)
                    {
                        engine.Execute(new ReadyNext((Seat)s));
                    }
                }
                else
                {
                    Assert.Fail("The engine waits in " + engine.Phase + ".");
                }

                engine.AssertInvariants();
            }

            return engine.ViewFor(Seat.South);
        }

        /// <summary>Every seat's real hand, from the authority's snapshot: only tests may look.</summary>
        public static CardSet[] TrueHands(MatchEngine engine)
        {
            MatchSnapshot snapshot = engine.Snapshot();
            var hands = new CardSet[4];
            for (int s = 0; s < 4; s++)
            {
                foreach (int id in snapshot.seats[s].hand)
                {
                    hands[s] = hands[s].With(Card.FromId(id));
                }
            }

            return hands;
        }

        /// <summary>Whether <paramref name="seat"/> finished first (alone or shared).</summary>
        public static bool Won(IReadOnlyList<Standing> standings, Seat seat)
        {
            for (int i = 0; i < standings.Count; i++)
            {
                if (standings[i].Seat == seat)
                {
                    return standings[i].Rank == 1;
                }
            }

            throw new ArgumentException("No standing for " + seat + ".");
        }

        private static void Accept(CommandResult result, ulong seed)
        {
            Assert.IsTrue(result.IsAccepted, "Rejected in match " + seed + ": " + result);
        }
    }

    /// <summary>
    /// A seat that calls and plays uniformly at random: it reaches every legal play, so it is the opponent
    /// for "whatever anyone legally does" properties. Reports itself as Easy.
    /// </summary>
    public sealed class RandomBot : IBotStrategy
    {
        public BotLevel Level => BotLevel.Easy;

        public int ChooseCall(PlayerView view, IRandom rng)
        {
            return view.Rules.MinCall + rng.NextInt(view.Rules.MaxCall - view.Rules.MinCall + 1);
        }

        public Card ChooseCard(PlayerView view, IRandom rng)
        {
            CardSet legal = view.LegalCards();
            return legal.ElementAt(rng.NextInt(legal.Count));
        }
    }

    /// <summary>Decision times per bot level (card plays and calls apart), from <see cref="Stopwatch"/> ticks.</summary>
    public sealed class DecisionTimings
    {
        private readonly List<long>[] _cards = { new List<long>(), new List<long>(), new List<long>() };
        private readonly List<long>[] _calls = { new List<long>(), new List<long>(), new List<long>() };

        public void AddCard(BotLevel level, long ticks)
        {
            _cards[(int)level].Add(ticks);
        }

        public void AddCall(BotLevel level, long ticks)
        {
            _calls[(int)level].Add(ticks);
        }

        public int Cards(BotLevel level)
        {
            return _cards[(int)level].Count;
        }

        public double MeanCardMilliseconds(BotLevel level)
        {
            return Mean(_cards[(int)level]);
        }

        public double MeanCallMilliseconds(BotLevel level)
        {
            return Mean(_calls[(int)level]);
        }

        /// <summary>E.g. "Hard: 60000 cards mean 0.412 ms, p99 2.1 ms, worst 9.8 ms; 5000 calls mean 1.9 ms, ...".</summary>
        public string Describe(BotLevel level)
        {
            return level + ": " + Describe(_cards[(int)level], "cards") + "; " + Describe(_calls[(int)level], "calls");
        }

        private static string Describe(List<long> ticks, string what)
        {
            if (ticks.Count == 0)
            {
                return "no " + what;
            }

            var sorted = new List<long>(ticks);
            sorted.Sort();
            long p99 = sorted[(int)Math.Min(sorted.Count - 1, sorted.Count * 99L / 100L)];
            return ticks.Count + " " + what + " mean " + Format(Mean(ticks)) + " ms, p99 " + Format(Milliseconds(p99))
                + " ms, worst " + Format(Milliseconds(sorted[sorted.Count - 1])) + " ms";
        }

        private static double Mean(List<long> ticks)
        {
            if (ticks.Count == 0)
            {
                return 0;
            }

            long total = 0;
            for (int i = 0; i < ticks.Count; i++)
            {
                total += ticks[i];
            }

            return Milliseconds(total) / ticks.Count;
        }

        private static double Milliseconds(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        private static string Format(double milliseconds)
        {
            return milliseconds.ToString("0.000", CultureInfo.InvariantCulture);
        }
    }
}
