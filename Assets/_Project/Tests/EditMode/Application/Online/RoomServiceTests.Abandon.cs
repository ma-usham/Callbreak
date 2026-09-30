using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §8 L660-668 and L689-690: not back within the seat hold, a player is recorded as having
    /// left, a loss that sets no best score, exactly as if they had tapped Leave. Pulling the plug
    /// gains nothing over leaving.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [Test]
        public void Match_GuestNotBackWithinTheSeatHold_CountsAsLeftLikeTappingLeave()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);
            IMatchSession session = guest.Rooms.Current.Match;
            Play(host, guest, 20);

            // The guest's connection goes and does not come back in time.
            guest.Network.IsOnline = false;
            _network.Drop(LinkOf(guest));
            Settle(Protocol.SeatHoldSeconds + 5);

            Assert.AreEqual(SessionStatus.Failed, session.Status, "the room is gone for the guest");
            Assert.AreEqual(1, guest.Stats.Current.gamesPlayed, "the match counts as played");
            Assert.AreEqual(0, guest.Stats.Current.wins, "and lost");
            Assert.IsFalse(guest.Stats.Current.hasBest, "and can't set a best score");
        }

        [Test]
        public void Match_Guest_TheTicketNamesTheMatch()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);

            StartMatch(host, guest);

            RejoinTicket ticket = guest.Saves.Peek<RejoinTicket>(RejoinStore.Key);
            Assert.AreEqual(guest.Rooms.Current.Match.View.MatchId, ticket.matchId);
        }

        [Test]
        public void AppKilled_BackAfterTheSeatHoldAndRefused_GivingTheTicketUpCountsAsLeft()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");
            JoinRoom(host, guest);
            StartMatch(host, guest);
            Kill(guest);
            Settle(Protocol.SeatHoldSeconds + 2);

            Device back = NewDevice("Asha", guest.Saves);
            Assert.IsTrue(back.Tickets.TryGet(out string code));
            Task<RoomResult> rejoining = back.Rooms.JoinRoomAsync(code, CancellationToken.None);
            Settle();
            Assert.AreEqual(RoomError.AlreadyStarted, rejoining.Result.Error);

            // What the "Rejoin match" offer does with a refused ticket.
            back.Tickets.Abandon();

            Assert.AreEqual(1, back.Stats.Current.gamesPlayed, "not back within the hold: the match counts as left");
            Assert.AreEqual(0, back.Stats.Current.wins);
            Assert.IsFalse(back.Tickets.TryGet(out _));
        }
    }
}
