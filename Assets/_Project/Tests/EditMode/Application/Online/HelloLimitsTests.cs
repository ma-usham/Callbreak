using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §25 "under 2 KB per client" and "per-connection token buckets": what a member sends is
    /// small and bounded, and a host accepts only values this build knows, because what one member
    /// says in its hello is sent on to everyone in every lobby and table update.
    /// </summary>
    public sealed class HelloLimitsTests
    {
        [Test]
        public void Hello_UnknownDeckAndAvatar_IsSeatedWithTheDefaults()
        {
            var room = new RoomFixture();

            List<byte[]> answers = Say(room, new HelloMessage(Protocol.Version, "player-asha", "Asha", 255, "gold-plated"));

            Assert.AreEqual((byte)HostMessageKind.Welcome, answers[0][0], "seated");
            RoomSeat seat = room.HostMember.Client.Seats[1];
            Assert.AreEqual("Asha", seat.Name);
            Assert.AreEqual(0, seat.AvatarId, "an avatar this build does not have is the default one");
            Assert.AreEqual("classic-crimson", seat.DeckThemeId, "and so is a deck");
        }

        [Test]
        public void Hello_KnownDeckAndAvatar_AreKept()
        {
            var room = new RoomFixture();

            Say(room, new HelloMessage(Protocol.Version, "player-asha", "Asha", 4, "kathmandu-gold"));

            Assert.AreEqual(4, room.HostMember.Client.Seats[1].AvatarId);
            Assert.AreEqual("kathmandu-gold", room.HostMember.Client.Seats[1].DeckThemeId);
        }

        [Test]
        public void Hello_WithAHugeDeckId_IsDroppedUnreadAndNothingIsSentOn()
        {
            var room = new RoomFixture();
            int before = room.Network.HostPayloads.Count;

            List<byte[]> answers = Say(room, new HelloMessage(Protocol.Version, "player-asha", "Asha", 1, new string('d', 60000)));

            Assert.AreEqual(0, answers.Count, "no answer to a message no member ever sends");
            Assert.IsFalse(room.HostMember.Client.Seats[1].Occupied);
            Assert.AreEqual(before, room.Network.HostPayloads.Count, "nothing went out because of it");
        }

        [TestCase(Protocol.MaxIdentityBytes + 1, 5, TestName = "Hello_IdentityOverTheLimit_IsNotSeated")]
        [TestCase(8, Protocol.MaxNameBytes + 1, TestName = "Hello_NameOverTheLimit_IsNotSeated")]
        public void Hello_FieldOverItsLimit_IsNotSeated(int identityLength, int nameLength)
        {
            var room = new RoomFixture();

            Say(room, new HelloMessage(Protocol.Version, new string('i', identityLength), new string('n', nameLength), 1, "classic-crimson"));

            Assert.IsFalse(room.HostMember.Client.Seats[1].Occupied);
        }

        [Test]
        public void Intent_PaddedPastTheSizeLimit_IsDroppedUnread()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            Seat seat = guest.Session.LocalSeat;
            for (int i = 0; i < 2000 && !room.Host.Table.ViewFor(seat).IsMyTurn; i++)
            {
                room.Step();
                RoomFixture.Act(room.HostMember.Session);
                room.Network.Pump();
            }

            Assert.IsTrue(room.Host.Table.ViewFor(seat).IsMyTurn, "the guest holds a call turn");
            byte[] call = ClientMessages.PlaceCall(3, room.Host.Table.ViewFor(seat).CurrentTurn);
            var padded = new byte[Protocol.MaxClientPayloadBytes + 1];
            System.Array.Copy(call, padded, call.Length);

            guest.Link.Send(padded);
            room.Network.Pump();

            Assert.IsFalse(room.Host.Table.ViewFor(seat).HasCalled(seat), "a message larger than any real one is never read");
        }

        [TestCase(0, true)]
        [TestCase(Protocol.MaxReportBytes, true)]
        [TestCase(Protocol.MaxReportBytes + 1, false)]
        public void Report_IsReadAsAHello_UpToItsOwnSizeLimit(int length, bool read)
        {
            var room = new RoomFixture();
            IClientLink link = room.Network.JoinAsync(RoomFixture.Code, CancellationToken.None).Result.Link;
            var answers = new List<byte[]>();
            link.Received += answers.Add;

            // ADR 030: a player who goes to a new host says hello with what it holds; the bytes after
            // the hello are this room's to ignore, but a report longer than any report is never read.
            byte[] hello = ClientMessages.Hello(new HelloMessage(Protocol.Version, "player-asha", "Asha", 1, "classic-crimson"));
            byte[] report = new byte[System.Math.Max(hello.Length, length)];
            System.Array.Copy(hello, report, hello.Length);
            report[0] = (byte)ClientMessageKind.Report;
            link.Send(report);
            room.Network.Pump();

            Assert.AreEqual(read, room.HostMember.Client.Seats[1].Occupied);
            Assert.AreEqual(read ? 1 : 0, answers.FindAll(a => a[0] == (byte)HostMessageKind.Welcome).Count);
        }

        /// <summary>A new connection says <paramref name="hello"/>; returns what the host answered it.</summary>
        private static List<byte[]> Say(RoomFixture room, HelloMessage hello)
        {
            IClientLink link = room.Network.JoinAsync(RoomFixture.Code, CancellationToken.None).Result.Link;
            var answers = new List<byte[]>();
            link.Received += answers.Add;
            link.Send(ClientMessages.Hello(hello));
            room.Network.Pump();
            return answers;
        }
    }
}
