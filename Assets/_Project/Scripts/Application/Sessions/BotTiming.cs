namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// How long computer players take (GDD §6 timing defaults, §9 "bots never act instantly"):
    /// a varied think time, a short one when the move is forced, and a pause while a finished
    /// trick stays on the table before it is swept to its winner.
    /// </summary>
    public sealed class BotTiming
    {
        public BotTiming(double thinkMinSeconds = 0.6, double thinkMaxSeconds = 1.4, double forcedSeconds = 0.35, double trickDisplaySeconds = 1.25, double dealSeconds = 1.6)
        {
            ThinkMinSeconds = thinkMinSeconds;
            ThinkMaxSeconds = thinkMaxSeconds;
            ForcedSeconds = forcedSeconds;
            TrickDisplaySeconds = trickDisplaySeconds;
            DealSeconds = dealSeconds;
        }

        /// <summary>Offline defaults: 0.6-1.4 s think, 0.35 s forced, 0.9 s + 0.35 s sweep, 1.6 s deal.</summary>
        public static BotTiming Offline { get; } = new BotTiming();

        /// <summary>No delays at all, for tests and simulations.</summary>
        public static BotTiming Instant { get; } = new BotTiming(0, 0, 0, 0, 0);

        public double ThinkMinSeconds { get; }

        public double ThinkMaxSeconds { get; }

        public double ForcedSeconds { get; }

        /// <summary>A completed trick stays 0.9 s, then sweeps for 0.35 s (GDD §6).</summary>
        public double TrickDisplaySeconds { get; }

        /// <summary>The deal animation at the start of a round (GDD §14: 1.6 s).</summary>
        public double DealSeconds { get; }
    }
}
