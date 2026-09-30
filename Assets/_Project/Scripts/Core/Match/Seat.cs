namespace Callbreak.Core.Match
{
    /// <summary>
    /// An absolute table position (GDD §22 domain model: "Seat enum byte 0–3"). Seats never move during a
    /// match; clients map them to a relative position with <see cref="SeatExtensions.RelativeTo"/>.
    /// Turn order is not seat order: always advance with <see cref="SeatExtensions.Next"/>.
    /// </summary>
    public enum Seat : byte
    {
        /// <summary>Seat 0. Anticlockwise play passes from here to <see cref="East"/>.</summary>
        South = 0,

        /// <summary>Seat 1.</summary>
        East = 1,

        /// <summary>Seat 2.</summary>
        North = 2,

        /// <summary>Seat 3.</summary>
        West = 3,
    }
}
