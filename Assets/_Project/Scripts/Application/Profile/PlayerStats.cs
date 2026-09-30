using System;

namespace Callbreak.Application.Profile
{
    /// <summary>
    /// Profile statistics (GDD §15): games played = completed matches; wins = finished 1st,
    /// including shared; best = highest final total. Also what the GDD tracks for later, counted
    /// over finished matches only and from the version that began recording it: rounds played,
    /// rounds in which the call was met, the calls added up, and the current and best run of wins.
    /// Plain serializable data.
    /// </summary>
    [Serializable]
    public sealed class PlayerStats
    {
        public int schemaVersion = 1;
        public int gamesPlayed;
        public int wins;
        public bool hasBest;
        public int bestTotalTenths;

        /// <summary>Rounds of finished matches; a match left early adds none.</summary>
        public int roundsPlayed;

        /// <summary>Of <see cref="roundsPlayed"/>, the rounds in which the call was met.</summary>
        public int roundsMet;

        /// <summary>The calls of <see cref="roundsPlayed"/> added up.</summary>
        public int callsTotal;

        /// <summary>Wins in a row up to the latest match; a lost or abandoned match ends the run.</summary>
        public int winStreak;

        /// <summary>The longest run of wins so far.</summary>
        public int bestWinStreak;

        /// <summary>Wins ÷ games, 0 when no games.</summary>
        public float WinRate => gamesPlayed == 0 ? 0f : (float)wins / gamesPlayed;

        /// <summary>Calls met ÷ rounds played ("calls-met %" as 0-1), 0 when no rounds.</summary>
        public float CallsMetRate => roundsPlayed == 0 ? 0f : (float)roundsMet / roundsPlayed;

        /// <summary>The average call per round, 0 when no rounds.</summary>
        public float AverageCall => roundsPlayed == 0 ? 0f : (float)callsTotal / roundsPlayed;
    }
}
