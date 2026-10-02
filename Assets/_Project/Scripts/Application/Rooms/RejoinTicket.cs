using System;

namespace Callbreak.Application.Rooms
{
    /// <summary>
    /// GDD §29 session.json: the private room whose match this device was playing, as a guest or as
    /// a host whose match would be handed over to the next host in line (ADR 030), so the next
    /// launch can offer "Rejoin match" after an app kill (GDD §27), and the match's id, so that not
    /// going back to it counts as having left it (GDD §8).
    /// </summary>
    [Serializable]
    public sealed class RejoinTicket
    {
        public int schemaVersion = 1;
        public string roomCode;

        /// <summary>Empty in a ticket written before the id was kept: such a ticket still rejoins, but settles nothing.</summary>
        public string matchId;

        public long savedAtUnixSeconds;
    }
}
