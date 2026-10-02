using System;
using System.Collections.Generic;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// The player taking a table over takes the others' reports (ADR 030): only from the people at that
    /// table, each by its identity in the room, never for its own seat; a newer connection of a player
    /// replaces the older; and it is done when everyone it waits for has reported, or the wait is over.
    /// </summary>
    public sealed class TakeOverCollectorTests
    {
        private static readonly PeerId First = new PeerId(1);
        private static readonly PeerId Second = new PeerId(2);

        private FakeLink _link;
        private TakeOverCollector _collector;
        private MatchEngine _engine;

        [SetUp]
        public void SetUp()
        {
            _link = new FakeLink();
            var package = new SuccessionPackage(0, 1, new[] { "id-south", "id-east", "id-north", null }, new CardSet[4]);

            // East takes over from South, the lost host, and waits for North; West is a bot.
            _collector = new TakeOverCollector(_link, package, Seat.East, new[] { Seat.North }, until: 8.0);
            var seats = new[]
            {
                new SeatProfile("Hari", "0", "classic-crimson", false),
                new SeatProfile("Asha", "1", "classic-crimson", false),
                new SeatProfile("Suman", "2", "classic-crimson", false),
                new SeatProfile("Bot", "3", "classic-crimson", true),
            };
            _engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(3), new MatchSetup("take-over", seats));
            _engine.Start();
        }

        [Test]
        public void ReportOfThePersonInThatSeat_IsKept_AndTheWaitIsOver()
        {
            _link.Receive(First, Report("id-north", 17, Seat.North));

            Assert.AreEqual(0, _collector.Missing);
            Assert.IsTrue(_collector.IsComplete(1.0));
            Assert.AreEqual(1, _collector.Reports.Count);
            Assert.AreEqual(Seat.North, _collector.Reports[0].Seat);
            Assert.AreEqual(17u, _collector.Reports[0].Seq);
            Assert.AreEqual(_engine.ViewFor(Seat.North), _collector.Reports[0].View);
            CollectionAssert.AreEqual(new[] { new KeyValuePair<PeerId, int>(First, (int)Seat.North) }, _collector.Present);
            Assert.That(_link.Sent, Is.Empty, "nobody is answered before the table is rebuilt");
        }

        [Test]
        public void PlainHello_IsHereWithNothingToReport()
        {
            _link.Receive(First, ClientMessages.Hello(new HelloMessage(Protocol.Version, "id-north", "Suman", 2, "classic-crimson")));

            Assert.AreEqual(1, _collector.Missing);
            Assert.IsFalse(_collector.IsComplete(1.0));
            Assert.AreEqual(1, _collector.Present.Count);
            Assert.That(_collector.Reports, Is.Empty);
        }

        [TestCase("id-somebody-else")]
        [TestCase("id-east")]
        [TestCase("")]
        public void SomeoneNotAtTheTable_OrClaimingTheNewHostsOwnSeat_IsTurnedAway(string identity)
        {
            _link.Receive(First, Report(identity, 17, Seat.North));

            Assert.AreEqual(1, _collector.Missing);
            Assert.That(_collector.Present, Is.Empty);
            Assert.AreEqual(1, _link.Sent.Count);
            Assert.AreEqual((byte)HostMessageKind.Rejected, _link.Sent[0].Payload[0]);
            Assert.AreEqual((byte)RoomError.Unknown, _link.Sent[0].Payload[1]);
            CollectionAssert.AreEqual(new[] { First }, _link.Dropped);
        }

        [Test]
        public void TheLostHostItself_IsWelcomeWithItsReport()
        {
            _link.Receive(First, Report("id-south", 30, Seat.South));

            Assert.AreEqual(1, _collector.Reports.Count, "its hand is known then, rather than worked out");
            Assert.AreEqual(Seat.South, _collector.Reports[0].Seat);
            Assert.AreEqual(1, _collector.Missing, "it was not waited for");
        }

        [Test]
        public void AnotherBuild_IsToldToUpdate()
        {
            var w = new WireWriter();
            w.Byte((byte)ClientMessageKind.Report);
            w.UInt16(Protocol.Version - 1);

            _link.Receive(First, w.ToArray());

            Assert.AreEqual((byte)RoomError.UpdateRequired, _link.Sent[0].Payload[1]);
            CollectionAssert.AreEqual(new[] { First }, _link.Dropped);
        }

        [Test]
        public void SecondConnectionOfAPlayer_ReplacesTheFirst()
        {
            _link.Receive(First, Report("id-north", 17, Seat.North));
            _link.Receive(Second, Report("id-north", 18, Seat.North));

            CollectionAssert.AreEqual(new[] { new KeyValuePair<PeerId, int>(Second, (int)Seat.North) }, _collector.Present);
            Assert.AreEqual(18u, _collector.Reports[0].Seq);
            Assert.AreEqual((byte)HostMessageKind.RoomClosed, _link.Sent[0].Payload[0]);
            Assert.AreEqual((byte)RoomError.Replaced, _link.Sent[0].Payload[1]);
            CollectionAssert.AreEqual(new[] { First }, _link.Dropped);
        }

        [Test]
        public void ReporterThatDrops_IsForgotten_AndTheWaitEndsOnTime()
        {
            _link.Receive(First, Report("id-north", 17, Seat.North));
            _link.Drop(First);

            Assert.AreEqual(1, _collector.Missing);
            Assert.That(_collector.Present, Is.Empty);
            Assert.IsFalse(_collector.IsComplete(7.9));
            Assert.IsTrue(_collector.IsComplete(8.0), "the wait is over, whoever has reported");
        }

        [Test]
        public void Released_HearsNothingMore()
        {
            _collector.Release();

            _link.Receive(First, Report("id-north", 17, Seat.North));

            Assert.AreEqual(1, _collector.Missing);
            Assert.That(_link.Sent, Is.Empty);
        }

        private byte[] Report(string identity, uint seq, Seat seat) =>
            ClientMessages.Report(new HelloMessage(Protocol.Version, identity, "Suman", 2, "classic-crimson"), seq, _engine.ViewFor(seat));

        private sealed class FakeLink : IHostLink
        {
            public event Action<PeerId> PeerConnected;

            public event Action<PeerId> PeerDisconnected;

            public event Action<PeerId, byte[]> Received;

            public List<(PeerId Peer, byte[] Payload)> Sent { get; } = new List<(PeerId Peer, byte[] Payload)>();

            public List<PeerId> Dropped { get; } = new List<PeerId>();

            public void Receive(PeerId peer, byte[] payload)
            {
                PeerConnected?.Invoke(peer);
                Received?.Invoke(peer, payload);
            }

            public void Drop(PeerId peer) => PeerDisconnected?.Invoke(peer);

            public void Send(PeerId peer, byte[] payload) => Sent.Add((peer, payload));

            public void Disconnect(PeerId peer) => Dropped.Add(peer);

            public void Dispose()
            {
            }
        }
    }
}
