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

        [Test]
        public void Pause_WhatReachesTheDeviceWaits_AndArrivesInOrderOnceItIsBack()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);
            a.Accept(b.Address, Channel);
            b.Accept(a.Address, Channel);
            world.Pump();
            var heard = new List<string>();
            b.Disconnected += (address, channel) => heard.Add("closed, with " + b.Waiting + " waiting");

            world.Pause(b);
            a.Send(b.Address, Channel, new byte[] { 1 });
            a.Send(b.Address, Channel, new byte[] { 2 });
            a.Close(b.Address, Channel);
            Task<QuickTableSearch> found = b.FindQuickTablesAsync(CancellationToken.None);
            world.Pump();

            Assert.AreEqual(0, b.Waiting, "nothing reached the paused device");
            Assert.That(heard, Is.Empty);
            Assert.IsFalse(found.IsCompleted, "nor did the directory's answer");
            Assert.IsTrue(world.IsQuiet, "and the network has nothing more to do meanwhile");

            world.Resume(b);
            world.Pump();

            CollectionAssert.AreEqual(new[] { "closed, with 2 waiting" }, heard, "the packets first, then the close");
            Assert.IsTrue(found.IsCompleted);
            Assert.IsTrue(b.TryReceive(out _, out _, out byte[] first));
            Assert.IsTrue(b.TryReceive(out _, out _, out byte[] second));
            CollectionAssert.AreEqual(new byte[] { 1 }, first);
            CollectionAssert.AreEqual(new byte[] { 2 }, second);
        }

        [Test]
        public void Listing_ALaterGeneration_IsNoClash_AndLookUpsFindTheLatest()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device first = SignedIn(world);
            InMemoryPeerNetwork.Device second = SignedIn(world);
            InMemoryPeerNetwork.Device third = SignedIn(world);
            Task<RoomError> creator = first.ListAsync("ABCDEF", CancellationToken.None);
            world.Pump();

            Task<RoomError> takenOver = second.ListAsync("ABCDEF", 1, CancellationToken.None);
            Task<RoomError> again = first.ListAsync("ABCDEF", CancellationToken.None);
            Task<RoomError> sameGeneration = third.ListAsync("ABCDEF", 1, CancellationToken.None);
            Task<RoomError> older = third.ListAsync("ABCDEF", 0, CancellationToken.None);
            Task<PeerLookup> latest = third.LookUpAsync("ABCDEF", CancellationToken.None);
            world.Pump();
            second.Unlist("ABCDEF");
            Task<PeerLookup> afterwards = third.LookUpAsync("ABCDEF", CancellationToken.None);
            world.Pump();

            Assert.AreEqual(RoomError.None, creator.Result);
            Assert.AreEqual(RoomError.None, takenOver.Result, "an earlier generation is no obstacle (ADR 030)");
            Assert.AreEqual(RoomError.None, again.Result, "the creator's own listing still stands");
            Assert.AreEqual(RoomError.AlreadyStarted, sameGeneration.Result, "one device per generation");
            Assert.AreEqual(RoomError.AlreadyStarted, older.Result, "and nobody new below the latest");
            Assert.AreEqual(second.Address, latest.Result.Address);
            Assert.AreEqual(1, latest.Result.Generation);
            Assert.AreEqual(first.Address, afterwards.Result.Address, "the creator's again, once the later one is gone");
            Assert.AreEqual(0, afterwards.Result.Generation);
            Assert.AreEqual(1, world.Listings("ABCDEF"));
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

        [Test]
        public void QuickTables_ShowAfterTheDelay_OldestFirst_OnlyWithASeatFree()
        {
            var world = new InMemoryPeerNetwork { ListingDelaySeconds = 2.0 };
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);
            InMemoryPeerNetwork.Device c = SignedIn(world);
            world.Now = 1.0;
            a.ListQuickAsync("BBBBBB", 3, 1000, CancellationToken.None);
            world.Pump();
            world.Now = 1.5;
            c.ListQuickAsync("CCCCCC", 1, 1500, CancellationToken.None);
            b.ListQuickAsync("AAAAAA", 2, 1500, CancellationToken.None);
            world.Pump();

            Task<QuickTableSearch> early = a.FindQuickTablesAsync(CancellationToken.None);
            world.Pump();
            world.Now = 3.5;
            c.SetQuickFreeSeats("CCCCCC", 0);
            Task<QuickTableSearch> later = a.FindQuickTablesAsync(CancellationToken.None);
            world.Pump();

            Assert.AreEqual(RoomError.None, early.Result.Error);
            Assert.AreEqual(0, early.Result.Tables.Count, "nothing shows before the delay");
            Assert.AreEqual(2, later.Result.Tables.Count, "a full table is hidden");
            Assert.AreEqual("BBBBBB", later.Result.Tables[0].RoomCode, "the oldest first, the device's own among them");
            Assert.AreEqual(a.Address, later.Result.Tables[0].HostAddress);
            Assert.AreEqual(1000, later.Result.Tables[0].OpenedAtUnixMs);
            Assert.AreEqual("AAAAAA", later.Result.Tables[1].RoomCode);
            Assert.AreEqual(2, later.Result.Tables[1].FreeSeats);
        }

        [Test]
        public void QuickTable_TakenDownWhileItsListingIsOnItsWay_NeverShows()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);

            Task<RoomError> listing = a.ListQuickAsync("ABCDEF", 3, 0, CancellationToken.None);
            a.UnlistQuick("ABCDEF");
            world.Pump();

            Assert.AreNotEqual(RoomError.None, listing.Result);
            Assert.AreEqual(0, world.QuickTables);
        }

        [Test]
        public void QuickTable_ListedAgainAfterALoss_KeepsTheDateItWasFirstListedWith()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            world.Now = 1.0;
            a.ListQuickAsync("ABCDEF", 3, 1000, CancellationToken.None);
            world.Pump();
            world.LoseQuickListing("ABCDEF");
            world.Now = 5.0;
            a.ListQuickAsync("ABCDEF", 3, 5000, CancellationToken.None);
            world.Pump();

            Task<QuickTableSearch> found = a.FindQuickTablesAsync(CancellationToken.None);
            world.Pump();

            Assert.AreEqual(1000, found.Result.Tables[0].OpenedAtUnixMs);
        }

        [Test]
        public void QuickFreeSeats_AChangeShowsInSearchesAfterTheDelay()
        {
            var world = new InMemoryPeerNetwork { FreeSeatsDelaySeconds = 1.7 };
            InMemoryPeerNetwork.Device a = SignedIn(world);
            a.ListQuickAsync("ABCDEF", 1, 0, CancellationToken.None);
            world.Pump();
            world.Now = 10.0;
            a.SetQuickFreeSeats("ABCDEF", 0);

            world.Now = 11.0;
            Task<QuickTableSearch> soon = a.FindQuickTablesAsync(CancellationToken.None);
            world.Pump();
            world.Now = 11.8;
            Task<QuickTableSearch> later = a.FindQuickTablesAsync(CancellationToken.None);
            world.Pump();

            Assert.AreEqual(1, soon.Result.Tables.Count, "still found for a moment");
            Assert.AreEqual(1, soon.Result.Tables[0].FreeSeats);
            Assert.AreEqual(0, later.Result.Tables.Count, "then hidden");
        }

        [Test]
        public void QuickDirectory_Unreachable_SaysSo()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            a.DirectoryError = RoomError.Offline;

            Task<RoomError> listing = a.ListQuickAsync("ABCDEF", 3, 0, CancellationToken.None);
            Task<QuickTableSearch> found = a.FindQuickTablesAsync(CancellationToken.None);
            world.Pump();

            Assert.AreEqual(RoomError.Offline, listing.Result);
            Assert.AreEqual(RoomError.Offline, found.Result.Error);
            Assert.AreEqual(0, world.QuickTables);
        }

        [Test]
        public void QuickTables_OfEachStake_AreListedAndFoundApart()
        {
            var world = new InMemoryPeerNetwork();
            InMemoryPeerNetwork.Device a = SignedIn(world);
            InMemoryPeerNetwork.Device b = SignedIn(world);
            InMemoryPeerNetwork.Device c = SignedIn(world);
            a.ListQuickAsync("AAAAAA", 3, 1000, CancellationToken.None);
            b.ForStake(50).ListQuickAsync("BBBBBB", 3, 1000, CancellationToken.None);
            c.ForStake(100).ListQuickAsync("CCCCCC", 3, 1000, CancellationToken.None);
            world.Pump();

            Task<QuickTableSearch> quick = c.FindQuickTablesAsync(CancellationToken.None);
            Task<QuickTableSearch> fifty = a.ForStake(50).FindQuickTablesAsync(CancellationToken.None);
            Task<QuickTableSearch> hundred = b.ForStake(100).FindQuickTablesAsync(CancellationToken.None);
            world.Pump();

            Assert.AreEqual(1, quick.Result.Tables.Count);
            Assert.AreEqual("AAAAAA", quick.Result.Tables[0].RoomCode, "Quick Match finds only Quick Match's tables");
            Assert.AreEqual(1, fifty.Result.Tables.Count);
            Assert.AreEqual("BBBBBB", fifty.Result.Tables[0].RoomCode, "a stake finds only its own");
            Assert.AreEqual(1, hundred.Result.Tables.Count);
            Assert.AreEqual("CCCCCC", hundred.Result.Tables[0].RoomCode);
            Assert.AreEqual(50, world.QuickStakeOf("BBBBBB"));
            Assert.AreSame(a, a.ForStake(0), "no stake is Quick Match's own directory");
            Assert.AreSame(a.ForStake(50), a.ForStake(100).ForStake(50), "asked of any of them, the same directories");

            // A table's free seats and its unlisting go by its code.
            b.SetQuickFreeSeats("BBBBBB", 0);
            c.ForStake(100).UnlistQuick("CCCCCC");
            Task<QuickTableSearch> fiftyLater = a.ForStake(50).FindQuickTablesAsync(CancellationToken.None);
            world.Pump();

            Assert.AreEqual(0, fiftyLater.Result.Tables.Count, "full: hidden");
            Assert.AreEqual(-1, world.QuickStakeOf("CCCCCC"), "unlisted");
            Assert.AreEqual(2, world.QuickTables);
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
