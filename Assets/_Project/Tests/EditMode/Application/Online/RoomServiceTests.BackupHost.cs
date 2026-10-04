using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// The owner's backup host (3 Oct 2026), as whole matches between devices over the in-memory peer
    /// network. When the host and the next host in line are lost together, the next person after them
    /// takes the match over once its turn comes, <see cref="Protocol.BackupTakeOverSeconds"/> after it
    /// took the host as lost: the round in play is dealt again, the rounds before keep their scores,
    /// bots play the missing seats, and whoever dropped can come back to their own seat within the seat
    /// hold. A late next host in line, an old host coming back and a backup whose own connection
    /// dropped all end at one table; while the next host in line is there, no backup ever hosts.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [Test]
        public void BackupHost_PrivateRoom_HostAndNextInLineLostTogether_TheThirdTakesOverAndDealsTheRoundAgain()
        {
            UsePeers();
            Device host = NewPeerDevice("Hari");
            Device asha = NewPeerDevice("Asha");
            Device suman = NewPeerDevice("Suman");
            string code = OpenPeerRoom(host, asha, suman);
            StartPeerMatch(host, asha, suman);
            PlayPeersUntil(() => MidTrick(host.Rooms.Host, 2), host, asha, suman);
            RoomHost table = host.Rooms.Host;
            MatchSnapshot before = table.Table.Snapshot();
            Assert.AreEqual(2, table.NextHost.Line.Count, "both other people are in the line");
            Device next = DeviceAt(table.NextHost.Line[0].Seat, asha, suman);
            Device backup = DeviceAt(table.NextHost.Line[1].Seat, asha, suman);
            int hostSeat = table.HostSeat;
            int nextSeat = next.Rooms.Current.LocalSeatIndex;
            List<Redealt> told = Redeals(backup.Rooms.Current.Match);

            double lostAt = _now;
            Unplug(host);
            Unplug(next);
            SettleUntil(() => backup.Rooms.Host != null, 30);
            double tookOverAfter = _now - lostAt;
            MatchSnapshot after = backup.Rooms.Host.Table.Snapshot();
            SettleUntil(() => Running(backup), 5);
            TestContext.Out.WriteLine("The backup hosted " + Seconds(tookOverAfter) + " after the loss and played again after " + Seconds(_now - lostAt) + ".");

            AssertDealtAgain(before, after, backup.Rooms.Host, tookOverAfter, Protocol.HostLostSeconds + Protocol.BackupTakeOverSeconds + 2.0);
            Assert.AreEqual(1, told.FindAll(r => r.Reason == RedealReason.HostLost && r.Round == 2).Count, "its screen says the round is dealt again, once");
            Assert.IsTrue(backup.Rooms.Current.IsHost, "the room is the backup's now");
            Assert.AreEqual(SeatConnection.Reconnecting, backup.Rooms.Current.Match.Seats[hostSeat].Connection, "the lost host's seat is held for it");
            Assert.AreEqual(SeatConnection.Reconnecting, backup.Rooms.Current.Match.Seats[nextSeat].Connection, "and the next in line's");
            Settle(PeerRoomTransport.RelistSeconds + 1);
            Assert.AreEqual(_peerOf[backup].Address, _peers.ListedBy(code), "the code leads to the backup");
            Assert.AreEqual(2, _peers.ListedGeneration(code));

            PlayPeers(20000, backup);
            Assert.AreEqual(SessionStatus.Ended, backup.Rooms.Current.Match.Status, "played to the end with bots in the missing seats");
            Assert.AreEqual(1, backup.Stats.Current.gamesPlayed);
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void BackupHost_PrivateRoom_TheNextInLineCannotOpenTheRoomAtFirst_ItTriesAgainAWaitLaterFromTheSameTrick()
        {
            UsePeers();
            Device host = NewPeerDevice("Hari");
            Device asha = NewSwitchedPeerDevice("Asha", out TakeOverSwitch takeOvers);
            OpenPeerRoom(host, asha);
            StartPeerMatch(host, asha);
            PlayPeersUntil(() => MidTrick(host.Rooms.Host, 2), host, asha);
            MatchSnapshot truth = host.Rooms.Host.Table.Snapshot();
            takeOvers.Refuses = true;

            double lostAt = _now;
            Unplug(host);
            SettleUntil(() => takeOvers.Refused == 1, 10);
            double failedAfter = _now - lostAt;
            takeOvers.Refuses = false;
            SettleUntil(() => asha.Rooms.Host != null, 20);
            double tookOverAfter = _now - lostAt;
            MatchSnapshot after = asha.Rooms.Host.Table.Snapshot();
            TestContext.Out.WriteLine("The first opening failed " + Seconds(failedAfter) + " after the loss; the next one hosted after " + Seconds(tookOverAfter) + ".");

            Assert.That(failedAfter, Is.InRange(Protocol.HostLostSeconds - 0.5, Protocol.HostLostSeconds + 1.5), "the next host in line tried at once");
            Assert.That(tookOverAfter - failedAfter, Is.InRange(Protocol.BackupTakeOverSeconds - 0.5, Protocol.BackupTakeOverSeconds + 1.5), "and again a wait later");
            Assert.AreEqual(1, asha.Rooms.Host.Generation, "at its own generation");
            Assert.AreEqual(Describe(truth), Describe(after), "it still held the bot's cards: the same trick, every hand where it was");
            SettleUntil(() => Running(asha), 5);
            PlayPeers(20000, asha);
            Assert.AreEqual(SessionStatus.Ended, asha.Rooms.Current.Match.Status);
            Assert.AreEqual(1, asha.Stats.Current.gamesPlayed);
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void BackupHost_QuickMatch_HostAndNextInLineLostTogether_TheThirdTakesOverAndDealsTheRoundAgain()
        {
            List<QuickPlayer> players = QuickTable(3);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 2), 20000);
            RoomHost table = host.Service.Host;
            MatchSnapshot before = table.Table.Snapshot();
            QuickPlayer next = AtSeat(players, table.NextHost.Line[0].Seat);
            QuickPlayer backup = AtSeat(players, table.NextHost.Line[1].Seat);
            Assert.IsTrue(next.Service.Client.Package.HasCards, "the next host in line holds the bots' cards");
            Assert.IsFalse(backup.Service.Client.Package.HasCards, "the backup holds a package without any");
            List<Redealt> told = Redeals(backup.Match);

            double lostAt = _now;
            GoQuiet(host, unplugged: true);
            GoQuiet(next, unplugged: true);
            SettleQuickUntil(() => backup.Service.Host != null, 30);
            double tookOverAfter = _now - lostAt;
            MatchSnapshot after = backup.Service.Host.Table.Snapshot();
            SettleQuickUntil(() => AllRunning(players, host, next), 5);
            TestContext.Out.WriteLine("The backup hosted " + Seconds(tookOverAfter) + " after the loss and played again after " + Seconds(_now - lostAt) + ".");

            AssertDealtAgain(before, after, backup.Service.Host, tookOverAfter, Protocol.HostLostSeconds + Protocol.BackupTakeOverSeconds + 2.0);
            Assert.AreEqual(1, told.FindAll(r => r.Reason == RedealReason.HostLost && r.Round == 2).Count, "its screen says the round is dealt again, once");
            Assert.AreEqual(SeatConnection.Reconnecting, backup.Match.Seats[table.HostSeat].Connection, "the lost host's seat is held for it");
            Assert.AreEqual(SeatConnection.Reconnecting, backup.Match.Seats[(int)next.Match.LocalSeat].Connection, "and the next in line's");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, host, next);
        }

        [Test]
        public void BackupHost_QuickMatch_FourPeople_HostAndNextInLineLostTogether_TheSecondInLineTakesOverAndTheLastPlaysThere()
        {
            List<QuickPlayer> players = QuickTable(4);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 3), 20000);
            RoomHost table = host.Service.Host;
            MatchSnapshot before = table.Table.Snapshot();
            Assert.AreEqual(3, table.NextHost.Line.Count, "the three other people, in seat order");
            QuickPlayer next = AtSeat(players, table.NextHost.Line[0].Seat);
            QuickPlayer backup = AtSeat(players, table.NextHost.Line[1].Seat);
            QuickPlayer last = AtSeat(players, table.NextHost.Line[2].Seat);
            int lastSeat = (int)last.Match.LocalSeat;
            List<Redealt> told = Redeals(last.Match);

            double lostAt = _now;
            GoQuiet(host, unplugged: true);
            GoQuiet(next, unplugged: true);
            SettleQuickUntil(() => backup.Service.Host != null, 40);
            double tookOverAfter = _now - lostAt;
            MatchSnapshot after = backup.Service.Host.Table.Snapshot();
            SettleQuickUntil(() => AllRunning(players, host, next) && backup.Match.Seats[lastSeat].Connection == SeatConnection.Connected, 20);
            double lastBackAfter = _now - lostAt;
            TestContext.Out.WriteLine("The backup hosted " + Seconds(tookOverAfter) + " after the loss; the last in line played again after " + Seconds(lastBackAfter) + ".");

            AssertDealtAgain(before, after, backup.Service.Host, tookOverAfter, Protocol.HostLostSeconds + Protocol.BackupTakeOverSeconds + Protocol.HandOverWaitSeconds + 1.0);
            Assert.IsNull(last.Service.Host, "the last in line takes nothing over");
            Assert.That(lastBackAfter, Is.LessThan(Protocol.HostLostSeconds + (2 * Protocol.BackupTakeOverSeconds)), "it is seated at the backup's table before its own turn would come");
            Assert.AreEqual(lastSeat, (int)last.Match.LocalSeat, "in its own seat");
            Assert.AreEqual(backup.Match.View.MatchId, last.Match.View.MatchId);
            Assert.AreEqual(1, told.FindAll(r => r.Reason == RedealReason.HostLost && r.Round == 3).Count, "its screen says the round is dealt again, once");
            Assert.AreEqual(1, players.FindAll(p => !p.Stopped && p.Service.Host != null).Count, "one table");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, host, next);
        }

        [Test]
        public void BackupHost_QuickMatch_TheNextInLineIsBackAfterTheBackupTookOver_ItPlaysOnAtTheBackupsTableInItsOwnSeat()
        {
            List<QuickPlayer> players = QuickTable(3);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 2), 20000);
            RoomHost table = host.Service.Host;
            QuickPlayer next = AtSeat(players, table.NextHost.Line[0].Seat);
            QuickPlayer backup = AtSeat(players, table.NextHost.Line[1].Seat);
            int nextSeat = (int)next.Match.LocalSeat;

            double lostAt = _now;
            GoQuiet(host, unplugged: true);
            Pause(next);
            SettleQuickUntil(() => backup.Service.Host != null, 30);
            PlayQuick(4);
            Assert.That(_now - lostAt, Is.InRange(14.0, 18.0), "the next host in line was away about 15 s");
            Resume(next);
            SettleQuickUntil(() => next.Match.Status == SessionStatus.Running && backup.Match.Seats[nextSeat].Connection == SeatConnection.Connected, 20);

            Assert.IsNull(next.Service.Host, "it hosts nothing");
            Assert.IsFalse(next.Service.Client.IsHost, "a player at the backup's table");
            Assert.AreEqual(nextSeat, (int)next.Match.LocalSeat, "in its own seat");
            Assert.AreEqual(backup.Match.View.MatchId, next.Match.View.MatchId);
            Assert.AreEqual(2, backup.Service.Host.Generation);
            Assert.AreEqual(1, players.FindAll(p => !p.Stopped && p.Service.Host != null).Count, "exactly one host");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, host);
        }

        [Test]
        public void BackupHost_QuickMatch_TheOldHostIsBackAfterTheBackupTookOver_ItPlaysOnThereInItsOwnSeat()
        {
            List<QuickPlayer> players = QuickTable(3);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 2), 20000);
            RoomHost table = host.Service.Host;
            int hostSeat = table.HostSeat;
            QuickPlayer next = AtSeat(players, table.NextHost.Line[0].Seat);
            QuickPlayer backup = AtSeat(players, table.NextHost.Line[1].Seat);

            Pause(host);
            GoQuiet(next, unplugged: true);
            SettleQuickUntil(() => backup.Service.Host != null && backup.Match.Status == SessionStatus.Running, 30);
            PlayQuick(20);
            Assert.AreEqual(SeatConnection.Reconnecting, backup.Match.Seats[hostSeat].Connection, "the old host's seat is held for it");
            Resume(host);
            SettleQuickUntil(() => host.Service.Host == null && host.Match.Status == SessionStatus.Running
                && backup.Match.Seats[hostSeat].Connection == SeatConnection.Connected, 20);

            Assert.IsFalse(host.Service.Client.IsHost, "it hosts nothing any more");
            Assert.AreEqual(hostSeat, (int)host.Match.LocalSeat, "in its own seat");
            Assert.AreEqual(backup.Match.View.MatchId, host.Match.View.MatchId);
            Assert.AreEqual(1, players.FindAll(p => !p.Stopped && p.Service.Host != null).Count, "one table: no split brain");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, next);
        }

        /// <param name="oldHost">The old host's app was killed and relaunches; otherwise the next host in line's.</param>
        [TestCase(true)]
        [TestCase(false)]
        public void BackupHost_PrivateRoom_AKilledPlayerRelaunchesWithItsTicket_ItRejoinsTheBackupInItsOwnSeat(bool oldHost)
        {
            UsePeers();
            Device host = NewPeerDevice("Hari");
            Device asha = NewPeerDevice("Asha");
            Device suman = NewPeerDevice("Suman");
            string code = OpenPeerRoom(host, asha, suman);
            StartPeerMatch(host, asha, suman);
            PlayPeersUntil(() => MidTrick(host.Rooms.Host, 2), host, asha, suman);
            RoomHost table = host.Rooms.Host;
            string matchId = host.Rooms.Current.Match.View.MatchId;
            Device next = DeviceAt(table.NextHost.Line[0].Seat, asha, suman);
            Device backup = DeviceAt(table.NextHost.Line[1].Seat, asha, suman);
            Device killed = oldHost ? host : next;
            int seat = killed.Rooms.Current.LocalSeatIndex;

            Unplug(host);
            Unplug(next);
            SettleUntil(() => backup.Rooms.Host != null && Running(backup), 30);
            PlayPeers(10, backup);

            Device back = NewPeerDevice(killed.Profile.DisplayName, killed.Saves);
            Assert.IsTrue(back.Tickets.TryGet(out string ticket), "the next launch offers Rejoin match");
            Assert.AreEqual(code, ticket);
            Task<RoomResult> rejoining = back.Rooms.JoinRoomAsync(ticket, CancellationToken.None);
            SettleUntil(() => rejoining.IsCompleted, 20);

            Assert.IsTrue(rejoining.Result.Succeeded, "the code leads to the backup, which seats it");
            Assert.IsFalse(back.Rooms.Current.IsHost, "as a player");
            Assert.AreEqual(seat, back.Rooms.Current.LocalSeatIndex, "in its own seat");
            Assert.AreEqual(matchId, back.Rooms.Current.Match.View.MatchId, "of the same match");
            back.Rooms.Current.Match.Start();
            Settle();
            Assert.AreEqual(SeatConnection.Connected, backup.Rooms.Current.Match.Seats[seat].Connection, "back in its seat");
            PlayPeers(20000, backup, back);
            foreach (Device d in new[] { backup, back })
            {
                Assert.AreEqual(SessionStatus.Ended, d.Rooms.Current.Match.Status, d.Profile.DisplayName + " played the match to its end");
                Assert.AreEqual(1, d.Stats.Current.gamesPlayed, d.Profile.DisplayName + "'s match counts once");
                for (int s = 0; s < 4; s++)
                {
                    Assert.AreEqual(backup.Rooms.Current.Match.View.Scores.TotalOf((Seat)s), d.Rooms.Current.Match.View.Scores.TotalOf((Seat)s), d.Profile.DisplayName + " ends on the same score for seat " + s);
                }
            }

            Assert.That(_peerFaults, Is.Empty);
        }

        /// <param name="offline">
        /// The backup's device knows it is offline. Otherwise only its connections are cut: it cannot tell
        /// that from everyone else being gone, so it takes the match over at a table of its own, which
        /// it gives up once it finds the next host in line's table with other people at it.
        /// </param>
        [TestCase(false)]
        [TestCase(true)]
        public void BackupHost_QuickMatch_TheBackupsOwnConnectionDrops_OnceBackItPlaysAtTheNextInLinesTable(bool offline)
        {
            List<QuickPlayer> players = QuickTable(4);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 2), 20000);
            RoomHost table = host.Service.Host;
            QuickPlayer next = AtSeat(players, table.NextHost.Line[0].Seat);
            QuickPlayer backup = AtSeat(players, table.NextHost.Line[1].Seat);
            QuickPlayer last = AtSeat(players, table.NextHost.Line[2].Seat);
            int backupSeat = (int)backup.Match.LocalSeat;
            int lastSeat = (int)last.Match.LocalSeat;

            double lostAt = _now;
            GoQuiet(host, unplugged: true);
            _peers.Cut(backup.Network, next.Network);
            _peers.Cut(backup.Network, last.Network);
            backup.Device.Network.IsOnline = !offline;
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(new List<QuickPlayer> { next, last }), 20);
            Assert.AreEqual(1, next.Service.Host.Generation, "the next host in line takes the match over");
            Assert.AreEqual(SeatConnection.Connected, next.Match.Seats[lastSeat].Connection, "with the last in line at its table");
            // Past the backup's turn and its wait for the last in line's report, which never comes.
            SettleQuick(Protocol.HostLostSeconds + Protocol.BackupTakeOverSeconds + Protocol.HandOverWaitSeconds + 2.0 - (_now - lostAt));
            if (offline)
            {
                Assert.IsNull(backup.Service.Host, "offline, the backup takes nothing over");
            }
            else
            {
                Assert.IsNotNull(backup.Service.Host, "cut off but online, the backup took the match over at its turn");
                Assert.AreEqual(2, backup.Service.Host.Generation);
            }

            _peers.Mend(backup.Network, next.Network);
            _peers.Mend(backup.Network, last.Network);
            backup.Device.Network.IsOnline = true;
            SettleQuickUntil(() => backup.Service.Host == null && backup.Match.Status == SessionStatus.Running
                && next.Match.Seats[backupSeat].Connection == SeatConnection.Connected, 30);

            Assert.AreEqual(backupSeat, (int)backup.Match.LocalSeat, "in its own seat");
            Assert.AreEqual(next.Match.View.MatchId, backup.Match.View.MatchId);
            Assert.AreEqual(1, next.Service.Host.Generation, "at the next host in line's table");
            Assert.AreEqual(1, players.FindAll(p => !p.Stopped && p.Service.Host != null).Count, "no second table survives");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, host);
        }

        [Test]
        public void BackupHost_QuickMatch_TheNextInLineTakesOver_NoBackupEverHosts()
        {
            List<QuickPlayer> players = QuickTable(4);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 2), 20000);
            RoomHost table = host.Service.Host;
            QuickPlayer next = AtSeat(players, table.NextHost.Line[0].Seat);
            var backups = new[] { AtSeat(players, table.NextHost.Line[1].Seat), AtSeat(players, table.NextHost.Line[2].Seat) };

            GoQuiet(host, unplugged: true);
            for (double until = _now + Protocol.HostLostSeconds + (3 * Protocol.BackupTakeOverSeconds); _now < until;)
            {
                PlayQuick(1);
                foreach (QuickPlayer backup in backups)
                {
                    Assert.IsNull(backup.Service.Host, backup.Name + " never hosts: the next host in line took the match over");
                }
            }

            Assert.IsNotNull(next.Service.Host);
            Assert.AreEqual(1, next.Service.Host.Generation, "one hand-over, one generation up");
            Assert.IsTrue(AllRunning(players, host), "everyone else plays on there");
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, host);
        }

        /// <summary>The redeals <paramref name="match"/>'s screen hears from now on.</summary>
        private static List<Redealt> Redeals(IMatchSession match)
        {
            var told = new List<Redealt>();
            match.EventReceived += e =>
            {
                if (e is Redealt redealt)
                {
                    told.Add(redealt);
                }
            };

            return told;
        }

        /// <summary>
        /// The backup took the match over at its turn, two generations up, with the round in play dealt
        /// again by the same dealer and the rounds before as they were scored.
        /// </summary>
        private static void AssertDealtAgain(MatchSnapshot before, MatchSnapshot after, RoomHost taken, double tookOverAfter, double latest)
        {
            Assert.That(tookOverAfter, Is.InRange(Protocol.HostLostSeconds + Protocol.BackupTakeOverSeconds - 1.0, latest), "the backup takes the match over at its turn");
            Assert.AreEqual(2, taken.Generation, "two generations up: its place in the line");
            Assert.AreEqual(before.round, after.round, "the same round");
            Assert.AreEqual((int)Phase.Calling, after.phase, "dealt again: calling from the start");
            Assert.AreEqual(before.dealer, after.dealer, "by the same dealer");
            Assert.That(after.completedTricks, Is.Empty);
            Assert.That(after.currentTrick.cards, Is.Empty);
            Assert.AreEqual(Describe(Rounds(before)), Describe(Rounds(after)), "the rounds before keep their scores");
            var dealt = new HashSet<int>();
            foreach (SeatSnapshot seat in after.seats)
            {
                Assert.AreEqual(13, seat.hand.Length);
                Assert.AreEqual(-1, seat.call);
                dealt.UnionWith(seat.hand);
            }

            Assert.AreEqual(52, dealt.Count, "a whole new deal");
        }

        private static string Seconds(double seconds) => seconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";

        /// <summary>A device on the peer network whose take-overs can be made to fail (<see cref="TakeOverSwitch"/>).</summary>
        private Device NewSwitchedPeerDevice(string name, out TakeOverSwitch takeOvers)
        {
            InMemoryPeerNetwork.Device network = _peers.AddDevice();
            takeOvers = new TakeOverSwitch(new PeerRoomTransport(network, _peerFaults.Add));
            var device = new Device(takeOvers, name, (ulong)(100 + _devices.Count), new InMemorySaveStore(), () => Epoch.AddSeconds(_now));
            _devices.Add(device);
            _peerOf[device] = network;
            return device;
        }

        /// <summary>
        /// A device's peer-to-peer transport that can refuse to open a room it takes over, as a device
        /// that cannot sign in to host would; everything else is the transport's own.
        /// </summary>
        private sealed class TakeOverSwitch : ITickedRoomTransport, IHandOverTransport
        {
            private readonly PeerRoomTransport _inner;

            public TakeOverSwitch(PeerRoomTransport inner)
            {
                _inner = inner;
            }

            /// <summary>While set, every take-over fails.</summary>
            public bool Refuses { get; set; }

            /// <summary>How many take-overs failed so.</summary>
            public int Refused { get; private set; }

            public string LocalAddress => _inner.LocalAddress;

            public Task<HostLinkResult> HostAsync(string roomCode, CancellationToken ct) => _inner.HostAsync(roomCode, ct);

            public Task<ClientLinkResult> JoinAsync(string roomCode, CancellationToken ct) => _inner.JoinAsync(roomCode, ct);

            public Task<ClientLinkResult> JoinAtAsync(string roomCode, int generation, string hostAddress, CancellationToken ct) =>
                _inner.JoinAtAsync(roomCode, generation, hostAddress, ct);

            public Task<HostLinkResult> TakeOverAsync(string roomCode, int generation, bool listed, CancellationToken ct)
            {
                if (!Refuses)
                {
                    return _inner.TakeOverAsync(roomCode, generation, listed, ct);
                }

                Refused++;
                return Task.FromResult(new HostLinkResult(null, RoomError.Unknown));
            }

            public void Tick(float deltaTime, double now) => _inner.Tick(deltaTime, now);
        }
    }
}
