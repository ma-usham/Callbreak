using System;
using System.Text;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// Writes the table protocol's payloads (GDD §25 "wire size": cards as a byte, hands as a
    /// ulong, scores in tenths). Little-endian, no padding, strings as UTF-8 with a 16-bit length.
    /// </summary>
    public sealed class WireWriter
    {
        private byte[] _buffer = new byte[256];
        private int _length;

        public int Length => _length;

        public void Reset() => _length = 0;

        public byte[] ToArray()
        {
            var copy = new byte[_length];
            Buffer.BlockCopy(_buffer, 0, copy, 0, _length);
            return copy;
        }

        public void Byte(byte value)
        {
            Reserve(1);
            _buffer[_length++] = value;
        }

        public void Bool(bool value) => Byte(value ? (byte)1 : (byte)0);

        public void UInt16(ushort value)
        {
            Reserve(2);
            _buffer[_length++] = (byte)value;
            _buffer[_length++] = (byte)(value >> 8);
        }

        public void UInt32(uint value)
        {
            Reserve(4);
            _buffer[_length++] = (byte)value;
            _buffer[_length++] = (byte)(value >> 8);
            _buffer[_length++] = (byte)(value >> 16);
            _buffer[_length++] = (byte)(value >> 24);
        }

        public void Int32(int value) => UInt32(unchecked((uint)value));

        public void UInt64(ulong value)
        {
            UInt32(unchecked((uint)value));
            UInt32((uint)(value >> 32));
        }

        /// <summary>Null is written as the empty string; text longer than 65535 bytes is refused.</summary>
        public void String(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                UInt16(0);
                return;
            }

            int bytes = Encoding.UTF8.GetByteCount(value);
            if (bytes > ushort.MaxValue)
            {
                throw new ArgumentException("Text is too long for the wire.", nameof(value));
            }

            UInt16((ushort)bytes);
            Reserve(bytes);
            Encoding.UTF8.GetBytes(value, 0, value.Length, _buffer, _length);
            _length += bytes;
        }

        private void Reserve(int count)
        {
            if (_length + count <= _buffer.Length)
            {
                return;
            }

            int size = _buffer.Length * 2;
            while (size < _length + count)
            {
                size *= 2;
            }

            Array.Resize(ref _buffer, size);
        }
    }
}
