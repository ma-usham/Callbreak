using System;
using System.Collections.Generic;

namespace Callbreak.Application.Profile
{
    /// <summary>
    /// stats.json: the profile counters (GDD §15) plus the ids of the most recently recorded
    /// matches, which make recording idempotent across app restarts. Plain serializable data read
    /// and written by <see cref="StatsService"/>. Field initializers are the defaults, so a file
    /// written before a field existed loads with that field's default. Adding a field bumps
    /// <see cref="CurrentVersion"/>.
    /// </summary>
    [Serializable]
    public sealed class StatsData
    {
        /// <summary>2 added what GDD §15 tracks for later: rounds, calls met, call total and win streaks.</summary>
        public const int CurrentVersion = 2;

        public int schemaVersion = CurrentVersion;
        public int gamesPlayed;
        public int wins;
        public bool hasBest;
        public int bestTotalTenths;
        public int roundsPlayed;
        public int roundsMet;
        public int callsTotal;
        public int winStreak;
        public int bestWinStreak;
        public List<string> recentMatchIds = new List<string>();
    }
}
