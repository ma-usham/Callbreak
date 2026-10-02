using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// The host's side of a Quick Match table (ADR 029): nobody needs to say ready; a bot sits down
    /// in the lobby where every member sees it, and plays that seat under the same name; a locked
    /// table turns newcomers away as full and tells every member when the match starts. A private
    /// room does none of this.
    /// </summary>
    public sealed class QuickTableHostTests
    {
        [Test]
        public void SeatBot_TakesTheFirstEmptySeat_AndEveryMemberSeesAReadyPlayer()
        {
            var room = new RoomFixture(quickTable: true);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");

            bool seated = room.Host.SeatBot();
            room.Network.Pump();

            Assert.IsTrue(seated);
            Assert.AreEqual(1, room.Host.FreeSeats);
            Assert.AreEqual(1, room.Host.Guests, "a bot is no guest");
            foreach (RoomFixture.Member m in room.Members)
            {
                RoomSeat bot = m.Client.Seats[2];
                Assert.IsTrue(bot.Occupied, m.Name + " sees the bot");
                Assert.IsTrue(bot.IsBot);
                Assert.IsTrue(bot.IsReady, "ready, like everyone at a Quick Match table");
                Assert.AreNotEqual("Hari", bot.Name);
                Assert.AreNotEqual("Asha", bot.Name);
                Assert.IsFalse(m.Client.Seats[3].Occupied);
            }

            Assert.IsTrue(guest.Client.Seats[1].IsReady, "the guest never said it, and is ready all the same");
        }

        [Test]
        public void SeatBot_FullTableOrPrivateRoom_SeatsNobody()
        {
            var room = new RoomFixture(quickTable: true);
            room.Join("p1", "Asha");
            room.Join("p2", "Bikash");
            room.Join("p3", "Suman");
            var privateRoom = new RoomFixture();

            Assert.IsFalse(room.Host.SeatBot(), "no seat is free");
            Assert.IsFalse(privateRoom.Host.SeatBot(), "a private room has no bots in its lobby");
            Assert.AreEqual(3, privateRoom.Host.FreeSeats);
        }

        [Test]
        public void StartMatch_LobbyBotsPlayTheirSeatsUnderTheNamesTheyShowed_AndNobodyHadToSayReady()
        {
            var room = new RoomFixture(quickTable: true);
            room.Join("player-asha", "Asha");
            room.Host.SeatBot();
            room.Host.SeatBot();
            room.Network.Pump();
            var names = new List<string> { room.HostMember.Client.Seats[2].Name, room.HostMember.Client.Seats[3].Name };

            bool started = room.HostMember.Client.StartAsync(false, CancellationToken.None).Result;
            room.Network.Pump();

            Assert.IsTrue(started, "four seats taken, nobody said ready, and it starts");
            Assert.IsFalse(room.Host.Table.IsHuman(Seat.North));
            Assert.IsFalse(room.Host.Table.IsHuman(Seat.West));
            Assert.IsTrue(room.Host.Table.IsHuman(Seat.East));
            for (int s = 0; s < 2; s++)
            {
                Assert.AreEqual(names[s], room.HostMember.Session.View.Seats[2 + s].Name, "the bot plays under the name it showed");
                Assert.IsTrue(room.HostMember.Session.View.Seats[2 + s].IsBot);
            }
        }

        [Test]
        public void Lock_NewPlayerIsTurnedAwayAsFull_AndEveryMemberLearnsWhenItStarts()
        {
            var room = new RoomFixture(quickTable: true);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.Step(2.0);

            room.Host.Lock(room.Now + 3.0);
            room.Network.Pump();
            room.Join("player-suman", "Suman", out RoomError error);

            Assert.IsTrue(room.Host.IsLocked);
            Assert.AreEqual(RoomError.Full, error, "seats are free, but the table is locked");
            Assert.AreEqual(room.Now + 3.0, guest.Client.StartsAt, 0.001, "the guest learned when it starts");
            Assert.AreEqual(room.Now + 3.0, room.HostMember.Client.StartsAt, 0.001, "and so did the host's own player");
        }

        [Test]
        public void PrivateRoom_LockDoesNothing_AndTheLobbyNeverSaysWhenItStarts()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("player-asha", "Asha");

            room.Host.Lock(room.Now + 3.0);
            room.Network.Pump();
            room.Join("player-suman", "Suman", out RoomError error);

            Assert.IsFalse(room.Host.IsLocked);
            Assert.AreEqual(RoomError.None, error);
            Assert.IsTrue(double.IsNaN(guest.Client.StartsAt));
        }
    }
}
