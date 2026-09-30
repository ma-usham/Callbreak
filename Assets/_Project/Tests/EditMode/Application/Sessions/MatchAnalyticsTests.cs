using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Match;
using Callbreak.Tests.EditMode.Application.Online;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Sessions
{
    /// <summary>GDD §15 "Analytics events": what a match reports, and that it is only ever the local seat's numbers.</summary>
    public sealed class MatchAnalyticsTests
    {
        private InMemorySaveStore _saves;
        private Recorder _events;
        private ManualClock _clock;

        [SetUp]
        public void SetUp()
        {
            _saves = new InMemorySaveStore();
            _events = new Recorder();
            _clock = new ManualClock();
        }

        [Test]
        public void OfflineMatch_PlayedToTheEnd_TracksTheStartEveryRoundAndTheEnd()
        {
            OfflineMatchSession session = NewOffline(BotDifficulty.Hard);
            var analytics = new MatchAnalytics(session, new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Hard), _events, _clock);
            analytics.Start();
            session.Start();

            PlayToEnd(session);

            Assert.AreEqual("match_start", _events.Names[0]);
            Assert.AreEqual("offline", _events.Of("match_start")[0]["mode"]);
            Assert.AreEqual("hard", _events.Of("match_start")[0]["difficulty"]);
            Assert.AreEqual(0, _events.Of("bot_fill").Count, "offline opponents are bots by design");

            List<Dictionary<string, object>> rounds = _events.Of("round_end");
            Assert.AreEqual(session.Rules.Rounds, rounds.Count);
            for (int i = 0; i < rounds.Count; i++)
            {
                Assert.AreEqual(i + 1, rounds[i]["round"]);
                Assert.AreEqual(session.View.Scores.Rounds[i][Seat.South].Call, rounds[i]["call"]);
                Assert.AreEqual(session.View.Scores.Rounds[i][Seat.South].Won, rounds[i]["won"]);
            }

            Dictionary<string, object> end = _events.Of("match_end").Single();
            Assert.AreEqual(session.View.FinalStandings.First(s => s.Seat == Seat.South).Rank, end["rank"]);
            Assert.AreEqual(false, end["abandoned"]);
            Assert.AreEqual((int)_clock.Now, end["duration"]);
            Assert.AreEqual("match_end", _events.Names[_events.Names.Count - 1]);
        }

        [Test]
        public void OfflineMatch_LeftForLaterAndResumed_IsOneMatchWithOneStartAndOneEnd()
        {
            OfflineMatchSession first = NewOffline(BotDifficulty.Normal);
            var request = new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Normal);
            var analytics = new MatchAnalytics(first, request, _events, _clock);
            analytics.Start();
            first.Start();
            for (int i = 0; i < 40; i++)
            {
                Step(first);
            }

            first.LeaveAsync(CancellationToken.None);
            analytics.Dispose();
            Assert.AreEqual(0, _events.Of("match_end").Count, "the match is kept for later, not ended");

            OfflineMatchSession second = NewOffline(BotDifficulty.Normal, resume: true);
            var resumed = new MatchAnalytics(second, new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Normal, resumeSaved: true), _events, _clock);
            resumed.Start();
            second.Start();
            PlayToEnd(second);

            Assert.AreEqual(1, _events.Of("match_start").Count);
            Assert.AreEqual(1, _events.Of("match_end").Count);
        }

        [Test]
        public void OnlineMatch_EmptySeatsFilledWithBots_TracksTheBotFill()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            MatchAnalytics analytics = StartWithAnalytics(room, guest);

            Assert.AreEqual("private_room", _events.Of("match_start").Single()["mode"]);
            Assert.AreEqual("none", _events.Of("match_start").Single()["difficulty"]);
            Assert.AreEqual(2, _events.Of("bot_fill").Single()["seats"]);
            analytics.Dispose();
        }

        [Test]
        public void OnlineMatch_PlayerLeaves_TracksAnAbandonedEnd()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            MatchAnalytics analytics = StartWithAnalytics(room, guest);
            _clock.Now = 42.6;

            guest.Session.LeaveAsync(CancellationToken.None);

            Dictionary<string, object> end = _events.Of("match_end").Single();
            Assert.AreEqual(SeatExtensions.Count, end["rank"]);
            Assert.AreEqual(true, end["abandoned"]);
            Assert.AreEqual(42, end["duration"]);
            analytics.Dispose();
            Assert.AreEqual(1, _events.Of("match_end").Count, "reported once");
        }

        [Test]
        public void OnlineMatch_ConnectionDropsAndComesBack_TracksTheReconnect()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            MatchAnalytics analytics = StartWithAnalytics(room, guest);

            _clock.Now = 10;
            room.Network.Drop(guest.Link);
            room.Network.Pump();
            _clock.Now = 17.2;
            Assert.AreEqual(RoomError.None, room.Rejoin(guest));
            room.Step();

            Dictionary<string, object> reconnect = _events.Of("reconnect").Single();
            Assert.AreEqual(true, reconnect["ok"]);
            Assert.AreEqual(7, reconnect["secs"]);
            analytics.Dispose();
        }

        [Test]
        public void OnlineMatch_RoomLostForGood_TracksTheFailedReconnect()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            MatchAnalytics analytics = StartWithAnalytics(room, guest);

            _clock.Now = 10;
            room.Network.Drop(guest.Link);
            room.Network.Pump();
            _clock.Now = 100;
            guest.Client.Close(RoomError.ConnectionLost);

            Dictionary<string, object> reconnect = _events.Of("reconnect").Single();
            Assert.AreEqual(false, reconnect["ok"]);
            Assert.AreEqual(90, reconnect["secs"]);
            Assert.AreEqual(0, _events.Of("match_end").Count, "a lost connection is not an abandoned match");
            analytics.Dispose();
        }

        [Test]
        public void OnlineMatch_TheClockPlaysForThePlayer_TracksAutoPlay()
        {
            var room = new RoomFixture(turnSeconds: 15);
            RoomFixture.Member guest = room.Join("p1", "Asha");
            MatchAnalytics analytics = StartWithAnalytics(room, guest);

            // Nobody at the guest's device acts; the host's player keeps up. Every turn of the guest times out.
            for (int i = 0; i < 400 && _events.Of("auto_play").Count < 2; i++)
            {
                room.Step(1.0);
                RoomFixture.Act(room.HostMember.Session);
                room.Network.Pump();
            }

            List<Dictionary<string, object>> autos = _events.Of("auto_play");
            Assert.GreaterOrEqual(autos.Count, 2);
            Assert.AreEqual("call", autos[0]["kind"]);
            Assert.AreEqual("card", autos[1]["kind"]);
            analytics.Dispose();
        }

        [Test]
        public void Events_NeverCarryNamesOrCards()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            MatchAnalytics analytics = StartWithAnalytics(room, guest);
            room.PlayToEnd();

            foreach (Dictionary<string, object> parameters in _events.All)
            {
                foreach (object value in parameters.Values)
                {
                    Assert.IsTrue(value is int || value is bool || value is string, "only numbers, flags and fixed words");
                    if (value is string text)
                    {
                        CollectionAssert.Contains(new[] { "private_room", "none" }, text);
                    }
                }
            }

            analytics.Dispose();
        }

        private MatchAnalytics StartWithAnalytics(RoomFixture room, RoomFixture.Member guest)
        {
            foreach (RoomFixture.Member m in room.Members)
            {
                if (m != room.HostMember)
                {
                    m.Client.SetReadyAsync(true, CancellationToken.None);
                }
            }

            room.Network.Pump();
            Assert.IsTrue(room.HostMember.Client.StartAsync(true, CancellationToken.None).Result);
            room.Network.Pump();
            var analytics = new MatchAnalytics(guest.Session, new MatchLaunchRequest(MatchMode.PrivateRoom, BotDifficulty.Hard), _events, _clock);
            analytics.Start();
            room.HostMember.Session.Start();
            guest.Session.Start();
            room.Network.Pump();
            room.Step();
            return analytics;
        }

        private OfflineMatchSession NewOffline(BotDifficulty difficulty, bool resume = false)
        {
            return new OfflineMatchSession(
                new MatchLaunchRequest(MatchMode.Offline, difficulty, resume),
                new OfflineMatchStore(_saves),
                new TestBots(),
                new ProfileService(_saves, new NameFilter()),
                new CosmeticsService(_saves),
                new SettingsService(_saves),
                new StatsService(_saves),
                BotTiming.Instant);
        }

        private void PlayToEnd(OfflineMatchSession session)
        {
            for (int step = 0; step < 20000 && session.Status != SessionStatus.Ended; step++)
            {
                Step(session);
            }
        }

        private void Step(OfflineMatchSession session)
        {
            _clock.Now += 0.25;
            session.Tick(_clock.Now);
            RoomFixture.Act(session);
        }

        private sealed class ManualClock : IClock
        {
            public double Now { get; set; }
        }

        private sealed class Recorder : IAnalytics
        {
            public List<string> Names { get; } = new List<string>();

            public List<Dictionary<string, object>> All { get; } = new List<Dictionary<string, object>>();

            public void Track(string eventName, params (string key, object value)[] parameters)
            {
                Names.Add(eventName);
                var map = new Dictionary<string, object>();
                foreach ((string key, object value) in parameters)
                {
                    map.Add(key, value);
                }

                All.Add(map);
            }

            public List<Dictionary<string, object>> Of(string eventName)
            {
                var found = new List<Dictionary<string, object>>();
                for (int i = 0; i < Names.Count; i++)
                {
                    if (Names[i] == eventName)
                    {
                        found.Add(All[i]);
                    }
                }

                return found;
            }
        }
    }
}
