using System;

namespace Callbreak.Application.Rooms
{
    /// <summary>
    /// GDD §29 session.json: the private room whose match this device was playing as a guest, so
    /// the next launch can offer "Rejoin match" after an app kill (GDD §27).
    /// </summary>
    [Serializable]
    public sealed class RejoinTicket
    {
        public int schemaVersion = 1;
        public string roomCode;
        public long savedAtUnixSeconds;
    }
}
