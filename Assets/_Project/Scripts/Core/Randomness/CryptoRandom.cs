using System;
using System.Security.Cryptography;

namespace Callbreak.Core.Randomness
{
    /// <summary>
    /// Production randomness backed by the operating system's CSPRNG (GDD §2 "The server shuffles with a
    /// cryptographically secure RNG"). It has no state, so a match run on it cannot be replayed; for a
    /// replayable match draw a seed with <see cref="NextSeed"/> and run the engine on
    /// <see cref="Pcg32Random"/> with that seed, logging the seed privately (GDD §22).
    /// </summary>
    public sealed class CryptoRandom : IRandom
    {
        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxExclusive"/> is not positive.</exception>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Bound must be positive.");
            }

            return RandomNumberGenerator.GetInt32(maxExclusive);
        }

        /// <summary>Draws 64 unpredictable bits, suitable as a <see cref="Pcg32Random"/> seed.</summary>
        /// <returns>A random seed.</returns>
        public ulong NextSeed()
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            ulong seed = 0UL;
            for (int i = 0; i < bytes.Length; i++)
            {
                seed = (seed << 8) | bytes[i];
            }

            return seed;
        }
    }
}
