using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §8 "back within 90 s: full state restored": a player whose match ended while they were
    /// reconnecting comes back to its end (the final results, recorded like everyone else's) and to
    /// the room, never to a "Reconnecting" cover that nothing lifts.
    /// </summary>
    public sealed class LateReturnTests
    {
        [Test]
        public void BackAfterTheMatchEnded_WithinTheHold_SeesTheEndAndHasItRecorded()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            PlayUntilLastRound(room);

            room.Network.Drop(guest.Link);
            room.Network.Pump();
            Assert.AreEqual(SessionStatus.Reconnecting, guest.Session.Status);
            room.PlayToEnd();
            room.Step();
            Assert.IsTrue(room.Host.Table.IsOver, "a bot finished the match for the guest");

            Assert.AreEqual(RoomError.None, room.Rejoin(guest), "back in the room");
            room.Step();

            Assert.AreEqual(SessionStatus.Ended, guest.Session.Status, "the table shows the end, not Reconnecting");
            Assert.AreEqual(Phase.MatchOver, guest.Session.View.Phase);
            Assert.AreEqual(4, guest.Session.View.FinalStandings.Count);
            Assert.AreEqual(1, guest.Stats.Current.gamesPlayed, "the match is recorded");
            Assert.AreEqual(guest.Session.View.Rules.Rounds, guest.Stats.Current.roundsPlayed, "as a finished one, not a left one");
            Assert.IsTrue(guest.Client.Seats[guest.Client.LocalSeatIndex].Occupied, "and the guest is in the room again, for Play Again");
        }

        [Test]
        public void BackAfterTheMatchEnded_AndTheNextOneStarted_StillSeesTheEndOfItsOwn()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            PlayUntilLastRound(room);
            room.Network.Drop(guest.Link);
            room.Network.Pump();
            room.PlayToEnd();
            room.Step();
            Assert.IsTrue(room.HostMember.Client.StartAsync(true, System.Threading.CancellationToken.None).Result, "the host plays again with bots");
            room.Network.Pump();

            room.Rejoin(guest);
            room.Step();

            Assert.AreEqual(SessionStatus.Ended, guest.Session.Status);
            Assert.AreEqual(Phase.MatchOver, guest.Session.View.Phase);
            Assert.AreEqual(1, guest.Stats.Current.gamesPlayed);
            Assert.AreEqual(guest.Session.View.Rules.Rounds, guest.Stats.Current.roundsPlayed);
        }

        [Test]
        public void BackAfterTheMatchEnded_AndAfterTheHold_TheSessionEndsAsLeftInsteadOfWaiting()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            PlayUntilLastRound(room);
            room.Network.Drop(guest.Link);
            room.Network.Pump();
            room.PlayToEnd();
            room.Step(Protocol.SeatHoldSeconds + 1.0);

            room.Rejoin(guest);
            room.Step();

            Assert.AreEqual(SessionStatus.Failed, guest.Session.Status, "no Reconnecting cover that nothing lifts");
            Assert.IsTrue(guest.Client.IsClosed, "the room lets the app go back to the menu");
            Assert.AreEqual(1, guest.Stats.Current.gamesPlayed, "not back within the hold: the match counts as left");
            Assert.AreEqual(0, guest.Stats.Current.roundsPlayed);
        }

        private static void PlayUntilLastRound(RoomFixture room)
        {
            int last = room.Host.Table.ViewFor(Seat.South).Rules.Rounds;
            for (int i = 0; i < 20000 && room.Host.Table.ViewFor(Seat.South).Round < last; i++)
            {
                room.Step();
                foreach (RoomFixture.Member m in room.Members)
                {
                    RoomFixture.Act(m.Session);
                }

                room.Network.Pump();
            }

            Assert.AreEqual(last, room.Host.Table.ViewFor(Seat.South).Round, "the last round was dealt");
        }
    }
}
