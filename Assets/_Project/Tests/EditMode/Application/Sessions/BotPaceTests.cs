using System.Collections.Generic;
using Callbreak.Application.Sessions;
using Callbreak.Bots;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Presentation.Table.Playback;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Sessions
{
    /// <summary>
    /// ADR 055: a bot's think time starts once the card before it has landed on the table, so
    /// every card rests there for a whole think time (0.5 s when the bot's move is forced) before
    /// the next one is thrown, and a finished trick's pause starts once its last card has landed.
    /// Before, the think time ran while the card was still flying, and a forced card followed the
    /// one before it 0.1 s after it landed.
    /// </summary>
    public sealed class BotPaceTests
    {
        private const double Step = 0.01;
        private const double Think = 1.0;
        private const double Forced = 0.5;
        private const double Flight = 0.26;
        private const double TrickShown = 1.25;

        private static readonly BotTiming Paced = new BotTiming(Think, Think, Forced, TrickShown, 0.0, Flight);

        [Test]
        public void EachBotCard_IsThrownAThinkTimeAfterTheCardBeforeItLanded()
        {
            List<Play> plays = PlayOneRound(Paced);
            int checkedPairs = 0, forced = 0;
            for (int i = 1; i < plays.Count; i++)
            {
                // A trick's lead comes after the trick before is shown (the next test); the last
                // trick plays itself (GDD §6), and the table, not a bot, paces its cards.
                if (plays[i].IndexInTrick == 0 || plays[i].ByEngine)
                {
                    continue;
                }

                double expected = Flight + (plays[i].WasForced ? Forced : Think);
                Assert.AreEqual(expected, plays[i].At - plays[i - 1].At, 1.5 * Step, $"card {i}: the card before landed, then the bot thought (forced {plays[i].WasForced})");
                checkedPairs++;
                forced += plays[i].WasForced ? 1 : 0;
            }

            Assert.AreEqual(12 * 3, checkedPairs, "every card after a trick's lead, in the twelve tricks the bots play");
            Assert.Greater(forced, 0, "the round had forced moves too");
        }

        [Test]
        public void AFinishedTrick_IsShownOnceItsLastCardHasLanded_BeforeTheNextLead()
        {
            List<Play> plays = PlayOneRound(Paced);
            int leads = 0;
            for (int i = 1; i < plays.Count; i++)
            {
                if (plays[i].IndexInTrick != 0)
                {
                    continue;
                }

                // The last trick plays itself (GDD §6): the engine leads it, nobody thinks.
                if (plays[i].ByEngine)
                {
                    continue;
                }

                double expected = Flight + TrickShown + (plays[i].WasForced ? Forced : Think);
                Assert.AreEqual(expected, plays[i].At - plays[i - 1].At, 1.5 * Step, $"lead {i}: the last card landed, the trick was shown and swept, then the leader thought");
                leads++;
            }

            Assert.Greater(leads, 8, "the leads of tricks 2 to 12");
        }

        [Test]
        public void TheTables_ShareOneFlightAndOneForcedPause()
        {
            Assert.AreEqual(0.26, BotTiming.TableCardFlightSeconds, 1e-9, "GDD §14: a played card flies for 260 ms");
            Assert.AreEqual(BotTiming.TableCardFlightSeconds, BotTiming.Offline.CardFlightSeconds, 1e-9);
            Assert.AreEqual(BotTiming.TableCardFlightSeconds, BotTiming.Hosted.CardFlightSeconds, 1e-9);
            Assert.AreEqual(0.0, BotTiming.Instant.CardFlightSeconds, 1e-9, "tests and simulations wait for nothing");

            Assert.AreEqual(0.5, BotTiming.Offline.ForcedSeconds, 1e-9, "ADR 055: a forced card rests 0.5 s");
            Assert.AreEqual(BotTiming.Offline.ForcedSeconds, BotTiming.Hosted.ForcedSeconds, 1e-9);
            Assert.AreEqual((float)BotTiming.Offline.ForcedSeconds, MatchEventPlayer.ForcedBeatSeconds, 1e-6f, "the last trick, which the engine plays, waits as long before each card");
        }

        private static List<Play> PlayOneRound(BotTiming timing)
        {
            var seats = new[]
            {
                new SeatProfile("Hari", "0", "classic-crimson", true),
                new SeatProfile("Suman", "1", "classic-crimson", true),
                new SeatProfile("Asha", "2", "classic-crimson", true),
                new SeatProfile("Bikash", "3", "classic-crimson", true),
            };
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(17), new MatchSetup("pace", seats));
            var normal = new NormalStrategy();
            var bots = new Dictionary<Seat, IBotStrategy>();
            for (int s = 0; s < 4; s++)
            {
                bots[(Seat)s] = normal;
            }

            var driver = new BotDriver(bots, normal, new Pcg32Random(5), new Pcg32Random(6), timing, null);
            var events = new List<MatchEvent>();
            var plays = new List<Play>();
            int inTrick = 0;
            double now = 0.0;
            engine.Start();
            Drain(engine, driver, events, now);

            for (int i = 0; i < 100000 && plays.Count < 52; i++)
            {
                bool forced = engine.Phase == Phase.Playing && !engine.CurrentTurn.IsNone
                    && engine.ViewFor(engine.CurrentSeat).LegalCards().Count == 1;
                if (driver.TryAct(engine, now, out _))
                {
                    Drain(engine, driver, events, now);
                    foreach (MatchEvent e in events)
                    {
                        if (e is CardPlayed played)
                        {
                            plays.Add(new Play(now, inTrick, played.IsForced ? true : forced, played.IsForced));
                            inTrick = (inTrick + 1) % 4;
                        }
                    }

                    continue;
                }

                now += Step;
            }

            // Four bots carry straight on into the next round: the first round's 52 cards are enough.
            Assert.GreaterOrEqual(plays.Count, 52, "a whole round was played");
            return plays.GetRange(0, 52);
        }

        private static void Drain(MatchEngine engine, BotDriver driver, List<MatchEvent> events, double now)
        {
            events.Clear();
            engine.DrainEvents(events);
            foreach (MatchEvent e in events)
            {
                driver.Observe(e, now);
            }
        }

        private readonly struct Play
        {
            public Play(double at, int indexInTrick, bool wasForced, bool byEngine)
            {
                At = at;
                IndexInTrick = indexInTrick;
                WasForced = wasForced;
                ByEngine = byEngine;
            }

            public double At { get; }

            public int IndexInTrick { get; }

            public bool WasForced { get; }

            public bool ByEngine { get; }
        }
    }
}
