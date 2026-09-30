using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Match;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    public sealed class RoomServiceTests
    {
        private InMemoryNetwork _network;
        private double _now;
        private List<Device> _devices;

        [SetUp]
        public void SetUp()
        {
            _network = new InMemoryNetwork();
            _now = 0;
            _devices = new List<Device>();
        }

        [Test]
        public void Create_Online_HostIsSeatedFirstWithAValidCode()
        {
            Device host = NewDevice("Hari");

            RoomResult result = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 30), CancellationToken.None).Result;

            Assert.IsTrue(result.Succeeded);
            Assert.AreSame(result.Session, host.Rooms.Current);
            Assert.IsTrue(RoomCode.IsValid(result.Session.Code));
            Assert.IsTrue(result.Session.IsHost);
            Assert.AreEqual(0, result.Session.LocalSeatIndex);
            Assert.AreEqual("Hari", result.Session.Seats[0].Name);
        }

        [Test]
        public void Create_NoInternet_IsOffline()
        {
            Device host = NewDevice("Hari");
            host.Network.IsOnline = false;

            RoomResult result = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 30), CancellationToken.None).Result;

            Assert.AreEqual(RoomError.Offline, result.Error);
            Assert.IsNull(host.Rooms.Current);
        }

        [Test]
        public void Create_RelayUnreachable_ReportsTheError()
        {
            Device host = NewDevice("Hari");
            _network.HostError = RoomError.Unknown;

            RoomResult result = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 30), CancellationToken.None).Result;

            Assert.AreEqual(RoomError.Unknown, result.Error);
        }

        [Test]
        public void Join_ByCodeTypedLoosely_SeatsTheGuest()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            string code = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 30), CancellationToken.None).Result.Session.Code;
            string typed = code.Substring(0, 3).ToLowerInvariant() + "-" + code.Substring(3) + " ";

            Task<RoomResult> joining = guest.Rooms.JoinRoomAsync(typed, CancellationToken.None);
            Settle();

            Assert.IsTrue(joining.IsCompleted);
            Assert.IsTrue(joining.Result.Succeeded);
            Assert.AreEqual(1, joining.Result.Session.LocalSeatIndex);
            Assert.AreEqual("Asha", host.Rooms.Current.Seats[1].Name);
        }

        [Test]
        public void Join_UnknownCode_IsNotFound()
        {
            Device guest = NewDevice("Asha");

            RoomResult result = guest.Rooms.JoinRoomAsync("ABCDEF", CancellationToken.None).Result;

            Assert.AreEqual(RoomError.NotFound, result.Error);
        }

        [Test]
        public void Join_InvalidCode_IsNotFoundWithoutTouchingTheNetwork()
        {
            Device guest = NewDevice("Asha");

            RoomResult result = guest.Rooms.JoinRoomAsync("AB", CancellationToken.None).Result;

            Assert.AreEqual(RoomError.NotFound, result.Error);
        }

        [Test]
        public void Join_TooManyAttempts_IsRateLimitedForAMinute()
        {
            Device guest = NewDevice("Asha");
            for (int i = 0; i < RoomService.JoinAttemptsPerMinute; i++)
            {
                Assert.AreEqual(RoomError.NotFound, guest.Rooms.JoinRoomAsync("ABCDEF", CancellationToken.None).Result.Error);
            }

            Assert.AreEqual(RoomError.RateLimited, guest.Rooms.JoinRoomAsync("ABCDEF", CancellationToken.None).Result.Error);

            Settle(61);
            Assert.AreEqual(RoomError.NotFound, guest.Rooms.JoinRoomAsync("ABCDEF", CancellationToken.None).Result.Error);
        }

        [Test]
        public void HostLeaves_Lobby_GuestIsToldAndBothHaveNoRoom()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            RoomError closed = RoomError.None;
            guest.Rooms.Current.Closed += reason => closed = reason;

            host.Rooms.Current.LeaveAsync(CancellationToken.None);
            Settle(2);

            Assert.AreEqual(RoomError.HostLeft, closed);
            Assert.IsNull(host.Rooms.Current);
            Assert.IsNull(guest.Rooms.Current);
        }

        [Test]
        public void Match_GuestDropsAndTheServiceReconnects_TheSameSessionContinues()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);
            IMatchSession guestSession = guest.Rooms.Current.Match;
            IClientLink guestLink = LinkOf(guest);

            _network.Drop(guestLink);
            Settle(0.5);
            Assert.AreEqual(SessionStatus.Reconnecting, guestSession.Status);

            for (int i = 0; i < 40 && guestSession.Status != SessionStatus.Running; i++)
            {
                Settle(0.5);
            }

            Assert.AreEqual(SessionStatus.Running, guestSession.Status, "the service rejoined by itself");
            Assert.AreSame(guestSession, guest.Rooms.Current.Match, "the table keeps its session");
            PlayToEnd(host, guest);
            Assert.AreEqual(SessionStatus.Ended, guestSession.Status);
        }

        [Test]
        public void Match_HostGoneForGood_GuestGivesUpAfterTheSeatHold()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);
            IMatchSession guestSession = guest.Rooms.Current.Match;

            // The host's process dies: no goodbye, the relay just loses the room.
            _network.Drop(LinkOf(guest));
            host.Rooms.Dispose();
            _devices.Remove(host);
            Settle(1.5);
            for (int i = 0; i < 120 && guestSession.Status != SessionStatus.Failed; i++)
            {
                Settle(1.0);
            }

            Assert.AreEqual(SessionStatus.Failed, guestSession.Status);
            Assert.IsNull(guest.Rooms.Current);
        }

        [Test]
        public void QuickMatch_NobodyElse_FillsWithBotsAtTheDelayAndStarts()
        {
            Device device = NewDevice("Hari");
            var reports = new List<MatchmakingProgress>();
            var config = new QuickMatchConfig(botFillDelaySeconds: 4, seatRevealSeconds: 0.5, tableFoundSeconds: 1.5);
            var quick = new QuickMatchService(config, device.Profile, device.Cosmetics, device.Auth, new TestBots(), device.Stats, device.Settings,
                device.Network, new NameFilter(), () => 77UL, BotTiming.Instant);

            Task<MatchmakingResult> search = quick.FindMatchAsync(new Collector(reports), CancellationToken.None);
            while (_now < 3.75)
            {
                _now += 0.25;
                quick.Tick(_now);
            }

            Assert.IsFalse(search.IsCompleted);
            Assert.IsNull(quick.Match, "no table before the bot-fill delay");
            MatchmakingProgress waiting = reports[reports.Count - 1];
            Assert.IsTrue(waiting.Seats[0].Occupied);
            Assert.IsFalse(waiting.Seats[1].Occupied);
            Assert.That(waiting.ElapsedSeconds, Is.InRange(3.0, 4.0));

            for (int i = 0; i < 40 && !search.IsCompleted; i++)
            {
                _now += 0.25;
                quick.Tick(_now);
            }

            Assert.IsTrue(search.IsCompleted);
            Assert.IsTrue(search.Result.Found);
            MatchmakingProgress last = reports[reports.Count - 1];
            Assert.IsTrue(last.TableFound);
            for (int s = 1; s < 4; s++)
            {
                Assert.IsTrue(last.Seats[s].Occupied);
                Assert.IsTrue(last.Seats[s].IsBot, "filled seats show as bots");
            }

            IMatchSession session = quick.Match;
            Assert.AreEqual(MatchMode.QuickMatch, session.Mode);
            session.Start();
            for (int i = 0; i < 20000 && session.Status != SessionStatus.Ended; i++)
            {
                _now += 0.25;
                quick.Tick(_now);
                RoomFixture.Act(session);
            }

            Assert.AreEqual(SessionStatus.Ended, session.Status);
            Assert.AreEqual(1, device.Stats.Current.gamesPlayed);
            quick.Dispose();
        }

        [Test]
        public void QuickMatch_Cancelled_WithdrawsAtOnce()
        {
            Device device = NewDevice("Hari");
            var quick = new QuickMatchService(new QuickMatchConfig(botFillDelaySeconds: 4), device.Profile, device.Cosmetics, device.Auth, new TestBots(),
                device.Stats, device.Settings, device.Network, new NameFilter(), () => 77UL, BotTiming.Instant);
            var cancel = new CancellationTokenSource();

            Task<MatchmakingResult> search = quick.FindMatchAsync(null, cancel.Token);
            quick.Tick(1.0);
            cancel.Cancel();

            Assert.IsTrue(search.IsCompleted);
            Assert.AreEqual(MatchmakingError.Cancelled, search.Result.Error);
            Assert.IsNull(quick.Match);
            quick.Tick(10.0);
            Assert.IsNull(quick.Match, "nothing keeps running after a cancel");
        }

        [Test]
        public void QuickMatch_NoInternet_IsOffline()
        {
            Device device = NewDevice("Hari");
            device.Network.IsOnline = false;
            var quick = new QuickMatchService(null, device.Profile, device.Cosmetics, device.Auth, new TestBots(), device.Stats, device.Settings,
                device.Network, new NameFilter(), () => 77UL);

            Assert.AreEqual(MatchmakingError.Offline, quick.FindMatchAsync(null, CancellationToken.None).Result.Error);
        }

        private Device NewDevice(string name)
        {
            var device = new Device(_network, name, (ulong)(100 + _devices.Count));
            _devices.Add(device);
            return device;
        }

        private void Settle(double seconds = 0.25)
        {
            double until = _now + seconds;
            do
            {
                _now = Math.Min(until, _now + 0.25);
                foreach (Device d in _devices)
                {
                    d.Rooms.Tick(_now);
                }

                _network.Pump();
            }
            while (_now < until);
        }

        private void JoinRoom(Device host, Device guest)
        {
            string code = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 0), CancellationToken.None).Result.Session.Code;
            Task<RoomResult> joining = guest.Rooms.JoinRoomAsync(code, CancellationToken.None);
            Settle();
            Assert.IsTrue(joining.Result.Succeeded);
        }

        private void StartMatch(Device host, Device guest)
        {
            guest.Rooms.Current.SetReadyAsync(true, CancellationToken.None);
            Settle();
            Assert.IsTrue(host.Rooms.Current.StartAsync(true, CancellationToken.None).Result);
            Settle();
            host.Rooms.Current.Match.Start();
            guest.Rooms.Current.Match.Start();
            Settle();
        }

        private void PlayToEnd(Device host, Device guest)
        {
            IMatchSession a = host.Rooms.Current.Match;
            IMatchSession b = guest.Rooms.Current.Match;
            for (int i = 0; i < 20000 && a.Status != SessionStatus.Ended; i++)
            {
                Settle();
                RoomFixture.Act(a);
                RoomFixture.Act(b);
                _network.Pump();
            }
        }

        private static IClientLink LinkOf(Device device) => device.Rooms.ClientLink;

        private sealed class Device
        {
            public Device(InMemoryNetwork network, string name, ulong seed)
            {
                var saves = new InMemorySaveStore();
                Settings = new SettingsService(saves);
                Profile = new ProfileService(saves, new NameFilter());
                Profile.TrySetDisplayName(name);
                Stats = new StatsService(saves);
                Cosmetics = new CosmeticsService(saves);
                Network = new FakeNetworkStatus();
                Auth = new FakeAuth("player-" + name.ToLowerInvariant());
                ulong next = seed;
                Rooms = new RoomService(network, Profile, Cosmetics, Auth, new TestBots(), Stats, Settings, Network, new NameFilter(),
                    () => next++ * 7919UL, BotTiming.Instant);
            }

            public SettingsService Settings { get; }

            public ProfileService Profile { get; }

            public StatsService Stats { get; }

            public CosmeticsService Cosmetics { get; }

            public FakeNetworkStatus Network { get; }

            public FakeAuth Auth { get; }

            public RoomService Rooms { get; }
        }

        private sealed class FakeNetworkStatus : INetworkStatus
        {
            public event Action<bool> Changed
            {
                add { }
                remove { }
            }

            public bool IsOnline { get; set; } = true;
        }

        private sealed class FakeAuth : IAuthService
        {
            public FakeAuth(string playerId)
            {
                PlayerId = playerId;
            }

            public bool IsSignedIn => true;

            public string PlayerId { get; }

            public Task<bool> SignInAsync(CancellationToken ct) => Task.FromResult(true);
        }

        private sealed class Collector : IProgress<MatchmakingProgress>
        {
            private readonly List<MatchmakingProgress> _into;

            public Collector(List<MatchmakingProgress> into)
            {
                _into = into;
            }

            public void Report(MatchmakingProgress value) => _into.Add(value);
        }
    }
}
