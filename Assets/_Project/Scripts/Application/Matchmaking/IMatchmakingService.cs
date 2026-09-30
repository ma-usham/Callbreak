using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;

namespace Callbreak.Application.Matchmaking
{
    /// <summary>
    /// Quick Match (GDD §11 screen 05, §27): find three opponents; after the configured delay
    /// (25 s) empty seats fill with Normal bots; the match starts 1.5 s after 4/4. Cancelling
    /// withdraws the ticket within a second. No fake ETA.
    /// </summary>
    public interface IMatchmakingService
    {
        /// <summary>
        /// Searches until four seats are filled or <paramref name="ct"/> is cancelled (the Cancel
        /// button). Progress reports seats as they fill.
        /// </summary>
        Task<MatchmakingResult> FindMatchAsync(IProgress<MatchmakingProgress> progress, CancellationToken ct);

        /// <summary>The match the last successful search found, the session the table plays through; otherwise null.</summary>
        IMatchSession Match { get; }
    }

    public sealed class MatchmakingProgress
    {
        public MatchmakingProgress(IReadOnlyList<RoomSeat> seats, double elapsedSeconds, bool tableFound)
        {
            Seats = seats;
            ElapsedSeconds = elapsedSeconds;
            TableFound = tableFound;
        }

        /// <summary>Four entries; unfilled seats have Occupied = false.</summary>
        public IReadOnlyList<RoomSeat> Seats { get; }

        public double ElapsedSeconds { get; }

        /// <summary>True for the short "Table found" beat before the table loads.</summary>
        public bool TableFound { get; }
    }

    public enum MatchmakingError
    {
        None,
        Cancelled,
        Offline,
        ConnectionFailed,
        UpdateRequired,
        Unavailable,
    }

    public readonly struct MatchmakingResult
    {
        public MatchmakingResult(MatchmakingError error)
        {
            Error = error;
        }

        public MatchmakingError Error { get; }

        public bool Found => Error == MatchmakingError.None;
    }
}
