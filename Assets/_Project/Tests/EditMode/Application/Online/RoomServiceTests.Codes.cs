using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Rooms;
using Callbreak.Core.Randomness;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD Appendix A and L2984 (32^6 codes, per-device join limits): a room code is an invitation,
    /// so it is drawn from the secure seed source and never follows from anything guests see; and a
    /// code someone else is hosting is never taken over, another one is drawn.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [Test]
        public void Create_TheCodeIsLive_AnotherIsDrawnAndBothRoomsWork()
        {
            Device first = NewDevice("Hari", seed: 555);
            Device second = NewDevice("Suman", seed: 555);
            Device guest = NewDevice("Asha");

            string taken = first.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 0), CancellationToken.None).Result.Session.Code;
            RoomResult result = second.Rooms.CreateRoomAsync(new RoomSettings("Suman's Table", 0), CancellationToken.None).Result;
            Task<RoomResult> joining = guest.Rooms.JoinRoomAsync(taken, CancellationToken.None);
            Settle();

            Assert.IsTrue(result.Succeeded, "the second host got a room");
            Assert.AreNotEqual(taken, result.Session.Code, "under a code of its own");
            Assert.IsTrue(joining.Result.Succeeded);
            Assert.AreEqual("Hari", joining.Result.Session.Seats[0].Name, "the first code still leads to the first host");
        }

        [Test]
        public void Create_EveryCodeDrawnIsLive_SaysItCouldNotConnect()
        {
            Device host = NewDevice("Hari");
            _network.HostError = RoomError.AlreadyStarted;

            RoomResult result = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 0), CancellationToken.None).Result;

            Assert.AreEqual(RoomError.Unknown, result.Error, "not \"the match has started\", which means nothing to a host");
            Assert.AreEqual(4, _network.HostAttempts, "the first code and three more");
        }

        [Test]
        public void Create_TheCode_DoesNotFollowFromTheGeneratorThatNamesTheBots()
        {
            Device host = NewDevice("Hari", seed: 777);

            string code = host.Rooms.CreateRoomAsync(new RoomSettings("Hari's Table", 0), CancellationToken.None).Result.Session.Code;

            // The generator whose names, avatars and decks every guest sees at the table.
            var botNames = new Pcg32Random(777UL * 7919UL);
            Assert.AreNotEqual(RoomCode.Generate(botNames.NextInt), code);
        }

        private Device NewDevice(string name, ulong seed)
        {
            var device = new Device(_network, name, seed, new InMemorySaveStore(), () => Epoch.AddSeconds(_now));
            _devices.Add(device);
            return device;
        }
    }
}
