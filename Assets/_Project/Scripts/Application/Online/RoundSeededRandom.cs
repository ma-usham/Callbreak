using System;
using System.Collections.Generic;
using Callbreak.Core.Randomness;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// The hosted engine's randomness (GDD §2: a cryptographically secure shuffle; §22: the seed
    /// never leaves the server). Every round is dealt from a seed of its own, drawn from the seed
    /// source when that round's first card is shuffled: the four hands a finished round shows say
    /// nothing about the next deal, and no seed of a later round exists yet for anyone reading the
    /// host's memory. With a deterministic seed source (tests, replays) it deals the same every time.
    /// </summary>
    internal sealed class RoundSeededRandom : IRandom
    {
        private readonly Func<ulong> _seeds;
        private readonly Func<int> _round;
        private readonly List<ulong> _drawn = new List<ulong>();

        private Pcg32Random _current;
        private int _currentRound;

        /// <param name="seeds">A CSPRNG in production (GDD §22).</param>
        /// <param name="round">The round being dealt; the engine sets it before it shuffles.</param>
        public RoundSeededRandom(Func<ulong> seeds, Func<int> round)
        {
            _seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
            _round = round ?? throw new ArgumentNullException(nameof(round));
        }

        /// <summary>The seeds drawn so far, oldest first (the first dealer's, then one per round), for tests.</summary>
        public IReadOnlyList<ulong> Seeds => _drawn;

        public int NextInt(int maxExclusive)
        {
            int round = _round();
            if (_current == null || round != _currentRound)
            {
                ulong seed = _seeds();
                _drawn.Add(seed);
                _current = new Pcg32Random(seed);
                _currentRound = round;
            }

            return _current.NextInt(maxExclusive);
        }
    }
}
