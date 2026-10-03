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
        /// 3: the lobby says when a full Quick Match table starts (RoomSnapshot.StartsInMs), and the
        /// bots that fill a Quick Match table sit in its lobby (ADR 029).
        /// 4: a waiting Quick Match host says it is still there about every second (Heartbeat), and a
        /// guest that hears nothing from it for a few seconds looks for another table (ADR 029).
        /// 5: a host whose players can take the table over (ADR 030) names the next host in line to
        /// everyone (NextHost), sends that player alone what it needs to take over (Succession), and
        /// keeps up its heartbeat during the match; a player who goes to a new host introduces itself
        /// with what it holds (Report).
        /// 6: a host that leaves during the match tells everyone to go to the next host in line now
        /// (HandOverNow) instead of closing the room (ADR 030).
        /// 7: the host names the whole line of succession (NextHost, HandOverNow: every person after it
        /// who can take over, with their addresses), and sends every successor after the first a
        /// package without cards (Succession), so a backup host takes over when the first cannot.
        /// </summary>
        public const ushort Version = 7;

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
        /// bytes. The host drops anything larger unread, before it is even copied. The one exception
        /// is a <see cref="ClientMessageKind.Report"/> (<see cref="MaxReportBytes"/>).
        /// </summary>
        public const int MaxClientPayloadBytes = 256;

        /// <summary>
        /// The largest <see cref="ClientMessageKind.Report"/>: a hello and the player's whole view,
        /// which at every limit (four 96-byte names, five rounds, 13 tricks) stays under 1.5 KB. Only a
        /// host that took a table over (ADR 030) is sent one.
        /// </summary>
        public const int MaxReportBytes = 4096;

        /// <summary>
        /// ADR 030: while a match is on, a host whose players can take the table over tells them it is
        /// still there this often, so that they notice when it is not.
        /// </summary>
        public const double HeartbeatSeconds = 1.0;

        /// <summary>ADR 030: during a match, a player who hears nothing from its host for this long takes the host as lost.</summary>
        public const double HostLostSeconds = 4.0;

        /// <summary>
        /// ADR 030: how long a player who takes a table over waits for the other players' reports
        /// before it rebuilds the table without the missing ones.
        /// </summary>
        public const double HandOverWaitSeconds = 8.0;

        /// <summary>ADR 030: each try to reach the new host, or the old one, gives up after this long; the next one follows.</summary>
        public const double HandOverAttemptSeconds = 3.0;

        /// <summary>
        /// The owner's backup host (3 Oct 2026): the k-th person in the line of succession (k ≥ 2) takes
        /// the match over itself when no host has seated it within (k − 1) times this long of looking
        /// for one after it took the host as lost.
        /// </summary>
        public const double BackupTakeOverSeconds = 10.0;
    }
}
