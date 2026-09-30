using System;
using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Bots;
using Callbreak.Core.Cards;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Sessions
{
    public sealed class OfflineMatchSessionTests
    {
        private InMemorySaveStore _saves;
        private SettingsService _settings;
        private StatsService _stats;
        private OfflineMatchStore _store;
        private ProfileService _profile;
        private CosmeticsService _cosmetics;

        [SetUp]
        public void SetUp()
        {
            _saves = new InMemorySaveStore();
            _settings = new SettingsService(_saves);
            _stats = new StatsService(_saves);
            _store = new OfflineMatchStore(_saves);
            _profile = new ProfileService(_saves, new NameFilter());
            _cosmetics = new CosmeticsService(_saves);
        }

        private OfflineMatchSession NewSession(BotTiming timing, bool resume = false)
        {
            return new OfflineMatchSession(
                new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Normal, resume),
                _store, new LowestCardBots(), _profile, _cosmetics, _settings, _stats, timing);
        }

        /// <summary>Plays the local seat: call 2, lowest legal card, ready at every summary.</summary>
        private static bool ActAsPlayer(OfflineMatchSession session)
        {
            PlayerView view = session.View;
            if (view.Phase == Phase.RoundSummary && !view.IsReady(session.LocalSeat))
            {
                session.ReadyNextAsync(CancellationToken.None).Wait();
                return true;
            }

            if (!view.IsMyTurn)
            {
                return false;
            }

            if (view.Phase == Phase.Calling)
            {
                session.PlaceCallAsync(2, CancellationToken.None).Wait();
            }
            else
            {
                session.PlayCardAsync(view.LegalCards().Lowest(), CancellationToken.None).Wait();
            }

            return true;
        }

        private static void PlayToEnd(OfflineMatchSession session, ref double now, int maxSteps = 20000)
        {
            for (int step = 0; step < maxSteps && session.Status != SessionStatus.Ended; step++)
            {
                now += 0.25;
                session.Tick(now);
                ActAsPlayer(session);
            }
        }

        [Test]
        public void Start_NewMatch_LocalPlayerAtSouthWithThreeNamedBots()
        {
            OfflineMatchSession session = NewSession(BotTiming.Instant);

            session.Start();

            Assert.AreEqual(Seat.South, session.LocalSeat);
            Assert.IsFalse(session.Seats[(int)Seat.South].IsBot);
            Assert.AreEqual("Player", session.Seats[(int)Seat.South].Name);
            var names = new HashSet<string>();
            for (int s = 1; s < 4; s++)
            {
                Assert.IsTrue(session.Seats[s].IsBot);
                Assert.IsTrue(names.Add(session.Seats[s].Name), "bot names are distinct");
            }

            Assert.AreEqual("Suman", session.Seats[(int)Seat.East].Name, "the first match uses the mockup names");
            Assert.AreEqual(13, session.View.Hand.Count);
        }

        [Test]
        public void PlayToEnd_WholeMatch_OnlyVisibleEventsInOrderAndStatsOnce()
        {
            OfflineMatchSession session = NewSession(BotTiming.Instant);
            var events = new List<MatchEvent>();
            session.EventReceived += events.Add;
            double now = 0;

            session.Start();
            PlayToEnd(session, ref now);

            Assert.AreEqual(SessionStatus.Ended, session.Status);
            Assert.IsInstanceOf<MatchStarted>(events[0]);
            Assert.IsInstanceOf<MatchEnded>(events[events.Count - 1]);
            int rounds = 0;
            foreach (MatchEvent e in events)
            {
                Assert.IsTrue(e.Visibility.IsVisibleTo(Seat.South), "no other seat's private event reaches the player: " + e);
                if (e is HandDealt dealt)
                {
                    Assert.AreEqual(Seat.South, dealt.Seat);
                }

                if (e is RoundScored)
                {
                    rounds++;
                }
            }

            Assert.AreEqual(5, rounds);
            Assert.AreEqual(1, _stats.Current.gamesPlayed);
            Assert.AreEqual(1, _settings.CompletedMatches);
            Assert.IsFalse(_store.HasSave, "a finished match leaves nothing to resume");
        }

        [Test]
        public void Tick_BotTurn_WaitsForThinkTimeBeforeActing()
        {
            var timing = new BotTiming(thinkMinSeconds: 1.0, thinkMaxSeconds: 1.0, forcedSeconds: 1.0, trickDisplaySeconds: 0, dealSeconds: 0);
            OfflineMatchSession session = NewSession(timing);
            session.Start();
            double now = 0;
            session.Tick(now);
            if (session.View.IsMyTurn)
            {
                ActAsPlayer(session);
            }

            Assert.IsFalse(session.View.IsMyTurn);
            Seat waiting = session.View.CurrentSeat;
            int calls = CallsPlaced(session.View);

            session.Tick(now += 0.1);   // the bot's think time starts
            session.Tick(now += 0.8);
            Assert.AreEqual(calls, CallsPlaced(session.View), "the bot has not called yet");
            Assert.AreEqual(waiting, session.View.CurrentSeat);

            session.Tick(now += 0.3);
            Assert.AreEqual(calls + 1, CallsPlaced(session.View), "the bot called after one second");
        }

        [Test]
        public void Pause_BotTurn_NoMovesUntilResumedAndThinkTimeShifts()
        {
            var timing = new BotTiming(thinkMinSeconds: 1.0, thinkMaxSeconds: 1.0, forcedSeconds: 1.0, trickDisplaySeconds: 0, dealSeconds: 0);
            OfflineMatchSession session = NewSession(timing);
            session.Start();
            double now = 0;
            session.Tick(now);
            if (session.View.IsMyTurn)
            {
                ActAsPlayer(session);
            }

            session.Tick(now += 0.1);
            int calls = CallsPlaced(session.View);
            session.Pause();
            session.Tick(now += 30);
            Assert.AreEqual(calls, CallsPlaced(session.View), "nothing moves while paused");
            Assert.IsTrue(_store.HasSave, "pausing saves the match");

            session.Resume();
            session.Tick(now += 0.5);
            Assert.AreEqual(calls, CallsPlaced(session.View), "the rest of the think time still applies");
            session.Tick(now += 0.6);
            Assert.AreEqual(calls + 1, CallsPlaced(session.View));
        }

        [Test]
        public void Resume_AfterATrick_RestoresTheSameView()
        {
            OfflineMatchSession first = NewSession(BotTiming.Instant);
            first.Start();
            double now = 0;
            int tricks = 0;
            first.EventReceived += e =>
            {
                if (e is TrickCompleted)
                {
                    tricks++;
                }
            };
            for (int step = 0; step < 2000 && tricks == 0; step++)
            {
                first.Tick(now += 0.25);
                ActAsPlayer(first);
            }

            Assert.IsTrue(_store.HasSave, "a snapshot is saved after every trick");
            first.Pause();
            PlayerView before = first.View;

            OfflineMatchSession second = NewSession(BotTiming.Instant, resume: true);
            PlayerView restored = null;
            second.Resynced += v => restored = v;
            second.Start();

            Assert.IsTrue(second.IsResumed);
            Assert.AreEqual(before, restored);
            Assert.AreEqual(first.MatchId, second.MatchId);
            PlayToEnd(second, ref now);
            Assert.AreEqual(SessionStatus.Ended, second.Status);
        }

        [Test]
        public void Leave_MidMatch_KeepsTheMatchForLater()
        {
            OfflineMatchSession session = NewSession(BotTiming.Instant);
            session.Start();
            double now = 0;
            for (int i = 0; i < 40; i++)
            {
                session.Tick(now += 0.25);
                ActAsPlayer(session);
            }

            session.LeaveAsync(CancellationToken.None).Wait();

            Assert.IsTrue(_store.HasSave);
            Assert.AreEqual(0, _stats.Current.gamesPlayed, "leaving an offline match records nothing");
        }

        [Test]
        public void Resume_SaveThatCannotBeContinued_DealsANewMatchInstead()
        {
            // A file with no real match in it, as a JSON store reads it back.
            _saves.Put(OfflineMatchStore.Key, new OfflineMatchSave { match = new MatchSnapshot() });

            OfflineMatchSession session = NewSession(BotTiming.Instant, resume: true);
            session.Start();

            Assert.IsFalse(session.IsResumed);
            Assert.AreEqual(13, session.View.Hand.Count, "a new match was dealt");
        }

        [Test]
        public void Resume_SaveWhoseGeneratorCannotBeRebuilt_DealsANewMatchInstead()
        {
            OfflineMatchSession first = NewSession(BotTiming.Instant);
            first.Start();
            double now = 0;
            PlayUntil(first, ref now, view => view.CompletedTricks.Count >= 1);
            first.LeaveAsync(CancellationToken.None).Wait();
            OfflineMatchSave save = _saves.Peek<OfflineMatchSave>(OfflineMatchStore.Key);
            save.rngIncrement = 0UL;
            _saves.Put(OfflineMatchStore.Key, save);

            OfflineMatchSession second = NewSession(BotTiming.Instant, resume: true);
            second.Start();

            Assert.IsFalse(second.IsResumed);
            Assert.AreNotEqual(first.MatchId, second.MatchId);
        }

        [Test]
        public void Resume_Twice_DealsTheCardsAnUninterruptedMatchWouldHaveDealt()
        {
            // Left in round 1, resumed, left again in round 2, resumed again: round 3 must come
            // from the generator where round 2's deal left it, not from where round 1's did.
            double now = 0;
            OfflineMatchSession first = NewSession(BotTiming.Instant);
            first.Start();
            PlayUntil(first, ref now, view => view.Round == 1 && view.CompletedTricks.Count >= 2);
            first.LeaveAsync(CancellationToken.None).Wait();
            ulong seed = _saves.Peek<OfflineMatchSave>(OfflineMatchStore.Key).seed;

            OfflineMatchSession second = NewSession(BotTiming.Instant, resume: true);
            second.Start();
            Assert.IsTrue(second.IsResumed);
            PlayUntil(second, ref now, view => view.Round == 2 && view.CompletedTricks.Count >= 2);
            second.LeaveAsync(CancellationToken.None).Wait();

            OfflineMatchSession third = NewSession(BotTiming.Instant, resume: true);
            var dealt = new List<CardSet>();
            third.EventReceived += e =>
            {
                if (e is HandDealt hand)
                {
                    dealt.Add(hand.Hand);
                }
            };
            third.Start();
            Assert.IsTrue(third.IsResumed);
            PlayUntil(third, ref now, view => view.Round == 3);

            Assert.AreEqual(1, dealt.Count, "round 3 was dealt in the third run");
            Assert.AreEqual(HandsOfSouth(seed)[2], dealt[0]);
        }

        [Test]
        public void PlayToEnd_RecordsTheLocalSeatsRoundsCallsAndPlaceInTheWinStreak()
        {
            OfflineMatchSession session = NewSession(BotTiming.Instant);
            double now = 0;

            session.Start();
            PlayToEnd(session, ref now);

            PlayerStats stats = _stats.Current;
            int rank = 0;
            foreach (Standing standing in session.View.FinalStandings)
            {
                if (standing.Seat == Seat.South)
                {
                    rank = standing.Rank;
                }
            }

            Assert.AreEqual(session.Rules.Rounds, stats.roundsPlayed);
            Assert.AreEqual(session.View.Scores.RoundsMetBy(Seat.South), stats.roundsMet);
            Assert.AreEqual(2 * session.Rules.Rounds, stats.callsTotal, "the test player calls 2 every round");
            Assert.AreEqual(rank == 1 ? 1 : 0, stats.winStreak);
            Assert.AreEqual(rank == 1 ? 1 : 0, stats.bestWinStreak);
        }

        private static void PlayUntil(OfflineMatchSession session, ref double now, Func<PlayerView, bool> reached)
        {
            for (int step = 0; step < 20000 && !reached(session.View); step++)
            {
                now += 0.25;
                session.Tick(now);
                ActAsPlayer(session);
            }

            Assert.IsTrue(reached(session.View), "the match got that far");
        }

        /// <summary>
        /// The hands South is dealt, round by round, in a match with this seed that nobody
        /// interrupts. Every seat plays its lowest legal card, as the test player and bots do;
        /// calls change no card.
        /// </summary>
        private static List<CardSet> HandsOfSouth(ulong seed)
        {
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(seed), TestSetups.With(false, true, true, true));
            var driver = new MatchDriver(engine, randomPolicy: false);
            driver.Start();
            driver.PlayToEnd();
            var hands = new List<CardSet>();
            foreach (MatchEvent e in driver.Events)
            {
                if (e is HandDealt hand && hand.Seat == Seat.South)
                {
                    hands.Add(hand.Hand);
                }
            }

            return hands;
        }

        private static int CallsPlaced(PlayerView view)
        {
            int n = 0;
            for (int s = 0; s < 4; s++)
            {
                if (view.HasCalled((Seat)s))
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>Test bots: call 2, play the lowest legal card.</summary>
        private sealed class LowestCardBots : IBotFactory, IBotStrategy
        {
            public BotLevel Level => BotLevel.Normal;

            public IBotStrategy Create(BotLevel level) => this;

            public int ChooseCall(PlayerView view, IRandom rng) => 2;

            public Card ChooseCard(PlayerView view, IRandom rng) => view.LegalCards().Lowest();
        }
    }
}
