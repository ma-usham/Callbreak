using System;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// Cuts a room message into the packets a peer-to-peer network carries (they are small, see
    /// <see cref="IPeerNetwork.MaxPacketSize"/>), and gives the empty packet a guest opens its
    /// connection with. Every packet starts with its <see cref="PacketType"/>: a message that fits
    /// goes as one <see cref="PacketType.Whole"/> packet; a longer one as a
    /// <see cref="PacketType.First"/> packet, which also says how long the whole message is (four
    /// bytes, little-endian, like the rest of the wire format), and as many
    /// <see cref="PacketType.More"/> packets as the rest takes. The network keeps a connection's
    /// packets in order, so the other side needs nothing else to put the message together again
    /// (<see cref="PacketAssembler"/>, one per sender).
    /// </summary>
    public static class PacketFramer
    {
        /// <summary>What a <see cref="PacketType.First"/> packet spends before its part of the message: its type and the message's length.</summary>
        public const int FirstHeaderBytes = 5;

        /// <summary>The smallest packet the framing works with: a <see cref="PacketType.First"/> header and one byte of the message.</summary>
        public const int MinPacketSize = FirstHeaderBytes + 1;

        /// <summary>The packet a guest opens its connection with. It carries nothing and never reaches the room.</summary>
        public static byte[] OpenPacket() => new[] { (byte)PacketType.Open };

        /// <summary>
        /// The packets that carry <paramref name="message"/>, in the order they are to be sent, none
        /// longer than <paramref name="maxPacketSize"/> bytes. Each is a new array.
        /// </summary>
        public static byte[][] Split(byte[] message, int maxPacketSize)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (maxPacketSize < MinPacketSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPacketSize), maxPacketSize, "A packet must hold at least " + MinPacketSize + " bytes.");
            }

            if (message.Length < maxPacketSize)
            {
                var whole = new byte[message.Length + 1];
                whole[0] = (byte)PacketType.Whole;
                Buffer.BlockCopy(message, 0, whole, 1, message.Length);
                return new[] { whole };
            }

            int firstPart = maxPacketSize - FirstHeaderBytes;
            int morePart = maxPacketSize - 1;
            long rest = message.Length - firstPart;
            var packets = new byte[1 + (int)((rest + morePart - 1) / morePart)][];

            var first = new byte[maxPacketSize];
            first[0] = (byte)PacketType.First;
            first[1] = (byte)message.Length;
            first[2] = (byte)(message.Length >> 8);
            first[3] = (byte)(message.Length >> 16);
            first[4] = (byte)(message.Length >> 24);
            Buffer.BlockCopy(message, 0, first, FirstHeaderBytes, firstPart);
            packets[0] = first;

            int offset = firstPart;
            for (int i = 1; i < packets.Length; i++)
            {
                int length = Math.Min(morePart, message.Length - offset);
                var more = new byte[length + 1];
                more[0] = (byte)PacketType.More;
                Buffer.BlockCopy(message, offset, more, 1, length);
                packets[i] = more;
                offset += length;
            }

            return packets;
        }
    }

    /// <summary>The first byte of every packet <see cref="PacketFramer"/> makes.</summary>
    public enum PacketType : byte
    {
        /// <summary>A guest opens its connection with it (the network opens a connection on its first packet). Nothing follows.</summary>
        Open = 0,

        /// <summary>A whole message follows.</summary>
        Whole = 1,

        /// <summary>The length of the whole message (four bytes, little-endian) and its first part follow; <see cref="More"/> packets bring the rest.</summary>
        First = 2,

        /// <summary>The next part of the message a <see cref="First"/> packet began.</summary>
        More = 3,
    }

    /// <summary>What one packet came to, as <see cref="PacketAssembler.Add"/> reads it.</summary>
    public enum PacketOutcome
    {
        /// <summary>Part of a message that is not complete yet.</summary>
        Partial,

        /// <summary>A message is complete.</summary>
        Message,

        /// <summary>The sender opened its connection: nothing for the room.</summary>
        Opened,

        /// <summary>Malformed, or part of a message longer than the limit: thrown away unread.</summary>
        Dropped,
    }

    /// <summary>
    /// Puts one sender's messages back together from its packets, in the order they came. A message
    /// longer than the limit is dropped as soon as its first packet says how long it is, and the
    /// packets that carry the rest of it are counted off without being kept. A malformed packet
    /// is dropped, never thrown, and so is the message it broke into: the next message starts
    /// afresh.
    /// </summary>
    public sealed class PacketAssembler
    {
        private readonly int _maxMessageBytes;
        private byte[] _message;
        private int _filled;
        private long _skipping;

        /// <param name="maxMessageBytes">The longest message kept; anything longer is dropped unread.</param>
        public PacketAssembler(int maxMessageBytes)
        {
            if (maxMessageBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMessageBytes), maxMessageBytes, "The limit cannot be negative.");
            }

            _maxMessageBytes = maxMessageBytes;
        }

        /// <summary>The longest message this assembler keeps.</summary>
        public int MaxMessageBytes => _maxMessageBytes;

        /// <summary>True between the first and the last packet of a message, whether it is kept or counted off.</summary>
        public bool InMessage => _message != null || _skipping > 0;

        /// <summary>The bytes this assembler holds for a message not yet complete: never more than the limit.</summary>
        public int Buffered => _message != null ? _message.Length : 0;

        /// <summary>
        /// Takes the sender's next packet. <paramref name="message"/> is the message it completed
        /// when the outcome is <see cref="PacketOutcome.Message"/>, and null otherwise. The packet
        /// itself is never kept.
        /// </summary>
        public PacketOutcome Add(byte[] packet, out byte[] message)
        {
            message = null;
            if (packet == null || packet.Length == 0)
            {
                Reset();
                return PacketOutcome.Dropped;
            }

            switch ((PacketType)packet[0])
            {
                case PacketType.Open:
                    // A new connection over the old one: whatever it was in the middle of is over.
                    Reset();
                    return PacketOutcome.Opened;

                case PacketType.Whole:
                {
                    Reset();
                    int length = packet.Length - 1;
                    if (length > _maxMessageBytes)
                    {
                        return PacketOutcome.Dropped;
                    }

                    message = new byte[length];
                    Buffer.BlockCopy(packet, 1, message, 0, length);
                    return PacketOutcome.Message;
                }

                case PacketType.First:
                    return Begin(packet);

                case PacketType.More:
                    return Continue(packet, out message);

                default:
                    Reset();
                    return PacketOutcome.Dropped;
            }
        }

        private PacketOutcome Begin(byte[] packet)
        {
            Reset();
            if (packet.Length < PacketFramer.FirstHeaderBytes)
            {
                return PacketOutcome.Dropped;
            }

            long total = (uint)(packet[1] | (packet[2] << 8) | (packet[3] << 16) | (packet[4] << 24));
            int part = packet.Length - PacketFramer.FirstHeaderBytes;
            if (total <= part)
            {
                // A message this short goes as one Whole packet: this sender does not frame like ours.
                return PacketOutcome.Dropped;
            }

            if (total > _maxMessageBytes)
            {
                _skipping = total - part;
                return PacketOutcome.Dropped;
            }

            _message = new byte[total];
            Buffer.BlockCopy(packet, PacketFramer.FirstHeaderBytes, _message, 0, part);
            _filled = part;
            return PacketOutcome.Partial;
        }

        private PacketOutcome Continue(byte[] packet, out byte[] message)
        {
            message = null;
            int part = packet.Length - 1;
            if (_skipping > 0)
            {
                if (part == 0 || part > _skipping)
                {
                    Reset();
                    return PacketOutcome.Dropped;
                }

                _skipping -= part;
                return PacketOutcome.Dropped;
            }

            if (_message == null || part == 0 || part > _message.Length - _filled)
            {
                Reset();
                return PacketOutcome.Dropped;
            }

            Buffer.BlockCopy(packet, 1, _message, _filled, part);
            _filled += part;
            if (_filled < _message.Length)
            {
                return PacketOutcome.Partial;
            }

            message = _message;
            _message = null;
            _filled = 0;
            return PacketOutcome.Message;
        }

        private void Reset()
        {
            _message = null;
            _filled = 0;
            _skipping = 0;
        }
    }
}
