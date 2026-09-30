using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    public sealed partial class RoomIdentityTests
    {
        [Test]
        public void ConnectionIdGivenOutAgain_TheOtherPlayerComingBackDoesNotUnseatItsNewOwner()
        {
            var room = new RoomFixture();
            RoomFixture.Member asha = room.Join("player-asha", "Asha");
            RoomFixture.Member bikash = room.Join("player-bikash", "Bikash");
            room.StartMatch();
            Seat bikashSeat = bikash.Session.LocalSeat;
            ulong ashasOldId = PeerIdOf(room, "player-asha");

            room.Network.Drop(asha.Link);
            room.Network.Drop(bikash.Link);
            room.Network.Pump();
            room.Network.NextPeerId = ashasOldId;
            Assert.AreEqual(RoomError.None, room.Rejoin(bikash), "Bikash comes back on the id Asha had");
            Assert.AreEqual(RoomError.None, room.Rejoin(asha), "then Asha, on a new one");
            room.Step();
            room.Step(Protocol.EmoteIntervalSeconds);
            int emotes = room.HostMember.Emotes.Count;

            bikash.Session.SendEmoteAsync(1, CancellationToken.None);
            room.Network.Pump();

            Assert.AreEqual(emotes + 1, room.HostMember.Emotes.Count, "the host still hears Bikash");
            Assert.IsTrue(bikash.Link.IsConnected);
            Assert.AreEqual(SeatConnection.Connected, room.Host.Table.StatusAt(room.Now).ConnectionOf(bikashSeat));
        }

        [Test]
        public void ConnectionIdGivenOutAgain_RemovingTheDroppedPlayerNeverReachesItsNewOwner()
        {
            var room = new RoomFixture();
            RoomFixture.Member asha = room.Join("player-asha", "Asha");
            RoomFixture.Member bikash = room.Join("player-bikash", "Bikash");
            room.StartMatch();
            int ashaSeat = asha.Client.LocalSeatIndex;
            ulong ashasOldId = PeerIdOf(room, "player-asha");
            RoomError bikashClosed = RoomError.None;
            bikash.Client.Closed += reason => bikashClosed = reason;

            room.Network.Drop(asha.Link);
            room.Network.Drop(bikash.Link);
            room.Network.Pump();
            room.Network.NextPeerId = ashasOldId;
            Assert.AreEqual(RoomError.None, room.Rejoin(bikash));
            room.Host.Kick(ashaSeat);
            room.Network.Pump();
            room.Step(2.0);

            Assert.AreEqual(RoomError.None, bikashClosed, "Bikash is not told he was removed");
            Assert.IsTrue(bikash.Link.IsConnected, "and his connection is not dropped");
        }

        [Test]
        public void Kicked_SaysHelloAgainOnTheSameConnection_IsNotSeated()
        {
            var room = new RoomFixture();
            RoomFixture.Member troll = room.Join("player-troll", "Troll");
            room.HostMember.Client.KickAsync(1, CancellationToken.None);
            room.Network.Pump();

            troll.Link.Send(ClientMessages.Hello(new HelloMessage(Protocol.Version, "player-troll", "Troll", 1, "classic-crimson")));
            room.Network.Pump();

            Assert.IsFalse(room.HostMember.Client.Seats[1].Occupied, "a connection that is being dropped is not heard");
        }

        [Test]
        public void Kicked_ComesBackOnANewConnection_IsToldItWasRemoved()
        {
            var room = new RoomFixture();
            room.Join("player-troll", "Troll");
            room.HostMember.Client.KickAsync(1, CancellationToken.None);
            room.Network.Pump();
            room.Step(2.0);

            room.Join("player-troll", "Troll", out RoomError error);

            Assert.AreEqual(RoomError.Kicked, error, "removed for as long as the room lasts (GDD §11 screen 04)");
            Assert.IsFalse(room.HostMember.Client.Seats[1].Occupied);
        }

        [Test]
        public void Hello_OfAnotherVersionLaidOutDifferently_IsToldToUpdate()
        {
            var room = new RoomFixture();
            IClientLink link = room.Network.JoinAsync(RoomFixture.Code, CancellationToken.None).Result.Link;
            var answers = new List<byte[]>();
            link.Received += answers.Add;

            // A later build's hello: the kind, its version, then fields this build cannot read.
            link.Send(new byte[] { (byte)ClientMessageKind.Hello, 99, 0, 0xFF, 0xFF, 1, 2, 3 });
            room.Network.Pump();

            Assert.AreEqual(1, answers.Count, "one answer, at once");
            Assert.AreEqual((byte)HostMessageKind.Rejected, answers[0][0]);
            Assert.AreEqual((byte)RoomError.UpdateRequired, answers[0][1]);
        }

        /// <summary>The connection id the host knows the member with this identity by, read from the hello it sent.</summary>
        private static ulong PeerIdOf(RoomFixture room, string identity)
        {
            RoomFixture.Member member = null;
            foreach (RoomFixture.Member m in room.Members)
            {
                if (m.Identity == identity)
                {
                    member = m;
                }
            }

            Assert.IsNotNull(member, identity);
            return room.Network.PeerOf(member.Link).Value;
        }
    }
}
