namespace Callbreak.Application.Online
{
    /// <summary>
    /// Constants of the table protocol (GDD §25). <see cref="Version"/> is checked when a member
    /// says hello; a mismatch is "Update required". Bump it whenever a message changes.
    /// </summary>
    public static class Protocol
    {
        /// <summary>
        /// 2: a hello names the player by its <see cref="RoomIdentity"/> instead of its player id,
        /// and its version is read before anything else in it; every text field has a byte limit
        /// and a member's payload a size limit; a clock's time left may be longer than the clock
        /// (it starts after the table's display hold, see <see cref="TableStatus"/>); and a player
        /// back after the match ended is sent its SyncState before the lobby answers.
        /// </summary>
        public const ushort Version = 2;

        /// <summary>GDD §25 anti-abuse: a connection may send this many intents per second.</summary>
        public const int IntentsPerSecond = 10;

        /// <summary>GDD §8: one emote every 3 s and 10 per round, enforced by the host.</summary>
        public const double EmoteIntervalSeconds = 3.0;

        public const int EmotesPerRound = 10;

        /// <summary>GDD §8: a dropped player's seat is held this long; a bot covers the turns.</summary>
        public const double SeatHoldSeconds = 90.0;

        /// <summary>GDD §8: the grace added to a turn clock to absorb latency.</summary>
        public const double TurnGraceSeconds = 0.5;

        /// <summary>GDD §8: this many timeouts in a row mark a seat Away.</summary>
        public const int TimeoutsBeforeAway = 2;

        /// <summary>How long the host waits for every member's table to load before dealing anyway.</summary>
        public const double TableReadyWaitSeconds = 8.0;

        /// <summary>The longest name on the wire in UTF-8 bytes: 16 characters of any script (GDD §11 screen 14), or a room name.</summary>
        public const int MaxNameBytes = 96;

        /// <summary>The longest identity a hello may carry (a <see cref="RoomIdentity"/> is 32 bytes).</summary>
        public const int MaxIdentityBytes = 64;

        /// <summary>The longest id on the wire: a deck, an avatar, a rule set, a room code or a match id.</summary>
        public const int MaxIdBytes = 64;

        /// <summary>
        /// The largest payload a member ever sends: a hello with every field at its limit is 234
        /// bytes. The host drops anything larger unread, before it is even copied.
        /// </summary>
        public const int MaxClientPayloadBytes = 256;
    }
}
