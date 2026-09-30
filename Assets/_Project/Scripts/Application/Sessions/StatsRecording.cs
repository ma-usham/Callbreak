using Callbreak.Application.Profile;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;

namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// How a session writes its finished match into the player's statistics (GDD §15): the place
    /// and total from the final standings, and the seat's rounds, met calls and calls from the
    /// final score sheet. Offline and online matches record the same way.
    /// </summary>
    internal static class StatsRecording
    {
        /// <param name="standing">The local seat's final standing.</param>
        /// <param name="scores">The match's final score sheet.</param>
        public static void RecordFinished(this IStatsService stats, string matchId, Standing standing, ScoreSheet scores)
        {
            Seat seat = standing.Seat;
            int calls = 0;
            for (int i = 0; i < scores.Rounds.Count; i++)
            {
                calls += scores.Rounds[i][seat].Call;
            }

            stats.RecordMatch(
                matchId,
                standing.Rank,
                standing.TotalTenths,
                abandoned: false,
                roundsPlayed: scores.Rounds.Count,
                roundsMet: scores.RoundsMetBy(seat),
                callsTotal: calls);
        }
    }
}
