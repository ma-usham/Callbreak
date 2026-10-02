using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// Whole online matches between devices that meet over a peer-to-peer network with a directory
    /// (EOS lobbies and P2P, in memory) instead of the relay: the room protocol runs unchanged over
    /// <see cref="PeerRoomTransport"/>, also through packets so small that nearly every message is
    /// cut up and put back together, and with a directory as slow to show a new room as EOS's; a
    /// guest who loses the connection comes back, even while the host's listing is being restored;
    /// and a host who leaves takes its code with it.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        private readonly Dictionary<Device, InMemoryPeerNetwork.Device> _peerOf = new Dictionary<Device, InMemoryPeerNetwork.Device>();
        private readonly List<Exception> _peerFaults = new List<Exception>();
        private InMemoryPeerNetwork _peers;

        [TestCase(InMemoryPeerNetwork.EosMaxPacketSize, 8.6)]
        [TestCase(48, 0)]
        public void PeerNetwork_WholeMatch_HostAndGuestPlayItToTheEnd(int maxPacketSize, double listingDelay)
        {
            UsePeers(maxPacketSize);
            _peers.ListingDelaySeconds = listingDelay;
            Device host = NewPeerDevice("Hari");
            Device guest = NewPeerDevice("Asha");

            string code = OpenPeerRoom(host, guest);
            if (listingDelay > 0)
            {
                Assert.Greater(_peerOf[guest].LookUpCalls, 1, "the guest looked again until the room showed");
            }

            Assert.AreEqual("Asha", host.Rooms.Current.Seats[1].Name);
            Assert.AreEqual("Hari", guest.Rooms.Current.Seats[0].Name);
            StartMatch(host, guest);
            IMatchSession hostSession = host.Rooms.Current.Match;
            IMatchSession guestSession = guest.Rooms.Current.Match;
            PlayToEnd(host, guest);
            Settle();

            Assert.AreEqual(SessionStatus.Ended, hostSession.Status);
            Assert.AreEqual(SessionStatus.Ended, guestSession.Status);
            Assert.AreEqual(hostSession.View.MatchId, guestSession.View.MatchId);
            for (int s = 0; s < 4; s++)
            {
                Assert.AreEqual(hostSession.View.Scores.TotalOf((Seat)s), guestSession.View.Scores.TotalOf((Seat)s), "both devices end on the same score for seat " + s);
            }

            Assert.AreEqual(1, host.Stats.Current.gamesPlayed);
            Assert.AreEqual(1, guest.Stats.Current.gamesPlayed);
            Assert.AreEqual(_peerOf[host].Address, _peers.ListedBy(code), "the room is still listed: it is a lobby again");
            Assert.That(_peerFaults, Is.Empty);
            if (maxPacketSize < InMemoryPeerNetwork.EosMaxPacketSize)
            {
                Assert.IsTrue(_peers.Sent.Exists(sent => sent.Packet[0] == (byte)PacketType.First), "messages were cut into packets");
            }
        }

        [Test]
        public void PeerNetwork_GuestLosesTheConnectionMidMatch_TheServiceReconnectsAndTheMatchGoesOn()
        {
            UsePeers();
            Device host = NewPeerDevice("Hari");
            Device guest = NewPeerDevice("Asha");
            OpenPeerRoom(host, guest);
            StartMatch(host, guest);
            IMatchSession guestSession = guest.Rooms.Current.Match;
            Play(host, guest, 20);

            _peers.Break(_peerOf[host], _peerOf[guest]);
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
            Assert.AreEqual(1, guest.Stats.Current.gamesPlayed);
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void PeerNetwork_HostLosesItsListingAsTheGuestDrops_TheGuestComesBackAtTheHostsAddressAndTheCodeIsListedAgain()
        {
            UsePeers();
            Device host = NewPeerDevice("Hari");
            Device guest = NewPeerDevice("Asha");
            string code = OpenPeerRoom(host, guest);
            StartMatch(host, guest);
            IMatchSession guestSession = guest.Rooms.Current.Match;
            Play(host, guest, 20);

            // The host's device loses the network for a moment: the directory drops its listing, the
            // guest's connection goes, and listing the code again fails for two seconds.
            InMemoryPeerNetwork.Device hostDevice = _peerOf[host];
            hostDevice.DirectoryError = RoomError.Offline;
            _peers.LoseListing(code);
            _peers.Break(hostDevice, _peerOf[guest]);
            Settle(0.5);
            Assert.AreEqual(SessionStatus.Reconnecting, guestSession.Status);
            Settle(1.5);

            // During a match the host has told its players where it is (ADR 030): no lookup is needed.
            Assert.AreEqual(SessionStatus.Running, guestSession.Status, "the guest is back at the host's address");
            Assert.IsNull(_peers.ListedBy(code), "though nobody finds the room by its code meanwhile");
            hostDevice.DirectoryError = RoomError.None;
            Settle(PeerRoomTransport.RelistSeconds + 1);
            Assert.AreEqual(hostDevice.Address, _peers.ListedBy(code), "then the code leads to it again, for whoever comes by it");
            PlayToEnd(host, guest);
            Assert.AreEqual(SessionStatus.Ended, guestSession.Status);
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void PeerNetwork_HostLeavesTheLobby_TheGuestIsToldAndTheCodeIsGone()
        {
            UsePeers();
            Device host = NewPeerDevice("Hari");
            Device guest = NewPeerDevice("Asha");
            string code = OpenPeerRoom(host, guest);
            RoomError closed = RoomError.None;
            guest.Rooms.Current.Closed += reason => closed = reason;

            host.Rooms.Current.LeaveAsync(CancellationToken.None);
            Settle(2);

            Assert.AreEqual(RoomError.HostLeft, closed);
            Assert.IsNull(host.Rooms.Current);
            Assert.IsNull(guest.Rooms.Current);
            Assert.IsNull(_peers.ListedBy(code), "the code is free again");
            Assert.AreEqual(0, _peers.Links, "every connection was closed");
            Assert.That(_peerFaults, Is.Empty);
        }

        /// <summary>The devices made from now on meet over a fresh peer-to-peer network, with packets of at most <paramref name="maxPacketSize"/> bytes.</summary>
        private void UsePeers(int maxPacketSize = InMemoryPeerNetwork.EosMaxPacketSize)
        {
            _peers = new InMemoryPeerNetwork(maxPacketSize);
            _peerOf.Clear();
            _peerFaults.Clear();
        }

        /// <param name="saves">The files of an earlier run of the same app, for a device that starts again.</param>
        private Device NewPeerDevice(string name, InMemorySaveStore saves = null)
        {
            InMemoryPeerNetwork.Device network = _peers.AddDevice();
            var device = new Device(new PeerRoomTransport(network, _peerFaults.Add), name, (ulong)(100 + _devices.Count), saves ?? new InMemorySaveStore(), () => Epoch.AddSeconds(_now));
            _devices.Add(device);
            _peerOf[device] = network;
            return device;
        }

        /// <summary>The host creates a room over the peer-to-peer network and the guest joins it by its code, which is returned.</summary>
        private string OpenPeerRoom(Device host, Device guest)
        {
            Task<RoomResult> creating = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 0), CancellationToken.None);
            for (int i = 0; i < 120 && !creating.IsCompleted; i++)
            {
                Settle();
            }

            Assert.IsTrue(creating.IsCompleted, "creating the room was answered");
            Assert.IsTrue(creating.Result.Succeeded, "the room was created");
            string code = creating.Result.Session.Code;
            Assert.AreEqual(_peerOf[host].Address, _peers.ListedBy(code), "under its code");

            // The guest types the code at once, before a slow directory shows the room: it keeps looking.
            Task<RoomResult> joining = guest.Rooms.JoinRoomAsync(code, CancellationToken.None);
            for (int i = 0; i < 120 && !joining.IsCompleted; i++)
            {
                Settle();
            }

            Assert.IsTrue(joining.IsCompleted, "joining was answered");
            Assert.IsTrue(joining.Result.Succeeded, "the guest joined");
            return code;
        }

        /// <summary>
        /// Delivers everything in flight: on the relay, and on the peer-to-peer network, where every
        /// device's transport takes its share until nothing is left.
        /// </summary>
        private void Pump()
        {
            _network.Pump();
            bool anyOnPeers = false;
            foreach (Device d in _devices)
            {
                anyOnPeers |= d.Transport != null;
            }

            // The fixture outlives a test: a peer network left from an earlier one is not this test's.
            if (_peers == null || !anyOnPeers)
            {
                return;
            }

            _peers.Now = _now;
            for (int i = 0; ; i++)
            {
                Assert.Less(i, 1000, "the peer-to-peer network never settles");
                _peers.Pump();
                foreach (Device d in _devices)
                {
                    // A device whose app is paused runs nothing.
                    if (d.Transport != null && !(_peerOf.TryGetValue(d, out InMemoryPeerNetwork.Device net) && net.IsPaused))
                    {
                        d.Transport.Tick(0.25f, _now);
                    }
                }

                if (_peers.IsQuiet)
                {
                    return;
                }
            }
        }
    }
}
