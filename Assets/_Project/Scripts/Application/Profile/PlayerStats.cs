using System;

namespace Callbreak.Application.Profile
{
    /// <summary>
    /// Profile statistics (GDD §15): games played = completed matches; wins = finished 1st,
    /// including shared; best = highest final total. Plain serializable data.
    /// </summary>
    [Serializable]
    public sealed class PlayerStats
    {
        public int schemaVersion = 1;
        public int gamesPlayed;
        public int wins;
        public bool hasBest;
        public int bestTotalTenths;

        /// <summary>Wins ÷ games, 0 when no games.</summary>
        public float WinRate => gamesPlayed == 0 ? 0f : (float)wins / gamesPlayed;
    }
}
