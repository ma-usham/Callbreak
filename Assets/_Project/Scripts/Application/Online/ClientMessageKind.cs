namespace Callbreak.Application.Online
{
    /// <summary>What a member sends the host. The first byte of every payload.</summary>
    public enum ClientMessageKind : byte
    {
        Hello = 1,
        SetReady = 2,
        LeaveRoom = 3,
        TableReady = 4,
        PlaceCall = 5,
        PlayCard = 6,
        ReadyNext = 7,
        Emote = 8,
        RequestResync = 9,
        Presence = 10,
        LeaveTable = 11,

        /// <summary>A hello to a host that took the table over, with what this player holds (ADR 030).</summary>
        Report = 12,
    }
}
