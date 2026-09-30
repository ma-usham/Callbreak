namespace Callbreak.Application.Rooms
{
    /// <summary>What the host chooses on Create Room (GDD §11 screen 02). Players and rounds are fixed in v1.</summary>
    public sealed class RoomSettings
    {
        public RoomSettings(string name, int turnSeconds, int rounds = 5)
        {
            Name = name;
            TurnSeconds = turnSeconds;
            Rounds = rounds;
        }

        /// <summary>3-20 characters, trimmed and filtered.</summary>
        public string Name { get; }

        /// <summary>15 or 30.</summary>
        public int TurnSeconds { get; }

        public int Rounds { get; }
    }

    /// <summary>One lobby seat as the lobby screen shows it (GDD §11 screen 04). The host is seat 0.</summary>
    public sealed class RoomSeat
    {
        public RoomSeat(int index, bool occupied, string name, int avatarId, string deckThemeId, bool isHost, bool isReady, bool isBot, bool isLocal)
        {
            Index = index;
            Occupied = occupied;
            Name = name;
            AvatarId = avatarId;
            DeckThemeId = deckThemeId;
            IsHost = isHost;
            IsReady = isReady;
            IsBot = isBot;
            IsLocal = isLocal;
        }

        public int Index { get; }

        public bool Occupied { get; }

        public string Name { get; }

        public int AvatarId { get; }

        public string DeckThemeId { get; }

        public bool IsHost { get; }

        public bool IsReady { get; }

        public bool IsBot { get; }

        public bool IsLocal { get; }
    }

    /// <summary>Why creating or joining a room failed (GDD §11 screens 02-03). Each has its own message.</summary>
    public enum RoomError
    {
        None,
        NotFound,
        Full,
        AlreadyStarted,
        UpdateRequired,
        Offline,
        RateLimited,
        HostLeft,
        Unknown,
    }

    public readonly struct RoomResult
    {
        public RoomResult(IRoomSession session, RoomError error)
        {
            Session = session;
            Error = error;
        }

        public IRoomSession Session { get; }

        public RoomError Error { get; }

        public bool Succeeded => Error == RoomError.None && Session != null;
    }
}
