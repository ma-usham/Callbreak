using System;
using System.Threading;
using Callbreak.Application.Online;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Match;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// A screen that throws while it handles a message never takes a room down (GDD L3005-3020: the
    /// failure modes assume a failure somebody notices). The host's own player runs in the same
    /// call as the room, so whatever its screens do, the table goes on flushing to everyone and
    /// timing their turns; and a member's own state follows the host whatever its screens do.
    /// </summary>
    public sealed class HandlerFaultTests
    {
        [Test]
        public void HostsOwnSession_ThrowsFromEveryEvent_TheTableStillReachesEveryoneAndTheMatchEnds()
        {
            var room = new RoomFixture(turnSeconds: 15);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            room.CatchFaults = true;
            room.HostMember.Session.EventReceived += _ => throw new InvalidOperationException("a table presenter failed");

            // Only the guest plays: the host's player is timed out, then played by a bot.
            for (int i = 0; i < 40000 && !room.Host.Table.IsOver; i++)
            {
                room.Step();
                room.Run(() => RoomFixture.Act(guest.Session));
                room.Network.Pump();
            }

            room.Step();
            Assert.IsTrue(room.Host.Table.IsOver, "the match finished");
            Assert.Greater(room.Faults.Count, 0, "the handler did throw");
            Assert.AreEqual(SessionStatus.Ended, guest.Session.Status);
            Assert.AreEqual(room.Host.Table.ViewFor(guest.Session.LocalSeat), guest.Session.View);
            Assert.AreEqual(SessionStatus.Ended, room.HostMember.Session.Status, "the host's own session followed the table to its end");
            Assert.AreEqual(room.Host.Table.ViewFor(Seat.South), room.HostMember.Session.View);
            Assert.AreEqual(1, room.HostMember.Stats.Current.gamesPlayed, "and recorded it");
        }

        [Test]
        public void GuestsSession_ThrowsFromAnEvent_ItsStateFollowsTheHostAndTheTableIsRebuilt()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            room.CatchFaults = true;
            int resyncs = guest.Resyncs;
            bool thrown = false;
            guest.Session.EventReceived += e =>
            {
                if (!thrown && e is Core.Events.CardPlayed)
                {
                    thrown = true;
                    throw new InvalidOperationException("a table presenter failed");
                }
            };

            for (int i = 0; i < 40000 && !room.Host.Table.IsOver; i++)
            {
                room.Step();
                foreach (RoomFixture.Member m in room.Members)
                {
                    room.Run(() => RoomFixture.Act(m.Session));
                }

                room.Network.Pump();
            }

            room.Step();
            Assert.IsTrue(thrown);
            Assert.AreEqual(resyncs + 1, guest.Resyncs, "the table was rebuilt from the whole state once");
            Assert.AreEqual(SessionStatus.Ended, guest.Session.Status);
            Assert.AreEqual(1, guest.Stats.Current.gamesPlayed);
            Assert.AreEqual(room.Host.Table.ViewFor(guest.Session.LocalSeat), guest.Session.View);
        }

        [Test]
        public void GuestsLobby_ThrowsWhenItChanges_TheJoinStillCompletes()
        {
            var room = new RoomFixture();
            room.CatchFaults = true;
            IClientLink link = room.Network.JoinAsync(RoomFixture.Code, CancellationToken.None).Result.Link;
            var saves = new InMemorySaveStore();
            var client = new RoomClient(
                link, new HelloMessage(Protocol.Version, "player-asha", "Asha", 1, "classic-crimson"), null, MatchMode.PrivateRoom,
                new StatsService(saves), new SettingsService(saves));
            client.Changed += () => throw new InvalidOperationException("a lobby presenter failed");

            var join = client.JoinAsync(CancellationToken.None);
            room.Network.Pump();

            Assert.IsTrue(join.IsCompleted, "the join was answered");
            Assert.AreEqual(RoomError.None, join.Result);
            Assert.AreEqual(1, client.LocalSeatIndex);
            Assert.AreEqual("Asha", room.HostMember.Client.Seats[1].Name);
        }

        [Test]
        public void GuestsSession_ThrowsWhenTheRoomCloses_TheRoomStillReportsItsEnd()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            room.CatchFaults = true;
            RoomError closed = RoomError.None;
            guest.Client.Closed += reason => closed = reason;
            guest.Session.StatusChanged += () => throw new InvalidOperationException("a table presenter failed");

            room.HostMember.Client.LeaveAsync(CancellationToken.None);
            room.Network.Pump();

            Assert.AreEqual(RoomError.HostLeft, closed, "the room says it is over, so the app lets it go");
            Assert.AreEqual(SessionStatus.Failed, guest.Session.Status);
        }
    }
}
