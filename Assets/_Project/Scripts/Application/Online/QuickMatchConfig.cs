namespace Callbreak.Application.Online
{
    /// <summary>
    /// Quick Match timings (GDD §11 screen 05; "Remote Config" values there). The bot-fill delay is
    /// 25 s in the GDD, which assumes a queue that other people can join during the wait. Until a
    /// matchmaker exists nobody can, so the default here is short; set it back to 25 when one does.
    /// </summary>
    public sealed class QuickMatchConfig
    {
        public QuickMatchConfig(
            double botFillDelaySeconds = 6.0,
            double seatRevealSeconds = 0.6,
            double tableFoundSeconds = 1.5,
            int turnSeconds = 30,
            double progressIntervalSeconds = 0.25)
        {
            BotFillDelaySeconds = botFillDelaySeconds;
            SeatRevealSeconds = seatRevealSeconds;
            TableFoundSeconds = tableFoundSeconds;
            TurnSeconds = turnSeconds;
            ProgressIntervalSeconds = progressIntervalSeconds;
        }

        /// <summary>How long the search runs before the empty seats are filled with bots.</summary>
        public double BotFillDelaySeconds { get; }

        /// <summary>The pause between one bot seat appearing and the next.</summary>
        public double SeatRevealSeconds { get; }

        /// <summary>GDD: the match starts 1.5 s after 4/4, the "Table found" beat.</summary>
        public double TableFoundSeconds { get; }

        public int TurnSeconds { get; }

        /// <summary>How often the elapsed time is reported while nothing else changes.</summary>
        public double ProgressIntervalSeconds { get; }
    }
}
