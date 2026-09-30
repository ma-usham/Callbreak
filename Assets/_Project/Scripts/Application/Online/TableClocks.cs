namespace Callbreak.Application.Online
{
    /// <summary>
    /// A hosted table's clocks (GDD §6 "Timing defaults"): how long a person has to call, how
    /// long to play a card, and how long the round results wait before the next round starts by
    /// itself. Quick Match has 20 s to call, 15 s for a card and 10 s of results; a private room
    /// uses the host's choice (15 or 30 s) for both kinds of turn and 15 s of results.
    /// </summary>
    public sealed class TableClocks
    {
        public const int QuickMatchCallSeconds = 20;
        public const int QuickMatchPlaySeconds = 15;
        public const double QuickMatchSummarySeconds = 10.0;
        public const double PrivateRoomSummarySeconds = 15.0;

        /// <param name="callSeconds">The call timer; 0 runs call turns without a clock.</param>
        /// <param name="playSeconds">The card timer; 0 runs play turns without a clock.</param>
        /// <param name="summarySeconds">The round results' countdown.</param>
        public TableClocks(int callSeconds, int playSeconds, double summarySeconds)
        {
            CallSeconds = callSeconds > 0 ? callSeconds : 0;
            PlaySeconds = playSeconds > 0 ? playSeconds : 0;
            SummarySeconds = summarySeconds > 0.0 ? summarySeconds : 0.0;
        }

        /// <summary>Quick Match: 20 s to call, 15 s to play a card, a 10 s round-results countdown.</summary>
        public static TableClocks QuickMatch { get; } = new TableClocks(QuickMatchCallSeconds, QuickMatchPlaySeconds, QuickMatchSummarySeconds);

        /// <summary>The seconds a person has to call; 0 means call turns have no clock.</summary>
        public int CallSeconds { get; }

        /// <summary>The seconds a person has to play a card; 0 means play turns have no clock.</summary>
        public int PlaySeconds { get; }

        /// <summary>How long the round results stay before the next round starts anyway (GDD §11 screen 09).</summary>
        public double SummarySeconds { get; }

        /// <summary>A private room: the host's turn time for calling and for playing, a 15 s round-results countdown.</summary>
        public static TableClocks PrivateRoom(int turnSeconds)
        {
            return new TableClocks(turnSeconds, turnSeconds, PrivateRoomSummarySeconds);
        }
    }
}
