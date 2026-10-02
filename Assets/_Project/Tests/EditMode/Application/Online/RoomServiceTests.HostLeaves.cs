using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// A host that leaves never ends the game (ADR 030): during a match, whether it taps Leave or its
    /// app closes, it tells everyone to go to the next host in line, which takes the table over at once
    /// from the same trick; the leaver's seat is a bot's for the rest of the match and the match counts
    /// as left for it. Before the start a private room still closes, and a Quick Match table's guests
    /// simply look again.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [TestCase(1, 4)]
        [TestCase(5, 4)]
        [TestCase(1, 2)]
        [TestCase(5, 2)]
        public void HostLeaves_QuickMatch_MidTrick_TheNextInLineTakesOverAtOnceFromTheSameTrick(int round, int people)
        {
            List<QuickPlayer> players = QuickTable(people);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, round), 20000);
            RoomHost table = host.Service.Host;
            int hostSeat = table.HostSeat;
            MatchSnapshot truth = table.Table.Snapshot();
            QuickPlayer next = AtSeat(players, table.NextHost.NextSeat);
            List<QuickPlayer> others = players.FindAll(p => p != host);
            var seen = Watch(others);
            IMatchSession leaving = host.Match;

            double leftAt = _now;
            leaving.LeaveAsync(CancellationToken.None);
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(players, except: host), 5);

            Assert.That(_now - leftAt, Is.LessThanOrEqualTo(1.0), "within about a second");
            Assert.IsTrue(table.IsClosed, "the leaver's table serves nobody");
            Assert.AreEqual(Describe(truth), Describe(next.Service.Host.Table.Snapshot()), "the same trick, every hand where it was");
            Assert.AreEqual(SeatConnection.Left, next.Match.Seats[hostSeat].Connection, "the leaver's seat is a bot's for good");
            AssertNeverTold(seen, RoomError.HostLeft);
            AssertCountsAsLeft(host, leaving);
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, except: host);
            Assert.AreEqual(1, players.FindAll(p => p.Service.Host != null && !p.Service.Host.IsClosed).Count, "one table: no split brain");
        }

        /// <param name="timeToSend">The app had a moment to tell everyone; otherwise its device was gone at once and the guests notice the silence.</param>
        [TestCase(true)]
        [TestCase(false)]
        public void HostLeaves_QuickMatch_ItsAppCloses_TheMatchGoesOnAtTheNextInLine(bool timeToSend)
        {
            List<QuickPlayer> players = QuickTable(4);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 2), 20000);
            RoomHost table = host.Service.Host;
            MatchSnapshot truth = table.Table.Snapshot();
            QuickPlayer next = AtSeat(players, table.NextHost.NextSeat);
            var seen = Watch(players.FindAll(p => p != host));
            IMatchSession leaving = host.Match;

            double closedAt = _now;
            host.Service.Dispose();
            if (timeToSend)
            {
                Pump();
            }

            GoQuiet(host, unplugged: true);
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(players, except: host), 15);

            Assert.That(_now - closedAt, Is.LessThanOrEqualTo(timeToSend ? 1.0 : Protocol.HostLostSeconds + 2.0));
            Assert.AreEqual(Describe(truth), Describe(next.Service.Host.Table.Snapshot()));
            AssertNeverTold(seen, RoomError.HostLeft);
            AssertCountsAsLeft(host, leaving);
            PlayQuick(20000);
            AssertPlayedToTheEnd(players, except: host);
        }

        [Test]
        public void HostLeaves_APlayerCannotReachTheNextHostForAWhile_ItKeepsTryingThatOneNeverTheLeaver()
        {
            List<QuickPlayer> players = QuickTable(4);
            QuickPlayer host = Hosting(players);
            PlayQuickUntil(() => MidTrick(host, 3), 20000);
            MatchSnapshot truth = host.Service.Host.Table.Snapshot();
            QuickPlayer next = AtSeat(players, host.Service.Host.NextHost.NextSeat);
            QuickPlayer late = players.Find(p => p != host && p != next);
            int sentBefore = _peers.Sent.Count;

            host.Match.LeaveAsync(CancellationToken.None);
            _peers.Cut(late.Network, next.Network);
            SettleQuick(2);
            _peers.Mend(late.Network, next.Network);
            SettleQuickUntil(() => next.Service.Host != null && AllRunning(players, except: host), 6);

            Assert.AreEqual(Describe(truth), Describe(next.Service.Host.Table.Snapshot()), "the new host waited for the late report: the same trick");
            for (int i = sentBefore; i < _peers.Sent.Count; i++)
            {
                var sent = _peers.Sent[i];
                bool asks = sent.Packet.Length == 1 && sent.Packet[0] == (byte)PacketType.Open;
                Assert.IsFalse(asks && sent.To == host.Network.Address, "nobody goes back to the host that left");
            }

            PlayQuick(20000);
            AssertPlayedToTheEnd(players, except: host);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void HostLeaves_PrivateRoom_MidMatch_TheNextInLineTakesOverAndTheRoomStaysWithIt(bool appCloses)
        {
            UsePeers();
            Device host = NewPeerDevice("Hari");
            Device asha = NewPeerDevice("Asha");
            Device suman = NewPeerDevice("Suman");
            string code = OpenPeerRoom(host, asha, suman);
            Assert.IsFalse(((IHandOverRoom)host.Rooms.Current).HandsOverWhenLeaving, "in the lobby the host leaving closes the room");
            StartPeerMatch(host, asha, suman);
            PlayPeersUntil(() => MidTrick(host.Rooms.Host, 2), host, asha, suman);
            Assert.IsTrue(((IHandOverRoom)host.Rooms.Current).HandsOverWhenLeaving, "during the match it does not: the leave dialog says a bot takes the seat");
            RoomHost table = host.Rooms.Host;
            int hostSeat = table.HostSeat;
            MatchSnapshot truth = table.Table.Snapshot();
            Device next = DeviceAt(table.NextHost.NextSeat, asha, suman);
            var told = new List<RoomError>();
            asha.Rooms.Current.Closed += told.Add;
            suman.Rooms.Current.Closed += told.Add;

            double leftAt = _now;
            if (appCloses)
            {
                host.Rooms.Dispose();
                _devices.Remove(host);
            }
            else
            {
                host.Rooms.Current.LeaveAsync(CancellationToken.None);
            }

            SettleUntil(() => next.Rooms.Host != null && Running(asha, suman), 5);

            Assert.That(_now - leftAt, Is.LessThanOrEqualTo(1.0), "within about a second");
            Assert.That(told, Is.Empty, "nobody's room closed");
            Assert.AreEqual(Describe(truth), Describe(next.Rooms.Host.Table.Snapshot()), "the same trick");
            Assert.AreEqual(SeatConnection.Left, next.Rooms.Current.Match.Seats[hostSeat].Connection);
            Assert.IsNull(host.Rooms.Current, "the leaver is out of the room");
            Assert.AreEqual(1, host.Stats.Current.gamesPlayed, "the match counts as left for the leaver");
            Assert.AreEqual(0, host.Stats.Current.wins);
            Settle(PeerRoomTransport.RelistSeconds + 1);
            Assert.AreEqual(_peerOf[next].Address, _peers.ListedBy(code), "the code leads to the new host");
            PlayPeers(20000, asha, suman);
            Assert.AreEqual(SessionStatus.Ended, asha.Rooms.Current.Match.Status);
            Assert.AreEqual(SessionStatus.Ended, suman.Rooms.Current.Match.Status);
            Assert.AreEqual(1, asha.Stats.Current.gamesPlayed);
            Settle(2);
            Assert.IsTrue(next.Rooms.Current.IsHost, "after the match the room stays with the new host");
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void HostLeaves_PrivateRoomOverTheRelay_TheRoomStillClosesForEveryone()
        {
            // The relay (PurrNet) hands nothing over: today's "host left" stays.
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);
            IMatchSession session = guest.Rooms.Current.Match;
            RoomError closed = RoomError.None;
            guest.Rooms.Current.Closed += reason => closed = reason;
            Assert.IsFalse(((IHandOverRoom)host.Rooms.Current).HandsOverWhenLeaving, "the leave dialog warns the host that the room closes");

            host.Rooms.Current.LeaveAsync(CancellationToken.None);
            Settle(2);

            Assert.AreEqual(RoomError.HostLeft, closed);
            Assert.AreEqual(SessionStatus.Failed, session.Status);
        }

        /// <param name="counting">The host leaves during the locked table's 3-2-1, its app closing; otherwise it cancels while the table still waits.</param>
        [TestCase(false)]
        [TestCase(true)]
        public void HostLeaves_QuickMatchBeforeTheStart_TheGuestsLookAgainWithoutBeingToldAnything(bool counting)
        {
            UseQuickMatch(listingDelay: 0.5);
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            QuickPlayer suman = NewQuickPlayer("Suman");
            Tap(hari);
            SettleQuickUntil(() => hari.TableCode != null, 5);
            Tap(asha);
            Tap(suman);
            SettleQuickUntil(() => SeatsTaken(asha) == 3 && SeatsTaken(suman) == 3, 15);
            Assert.AreSame(hari, HostOf(hari, asha, suman), "Hari hosts the table the others sit at");
            if (counting)
            {
                SettleQuickUntil(() => !double.IsNaN(asha.Service.Client.StartsAt), 20);
                hari.Service.Dispose();
                hari.Stopped = true;
            }
            else
            {
                hari.Cancel.Cancel();
            }

            SettleQuickUntil(() => asha.Search.IsCompleted && suman.Search.IsCompleted, 30);
            SettleQuick(1);

            Assert.IsTrue(asha.Search.Result.Found, "Asha found a table, not a message");
            Assert.IsTrue(suman.Search.Result.Found);
            Assert.AreNotEqual(hari.Network.Address, TableHostOf(asha), "a table that is not Hari's");
            Assert.AreNotEqual(hari.Network.Address, TableHostOf(suman));
            if (!counting)
            {
                Assert.AreEqual(TableHostOf(asha), TableHostOf(suman), "with time to spare, the two met again at one table");
            }

            Assert.That(_peerFaults, Is.Empty);
        }

        /// <summary>Every session status and room closing each player's screen hears from now on.</summary>
        private static Dictionary<QuickPlayer, List<string>> Watch(List<QuickPlayer> players)
        {
            var seen = new Dictionary<QuickPlayer, List<string>>();
            foreach (QuickPlayer p in players)
            {
                var log = new List<string>();
                seen[p] = log;
                IMatchSession match = p.Match;
                match.StatusChanged += () => log.Add("status " + match.Status);
                p.Service.Client.Closed += reason => log.Add("closed " + reason);
            }

            return seen;
        }

        /// <summary>Nobody's screen heard the room close with <paramref name="reason"/>, or its match fail; each went through "Reconnecting…" back to play.</summary>
        private static void AssertNeverTold(Dictionary<QuickPlayer, List<string>> seen, RoomError reason)
        {
            foreach (KeyValuePair<QuickPlayer, List<string>> pair in seen)
            {
                CollectionAssert.DoesNotContain(pair.Value, "closed " + reason, pair.Key.Name);
                CollectionAssert.DoesNotContain(pair.Value, "status " + SessionStatus.Failed, pair.Key.Name);
                CollectionAssert.Contains(pair.Value, "status " + SessionStatus.Reconnecting, pair.Key.Name + " showed Reconnecting meanwhile");
                Assert.AreEqual(SessionStatus.Running, pair.Key.Match.Status, pair.Key.Name + " plays on");
                Assert.AreEqual(RoomError.None, ((OnlineMatchSession)pair.Key.Match).FailureReason, pair.Key.Name);
            }
        }

        /// <summary>The leaver's match counts as played and lost, as when anyone taps Leave (GDD §8).</summary>
        private static void AssertCountsAsLeft(QuickPlayer leaver, IMatchSession match)
        {
            Assert.AreEqual(SessionStatus.Ended, match.Status, "the leaver's own table is over");
            Assert.AreEqual(1, leaver.Device.Stats.Current.gamesPlayed, "the match counts as left");
            Assert.AreEqual(0, leaver.Device.Stats.Current.wins);
            Assert.IsFalse(leaver.Device.Stats.Current.hasBest);
        }

        /// <summary>The address of the device that hosts the table <paramref name="player"/> plays at: its own, or as that host said.</summary>
        private static string TableHostOf(QuickPlayer player)
        {
            if (player.Service.Host != null)
            {
                return player.Network.Address;
            }

            NextHostNotice notice = player.Service.Client.NextHost;
            Assert.IsNotNull(notice, player.Name + " was told where its host is");
            return notice.HostAddress;
        }
    }
}
