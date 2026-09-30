using System;
using System.Collections.Generic;
using Callbreak.Core.Randomness;

namespace Callbreak.Tests.EditMode.Support
{
    /// <summary>
    /// An <see cref="IRandom"/> that replays scripted values, then continues with a seeded PCG stream.
    /// Lets a test force the first dealer and the exact cards of a deal.
    /// </summary>
    public sealed class SequenceRandom : IRandom
    {
        private readonly Queue<int> _script;
        private readonly Pcg32Random _fallback;

        public SequenceRandom(IEnumerable<int> script, ulong fallbackSeed = 1UL)
        {
            _script = new Queue<int>(script);
            _fallback = new Pcg32Random(fallbackSeed);
        }

        public int Remaining => _script.Count;

        public int Draws { get; private set; }

        public int NextInt(int maxExclusive)
        {
            Draws++;
            if (_script.Count == 0)
            {
                return _fallback.NextInt(maxExclusive);
            }

            int value = _script.Dequeue();
            if (value < 0 || value >= maxExclusive)
            {
                throw new InvalidOperationException(
                    "Scripted value " + value + " is outside [0, " + maxExclusive + ") at draw " + Draws + ".");
            }

            return value;
        }
    }
}
