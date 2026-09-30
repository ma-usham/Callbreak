using System;
using System.Text;

namespace Callbreak.Application.Online
{
    /// <summary>Reads what <see cref="WireWriter"/> wrote; anything short or out of range is a <see cref="WireFormatException"/>.</summary>
    public sealed class WireReader
    {
        private readonly byte[] _data;
        private readonly int _end;
        private int _position;

        public WireReader(byte[] data)
            : this(data, 0, data != null ? data.Length : 0)
        {
        }

        public WireReader(byte[] data, int offset, int count)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            if (offset < 0 || count < 0 || offset + count > data.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            _position = offset;
            _end = offset + count;
        }

        public bool AtEnd => _position >= _end;

        public byte Byte()
        {
            Require(1);
            return _data[_position++];
        }

        public bool Bool()
        {
            byte value = Byte();
            if (value > 1)
            {
                throw new WireFormatException("A flag must be 0 or 1.");
            }

            return value == 1;
        }

        public ushort UInt16()
        {
            Require(2);
            int value = _data[_position] | (_data[_position + 1] << 8);
            _position += 2;
            return (ushort)value;
        }

        public uint UInt32()
        {
            Require(4);
            uint value = _data[_position]
                | ((uint)_data[_position + 1] << 8)
                | ((uint)_data[_position + 2] << 16)
                | ((uint)_data[_position + 3] << 24);
            _position += 4;
            return value;
        }

        public int Int32() => unchecked((int)UInt32());

        public ulong UInt64()
        {
            ulong low = UInt32();
            ulong high = UInt32();
            return low | (high << 32);
        }

        /// <summary>
        /// Text of at most <paramref name="maxBytes"/> UTF-8 bytes; longer is a
        /// <see cref="WireFormatException"/>, so no field on the wire is bigger than its purpose.
        /// </summary>
        public string String(int maxBytes)
        {
            int bytes = UInt16();
            if (bytes == 0)
            {
                return string.Empty;
            }

            if (bytes > maxBytes)
            {
                throw new WireFormatException("Text of " + bytes + " bytes exceeds the limit of " + maxBytes + ".");
            }

            Require(bytes);
            string value;
            try
            {
                value = new UTF8Encoding(false, true).GetString(_data, _position, bytes);
            }
            catch (ArgumentException e)
            {
                throw new WireFormatException("Text is not valid UTF-8.", e);
            }

            _position += bytes;
            return value;
        }

        /// <summary>A count that must not exceed <paramref name="max"/> (guards every list on the wire).</summary>
        public int Count(int max)
        {
            int count = Byte();
            if (count > max)
            {
                throw new WireFormatException("A list of " + count + " exceeds the limit of " + max + ".");
            }

            return count;
        }

        private void Require(int count)
        {
            if (_position + count > _end)
            {
                throw new WireFormatException("The message ends early.");
            }
        }
    }
}
