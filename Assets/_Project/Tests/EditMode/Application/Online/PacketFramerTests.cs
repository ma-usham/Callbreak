using System;
using System.Collections.Generic;
using Callbreak.Application.Online;
using Callbreak.Core.Randomness;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// The framing of room messages over a peer-to-peer network whose packets are small (Epic Online
    /// Services: 1170 bytes): every message is cut to fit and comes back whole and in order, the open
    /// packet is never a message, a guest's oversized message costs the host nothing, and no packet,
    /// however malformed, makes it throw.
    /// </summary>
    public sealed class PacketFramerTests
    {
        private const int EosPacket = 1170;

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(EosPacket - 1)]
        public void Split_MessageThatFits_IsOneWholePacket(int length)
        {
            byte[] message = Bytes(length, seed: 1);

            byte[][] packets = PacketFramer.Split(message, EosPacket);

            Assert.AreEqual(1, packets.Length);
            Assert.AreEqual((byte)PacketType.Whole, packets[0][0]);
            Assert.AreEqual(length + 1, packets[0].Length);
            CollectionAssert.AreEqual(message, Reassemble(packets, int.MaxValue));
        }

        [TestCase(EosPacket, EosPacket)]
        [TestCase(EosPacket + 1, EosPacket)]
        [TestCase(10 * EosPacket, EosPacket)]
        [TestCase(65537, EosPacket)]
        [TestCase(6, PacketFramer.MinPacketSize)]
        [TestCase(300, PacketFramer.MinPacketSize)]
        [TestCase(257, 16)]
        [TestCase(4096, 64)]
        public void Split_LongerMessage_EveryPacketFitsAndTheyMakeTheMessageAgain(int length, int maxPacket)
        {
            byte[] message = Bytes(length, seed: (ulong)length);

            byte[][] packets = PacketFramer.Split(message, maxPacket);

            Assert.Greater(packets.Length, 1, "it took more than one packet");
            Assert.AreEqual((byte)PacketType.First, packets[0][0]);
            for (int i = 0; i < packets.Length; i++)
            {
                Assert.LessOrEqual(packets[i].Length, maxPacket, "packet " + i + " fits");
                if (i > 0)
                {
                    Assert.AreEqual((byte)PacketType.More, packets[i][0]);
                }
            }

            CollectionAssert.AreEqual(message, Reassemble(packets, int.MaxValue));
        }

        [Test]
        public void Split_PacketsTooSmallForTheHeader_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PacketFramer.Split(new byte[10], PacketFramer.MinPacketSize - 1));
        }

        [Test]
        public void Assembler_ManyMessagesOfEverySize_ComeBackInOrder()
        {
            var assembler = new PacketAssembler(1 << 20);
            var sent = new List<byte[]>();
            var received = new List<byte[]>();
            for (int i = 0; i < 200; i++)
            {
                byte[] message = Bytes((i * 97) % 5000, seed: (ulong)i);
                sent.Add(message);
                foreach (byte[] packet in PacketFramer.Split(message, 64))
                {
                    if (assembler.Add(packet, out byte[] whole) == PacketOutcome.Message)
                    {
                        received.Add(whole);
                    }
                }
            }

            Assert.AreEqual(sent.Count, received.Count);
            for (int i = 0; i < sent.Count; i++)
            {
                CollectionAssert.AreEqual(sent[i], received[i], "message " + i);
            }
        }

        [Test]
        public void Open_IsOneByteAndNeverAMessage()
        {
            var assembler = new PacketAssembler(Protocol.MaxClientPayloadBytes);

            byte[] open = PacketFramer.OpenPacket();
            PacketOutcome outcome = assembler.Add(open, out byte[] message);

            Assert.AreEqual(1, open.Length);
            Assert.AreEqual(PacketOutcome.Opened, outcome);
            Assert.IsNull(message);
        }

        [Test]
        public void Open_InTheMiddleOfAMessage_DropsItAndTheNextMessageArrives()
        {
            var assembler = new PacketAssembler(1000);
            byte[][] cut = PacketFramer.Split(Bytes(200, seed: 3), 32);
            assembler.Add(cut[0], out _);
            assembler.Add(cut[1], out _);

            Assert.AreEqual(PacketOutcome.Opened, assembler.Add(PacketFramer.OpenPacket(), out _));
            Assert.IsFalse(assembler.InMessage, "the message it was in the middle of is gone");
            Assert.AreEqual(PacketOutcome.Dropped, assembler.Add(cut[2], out _), "and its rest is not taken for anything");
            byte[] next = Bytes(100, seed: 4);
            CollectionAssert.AreEqual(next, Reassemble(assembler, PacketFramer.Split(next, 32)));
        }

        [Test]
        public void Assembler_WholeMessageOverTheLimit_IsDroppedAndTheNextArrives()
        {
            var assembler = new PacketAssembler(Protocol.MaxClientPayloadBytes);

            PacketOutcome outcome = assembler.Add(PacketFramer.Split(Bytes(Protocol.MaxClientPayloadBytes + 1, seed: 5), EosPacket)[0], out byte[] message);

            Assert.AreEqual(PacketOutcome.Dropped, outcome);
            Assert.IsNull(message);
            byte[] atTheLimit = Bytes(Protocol.MaxClientPayloadBytes, seed: 6);
            CollectionAssert.AreEqual(atTheLimit, Reassemble(assembler, PacketFramer.Split(atTheLimit, EosPacket)));
        }

        [Test]
        public void Assembler_LongMessageOverTheLimit_IsDroppedFromItsFirstPacketAndNeverBuffered()
        {
            var assembler = new PacketAssembler(Protocol.MaxClientPayloadBytes);
            byte[][] packets = PacketFramer.Split(Bytes(100000, seed: 7), 64);

            foreach (byte[] packet in packets)
            {
                Assert.AreEqual(PacketOutcome.Dropped, assembler.Add(packet, out byte[] message));
                Assert.IsNull(message);
                Assert.AreEqual(0, assembler.Buffered, "nothing of it is kept");
            }

            Assert.IsFalse(assembler.InMessage, "its last packet ended it");
            byte[] next = Bytes(200, seed: 8);
            CollectionAssert.AreEqual(next, Reassemble(assembler, PacketFramer.Split(next, 64)), "the message after it arrives");
        }

        [Test]
        public void Assembler_MessageCutOffByTheNextOne_IsDroppedAndTheNextArrives()
        {
            var assembler = new PacketAssembler(1000);
            byte[][] first = PacketFramer.Split(Bytes(300, seed: 9), 64);
            assembler.Add(first[0], out _);

            byte[] whole = Bytes(20, seed: 10);
            PacketOutcome outcome = assembler.Add(PacketFramer.Split(whole, 64)[0], out byte[] message);

            Assert.AreEqual(PacketOutcome.Message, outcome);
            CollectionAssert.AreEqual(whole, message);
            Assert.AreEqual(PacketOutcome.Dropped, assembler.Add(first[1], out _), "the rest of the cut-off message is not taken for anything");
        }

        private static IEnumerable<TestCaseData> Malformed()
        {
            yield return new TestCaseData(null, null).SetName("Malformed_Null");
            yield return new TestCaseData(new byte[0], null).SetName("Malformed_Empty");
            yield return new TestCaseData(new byte[] { 9, 1, 2 }, null).SetName("Malformed_UnknownType");
            yield return new TestCaseData(new byte[] { 2, 10, 0 }, null).SetName("Malformed_FirstWithoutItsLength");
            yield return new TestCaseData(new byte[] { 2, 2, 0, 0, 0, 7, 7 }, null).SetName("Malformed_FirstNoLongerThanItsPart");
            yield return new TestCaseData(new byte[] { 3, 1, 2, 3 }, null).SetName("Malformed_MoreWithoutAFirst");
            yield return new TestCaseData(new byte[] { 2, 6, 0, 0, 0, 1, 2, 3 }, new byte[] { 3, 4, 5, 6, 7 }).SetName("Malformed_MoreLongerThanTheMessage");
            yield return new TestCaseData(new byte[] { 2, 6, 0, 0, 0, 1, 2, 3 }, new byte[] { 3 }).SetName("Malformed_EmptyMore");
            yield return new TestCaseData(new byte[] { 2, 255, 255, 255, 255, 1 }, new byte[] { 3 }).SetName("Malformed_EmptyMoreOfASkippedMessage");

            // 513 bytes announced (over the limit of 512), 10 of them in the first packet: 503 to count
            // off, and the next packet brings 504.
            var skipped = new byte[PacketFramer.FirstHeaderBytes + 10];
            skipped[0] = (byte)PacketType.First;
            skipped[1] = 1;
            skipped[2] = 2;
            var tooLong = new byte[1 + 504];
            tooLong[0] = (byte)PacketType.More;
            yield return new TestCaseData(skipped, tooLong).SetName("Malformed_MoreLongerThanTheSkippedRest");
        }

        [TestCaseSource(nameof(Malformed))]
        public void Assembler_MalformedPacket_IsDroppedNeverThrownAndTheNextMessageArrives(byte[] packet, byte[] then)
        {
            var assembler = new PacketAssembler(512);

            PacketOutcome first = assembler.Add(packet, out byte[] message);
            PacketOutcome second = then != null ? assembler.Add(then, out message) : PacketOutcome.Dropped;

            Assert.AreNotEqual(PacketOutcome.Message, first);
            Assert.AreEqual(PacketOutcome.Dropped, second);
            Assert.IsNull(message);
            Assert.IsFalse(assembler.InMessage, "nothing is left half-read");
            byte[] next = Bytes(300, seed: 11);
            CollectionAssert.AreEqual(next, Reassemble(assembler, PacketFramer.Split(next, 64)));
        }

        [Test]
        public void Assembler_RandomPackets_NeverThrowAndNeverKeepMoreThanTheLimit()
        {
            var rng = new Pcg32Random(12);
            var assembler = new PacketAssembler(Protocol.MaxClientPayloadBytes);
            for (int i = 0; i < 20000; i++)
            {
                var packet = new byte[rng.NextInt(40)];
                for (int b = 0; b < packet.Length; b++)
                {
                    packet[b] = (byte)rng.NextInt(256);
                }

                if (packet.Length > 0 && rng.NextInt(2) == 0)
                {
                    packet[0] = (byte)rng.NextInt(5);
                }

                PacketOutcome outcome = assembler.Add(packet, out byte[] message);
                Assert.LessOrEqual(assembler.Buffered, Protocol.MaxClientPayloadBytes);
                if (outcome == PacketOutcome.Message)
                {
                    Assert.LessOrEqual(message.Length, Protocol.MaxClientPayloadBytes);
                }
            }
        }

        private static byte[] Reassemble(byte[][] packets, int limit) => Reassemble(new PacketAssembler(limit), packets);

        /// <summary>Feeds the packets of one message; the message once its last packet is in, and only then.</summary>
        private static byte[] Reassemble(PacketAssembler assembler, byte[][] packets)
        {
            byte[] result = null;
            for (int i = 0; i < packets.Length; i++)
            {
                PacketOutcome outcome = assembler.Add(packets[i], out byte[] message);
                bool last = i == packets.Length - 1;
                Assert.AreEqual(last ? PacketOutcome.Message : PacketOutcome.Partial, outcome, "packet " + i);
                result = message;
            }

            return result;
        }

        private static byte[] Bytes(int length, ulong seed)
        {
            var rng = new Pcg32Random(seed);
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = (byte)rng.NextInt(256);
            }

            return bytes;
        }
    }
}
