using System.Threading;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §27 failure modes (L3010: "The host left — the match has ended"; back to the menu, the
    /// match not recorded): a guest who is reconnecting when the host goes learns it from the relay
    /// no longer knowing the room, well before the seat hold would run out.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [Test]
        public void Match_HostLeavesWhileTheGuestIsReconnecting_TheGuestIsToldTheHostLeftLongBeforeTheHoldEnds()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);
            var session = (OnlineMatchSession)guest.Rooms.Current.Match;
            Play(host, guest, 20);

            _network.Drop(LinkOf(guest));
            host.Rooms.Current.LeaveAsync(CancellationToken.None);
            Settle(30);

            Assert.AreEqual(SessionStatus.Failed, session.Status, "the guest is not left on Reconnecting");
            Assert.AreEqual(RoomError.HostLeft, session.FailureReason, "and the table can say the host left");
            Assert.IsNull(guest.Rooms.Current);
            Assert.AreEqual(0, guest.Stats.Current.gamesPlayed, "the match is not recorded");
        }
    }
}
