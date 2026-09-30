namespace Callbreak.Application.Online
{
    /// <summary>
    /// Quick Match timings (GDD §6 "Timing defaults", §11 screen 05; "Remote Config" values
    /// there). The bot-fill delay is 25 s in the GDD, which assumes a queue that other people can
    /// join during the wait. Until a matchmaker exists nobody can, so the default here is short;
    /// set it back to 25 when one does.
    /// </summary>
    public sealed class QuickMatchConfig
    {
        /// <param name="clocks">The table's clocks; null gives Quick Match's own (20 s to call, 15 s to play a card, 10 s of round results).</param>
        public QuickMatchConfig(
            double botFillDelaySeconds = 6.0,
            double seatRevealSeconds = 0.6,
            double tableFoundSeconds = 1.5,
            TableClocks clocks = null,
            double progressIntervalSeconds = 0.25)
        {
            BotFillDelaySeconds = botFillDelaySeconds;
            SeatRevealSeconds = seatRevealSeconds;
            TableFoundSeconds = tableFoundSeconds;
            Clocks = clocks ?? TableClocks.QuickMatch;
            ProgressIntervalSeconds = progressIntervalSeconds;
        }

        /// <summary>How long the search runs before the empty seats are filled with bots.</summary>
        public double BotFillDelaySeconds { get; }

        /// <summary>The pause between one bot seat appearing and the next.</summary>
        public double SeatRevealSeconds { get; }

        /// <summary>GDD: the match starts 1.5 s after 4/4, the "Table found" beat.</summary>
        public double TableFoundSeconds { get; }

        /// <summary>The call timer, the card timer and the round-results countdown of the table that is found.</summary>
        public TableClocks Clocks { get; }

        /// <summary>How often the elapsed time is reported while nothing else changes.</summary>
        public double ProgressIntervalSeconds { get; }
    }
}
