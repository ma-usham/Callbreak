using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §27 identity and L2934 ("other players' account ids" are deliberately absent): a guest is
    /// known to a host by a value that is its own in that room only. It finds the seat again after
    /// a drop or an app kill, and is worth nothing to anyone in any other room.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [Test]
        public void Join_TheHello_NeverCarriesThePlayersPermanentId()
        {
            Device host = NewDevice("Hari");
            Device guest = NewDevice("Asha");

            JoinRoom(host, guest);

            byte[] permanent = Encoding.UTF8.GetBytes(guest.Auth.PlayerId);
            Assert.Greater(HellosTo(host.Rooms.Current.Code).Count, 0, "the guest said hello");
            foreach ((string _, byte[] payload) in _network.ClientPayloads)
            {
                Assert.Less(IndexOf(payload, permanent), 0, "a payload to the host carries the player id");
            }
        }

        [Test]
        public void Identity_SamePlayer_OneValuePerRoom()
        {
            Device first = NewDevice("Hari");
            Device second = NewDevice("Suman");
            Device guest = NewDevice("Asha");
            string codeA = first.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 0), CancellationToken.None).Result.Session.Code;
            string codeB = second.Rooms.CreateRoomAsync(new RoomSettings("Suman's Table", 0), CancellationToken.None).Result.Session.Code;

            Task<RoomResult> joining = guest.Rooms.JoinRoomAsync(codeA, CancellationToken.None);
            Settle();
            Assert.IsTrue(joining.Result.Succeeded);
            joining = guest.Rooms.JoinRoomAsync(codeB, CancellationToken.None);
            Settle();
            Assert.IsTrue(joining.Result.Succeeded);
            joining = guest.Rooms.JoinRoomAsync(codeA, CancellationToken.None);
            Settle();
            Assert.IsTrue(joining.Result.Succeeded);

            List<string> inA = HellosTo(codeA);
            List<string> inB = HellosTo(codeB);
            Assert.AreEqual(2, inA.Count, "two hellos to the first room");
            Assert.AreEqual(1, inB.Count, "one to the second");
            Assert.AreEqual(inA[0], inA[1], "the same player is the same in one room, so a rejoin finds the seat");
            Assert.AreNotEqual(inA[0], inB[0], "and someone else in another room");
        }

        [Test]
        public void Identity_LearnedInOneRoom_TakesNoSeatInAnother()
        {
            Device mallory = NewDevice("Mallory");
            Device asha = NewDevice("Asha");
            Device hari = NewDevice("Hari");

            // Asha once played in Mallory's room, so Mallory's device was handed whatever Asha says there.
            JoinRoom(mallory, asha);
            string learned = HellosTo(mallory.Rooms.Current.Code)[0];
            asha.Rooms.Current.LeaveAsync(CancellationToken.None);
            Settle(1);

            // Later Asha plays in Hari's room, and Mallory tries to take her seat with it.
            JoinRoom(hari, asha);
            StartMatch(hari, asha);
            IMatchSession session = asha.Rooms.Current.Match;
            Seat seat = session.LocalSeat;
            IClientLink thief = _network.JoinAsync(hari.Rooms.Current.Code, CancellationToken.None).Result.Link;
            var answers = new List<byte[]>();
            thief.Received += answers.Add;
            thief.Send(ClientMessages.Hello(new HelloMessage(Protocol.Version, learned, "Mallory", 0, "classic-crimson")));
            Settle();

            Assert.AreEqual(SessionStatus.Running, session.Status, "Asha keeps playing");
            Assert.IsNotNull(asha.Rooms.Current, "Asha is still in the room");
            Assert.AreEqual(SeatConnection.Connected, hari.Rooms.Current.Match.Seats[(int)seat].Connection);
            Assert.AreEqual(1, answers.Count, "the stranger gets one answer");
            Assert.AreEqual((byte)HostMessageKind.Rejected, answers[0][0], "a refusal, not a seat");
        }

        /// <summary>The identities in the hellos sent to the room <paramref name="code"/>, in order.</summary>
        private List<string> HellosTo(string code)
        {
            var identities = new List<string>();
            foreach ((string to, byte[] payload) in _network.ClientPayloads)
            {
                var reader = new WireReader(payload);
                if (to == code && reader.Byte() == (byte)ClientMessageKind.Hello)
                {
                    identities.Add(HelloMessage.Read(reader).Identity);
                }
            }

            return identities;
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j])
                {
                    j++;
                }

                if (j == needle.Length)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
