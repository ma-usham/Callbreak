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
    public sealed partial class RoomServiceTests
    {
        private static readonly DateTime Epoch = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

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

        // GDD §11 screen 02: the name is "3-20 characters, trimmed, and profanity-filtered (checked
        // again on the server)". In this phase the host is the server.

        [TestCase("ab")]
        [TestCase("  ab  ")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("Twenty-one characters")]
        [TestCase("Shit Table")]
        public void Create_NameTheScreenShouldNeverHaveSent_IsRefusedBeforeTheNetworkIsTouched(string name)
        {
            Device host = NewDevice("Hari");

            RoomResult result = host.Rooms.CreateRoomAsync(new RoomSettings(name, 30), CancellationToken.None).Result;

            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(RoomError.Unknown, result.Error, "a bad name has no error of its own");
            Assert.IsNull(host.Rooms.Current);
            Assert.AreEqual(0, _network.HostAttempts, "the relay was never asked for a room");
            CollectionAssert.IsEmpty(host.Tracked.Names, "no room was created");
        }

        [TestCase("abc")]
        [TestCase("Twenty characters ok")]
        public void Create_NameAtTheLimits_OpensTheRoom(string name)
        {
            Device host = NewDevice("Hari");

            RoomResult result = host.Rooms.CreateRoomAsync(new RoomSettings(name, 30), CancellationToken.None).Result;

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(name, result.Session.Settings.Name);
            Assert.AreEqual(1, _network.HostAttempts);
        }

        [Test]
        public void Create_NameWithSpacesAround_OpensTheRoomUnderTheTrimmedName()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");

            RoomResult result = host.Rooms.CreateRoomAsync(new RoomSettings("  Hari's   Table ", 15), CancellationToken.None).Result;
            Task<RoomResult> joining = guest.Rooms.JoinRoomAsync(result.Session.Code, CancellationToken.None);
            Settle();

            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("Hari's Table", result.Session.Settings.Name);
            Assert.AreEqual(15, result.Session.Settings.TurnSeconds, "the rest of the settings are kept");
            Assert.AreEqual("Hari's Table", joining.Result.Session.Settings.Name, "guests see the trimmed name");
        }

        [Test]
        public void Create_RefusedName_LeavesTheRoomThisDeviceIsInAlone()
        {
            Device host = NewDevice("Hari");
            IRoomSession first = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 30), CancellationToken.None).Result.Session;

            RoomResult second = host.Rooms.CreateRoomAsync(new RoomSettings("ab", 30), CancellationToken.None).Result;

            Assert.AreEqual(RoomError.Unknown, second.Error);
            Assert.AreSame(first, host.Rooms.Current, "a refused room does not cost the open one");
        }

        [Test]
        public void Create_PrivateRoom_RunsBothTurnClocksOnTheHostsChoice()
        {
            Device host = NewDevice("Hari");
            IRoomSession room = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 15), CancellationToken.None).Result.Session;
            Assert.IsTrue(room.StartAsync(true, CancellationToken.None).Result);
            Settle();
            IMatchSession session = room.Match;
            session.Start();
            Settle();

            double call = DurationOnTurn(session, Phase.Calling);
            double play = DurationOnTurn(session, Phase.Playing);

            Assert.AreEqual(15.0, call, 0.001, "GDD §6: a private room's call timer is the host's choice");
            Assert.AreEqual(15.0, play, 0.001, "and so is its card timer");
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
            CollectionAssert.IsEmpty(guest.Tracked.Names, "a failed join is not reported as joined");
        }

        [Test]
        public void CreateAndJoin_Succeed_EachDeviceReportsItsOwn()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");

            JoinRoom(host, guest);

            CollectionAssert.AreEqual(new[] { "room_created" }, host.Tracked.Names);
            CollectionAssert.AreEqual(new[] { "room_joined" }, guest.Tracked.Names);
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
        public void Match_Guest_HoldsARejoinTicketUntilTheMatchEnds()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            Settle();
            Assert.IsFalse(guest.Tickets.TryGet(out _), "no ticket in the lobby");

            StartMatch(host, guest);

            Assert.IsTrue(guest.Tickets.TryGet(out string code));
            Assert.AreEqual(host.Rooms.Current.Code, code);
            Assert.IsFalse(host.Tickets.TryGet(out _), "the host's room dies with its app: nothing to rejoin");

            PlayToEnd(host, guest);
            Settle();
            Assert.IsFalse(guest.Tickets.TryGet(out _), "the match is over");
        }

        [Test]
        public void Match_GuestSitsLongerThanTenMinutes_TheTicketStaysFresh()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);

            Settle(RejoinStore.LifetimeSeconds + 30);

            Assert.IsTrue(guest.Tickets.TryGet(out _));
        }

        [Test]
        public void Match_GuestLeavesOnPurpose_TheTicketIsGone()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);
            Assert.IsTrue(guest.Tickets.TryGet(out _));

            guest.Rooms.Current.LeaveAsync(CancellationToken.None);
            Settle();

            Assert.IsFalse(guest.Tickets.TryGet(out _));
        }

        [Test]
        public void AppKilled_GuestComesBackWithItsTicket_TakesTheSeatBackInTheRunningMatch()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);
            Seat seat = guest.Rooms.Current.Match.LocalSeat;
            Play(host, guest, 20);
            Kill(guest);
            Settle(5);
            Assert.AreEqual(SeatConnection.Reconnecting, host.Rooms.Current.Match.Seats[(int)seat].Connection);

            Device back = NewDevice("Asha", guest.Saves);
            Assert.IsTrue(back.Tickets.TryGet(out string code));
            Task<RoomResult> rejoining = back.Rooms.JoinRoomAsync(code, CancellationToken.None);
            Settle();

            Assert.IsTrue(rejoining.Result.Succeeded);
            IMatchSession session = back.Rooms.Current.Match;
            Assert.IsNotNull(session, "the running match came with the seat");
            Assert.AreEqual(seat, session.LocalSeat);
            session.Start();
            Settle();
            Assert.AreEqual(SessionStatus.Running, session.Status);
            Assert.AreEqual(SeatConnection.Connected, host.Rooms.Current.Match.Seats[(int)seat].Connection);
            PlayToEnd(host, back);
            Assert.AreEqual(SessionStatus.Ended, session.Status);
        }

        [Test]
        public void AppKilled_BackAfterTheSeatHold_IsRefusedAndTheBotKeepsTheSeat()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);
            Seat seat = guest.Rooms.Current.Match.LocalSeat;
            Kill(guest);
            Settle(Protocol.SeatHoldSeconds + 2);

            Device back = NewDevice("Asha", guest.Saves);
            Assert.IsTrue(back.Tickets.TryGet(out string code), "still inside the ten minutes");
            Task<RoomResult> rejoining = back.Rooms.JoinRoomAsync(code, CancellationToken.None);
            Settle();

            Assert.AreEqual(RoomError.AlreadyStarted, rejoining.Result.Error);
            Assert.IsNull(back.Rooms.Current);
            Assert.AreEqual(SeatConnection.BotControlled, host.Rooms.Current.Match.Seats[(int)seat].Connection);
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

        /// <param name="saves">The files of an earlier run of the same app, for a device that starts again.</param>
        private Device NewDevice(string name, InMemorySaveStore saves = null)
        {
            var device = new Device(_network, name, (ulong)(100 + _devices.Count), saves ?? new InMemorySaveStore(), () => Epoch.AddSeconds(_now));
            _devices.Add(device);
            return device;
        }

        /// <summary>The device's process dies: no goodbye, and nothing of it keeps running.</summary>
        private void Kill(Device device)
        {
            _network.Drop(LinkOf(device));
            _devices.Remove(device);
        }

        private void Settle(double seconds = 0.25)
        {
            double until = _now + seconds;
            do
            {
                _now = Math.Min(until, _now + 0.25);
                foreach (Device d in _devices)
                {
                    // A device whose app is paused runs nothing.
                    if (!(_peerOf.TryGetValue(d, out InMemoryPeerNetwork.Device net) && net.IsPaused))
                    {
                        d.Rooms.Tick(_now);
                    }
                }

                Pump();
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

        private void PlayToEnd(Device host, Device guest) => Play(host, guest, 20000);

        private void Play(Device host, Device guest, int steps)
        {
            IMatchSession a = host.Rooms.Current.Match;
            IMatchSession b = guest.Rooms.Current.Match;
            for (int i = 0; i < steps && a.Status != SessionStatus.Ended; i++)
            {
                Settle();
                RoomFixture.Act(a);
                RoomFixture.Act(b);
                Pump();
            }
        }

        /// <summary>Plays on until the session's seat holds a turn of that kind; gives the length of the clock on it.</summary>
        private double DurationOnTurn(IMatchSession session, Phase phase)
        {
            for (int i = 0; i < 4000; i++)
            {
                if (session.View.IsMyTurn && session.View.Phase == phase)
                {
                    Assert.IsTrue(session.Timer.IsRunning, "a clock runs on a person's turn");
                    return session.Timer.Duration;
                }

                RoomFixture.Act(session);
                Settle();
            }

            Assert.Fail("the player never held that kind of turn");
            return 0.0;
        }

        private static IClientLink LinkOf(Device device) => device.Rooms.ClientLink;

        private sealed class Device
        {
            /// <param name="network">The in-memory relay, or a transport of the device's own that is ticked (<see cref="Transport"/>).</param>
            public Device(IRoomTransport network, string name, ulong seed, InMemorySaveStore saves, Func<DateTime> utcNow)
            {
                Transport = network as ITickedRoomTransport;
                Saves = saves;
                Stats = new StatsService(saves);
                Tickets = new RejoinStore(saves, utcNow, Stats);
                Settings = new SettingsService(saves);
                Profile = new ProfileService(saves, new NameFilter());
                Profile.TrySetDisplayName(name);

                // Every deck but the first costs coins since 6 Oct 2026: a new device has bought them
                // all, so the tests can choose any (a device back from an app kill keeps its file).
                if (!saves.TryLoad(CosmeticsService.SaveKey, out CosmeticsData _))
                {
                    var everyDeck = new CosmeticsData();
                    everyDeck.boughtDeckIds.Add(CosmeticsService.HimalayanIvory);
                    everyDeck.boughtDeckIds.Add(CosmeticsService.KathmanduGold);
                    saves.Put(CosmeticsService.SaveKey, everyDeck);
                }

                Cosmetics = new CosmeticsService(saves);
                Network = new FakeNetworkStatus();
                Auth = new FakeAuth("player-" + name.ToLowerInvariant());
                ulong next = seed;
                Rooms = new RoomService(network, Profile, Cosmetics, Auth, new TestBots(), Stats, Settings, Network, new NameFilter(),
                    () => next++ * 7919UL, BotTiming.Instant, Tickets, Tracked);
            }

            public EventNames Tracked { get; } = new EventNames();

            /// <summary>The device's own transport when it needs ticking (the peer-to-peer one); null on the relay.</summary>
            public ITickedRoomTransport Transport { get; }

            public InMemorySaveStore Saves { get; }

            public RejoinStore Tickets { get; }

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

        private sealed class EventNames : IAnalytics
        {
            public List<string> Names { get; } = new List<string>();

            public void Track(string eventName, params (string key, object value)[] parameters) => Names.Add(eventName);
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
