using System;
using System.Globalization;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// How the coin screens write numbers: a balance with thousands separators ("1,250"), the same in
    /// every language like scores (GDD §15), and the time left until the ad rewards reset as hours,
    /// minutes and seconds ("04:59:59"; the designer's Earn Coins prototype), rounded up so it reads
    /// 00:00:00 only when the set has reset. Engine-free.
    /// </summary>
    public static class CoinsFormat
    {
        public static string Coins(int coins) => coins.ToString("N0", CultureInfo.InvariantCulture);

        /// <summary>Whole seconds left, rounded up; 0 once the moment has come.</summary>
        public static long SecondsLeft(TimeSpan left) => left <= TimeSpan.Zero ? 0L : (long)Math.Ceiling(left.TotalSeconds);

        public static string TimeLeft(long seconds)
        {
            seconds = Math.Max(0L, seconds);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}", seconds / 3600, seconds / 60 % 60, seconds % 60);
        }
    }
}
