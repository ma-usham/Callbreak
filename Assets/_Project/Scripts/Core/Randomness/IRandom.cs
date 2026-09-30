namespace Callbreak.Core.Randomness
{
    /// <summary>
    /// The engine's only source of chance (GDD §22 "IRandom: CryptoRandom in production; Pcg32Random(seed)
    /// for tests and replays"). Injecting it keeps Core deterministic: the same seed and commands always
    /// produce the same events.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns a uniformly distributed integer in <c>[0, maxExclusive)</c>.</summary>
        /// <param name="maxExclusive">The exclusive upper bound; must be positive.</param>
        /// <returns>An integer from 0 to <paramref name="maxExclusive"/> − 1.</returns>
        int NextInt(int maxExclusive);
    }
}
