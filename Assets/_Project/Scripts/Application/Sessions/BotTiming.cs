namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// How long computer players take (GDD §6 timing defaults, §9 "bots never act instantly"):
    /// a varied think time, a short one when the move is forced, and a pause while a finished
    /// trick stays on the table before it is swept to its winner. A bot's think time starts once
    /// the card before it has landed on the table, so every card rests there for a think time of
    /// its own before the next one is thrown (ADR 055).
    /// </summary>
    public sealed class BotTiming
    {
        /// <summary>
        /// How long the table shows a played card flying to the trick (GDD §14 "Card played",
        /// 260 ms; the table's CardFlight.Seconds).
        /// </summary>
        public const double TableCardFlightSeconds = 0.26;

        public BotTiming(double thinkMinSeconds = 0.6, double thinkMaxSeconds = 1.4, double forcedSeconds = 0.5, double trickDisplaySeconds = 1.25, double dealSeconds = 2.7, double cardFlightSeconds = 0.0)
        {
            ThinkMinSeconds = thinkMinSeconds;
            ThinkMaxSeconds = thinkMaxSeconds;
            ForcedSeconds = forcedSeconds;
            TrickDisplaySeconds = trickDisplaySeconds;
            DealSeconds = dealSeconds;
            CardFlightSeconds = cardFlightSeconds;
        }

        /// <summary>
        /// Offline defaults: 0.6-1.4 s think, 0.5 s forced, each counted from when the card before
        /// has landed (0.26 s); 0.9 s + 0.35 s sweep; 2.7 s shuffle and deal.
        /// </summary>
        public static BotTiming Offline { get; } = new BotTiming(cardFlightSeconds: TableCardFlightSeconds);

        /// <summary>
        /// Hosted tables, Quick Match and private rooms alike (GDD §6): bots think 0.8-1.6 s; a
        /// forced move, the card's flight, the finished trick and the deal take as long as they do offline.
        /// </summary>
        public static BotTiming Hosted { get; } = new BotTiming(thinkMinSeconds: 0.8, thinkMaxSeconds: 1.6, cardFlightSeconds: TableCardFlightSeconds);

        /// <summary>No delays at all, for tests and simulations.</summary>
        public static BotTiming Instant { get; } = new BotTiming(0, 0, 0, 0, 0);

        public double ThinkMinSeconds { get; }

        public double ThinkMaxSeconds { get; }

        /// <summary>
        /// A bot with one legal card waits this long after the card before it has landed: 0.5 s,
        /// so even a forced card rests on the table long enough to be seen (ADR 055; GDD §9 had 0.35 s).
        /// </summary>
        public double ForcedSeconds { get; }

        /// <summary>A completed trick stays 0.9 s, then sweeps for 0.35 s (GDD §6), once its last card has landed.</summary>
        public double TrickDisplaySeconds { get; }

        /// <summary>
        /// The shuffle and the deal at the start of a round (ADR 022: 1.1 s + 1.6 s; GDD §14 had the
        /// deal alone). Nobody moves before the table has dealt, so it never falls behind and
        /// plays the deal at double speed (ADR 024).
        /// </summary>
        public double DealSeconds { get; }

        /// <summary>
        /// How long a played card flies to the trick on the table (<see cref="TableCardFlightSeconds"/>
        /// for real tables, 0 for tests): no bot starts thinking, and no finished trick starts its
        /// pause, before it has landed (ADR 055).
        /// </summary>
        public double CardFlightSeconds { get; }
    }
}
