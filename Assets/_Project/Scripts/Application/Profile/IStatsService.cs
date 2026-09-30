using System;

namespace Callbreak.Application.Profile
{
    /// <summary>Statistics that change exactly once per finished match (GDD §11 screen 10).</summary>
    public interface IStatsService
    {
        event Action Changed;

        PlayerStats Current { get; }

        /// <summary>
        /// Records a finished match. Idempotent per <paramref name="matchId"/>. A match left early
        /// counts as played and lost, can't set a best score and ends the win streak; its rounds
        /// are not counted (GDD §8, §15).
        /// </summary>
        /// <param name="roundsPlayed">The rounds of the match, for the calls-met rate and the average call.</param>
        /// <param name="roundsMet">The rounds in which this player's call was met.</param>
        /// <param name="callsTotal">This player's calls of those rounds, added up.</param>
        void RecordMatch(string matchId, int rank, int totalTenths, bool abandoned, int roundsPlayed = 0, int roundsMet = 0, int callsTotal = 0);
    }
}
