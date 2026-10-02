namespace Callbreak.Application.Online
{
    /// <summary>
    /// Quick Match timings (GDD §6 "Timing defaults", §11 screen 05; "Remote Config" values there),
    /// as the owner set them for real players (ADR 029): a 20 s countdown from the tap; seats for
    /// people only until 14 s; then bots fill the empty seats one by one, about a second apart; and
    /// once the table is full, the same 3 s countdown on every screen before the match starts.
    /// Without a table directory (no peer-to-peer network, or it cannot be reached) the device plays
    /// at a table of its own, with the short bot-fill delay below.
    /// </summary>
    public sealed class QuickMatchConfig
    {
        /// <param name="botFillDelaySeconds">A table of the device's own: how long before its empty seats fill with bots.</param>
        /// <param name="seatRevealSeconds">A table of the device's own: the pause between one bot appearing and the next.</param>
        /// <param name="tableFoundSeconds">A table of the device's own: the "Table found" beat before the match.</param>
        /// <param name="clocks">The table's clocks; null gives Quick Match's own (20 s to call, 15 s to play a card, 10 s of round results).</param>
        /// <param name="countdownSeconds">The countdown from the player's tap: the match starts by then.</param>
        /// <param name="peopleOnlySeconds">Until this point of the countdown only people take seats; then bots fill the rest.</param>
        /// <param name="botGapMinSeconds">The shortest pause between one bot taking a seat and the next.</param>
        /// <param name="botGapMaxSeconds">The longest pause between one bot taking a seat and the next.</param>
        /// <param name="startCountdownSeconds">The "Table found" countdown, the same on every screen, from a full table to the start.</param>
        /// <param name="searchIntervalSeconds">How often a host still alone at its table looks for an older one to move to.</param>
        public QuickMatchConfig(
            double botFillDelaySeconds = 6.0,
            double seatRevealSeconds = 0.6,
            double tableFoundSeconds = 1.5,
            TableClocks clocks = null,
            double progressIntervalSeconds = 0.25,
            double countdownSeconds = 20.0,
            double peopleOnlySeconds = 14.0,
            double botGapMinSeconds = 0.8,
            double botGapMaxSeconds = 1.2,
            double startCountdownSeconds = 3.0,
            double searchIntervalSeconds = 2.5)
        {
            BotFillDelaySeconds = botFillDelaySeconds;
            SeatRevealSeconds = seatRevealSeconds;
            TableFoundSeconds = tableFoundSeconds;
            Clocks = clocks ?? TableClocks.QuickMatch;
            ProgressIntervalSeconds = progressIntervalSeconds;
            CountdownSeconds = countdownSeconds;
            PeopleOnlySeconds = peopleOnlySeconds;
            BotGapMinSeconds = botGapMinSeconds;
            BotGapMaxSeconds = botGapMaxSeconds < botGapMinSeconds ? botGapMinSeconds : botGapMaxSeconds;
            StartCountdownSeconds = startCountdownSeconds;
            SearchIntervalSeconds = searchIntervalSeconds;
        }

        /// <summary>A table of the device's own: how long the search runs before its empty seats are filled with bots.</summary>
        public double BotFillDelaySeconds { get; }

        /// <summary>A table of the device's own: the pause between one bot seat appearing and the next.</summary>
        public double SeatRevealSeconds { get; }

        /// <summary>A table of the device's own: GDD's "Table found" beat, 1.5 s after 4/4.</summary>
        public double TableFoundSeconds { get; }

        /// <summary>The call timer, the card timer and the round-results countdown of the table that is found.</summary>
        public TableClocks Clocks { get; }

        /// <summary>How often the elapsed time is reported while nothing else changes.</summary>
        public double ProgressIntervalSeconds { get; }

        /// <summary>The countdown from the player's tap; the match starts by its end (ADR 029).</summary>
        public double CountdownSeconds { get; }

        /// <summary>Until this point of the countdown only people take seats; then bots fill the empty ones.</summary>
        public double PeopleOnlySeconds { get; }

        /// <summary>The shortest pause between one bot taking a seat and the next.</summary>
        public double BotGapMinSeconds { get; }

        /// <summary>The longest pause between one bot taking a seat and the next.</summary>
        public double BotGapMaxSeconds { get; }

        /// <summary>The "Table found" countdown from a full table to its start, the same on every screen.</summary>
        public double StartCountdownSeconds { get; }

        /// <summary>How often a host that is still alone at its table looks for an older table to move to.</summary>
        public double SearchIntervalSeconds { get; }
    }
}
