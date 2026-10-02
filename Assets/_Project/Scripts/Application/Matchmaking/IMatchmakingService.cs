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
        /// <param name="secondsLeft">This player's own countdown while the search runs (ADR 029); NaN when there is none.</param>
        /// <param name="startsInSeconds">While <paramref name="tableFound"/>: the time until the match starts, the same on every player's screen; NaN otherwise.</param>
        public MatchmakingProgress(
            IReadOnlyList<RoomSeat> seats,
            double elapsedSeconds,
            bool tableFound,
            double secondsLeft = double.NaN,
            double startsInSeconds = double.NaN)
        {
            Seats = seats;
            ElapsedSeconds = elapsedSeconds;
            TableFound = tableFound;
            SecondsLeft = secondsLeft;
            StartsInSeconds = startsInSeconds;
        }

        /// <summary>
        /// Four entries in the table's seat order (the host's seat first); unfilled seats have
        /// Occupied = false, and the player looking at the screen has IsLocal = true. The screen
        /// puts that player first (ADR 029).
        /// </summary>
        public IReadOnlyList<RoomSeat> Seats { get; }

        public double ElapsedSeconds { get; }

        /// <summary>True once every seat is filled and the table is locked: the "Table found" countdown before the table loads.</summary>
        public bool TableFound { get; }

        /// <summary>This player's own countdown from the moment they started the search; NaN when the search has none.</summary>
        public double SecondsLeft { get; }

        /// <summary>While <see cref="TableFound"/>: the time until the match starts, shared by every player at the table; NaN otherwise.</summary>
        public double StartsInSeconds { get; }
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
