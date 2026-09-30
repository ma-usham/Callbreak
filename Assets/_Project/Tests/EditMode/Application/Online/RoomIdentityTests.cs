using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// Who may take which seat (GDD §27 "idempotent rejoin", §11 screen 04): the host's seat is the
    /// host device's alone, a removed player stays removed, a hello from another build is told to
    /// update, and a connection id the relay hands out again never unseats anyone.
    /// </summary>
    public sealed partial class RoomIdentityTests
    {
        [Test]
        public void Hello_WithTheHostsIdentity_InTheLobby_IsRefusedAndTheHostKeepsItsSeat()
        {
            var room = new RoomFixture();
            RoomError hostClosed = RoomError.None;
            room.HostMember.Client.Closed += reason => hostClosed = reason;

            room.Join("player-host", "Mallory", out RoomError error);

            Assert.AreNotEqual(RoomError.None, error, "a remote hello never binds to the host's seat");
            Assert.AreEqual(RoomError.None, hostClosed, "the host's own player is not replaced");
            Assert.IsFalse(room.HostMember.Client.IsClosed);
            Assert.AreEqual("Hari", room.HostMember.Client.Seats[0].Name);
        }

        [Test]
        public void Hello_WithTheHostsIdentity_InAMatch_IsRefusedAndTheHostPlaysOn()
        {
            var room = new RoomFixture();
            room.Join("player-asha", "Asha");
            room.StartMatch();

            room.Join("player-host", "Mallory", out RoomError error);
            room.Step();

            Assert.AreNotEqual(RoomError.None, error);
            Assert.IsFalse(room.HostMember.Client.IsClosed, "the room is not closed from outside");
            Assert.AreEqual(SessionStatus.Running, room.HostMember.Session.Status);
            Assert.AreEqual(SeatConnection.Connected, room.Host.Table.StatusAt(room.Now).ConnectionOf(Seat.South));
        }
    }
}
