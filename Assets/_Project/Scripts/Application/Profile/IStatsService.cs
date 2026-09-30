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
        /// counts as played and lost and can't set a best score (GDD §8).
        /// </summary>
        void RecordMatch(string matchId, int rank, int totalTenths, bool abandoned);
    }
}
