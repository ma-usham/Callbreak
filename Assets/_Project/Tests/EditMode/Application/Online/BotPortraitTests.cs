using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Callbreak.Application.Rooms;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// ADR 041 online: a bot that sits down in a Quick Match lobby, or fills an empty seat of a
    /// private room, shows a portrait nobody at the table has. The fixture's members have avatars
    /// 0 (the host), 1, 2... in the order they join.
    /// </summary>
    public sealed class BotPortraitTests
    {
        [Test]
        public void QuickMatch_BotsInTheLobby_ShowPortraitsNobodyElseHas()
        {
            var room = new RoomFixture(quickTable: true);
            room.Join("player-asha", "Asha");

            room.Host.SeatBot();
            room.Host.SeatBot();
            room.Network.Pump();

            foreach (RoomFixture.Member m in room.Members)
            {
                List<int> faces = m.Client.Seats.Select(s => s.AvatarId).ToList();
                Assert.AreEqual(4, faces.Distinct().Count(), m.Name + " sees " + string.Join(",", faces));
            }
        }

        [Test]
        public void PrivateRoom_BotsThatFillTheTable_ShowPortraitsNobodyElseHas()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            guest.Client.SetReadyAsync(true, CancellationToken.None);
            room.Network.Pump();

            Assert.IsTrue(room.HostMember.Client.StartAsync(true, CancellationToken.None).Result);
            room.Network.Pump();

            foreach (RoomFixture.Member m in room.Members)
            {
                List<string> faces = m.Session.View.Seats.Select(s => s.AvatarId).ToList();
                Assert.AreEqual(4, faces.Distinct().Count(), m.Name + " sees " + string.Join(",", faces));
            }
        }
    }
}
