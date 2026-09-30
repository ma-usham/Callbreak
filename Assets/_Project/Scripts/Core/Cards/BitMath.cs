namespace Callbreak.Core.Cards
{
    /// <summary>
    /// Constant-time bit tricks for <see cref="CardSet"/>. Written by hand because
    /// <c>System.Numerics.BitOperations</c> is not part of netstandard2.1, and kept table-free so there is
    /// no static data at all.
    /// </summary>
    internal static class BitMath
    {
        /// <summary>Number of set bits (SWAR population count).</summary>
        internal static int PopCount(ulong value)
        {
            value -= (value >> 1) & 0x5555555555555555UL;
            value = (value & 0x3333333333333333UL) + ((value >> 2) & 0x3333333333333333UL);
            value = (value + (value >> 4)) & 0x0F0F0F0F0F0F0F0FUL;
            return (int)(unchecked(value * 0x0101010101010101UL) >> 56);
        }

        /// <summary>Index of the lowest set bit; <paramref name="value"/> must be non-zero.</summary>
        internal static int LowestBitIndex(ulong value)
        {
            // (value & -value) isolates the lowest bit; the bits below it, counted, are its index.
            ulong lowest = value & unchecked(~value + 1UL);
            return PopCount(lowest - 1UL);
        }

        /// <summary>Index of the highest set bit; <paramref name="value"/> must be non-zero.</summary>
        internal static int HighestBitIndex(ulong value)
        {
            // Smearing the top bit rightwards leaves (index + 1) ones.
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value |= value >> 32;
            return PopCount(value) - 1;
        }
    }
}
