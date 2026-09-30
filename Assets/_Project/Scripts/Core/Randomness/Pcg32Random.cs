using System;

namespace Callbreak.Core.Randomness
{
    /// <summary>
    /// PCG-XSH-RR 64/32 (O'Neill, pcg-random.org): a small, fast generator whose whole state is two
    /// integers, so a seed reproduces a match exactly (GDD §22 "Determinism &amp; replay"). Not secure; the
    /// seed itself should come from <see cref="CryptoRandom.NextSeed"/> in production.
    /// </summary>
    public sealed class Pcg32Random : IRandom
    {
        /// <summary>Stream selector used when none is given.</summary>
        public const ulong DefaultStream = 0xda3e39cb94b95bdbUL;

        private const ulong Multiplier = 6364136223846793005UL;

        private readonly ulong _increment;
        private ulong _state;

        /// <summary>Seeds the generator on the default stream.</summary>
        /// <param name="seed">Any 64-bit seed.</param>
        public Pcg32Random(ulong seed)
            : this(seed, DefaultStream)
        {
        }

        /// <summary>Seeds the generator exactly like the reference <c>pcg32_srandom_r(seed, stream)</c>.</summary>
        /// <param name="seed">Any 64-bit seed.</param>
        /// <param name="stream">Selects one of 2^63 independent sequences.</param>
        public Pcg32Random(ulong seed, ulong stream)
        {
            _increment = (stream << 1) | 1UL;
            _state = 0UL;
            NextUInt32();
            _state += seed;
            NextUInt32();
        }

        private Pcg32Random(ulong state, ulong increment, bool fromState)
        {
            _state = state;
            _increment = increment;
        }

        /// <summary>The internal state; with <see cref="Increment"/> it resumes the sequence via <see cref="FromState"/>.</summary>
        public ulong State => _state;

        /// <summary>The odd increment derived from the stream selector.</summary>
        public ulong Increment => _increment;

        /// <summary>
        /// Rebuilds a generator mid-sequence, e.g. next to a saved <c>MatchSnapshot</c> so a restored match
        /// deals exactly what the original would have.
        /// </summary>
        /// <param name="state">A value previously read from <see cref="State"/>.</param>
        /// <param name="increment">A value previously read from <see cref="Increment"/>; must be odd.</param>
        /// <returns>A generator that continues the original sequence.</returns>
        /// <exception cref="ArgumentException"><paramref name="increment"/> is even.</exception>
        public static Pcg32Random FromState(ulong state, ulong increment)
        {
            if ((increment & 1UL) == 0UL)
            {
                throw new ArgumentException("A PCG increment is always odd.", nameof(increment));
            }

            return new Pcg32Random(state, increment, fromState: true);
        }

        /// <summary>Returns the next 32 uniformly distributed bits.</summary>
        /// <returns>A 32-bit value.</returns>
        public uint NextUInt32()
        {
            ulong old = _state;
            _state = unchecked((old * Multiplier) + _increment);
            uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            int rotation = (int)(old >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is not positive.</exception>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Bound must be positive.");
            }

            // Rejecting the lowest (2^32 mod bound) values removes modulo bias, as in pcg32_boundedrand_r.
            uint bound = (uint)maxExclusive;
            uint threshold = unchecked(0u - bound) % bound;
            while (true)
            {
                uint value = NextUInt32();
                if (value >= threshold)
                {
                    return (int)(value % bound);
                }
            }
        }
    }
}
