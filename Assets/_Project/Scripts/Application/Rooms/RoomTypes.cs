using Callbreak.Core.Rules;

namespace Callbreak.Application.Rooms
{
    /// <summary>What the host chooses on Create Room (GDD §11 screen 02). Players and rounds are fixed in v1.</summary>
    public sealed class RoomSettings
    {
        /// <summary>A room whose matches are as long as the rules say (<see cref="DefaultRounds"/>).</summary>
        public RoomSettings(string name, int turnSeconds)
            : this(name, turnSeconds, DefaultRounds)
        {
        }

        /// <param name="rounds">The match length the host published, as a member reads it from the lobby.</param>
        public RoomSettings(string name, int turnSeconds, int rounds)
        {
            Name = name;
            TurnSeconds = turnSeconds;
            Rounds = rounds;
        }

        /// <summary>
        /// The match length of every room in v1 (GDD §5 "Rounds", the "5 rounds" tile on Create
        /// Room): the rule set's, never a number of its own.
        /// </summary>
        public static int DefaultRounds => RuleSet.NepaliStandard.Rounds;

        /// <summary>3-20 characters, trimmed and filtered.</summary>
        public string Name { get; }

        /// <summary>15 or 30.</summary>
        public int TurnSeconds { get; }

        /// <summary>The rounds of a match in this room.</summary>
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

        /// <summary>The host removed this player from the room.</summary>
        Kicked,

        /// <summary>The same player connected again from elsewhere and took the seat over.</summary>
        Replaced,

        /// <summary>The connection to the host was lost and could not be restored.</summary>
        ConnectionLost,
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
