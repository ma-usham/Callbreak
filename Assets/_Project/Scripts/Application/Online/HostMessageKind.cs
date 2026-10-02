namespace Callbreak.Application.Online
{
    /// <summary>What the host sends a member. The first byte of every payload.</summary>
    public enum HostMessageKind : byte
    {
        Welcome = 1,
        Rejected = 2,
        RoomState = 3,
        RoomClosed = 4,
        MatchStarting = 5,
        TableBatch = 6,
        SyncState = 7,
        IntentRejected = 8,
        EmoteShown = 9,

        /// <summary>The host is still there (ADR 029, ADR 030): nothing follows.</summary>
        Heartbeat = 10,

        /// <summary>Who takes the table over if the host is lost, and where to find both (ADR 030): to every player.</summary>
        NextHost = 11,

        /// <summary>What the next host in line needs to take the table over (ADR 030): to that player alone.</summary>
        Succession = 12,
    }
}
