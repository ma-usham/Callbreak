using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Rooms;

namespace Callbreak.Application.Online
{
    /// <summary>
    /// The directory of Quick Match tables (ADR 029): a device that opens a table lists it with
    /// the seats it still has free, and a device looking for a game finds the open tables, oldest
    /// first. Kept apart from private rooms, which are only ever found by their exact code.
    /// Implemented over EOS lobbies in Callbreak.Networking, and in memory for tests.
    /// </summary>
    public interface IQuickTableDirectory
    {
        /// <summary>
        /// Lists this device's table under <paramref name="roomCode"/> as open with
        /// <paramref name="freeSeats"/> free, opened at <paramref name="openedAtUnixMs"/>: when its
        /// host tapped Quick Match, by the host's clock (UTC ms), so an older table's countdown ends
        /// first. A code listed again (after the directory lost it) keeps the time it was first
        /// listed with. None when listed; Offline or Unknown when the directory cannot be reached.
        /// </summary>
        Task<RoomError> ListQuickAsync(string roomCode, int freeSeats, long openedAtUnixMs, CancellationToken ct);

        /// <summary>
        /// Tells the directory how many seats this device's table has free. 0 hides it from
        /// searches: the table is full or starting, and nobody else may join.
        /// </summary>
        void SetQuickFreeSeats(string roomCode, int freeSeats);

        /// <summary>Takes this device's table listing down; nothing if there is none.</summary>
        void UnlistQuick(string roomCode);

        /// <summary>The open tables with at least one free seat, oldest first (a handful at most).</summary>
        Task<QuickTableSearch> FindQuickTablesAsync(CancellationToken ct);
    }

    /// <summary>One open Quick Match table as the directory lists it.</summary>
    public readonly struct QuickTable
    {
        public QuickTable(string roomCode, string hostAddress, int freeSeats, long openedAtUnixMs)
        {
            RoomCode = roomCode;
            HostAddress = hostAddress;
            FreeSeats = freeSeats;
            OpenedAtUnixMs = openedAtUnixMs;
        }

        /// <summary>The room code the table's host listens under (never shown to anyone).</summary>
        public string RoomCode { get; }

        /// <summary>The host device's network address.</summary>
        public string HostAddress { get; }

        public int FreeSeats { get; }

        /// <summary>
        /// When the table's host tapped Quick Match, by the host's clock (UTC, ms since 1970): an
        /// older table's countdown ends first, and older tables win a merge.
        /// </summary>
        public long OpenedAtUnixMs { get; }
    }

    /// <summary>The answer to <see cref="IQuickTableDirectory.FindQuickTablesAsync"/>.</summary>
    public readonly struct QuickTableSearch
    {
        public QuickTableSearch(IReadOnlyList<QuickTable> tables, RoomError error)
        {
            Tables = tables ?? System.Array.Empty<QuickTable>();
            Error = error;
        }

        /// <summary>Open tables, oldest first; empty when none or on an error.</summary>
        public IReadOnlyList<QuickTable> Tables { get; }

        /// <summary>None when the search ran (even with no tables), Offline or Unknown when the directory cannot be reached.</summary>
        public RoomError Error { get; }
    }
}
