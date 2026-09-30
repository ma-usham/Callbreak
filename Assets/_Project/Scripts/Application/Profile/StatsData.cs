using System;
using System.Collections.Generic;

namespace Callbreak.Application.Profile
{
    /// <summary>
    /// stats.json: the profile counters (GDD §15) plus the ids of the most recently recorded
    /// matches, which make recording idempotent across app restarts. Plain serializable data read
    /// and written by <see cref="StatsService"/>.
    /// </summary>
    [Serializable]
    public sealed class StatsData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;
        public int gamesPlayed;
        public int wins;
        public bool hasBest;
        public int bestTotalTenths;
        public List<string> recentMatchIds = new List<string>();
    }
}
