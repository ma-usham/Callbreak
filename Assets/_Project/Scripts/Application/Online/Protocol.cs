namespace Callbreak.Application.Online
{
    /// <summary>
    /// Constants of the table protocol (GDD §25). <see cref="Version"/> is checked when a member
    /// says hello; a mismatch is "Update required". Bump it whenever a message changes.
    /// </summary>
    public static class Protocol
    {
        public const ushort Version = 1;

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

        /// <summary>GDD §11 screen 09: the round summary's countdown online.</summary>
        public const double SummarySeconds = 10.0;

        /// <summary>How long the host waits for every member's table to load before dealing anyway.</summary>
        public const double TableReadyWaitSeconds = 8.0;

        public const int MaxNameBytes = 96;
    }
}
