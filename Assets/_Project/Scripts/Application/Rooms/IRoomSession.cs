using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Sessions;

namespace Callbreak.Application.Rooms
{
    /// <summary>
    /// An open private room (GDD §11 screen 04, §27): seats, ready flags and settings, the same on
    /// every member within a second. When the host starts, every member moves to the Table scene
    /// together.
    /// </summary>
    public interface IRoomSession : IDisposable
    {
        /// <summary>Raised whenever seats, ready flags or settings change.</summary>
        event Action Changed;

        /// <summary>Raised when the match is starting; the app then loads the table.</summary>
        event Action MatchStarting;

        /// <summary>Raised when the room goes away (host left, kicked, connection lost).</summary>
        event Action<RoomError> Closed;

        string Code { get; }

        RoomSettings Settings { get; }

        IReadOnlyList<RoomSeat> Seats { get; }

        bool IsHost { get; }

        int LocalSeatIndex { get; }

        /// <summary>
        /// The room's running or last match, the session the table plays through; null in the
        /// lobby before the first start. It is set when <see cref="MatchStarting"/> is raised.
        /// </summary>
        IMatchSession Match { get; }

        Task SetReadyAsync(bool ready, CancellationToken ct);

        /// <summary>Host only. Empty seats are filled with bots when <paramref name="fillWithBots"/>.</summary>
        Task<bool> StartAsync(bool fillWithBots, CancellationToken ct);

        /// <summary>Host only: removes a guest (long-press on a seat).</summary>
        Task KickAsync(int seatIndex, CancellationToken ct);

        Task LeaveAsync(CancellationToken ct);
    }

    /// <summary>Creates and joins private rooms (GDD §27 "Private rooms").</summary>
    public interface IRoomService
    {
        /// <summary>The room this device is in, or null.</summary>
        IRoomSession Current { get; }

        Task<RoomResult> CreateRoomAsync(RoomSettings settings, CancellationToken ct);

        Task<RoomResult> JoinRoomAsync(string code, CancellationToken ct);
    }
}
