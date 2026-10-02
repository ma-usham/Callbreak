using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// The in-memory peer network keeps the rules of the EOS adapter that the room transport counts
    /// on: a first packet asks the other device to connect and waits until it accepts; a close is
    /// heard by the other end only; channels and packets EOS would refuse throw; and a cancelled
    /// call ends with its failure instead of throwing.
    /// </summary>
    public sealed class InMemoryPeerNetworkTests
    {
        private const string Channel = "cbABCDEF";

        [Test]
        public void FirstPacket_AsksTheOtherDevice_AndArrivesOnlyOnceItAccepts()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);
            var heard = new List<string>();
            b.ConnectionRequested += (address, channel) => heard.Add("b asked by " + address + " on " + channel);
            a.Connected += (address, channel) => heard.Add("a connected to " + address);
            b.Connected += (address, channel) => heard.Add("b connected to " + address);

            a.Send(b.Address, Channel, new byte[] { 7 });
            world.Pump();
            bool early = b.TryReceive(out _, out _, out _);
            b.Accept(a.Address, Channel);
            world.Pump();

            Assert.IsFalse(early, "nothing arrives before the connection is accepted");
            CollectionAssert.AreEqual(new[] { "b asked by " + a.Address + " on " + Channel, "a connected to " + b.Address, "b connected to " + a.Address }, heard);
            Assert.IsTrue(b.TryReceive(out string from, out string on, out byte[] packet));
            Assert.AreEqual(a.Address, from);
            Assert.AreEqual(Channel, on);
            CollectionAssert.AreEqual(new byte[] { 7 }, packet);
        }

        [Test]
        public void Close_IsHeardByTheOtherEndAfterWhatWasSentBefore_AndThenByTheCloser()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);
            a.Accept(b.Address, Channel);
            b.Accept(a.Address, Channel);
            world.Pump();
            var heard = new List<string>();
            a.Disconnected += (address, channel) => heard.Add("a, from " + address);
            b.Disconnected += (address, channel) => heard.Add("b, with " + b.Waiting + " waiting");

            a.Send(b.Address, Channel, new byte[] { 1 });
            a.Close(b.Address, Channel);
            world.Pump();

            CollectionAssert.AreEqual(new[] { "b, with 1 waiting", "a, from " + b.Address }, heard);
            Assert.AreEqual(0, world.Links);
        }

        [Test]
        public void SendWhileItsCloseIsNotThrough_IsDropped()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);
            a.Accept(b.Address, Channel);
            b.Accept(a.Address, Channel);
            world.Pump();
            var asked = new List<string>();
            b.ConnectionRequested += (address, channel) => asked.Add(address);

            a.Close(b.Address, Channel);
            a.Send(b.Address, Channel, new byte[] { 2 });
            world.Pump();

            Assert.IsFalse(b.TryReceive(out _, out _, out _), "nothing arrived");
            Assert.That(asked, Is.Empty, "and nothing opened the connection again");
            Assert.AreEqual(0, world.Links);
        }

        [Test]
        public void CloseOfARequestNeverAllowed_IsHeardByTheOtherEndOnly()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);
            var heard = new List<string>();
            a.Disconnected += (address, channel) => heard.Add("a");
            b.Disconnected += (address, channel) => heard.Add("b");
            a.Send(b.Address, Channel, new byte[] { 3 });
            world.Pump();

            b.Close(a.Address, Channel);
            world.Pump();

            CollectionAssert.AreEqual(new[] { "a" }, heard, "the request was turned down");
        }

        [TestCase("")]
        [TestCase("cb:ABCDEF")]
        [TestCase("cb/ABCDEF")]
        [TestCase("cbABCDEF-and-twenty-five-more-chars")]
        [TestCase(null)]
        public void ChannelEosRefuses_Throws(string channel)
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);

            Assert.Throws<ArgumentException>(() => a.Accept(b.Address, channel));
            Assert.Throws<ArgumentException>(() => a.Send(b.Address, channel, new byte[1]));
            Assert.Throws<ArgumentException>(() => a.Close(b.Address, channel));
        }

        [Test]
        public void ChannelOfThirtyTwoAllowedCharacters_IsTaken()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);

            const string longest = "cb-ABC_def+12=3.4 xyz78901234567";
            Assert.AreEqual(InMemoryPeerNetwork.MaxChannelLength, longest.Length);
            Assert.DoesNotThrow(() => a.Send(b.Address, longest, new byte[1]));
        }

        [Test]
        public void PacketLongerThanTheLimit_Throws()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);

            Assert.DoesNotThrow(() => a.Send(b.Address, Channel, new byte[InMemoryPeerNetwork.EosMaxPacketSize]));
            Assert.Throws<ArgumentException>(() => a.Send(b.Address, Channel, new byte[InMemoryPeerNetwork.EosMaxPacketSize + 1]));
        }

        [Test]
        public void CancelledCalls_EndWithTheirFailureAtOnce()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            var cancel = new CancellationTokenSource();

            Task<RoomError> listing = a.ListAsync("ABCDEF", cancel.Token);
            Task<PeerLookup> lookup = a.LookUpAsync("ABCDEF", cancel.Token);
            Task<bool> signIn = world.AddDevice().SignInAsync(cancel.Token);
            cancel.Cancel();
            world.Pump();

            Assert.AreEqual(RoomError.Unknown, listing.Result);
            Assert.AreEqual(RoomError.Unknown, lookup.Result.Error);
            Assert.IsNull(lookup.Result.Address);
            Assert.IsFalse(signIn.Result);
            Assert.IsNull(world.ListedBy("ABCDEF"), "a cancelled listing lists nothing");
        }

        private static InMemoryPeerNetwork.Device SignedIn(InMemoryPeerNetwork world)
        {
            InMemoryPeerNetwork.Device device = world.AddDevice();
            Task<bool> signIn = device.SignInAsync(CancellationToken.None);
            world.Pump();
            Assert.IsTrue(signIn.Result);
            return device;
        }
    }
}
