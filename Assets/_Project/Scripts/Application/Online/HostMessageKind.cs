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
    }
}
