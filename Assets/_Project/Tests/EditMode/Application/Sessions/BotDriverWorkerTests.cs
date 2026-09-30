using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Sessions;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Sessions
{
    /// <summary>
    /// GDD §9 (Hard: ≤ 40 ms per decision, on a worker thread) and §20 (Task.Run against an immutable
    /// PlayerView, the result marshalled back): a Hard bot starts thinking when its think delay
    /// starts, and its move is played on the main thread once both are over. Each decision has a
    /// generator of its own, so what it decides does not depend on which thread ran it or when.
    /// </summary>
    public sealed class BotDriverWorkerTests
    {
        private static readonly BotTiming OneSecond = new BotTiming(1.0, 1.0, 1.0, 0.0, 0.0);

        [Test]
        public void HardBot_StartsThinkingWithItsDelay_AndMovesWhenBothAreOver()
        {
            MatchEngine engine = NewEngine();
            var hard = new Recording(BotLevel.Hard);
            var worker = new ManualWorker();
            BotDriver driver = NewDriver(hard, worker);

            Assert.IsFalse(driver.TryAct(engine, 0.0, out _), "the think delay starts");
            Assert.AreEqual(1, worker.Queued, "and so does the decision, on the worker");
            Assert.AreEqual(0, hard.Decisions, "nothing was decided on this thread");

            worker.RunAll();
            Assert.AreEqual(1, hard.Decisions);
            Assert.IsFalse(driver.TryAct(engine, 0.9, out _), "decided, but the think time is not over");
            Assert.IsTrue(driver.TryAct(engine, 1.0, out CommandResult result), "both are over");
            Assert.IsTrue(result.IsAccepted);
            Assert.AreEqual(hard.LastCall, CallOf(engine), "the worker's decision is the one played");
        }

        [Test]
        public void HardBot_ThinkTimeOverBeforeTheDecision_TheMoveWaitsForIt()
        {
            MatchEngine engine = NewEngine();
            var worker = new ManualWorker();
            BotDriver driver = NewDriver(new Recording(BotLevel.Hard), worker);
            driver.TryAct(engine, 0.0, out _);

            Assert.IsFalse(driver.TryAct(engine, 5.0, out _), "still thinking");
            worker.RunAll();

            Assert.IsTrue(driver.TryAct(engine, 5.0, out CommandResult result));
            Assert.IsTrue(result.IsAccepted);
        }

        [Test]
        public void HardBot_EachDecisionHasAGeneratorOfItsOwn()
        {
            MatchEngine engine = NewEngine();
            var hard = new Recording(BotLevel.Hard);
            var worker = new ManualWorker();
            var decisions = new Pcg32Random(3);
            var driver = new BotDriver(AllSeats(hard), hard, decisions, new Pcg32Random(4), OneSecond, worker);

            double now = 0.0;
            for (int i = 0; i < 40 && hard.Randoms.Count < 3; i++)
            {
                driver.TryAct(engine, now, out _);
                worker.RunAll();
                now += 1.0;
            }

            Assert.AreEqual(3, hard.Randoms.Count, "three decisions were made");
            Assert.AreEqual(3, new HashSet<IRandom>(hard.Randoms).Count, "each with its own generator");
            CollectionAssert.DoesNotContain(hard.Randoms, decisions, "never the driver's own, which other decisions go on drawing from");
        }

        [Test]
        public void HardBot_TurnOverBeforeItsDecisionIsDone_TheDecisionIsNeverPlayed()
        {
            MatchEngine engine = NewEngine();
            Seat first = engine.CurrentSeat;
            var hard = new Recording(BotLevel.Hard);
            var worker = new ManualWorker();
            var bots = new Dictionary<Seat, IBotStrategy>();
            for (int s = 0; s < 4; s++)
            {
                if ((Seat)s != first)
                {
                    bots[(Seat)s] = hard;
                }
            }

            var driver = new BotDriver(bots, hard, new Pcg32Random(3), new Pcg32Random(4), OneSecond, worker);
            driver.TakeOver(first, hard);
            driver.TryAct(engine, 0.0, out _);

            // The person is back and calls for themself before the bot has finished thinking.
            driver.Release(first);
            Assert.IsTrue(engine.Execute(new PlaceCall(first, 7, engine.CurrentTurn)).IsAccepted);
            Seat next = engine.CurrentSeat;
            worker.RunAll();
            driver.TryAct(engine, 2.0, out _);
            driver.TryAct(engine, 3.0, out _);

            Assert.AreEqual(7, engine.ViewFor(first).CallOf(first), "the person's call stands");
            Assert.AreEqual(CallRules.NotCalled, engine.ViewFor(next).CallOf(next), "the next bot waits for a decision of its own, not the stale one");
            worker.RunAll();
            Assert.IsTrue(driver.TryAct(engine, 3.0, out CommandResult result) && result.IsAccepted, "and then moves");
        }

        [Test]
        public void NormalBot_WithAWorker_StillDecidesAtOnceWhenItMoves()
        {
            MatchEngine engine = NewEngine();
            var normal = new Recording(BotLevel.Normal);
            var worker = new ManualWorker();
            BotDriver driver = NewDriver(normal, worker);

            driver.TryAct(engine, 0.0, out _);
            Assert.IsTrue(driver.TryAct(engine, 1.0, out CommandResult result));

            Assert.IsTrue(result.IsAccepted);
            Assert.AreEqual(0, worker.Queued, "only Hard bots take long enough to need a worker");
        }

        [Test]
        public void ThreadPool_RunsTheDecisionOffTheCallingThread()
        {
            int caller = Thread.CurrentThread.ManagedThreadId;

            Task<int> ran = BotDriver.ThreadPool.Run(() => Thread.CurrentThread.ManagedThreadId);

            Assert.IsTrue(ran.Wait(TimeSpan.FromSeconds(10)));
            Assert.AreNotEqual(caller, ran.Result);
        }

        private static BotDriver NewDriver(IBotStrategy strategy, BotDriver.IWorker worker) =>
            new BotDriver(AllSeats(strategy), strategy, new Pcg32Random(3), new Pcg32Random(4), OneSecond, worker);

        private static Dictionary<Seat, IBotStrategy> AllSeats(IBotStrategy strategy)
        {
            var bots = new Dictionary<Seat, IBotStrategy>();
            for (int s = 0; s < 4; s++)
            {
                bots[(Seat)s] = strategy;
            }

            return bots;
        }

        private static MatchEngine NewEngine()
        {
            var seats = new[]
            {
                new SeatProfile("Hari", "0", "classic-crimson", false),
                new SeatProfile("Suman", "1", "himalayan-ivory", false),
                new SeatProfile("Asha", "2", "kathmandu-gold", false),
                new SeatProfile("Bikash", "3", "classic-crimson", false),
            };
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(21), new MatchSetup("worker", seats));
            engine.Start();
            engine.DrainEvents(new List<MatchEvent>());
            return engine;
        }

        private static int CallOf(MatchEngine engine)
        {
            for (int s = 0; s < 4; s++)
            {
                int call = engine.ViewFor((Seat)s).CallOf((Seat)s);
                if (call != CallRules.NotCalled)
                {
                    return call;
                }
            }

            return CallRules.NotCalled;
        }

        /// <summary>Runs what it is given only when the test says so, on the test's thread: deterministic and synchronous.</summary>
        private sealed class ManualWorker : BotDriver.IWorker
        {
            private readonly List<Action> _queued = new List<Action>();

            public int Queued { get; private set; }

            public Task<T> Run<T>(Func<T> decision)
            {
                var done = new TaskCompletionSource<T>();
                Queued++;
                _queued.Add(() => done.SetResult(decision()));
                return done.Task;
            }

            public void RunAll()
            {
                Action[] all = _queued.ToArray();
                _queued.Clear();
                foreach (Action work in all)
                {
                    work();
                }
            }
        }

        /// <summary>Calls 3 and plays the lowest legal card, and remembers what it was given.</summary>
        private sealed class Recording : IBotStrategy
        {
            public Recording(BotLevel level)
            {
                Level = level;
            }

            public BotLevel Level { get; }

            public int Decisions { get; private set; }

            public int LastCall { get; private set; }

            public List<IRandom> Randoms { get; } = new List<IRandom>();

            public int ChooseCall(PlayerView view, IRandom rng)
            {
                Decisions++;
                Randoms.Add(rng);
                LastCall = 3;
                return LastCall;
            }

            public Card ChooseCard(PlayerView view, IRandom rng)
            {
                Decisions++;
                Randoms.Add(rng);
                return view.LegalCards().Lowest();
            }
        }
    }
}
