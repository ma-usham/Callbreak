using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Core.Randomness;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// Private rooms over a peer-to-peer network with a directory (EOS lobbies and P2P, in memory):
    /// the host lists its code and a guest finds it, also while the directory is still slow to show
    /// it; every failure says what it was, and nothing hangs; messages of any length arrive whole and in order; a
    /// guest's oversized message is dropped; drops, kicks, lost listings and handler faults behave
    /// as over the relay; and the packet a guest opens its connection with never reaches the room.
    /// </summary>
    public sealed class PeerRoomTransportTests
    {
        private const string Code = "ABCDEF";
        private const string Channel = PeerRoomTransport.ChannelPrefix + Code;

        private InMemoryPeerNetwork _world;
        private List<PeerRoomTransport> _transports;
        private List<Exception> _faults;
        private double _now;

        [SetUp]
        public void SetUp()
        {
            _world = new InMemoryPeerNetwork();
            _transports = new List<PeerRoomTransport>();
            _faults = new List<Exception>();
            _now = 0;
        }

        [Test]
        public void Host_ListsTheCode_AndAGuestThatLooksItUpConnects()
        {
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device hostDevice);
            PeerRoomTransport guest = NewTransport(out InMemoryPeerNetwork.Device guestDevice);

            HostLinkResult hosted = Host(host);
            var hostLog = new HostLog(hosted.Link);
            ClientLinkResult joined = Join(guest);

            Assert.IsTrue(hosted.Succeeded);
            Assert.AreEqual(hostDevice.Address, _world.ListedBy(Code));
            Assert.IsTrue(joined.Succeeded);
            Assert.IsTrue(joined.Link.IsConnected);
            Assert.IsTrue(_world.AreConnected(hostDevice, guestDevice, Channel), "on the room's own channel");
            Assert.AreEqual(1, hostLog.Connected.Count, "the host heard the guest connect, once");
            Assert.IsFalse(hostLog.Connected[0].IsLocal, "a guest is never the host's own player");
            Assert.That(_faults, Is.Empty);
        }

        [Test]
        public void Host_CodeListedByAnotherDevice_IsAlreadyStarted()
        {
            PeerRoomTransport host = NewTransport(out _);
            _world.Take(Code);

            HostLinkResult hosted = Host(host);

            Assert.AreEqual(RoomError.AlreadyStarted, hosted.Error);
            Assert.IsNull(hosted.Link);
            Assert.AreEqual("elsewhere", _world.ListedBy(Code), "the other listing stands");
        }

        [Test]
        public void Host_DeviceCannotSignIn_IsOffline()
        {
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device device);
            device.CanSignIn = false;

            HostLinkResult hosted = Host(host);

            Assert.AreEqual(RoomError.Offline, hosted.Error);
            Assert.AreEqual(0, device.ListCalls, "nothing is listed for a device that is not signed in");
        }

        [Test]
        public void Join_DeviceCannotSignIn_IsOffline()
        {
            Assert.IsTrue(Host(NewTransport(out _)).Succeeded);
            PeerRoomTransport guest = NewTransport(out InMemoryPeerNetwork.Device device);
            device.CanSignIn = false;

            Assert.AreEqual(RoomError.Offline, Join(guest).Error);
            Assert.AreEqual(0, device.LookUpCalls);
        }

        [TestCase(RoomError.Offline)]
        [TestCase(RoomError.Unknown)]
        public void Host_DirectoryUnreachable_SaysWhy(RoomError error)
        {
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device device);
            device.DirectoryError = error;

            Assert.AreEqual(error, Host(host).Error);
            Assert.IsNull(_world.ListedBy(Code));
        }

        [TestCase(RoomError.Offline)]
        [TestCase(RoomError.Unknown)]
        public void Join_DirectoryUnreachable_SaysWhy(RoomError error)
        {
            Assert.IsTrue(Host(NewTransport(out _)).Succeeded);
            PeerRoomTransport guest = NewTransport(out InMemoryPeerNetwork.Device device);
            device.DirectoryError = error;

            Assert.AreEqual(error, Join(guest).Error);
            Assert.AreEqual(1, device.LookUpCalls, "not looked up again");
        }

        [Test]
        public void Join_NobodyListsTheCode_IsNotFoundOnceTheLookUpBudgetIsSpent()
        {
            PeerRoomTransport guest = NewTransport(out InMemoryPeerNetwork.Device device);

            Task<ClientLinkResult> joining = guest.JoinAsync(Code, CancellationToken.None);
            Settle(PeerRoomTransport.LookUpBudgetSeconds - 1.5);
            Assert.IsFalse(joining.IsCompleted, "still looking");
            Settle(1.5);

            Assert.IsTrue(joining.IsCompleted);
            Assert.AreEqual(RoomError.NotFound, joining.Result.Error);
            Assert.That(device.LookUpCalls, Is.InRange(5, 12), "looked up again after a pause each time, not every frame");
            Assert.AreEqual(0, _world.Links, "no connection was asked for");
        }

        [Test]
        public void Join_ListingNotShownYet_TheGuestKeepsLookingAndGetsIn()
        {
            _world.ListingDelaySeconds = 8.6;
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device hostDevice);
            Assert.IsTrue(Host(host).Succeeded);
            PeerRoomTransport guest = NewTransport(out InMemoryPeerNetwork.Device guestDevice);

            Task<ClientLinkResult> joining = guest.JoinAsync(Code, CancellationToken.None);
            Settle(8);
            Assert.IsFalse(joining.IsCompleted, "the directory does not show the room yet");
            Settle(2);

            Assert.IsTrue(joining.IsCompleted);
            Assert.IsTrue(joining.Result.Succeeded, "the guest got in once the room showed");
            Assert.Greater(guestDevice.LookUpCalls, 1);
            Assert.IsTrue(_world.AreConnected(hostDevice, guestDevice, Channel));
        }

        [Test]
        public void Join_ConnectTimeout_CountsFromTheMomentTheHostIsFound()
        {
            // A device lists the code but never answers a connection, and the listing shows late.
            _world.ListingDelaySeconds = 8;
            InMemoryPeerNetwork.Device silent = _world.AddDevice();
            silent.SignInAsync(CancellationToken.None);
            _world.Pump();
            silent.ListAsync(Code, CancellationToken.None);
            _world.Pump();
            PeerRoomTransport guest = NewTransport(out _);

            Task<ClientLinkResult> joining = guest.JoinAsync(Code, CancellationToken.None);
            Settle(8 + PeerRoomTransport.ConnectTimeoutSeconds - 1);
            Assert.IsFalse(joining.IsCompleted, "the connection has had less than its own timeout");
            Settle(3);

            Assert.IsTrue(joining.IsCompleted);
            Assert.AreEqual(RoomError.Unknown, joining.Result.Error);
            Assert.AreEqual(0, _world.Links, "the connection asked for was given up");
        }

        [Test]
        public void Join_CancelledWhileStillLooking_EndsAtOnce()
        {
            PeerRoomTransport guest = NewTransport(out InMemoryPeerNetwork.Device device);
            var cancel = new CancellationTokenSource();

            Task<ClientLinkResult> joining = guest.JoinAsync(Code, cancel.Token);
            Settle(3);
            int lookups = device.LookUpCalls;
            cancel.Cancel();

            Assert.IsTrue(joining.IsCompleted, "without waiting for a tick");
            Assert.AreEqual(RoomError.Unknown, joining.Result.Error);
            Settle(5);
            Assert.AreEqual(lookups, device.LookUpCalls, "and it stops looking");
        }

        [Test]
        public void Join_ListedDeviceNoLongerHostsTheRoom_IsNotFoundAtOnce()
        {
            NewTransport(out InMemoryPeerNetwork.Device gone);
            PeerRoomTransport guest = NewTransport(out _);
            _world.Take(Code, gone.Address);

            Task<ClientLinkResult> joining = guest.JoinAsync(Code, CancellationToken.None);
            Settle(0.5);

            Assert.IsTrue(joining.IsCompleted, "that device turned the connection down at once");
            Assert.AreEqual(RoomError.NotFound, joining.Result.Error);
            Assert.AreEqual(0, _world.Links);
        }

        [Test]
        public void Join_OwnListing_IsNotFound()
        {
            PeerRoomTransport device = NewTransport(out _);
            Assert.IsTrue(Host(device).Succeeded);

            Assert.AreEqual(RoomError.NotFound, Join(device).Error, "a device never connects to itself");
        }

        [Test]
        public void Join_ListedHostNeverAnswers_FailsAfterTheConnectTimeout()
        {
            PeerRoomTransport guest = NewTransport(out _);
            _world.Take(Code);

            Task<ClientLinkResult> joining = guest.JoinAsync(Code, CancellationToken.None);
            Settle(PeerRoomTransport.ConnectTimeoutSeconds - 1);
            Assert.IsFalse(joining.IsCompleted, "still trying");
            Settle(2);

            Assert.IsTrue(joining.IsCompleted);
            Assert.AreEqual(RoomError.Unknown, joining.Result.Error);
            Assert.AreEqual(0, _world.Links, "the connection asked for was given up");
        }

        [Test]
        public void Host_ServicesNeverAnswer_FailsAfterTheConnectTimeoutAndListsNothingLater()
        {
            PeerRoomTransport host = NewTransport(out _);
            _world.HoldAnswers = true;

            Task<HostLinkResult> hosting = host.HostAsync(Code, CancellationToken.None);
            Settle(PeerRoomTransport.ConnectTimeoutSeconds - 1);
            Assert.IsFalse(hosting.IsCompleted, "still trying");
            Settle(2);
            Assert.IsTrue(hosting.IsCompleted);
            Assert.AreEqual(RoomError.Unknown, hosting.Result.Error);

            _world.HoldAnswers = false;
            Settle(1);
            Assert.IsNull(_world.ListedBy(Code));
        }

        [Test]
        public void Host_Cancelled_EndsAtOnce()
        {
            PeerRoomTransport host = NewTransport(out _);
            var cancel = new CancellationTokenSource();
            _world.HoldAnswers = true;

            Task<HostLinkResult> hosting = host.HostAsync(Code, cancel.Token);
            Settle();
            cancel.Cancel();

            Assert.IsTrue(hosting.IsCompleted, "without waiting for a tick");
            Assert.AreEqual(RoomError.Unknown, hosting.Result.Error);
            Assert.AreEqual(RoomError.Unknown, host.HostAsync(Code, new CancellationToken(true)).Result.Error, "and so does one cancelled before it starts");
        }

        [Test]
        public void Host_CancelledJustAfterTheListingArrived_TakesTheListingDown()
        {
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device device);
            var cancel = new CancellationTokenSource();
            Task<HostLinkResult> hosting = host.HostAsync(Code, cancel.Token);
            for (int i = 0; i < 10 && device.ListCalls == 0; i++)
            {
                _world.Pump();
                host.Tick(0.1f, _now);
            }

            _world.Pump();
            Assert.AreEqual(device.Address, _world.ListedBy(Code), "the directory listed it");
            cancel.Cancel();
            Assert.AreEqual(RoomError.Unknown, hosting.Result.Error, "but the caller gave up first");
            Settle();

            Assert.IsNull(_world.ListedBy(Code), "and nothing is left listed");
        }

        [Test]
        public void Join_Cancelled_EndsAtOnceAndLeavesNoConnection()
        {
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device hostDevice);
            var hostLog = new HostLog(Host(host).Link);
            PeerRoomTransport guest = NewTransport(out InMemoryPeerNetwork.Device guestDevice);
            var cancel = new CancellationTokenSource();
            Task<ClientLinkResult> joining = guest.JoinAsync(Code, cancel.Token);
            for (int i = 0; i < 10 && !SentOpen(guestDevice, hostDevice); i++)
            {
                _world.Pump();
                guest.Tick(0.1f, _now);
            }

            Assert.IsTrue(SentOpen(guestDevice, hostDevice), "the connection was asked for");
            cancel.Cancel();

            Assert.IsTrue(joining.IsCompleted, "without waiting for a tick");
            Assert.AreEqual(RoomError.Unknown, joining.Result.Error);
            Settle();
            Assert.AreEqual(0, _world.Links, "the connection was given up");
            Assert.AreEqual(hostLog.Connected.Count, hostLog.Disconnected.Count, "the host keeps no guest from it");
            Assert.AreEqual(RoomError.Unknown, guest.JoinAsync(Code, new CancellationToken(true)).Result.Error, "and one cancelled before it starts ends at once");
        }

        [TestCase(InMemoryPeerNetwork.EosMaxPacketSize)]
        [TestCase(16)]
        public void Messages_LongerThanOnePacket_ArriveWholeAndInOrderBothWays(int maxPacket)
        {
            _world = new InMemoryPeerNetwork(maxPacket);
            Pair pair = Connect();
            var fromHost = new List<byte[]> { Bytes(5000, 1), Bytes(maxPacket, 2), Bytes(maxPacket - 1, 3), Bytes(1, 4), Bytes(0, 5), Bytes(70000, 6) };
            var fromGuest = new List<byte[]> { Bytes(Protocol.MaxClientPayloadBytes, 7), Bytes(3, 8), Bytes(200, 9) };

            foreach (byte[] message in fromHost)
            {
                pair.HostLink.Send(pair.Peer, message);
            }

            foreach (byte[] message in fromGuest)
            {
                pair.GuestLink.Send(message);
            }

            Settle();

            AssertSame(fromHost, pair.GuestLog.Messages);
            AssertSame(fromGuest, pair.HostLog.Payloads);
            Assert.Greater(_world.Sent.Count, fromHost.Count + fromGuest.Count + 1, "they took more packets than messages");
            foreach (var sent in _world.Sent)
            {
                Assert.LessOrEqual(sent.Packet.Length, maxPacket);
                Assert.AreEqual(Channel, sent.Channel);
            }

            Assert.That(_faults, Is.Empty);
        }

        [TestCase(InMemoryPeerNetwork.EosMaxPacketSize)]
        [TestCase(16)]
        public void GuestMessage_LongerThanAnyMemberSends_IsDroppedAndTheNextOneArrives(int maxPacket)
        {
            _world = new InMemoryPeerNetwork(maxPacket);
            Pair pair = Connect();
            byte[] next = Bytes(10, 10);

            pair.GuestLink.Send(Bytes(Protocol.MaxClientPayloadBytes + 1, 11));
            pair.GuestLink.Send(Bytes(100000, 12));
            pair.GuestLink.Send(next);
            Settle();

            Assert.AreEqual(1, pair.HostLog.Payloads.Count, "only the message within the limit");
            CollectionAssert.AreEqual(next, pair.HostLog.Payloads[0]);
            Assert.AreEqual(0, pair.HostLog.Disconnected.Count, "the guest is not dropped for it here (the room decides)");
        }

        [Test]
        public void Disconnect_Guest_TheGuestHearsItOnceAndSoDoesTheHost()
        {
            Pair pair = Connect();

            pair.HostLink.Disconnect(pair.Peer);
            Settle();
            pair.GuestLink.Send(Bytes(5, 13));
            pair.HostLink.Send(pair.Peer, Bytes(5, 14));
            pair.HostLink.Disconnect(pair.Peer);
            Settle();

            CollectionAssert.AreEqual(new[] { "closed" }, pair.GuestLog.Events);
            Assert.IsFalse(pair.GuestLink.IsConnected);
            CollectionAssert.AreEqual(new[] { "connected 1", "disconnected 1" }, pair.HostLog.Events, "nothing comes from a connection that is gone");
            Assert.AreEqual(0, _world.Links);
        }

        [Test]
        public void ConnectionLost_EachSideGetsWhatCameBeforeTheDropThenTheDrop()
        {
            _world = new InMemoryPeerNetwork(64);
            Pair pair = Connect();
            pair.HostLink.Send(pair.Peer, Bytes(20, 15));
            pair.HostLink.Send(pair.Peer, Bytes(500, 16));
            pair.GuestLink.Send(Bytes(30, 17));
            pair.GuestLink.Send(Bytes(200, 18));

            _world.Pump();
            _world.Break(pair.HostDevice, pair.GuestDevice);
            Settle();

            CollectionAssert.AreEqual(new[] { "received 20", "received 500", "closed" }, pair.GuestLog.Events);
            CollectionAssert.AreEqual(new[] { "connected 1", "received 1: 30", "received 1: 200", "disconnected 1" }, pair.HostLog.Events);
            Assert.IsFalse(pair.GuestLink.IsConnected);
        }

        [Test]
        public void ConnectionLost_ThenTheLinkIsDisposed_ClosedIsNotRaisedAgain()
        {
            Pair pair = Connect();

            _world.Break(pair.HostDevice, pair.GuestDevice);
            Settle();
            pair.GuestLink.Dispose();
            Settle();

            CollectionAssert.AreEqual(new[] { "closed" }, pair.GuestLog.Events);
        }

        [Test]
        public void ListingLost_TheHostListsTheCodeAgain()
        {
            Pair pair = Connect();

            _world.LoseListing(Code);
            Settle();

            Assert.AreEqual(pair.HostDevice.Address, _world.ListedBy(Code));
            Assert.IsTrue(_world.AreConnected(pair.HostDevice, pair.GuestDevice, Channel), "the guests' connections do not hang on the listing");
            Assert.AreEqual(0, pair.HostLog.Disconnected.Count);
        }

        [Test]
        public void ListingLost_DirectoryUnreachable_TheHostTriesAgainEveryFewSecondsUntilItIsBack()
        {
            Pair pair = Connect();
            int before = pair.HostDevice.ListCalls;
            pair.HostDevice.DirectoryError = RoomError.Offline;

            _world.LoseListing(Code);
            Settle((3 * PeerRoomTransport.RelistSeconds) - 1);
            int tries = pair.HostDevice.ListCalls - before;
            Assert.IsNull(_world.ListedBy(Code));
            Assert.AreEqual(3, tries, "at once, then every few seconds, not every frame");

            pair.HostDevice.DirectoryError = RoomError.None;
            Settle(PeerRoomTransport.RelistSeconds);
            Assert.AreEqual(pair.HostDevice.Address, _world.ListedBy(Code));
            int listed = pair.HostDevice.ListCalls;
            Settle(10);
            Assert.AreEqual(listed, pair.HostDevice.ListCalls, "and stops once it is listed");
        }

        [Test]
        public void ListingLost_AGuestThatDroppedReconnectsOnceTheHostIsListedAgain()
        {
            Pair pair = Connect();
            pair.HostDevice.DirectoryError = RoomError.Offline;
            _world.LoseListing(Code);
            _world.Break(pair.HostDevice, pair.GuestDevice);
            Settle();
            Assert.AreEqual(1, pair.GuestLog.Closed);
            Assert.AreEqual(RoomError.NotFound, Join(pair.Guest).Error, "nobody lists the code while the host cannot, however long the guest looks");

            pair.HostDevice.DirectoryError = RoomError.None;
            Settle(PeerRoomTransport.RelistSeconds);
            ClientLinkResult back = Join(pair.Guest);

            Assert.IsTrue(back.Succeeded, "found again");
            CollectionAssert.AreEqual(new[] { "connected 1", "disconnected 1", "connected 2" }, pair.HostLog.Events, "under a new peer id");
            back.Link.Send(Bytes(4, 19));
            Settle();
            Assert.AreEqual("received 2: 4", pair.HostLog.Events[3]);
        }

        [Test]
        public void ListingLost_WhileTheListingIsOnItsWay_IsListedAgain()
        {
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device device);
            Task<HostLinkResult> hosting = host.HostAsync(Code, CancellationToken.None);
            for (int i = 0; i < 10 && device.ListCalls == 0; i++)
            {
                _world.Pump();
                host.Tick(0.1f, _now);
            }

            // Listed, and lost again, before the transport heard back from the directory.
            _world.Pump();
            Assert.AreEqual(device.Address, _world.ListedBy(Code));
            _world.LoseListing(Code);
            _world.Pump();
            Assert.IsFalse(hosting.IsCompleted);
            Settle();

            Assert.IsTrue(hosting.Result.Succeeded);
            Assert.AreEqual(device.Address, _world.ListedBy(Code));
        }

        [Test]
        public void HostLinkDisposed_TakesTheListingDownAndClosesEveryGuest()
        {
            Pair pair = Connect();

            pair.HostLink.Dispose();
            Settle();

            Assert.IsNull(_world.ListedBy(Code));
            CollectionAssert.AreEqual(new[] { "closed" }, pair.GuestLog.Events);
            CollectionAssert.AreEqual(new[] { "connected 1" }, pair.HostLog.Events, "a disposed link raises nothing");
            Assert.AreEqual(0, _world.Links);
            Assert.AreEqual(RoomError.NotFound, Join(NewTransport(out _)).Error, "nobody finds the room any more");
        }

        [Test]
        public void TransportDisposed_EndsWhatIsUnderWayAndLetsGoOfEverything()
        {
            Pair pair = Connect();
            _world.HoldAnswers = true;
            PeerRoomTransport other = NewTransport(out _);
            Task<HostLinkResult> hosting = other.HostAsync("GHJKLM", CancellationToken.None);
            Task<ClientLinkResult> joining = other.JoinAsync(Code, CancellationToken.None);
            Settle();

            other.Dispose();
            pair.Host.Dispose();
            _world.HoldAnswers = false;
            Settle();

            Assert.AreEqual(RoomError.Unknown, hosting.Result.Error);
            Assert.AreEqual(RoomError.Unknown, joining.Result.Error);
            Assert.IsNull(_world.ListedBy(Code));
            Assert.IsNull(_world.ListedBy("GHJKLM"));
            Assert.AreEqual(1, pair.GuestLog.Closed);
            Assert.AreEqual(0, _world.Links);
            Assert.AreEqual(RoomError.Unknown, other.HostAsync(Code, CancellationToken.None).Result.Error, "a disposed transport hosts nothing");
        }

        [Test]
        public void HostAgain_LetsGoOfTheRoomHostedBefore()
        {
            Pair pair = Connect();

            Task<HostLinkResult> next = pair.Host.HostAsync("GHJKLM", CancellationToken.None);
            Settle();

            Assert.IsTrue(next.Result.Succeeded);
            Assert.IsNull(_world.ListedBy(Code));
            Assert.AreEqual(pair.HostDevice.Address, _world.ListedBy("GHJKLM"));
            Assert.AreEqual(1, pair.GuestLog.Closed);
        }

        [Test]
        public void JoinAgain_ReplacesTheConnectionMadeBefore()
        {
            Pair pair = Connect();

            ClientLinkResult again = Join(pair.Guest);

            Assert.IsTrue(again.Succeeded);
            Assert.IsFalse(pair.GuestLink.IsConnected);
            Assert.That(pair.GuestLog.Events, Is.Empty, "the link given up raises nothing");
            CollectionAssert.AreEqual(new[] { "connected 1", "disconnected 1", "connected 2" }, pair.HostLog.Events);
            Assert.IsTrue(again.Link.IsConnected);
        }

        [Test]
        public void HandlerThatThrows_IsReportedAndTheNoticesAfterItStillCome()
        {
            PeerRoomTransport host = NewTransport(out _);
            IHostLink hostLink = Host(host).Link;
            var hostLog = new HostLog(hostLink);
            hostLink.PeerConnected += _ => throw new InvalidOperationException("a room handler failed");
            PeerRoomTransport guest = NewTransport(out _);
            Task<ClientLinkResult> joining = guest.JoinAsync(Code, CancellationToken.None);
            for (int i = 0; i < 20 && !joining.IsCompleted; i++)
            {
                _world.Pump();
                guest.Tick(0.1f, _now);
            }

            IClientLink link = joining.Result.Link;
            var guestLog = new ClientLog(link);
            link.Received += _ => throw new InvalidOperationException("a member handler failed");
            link.Send(Bytes(8, 20));
            _world.Pump();
            host.Tick(0.1f, _now);

            CollectionAssert.AreEqual(new[] { "connected 1", "received 1: 8" }, hostLog.Events, "the message after the failed notice arrived in the same tick");
            Assert.AreEqual(1, _faults.Count);

            PeerId peer = hostLog.Connected[0];
            hostLink.Send(peer, Bytes(3, 21));
            hostLink.Send(peer, Bytes(4, 22));
            hostLink.Disconnect(peer);
            Settle();
            CollectionAssert.AreEqual(new[] { "received 3", "received 4", "closed" }, guestLog.Events);
            Assert.AreEqual(3, _faults.Count, "each failure is reported");
        }

        [Test]
        public void OpenPacket_GoesOverTheNetworkButNeverReachesTheRoom()
        {
            Pair pair = Connect();

            List<byte[]> fromGuest = PacketsFrom(pair.GuestDevice);
            Assert.AreEqual(1, fromGuest.Count, "the guest sent one packet to connect");
            CollectionAssert.AreEqual(PacketFramer.OpenPacket(), fromGuest[0]);
            CollectionAssert.AreEqual(new[] { "connected 1" }, pair.HostLog.Events, "and the room heard nothing but the guest connect");

            pair.GuestLink.Send(new byte[] { 0 });
            Settle();
            CollectionAssert.AreEqual(new[] { "connected 1", "received 1: 1" }, pair.HostLog.Events, "a message that starts like it is still a message");
        }

        [Test]
        public void OpenAgainOverALiveConnection_IsANewPeerInPlaceOfTheOld()
        {
            Pair pair = Connect();

            // The guest's app started again and opened its connection anew before the host noticed the old one was gone.
            pair.GuestDevice.Send(pair.HostDevice.Address, Channel, PacketFramer.OpenPacket());
            Settle();
            pair.HostLink.Send(pair.Peer, Bytes(6, 23));
            pair.HostLink.Send(new PeerId(2), Bytes(7, 24));
            Settle();

            CollectionAssert.AreEqual(new[] { "connected 1", "disconnected 1", "connected 2" }, pair.HostLog.Events);
            CollectionAssert.AreEqual(new[] { "received 7" }, pair.GuestLog.Events, "only the new peer is reached");
        }

        [Test]
        public void Open_HostsWithoutAListing_AndAGuestJoinsAtItsAddress()
        {
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device hostDevice);
            PeerRoomTransport guest = NewTransport(out InMemoryPeerNetwork.Device guestDevice);

            Task<HostLinkResult> opening = host.OpenAsync(Code, CancellationToken.None);
            Settle();
            var hostLog = new HostLog(opening.Result.Link);
            Task<ClientLinkResult> joining = guest.JoinAtAsync(Code, hostDevice.Address, CancellationToken.None);
            Settle();
            joining.Result.Link.Send(Bytes(5, 30));
            Settle();

            Assert.IsTrue(opening.Result.Succeeded);
            Assert.IsNull(_world.ListedBy(Code), "nothing is listed under the code");
            Assert.AreEqual(0, hostDevice.ListCalls);
            Assert.IsTrue(joining.Result.Succeeded);
            Assert.AreEqual(0, guestDevice.LookUpCalls, "nothing was looked up");
            CollectionAssert.AreEqual(new[] { "connected 1", "received 1: 5" }, hostLog.Events);
            Assert.AreEqual(hostDevice.Address, host.LocalAddress);
        }

        [Test]
        public void JoinAt_DeviceThatHostsNoSuchRoom_IsNotFoundAtOnce()
        {
            NewTransport(out InMemoryPeerNetwork.Device other);
            PeerRoomTransport guest = NewTransport(out _);

            Task<ClientLinkResult> joining = guest.JoinAtAsync(Code, other.Address, CancellationToken.None);
            Settle(0.5);

            Assert.IsTrue(joining.IsCompleted, "turned down at once");
            Assert.AreEqual(RoomError.NotFound, joining.Result.Error);
            Assert.AreEqual(0, _world.Links);
        }

        [Test]
        public void JoinAt_ThisDevicesOwnAddressOrNone_IsNotFound()
        {
            PeerRoomTransport device = NewTransport(out InMemoryPeerNetwork.Device me);

            Task<ClientLinkResult> own = device.JoinAtAsync(Code, me.Address, CancellationToken.None);
            Settle();

            Assert.AreEqual(RoomError.NotFound, own.Result.Error, "a device never connects to itself");
            Assert.AreEqual(RoomError.NotFound, device.JoinAtAsync(Code, null, CancellationToken.None).Result.Error);
        }

        [Test]
        public void ListingLost_OfAListingThatIsNotTheTransports_IsPassedOnAndNotListedAgain()
        {
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device device);
            var lost = new List<string>();
            host.ListingLost += lost.Add;
            Task<HostLinkResult> opening = host.OpenAsync(Code, CancellationToken.None);
            Settle();
            Assert.IsTrue(opening.Result.Succeeded);
            device.ListQuickAsync(Code, 3, 0, CancellationToken.None);
            Settle();

            _world.LoseQuickListing(Code);
            Settle();

            CollectionAssert.AreEqual(new[] { Code }, lost, "whoever listed it hears of it");
            Assert.AreEqual(0, device.ListCalls, "the transport lists nothing of its own");
        }

        [Test]
        public void ListingLost_OfTheTransportsOwnRoom_IsNotPassedOn()
        {
            PeerRoomTransport host = NewTransport(out _);
            var lost = new List<string>();
            host.ListingLost += lost.Add;
            Assert.IsTrue(Host(host).Succeeded);

            _world.LoseListing(Code);
            Settle();

            Assert.That(lost, Is.Empty, "the transport lists its room again itself");
            Assert.IsNotNull(_world.ListedBy(Code));
        }

        private static void AssertSame(List<byte[]> sent, List<byte[]> received)
        {
            Assert.AreEqual(sent.Count, received.Count, "every message arrived, once");
            for (int i = 0; i < sent.Count; i++)
            {
                CollectionAssert.AreEqual(sent[i], received[i], "message " + i);
            }
        }

        private static byte[] Bytes(int length, ulong seed)
        {
            var rng = new Pcg32Random(seed);
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = (byte)rng.NextInt(256);
            }

            return bytes;
        }

        private PeerRoomTransport NewTransport(out InMemoryPeerNetwork.Device device)
        {
            device = _world.AddDevice();
            var transport = new PeerRoomTransport(device, _faults.Add);
            _transports.Add(transport);
            return transport;
        }

        private List<byte[]> PacketsFrom(InMemoryPeerNetwork.Device device)
        {
            var packets = new List<byte[]>();
            foreach (var sent in _world.Sent)
            {
                if (sent.From == device.Address)
                {
                    packets.Add(sent.Packet);
                }
            }

            return packets;
        }

        /// <summary>True once <paramref name="from"/> sent <paramref name="to"/> the packet that opens a connection.</summary>
        private bool SentOpen(InMemoryPeerNetwork.Device from, InMemoryPeerNetwork.Device to)
        {
            foreach (var sent in _world.Sent)
            {
                if (sent.From == from.Address && sent.To == to.Address && sent.Packet.Length == 1 && sent.Packet[0] == (byte)PacketType.Open)
                {
                    return true;
                }
            }

            return false;
        }

        private HostLinkResult Host(PeerRoomTransport transport, string code = Code)
        {
            Task<HostLinkResult> hosting = transport.HostAsync(code, CancellationToken.None);
            for (int i = 0; i < 600 && !hosting.IsCompleted; i++)
            {
                Settle();
            }

            Assert.IsTrue(hosting.IsCompleted, "hosting was answered");
            return hosting.Result;
        }

        private ClientLinkResult Join(PeerRoomTransport transport, string code = Code)
        {
            Task<ClientLinkResult> joining = transport.JoinAsync(code, CancellationToken.None);
            for (int i = 0; i < 600 && !joining.IsCompleted; i++)
            {
                Settle();
            }

            Assert.IsTrue(joining.IsCompleted, "joining was answered");
            return joining.Result;
        }

        /// <summary>A host with its room listed, and one guest connected to it.</summary>
        private Pair Connect()
        {
            PeerRoomTransport host = NewTransport(out InMemoryPeerNetwork.Device hostDevice);
            PeerRoomTransport guest = NewTransport(out InMemoryPeerNetwork.Device guestDevice);
            HostLinkResult hosted = Host(host);
            Assert.IsTrue(hosted.Succeeded, "hosted");
            var hostLog = new HostLog(hosted.Link);
            ClientLinkResult joined = Join(guest);
            Assert.IsTrue(joined.Succeeded, "joined");
            Assert.AreEqual(1, hostLog.Connected.Count, "the host heard the guest connect");
            return new Pair(host, hostDevice, hosted.Link, hostLog, guest, guestDevice, joined.Link, new ClientLog(joined.Link), hostLog.Connected[0]);
        }

        /// <summary>Time passes in tenths of a second, for the network too; in each, it settles and every transport takes its share.</summary>
        private void Settle(double seconds = 0.1)
        {
            double until = _now + seconds;
            do
            {
                _now = Math.Min(until, _now + 0.1);
                _world.Now = _now;
                for (int i = 0; ; i++)
                {
                    Assert.Less(i, 1000, "the network never settles");
                    _world.Pump();
                    foreach (PeerRoomTransport transport in _transports)
                    {
                        transport.Tick(0.1f, _now);
                    }

                    if (_world.IsQuiet)
                    {
                        break;
                    }
                }
            }
            while (_now < until);
        }

        private sealed class Pair
        {
            public Pair(
                PeerRoomTransport host,
                InMemoryPeerNetwork.Device hostDevice,
                IHostLink hostLink,
                HostLog hostLog,
                PeerRoomTransport guest,
                InMemoryPeerNetwork.Device guestDevice,
                IClientLink guestLink,
                ClientLog guestLog,
                PeerId peer)
            {
                Host = host;
                HostDevice = hostDevice;
                HostLink = hostLink;
                HostLog = hostLog;
                Guest = guest;
                GuestDevice = guestDevice;
                GuestLink = guestLink;
                GuestLog = guestLog;
                Peer = peer;
            }

            public PeerRoomTransport Host { get; }

            public InMemoryPeerNetwork.Device HostDevice { get; }

            public IHostLink HostLink { get; }

            public HostLog HostLog { get; }

            public PeerRoomTransport Guest { get; }

            public InMemoryPeerNetwork.Device GuestDevice { get; }

            public IClientLink GuestLink { get; }

            public ClientLog GuestLog { get; }

            /// <summary>The guest as the host knows it.</summary>
            public PeerId Peer { get; }
        }

        /// <summary>What a host link told the room, in order.</summary>
        private sealed class HostLog
        {
            public HostLog(IHostLink link)
            {
                link.PeerConnected += peer =>
                {
                    Events.Add("connected " + peer.Value);
                    Connected.Add(peer);
                };
                link.PeerDisconnected += peer =>
                {
                    Events.Add("disconnected " + peer.Value);
                    Disconnected.Add(peer);
                };
                link.Received += (peer, payload) =>
                {
                    Events.Add("received " + peer.Value + ": " + payload.Length);
                    Payloads.Add(payload);
                };
            }

            public List<string> Events { get; } = new List<string>();

            public List<PeerId> Connected { get; } = new List<PeerId>();

            public List<PeerId> Disconnected { get; } = new List<PeerId>();

            public List<byte[]> Payloads { get; } = new List<byte[]>();
        }

        /// <summary>What a client link told its member, in order.</summary>
        private sealed class ClientLog
        {
            public ClientLog(IClientLink link)
            {
                link.Received += payload =>
                {
                    Events.Add("received " + payload.Length);
                    Messages.Add(payload);
                };
                link.Closed += () =>
                {
                    Events.Add("closed");
                    Closed++;
                };
            }

            public List<string> Events { get; } = new List<string>();

            public List<byte[]> Messages { get; } = new List<byte[]>();

            public int Closed { get; private set; }
        }
    }
}
