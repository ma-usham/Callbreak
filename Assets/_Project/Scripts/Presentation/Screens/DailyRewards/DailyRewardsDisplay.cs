namespace Callbreak.Presentation.Screens
{
    /// <summary>How a day tile of the Daily Rewards page looks (mockup 26).</summary>
    public enum DayTileState
    {
        /// <summary>Paid this week: the gold check.</summary>
        Claimed,

        /// <summary>Today's, ready to claim: the crimson frame and the light behind its coins.</summary>
        Today,

        /// <summary>A later day: the quiet ivory (or the gold bonus) tile.</summary>
        Upcoming,
    }

    /// <summary>
    /// What the Daily Rewards page shows for a week of <c>IDailyRewards</c> (mockup 26): each tile's
    /// look, the string keys and the day the footer names. Days before today are claimed; today is
    /// ready until it is claimed, then claimed too. The footer always names tomorrow's day, Day 1
    /// after Day 7, as the mockup's "Come back tomorrow for Day 2" does beside a Day 1 still to
    /// claim. Engine-free.
    /// </summary>
    public static class DailyRewardsDisplay
    {
        public const string ProgressKey = "ui.daily_rewards.progress";
        public const string DayKey = "ui.daily_rewards.day";
        public const string AmountKey = "ui.daily_rewards.amount";
        public const string ClaimKey = "ui.daily_rewards.claim";
        public const string ClaimedKey = "ui.daily_rewards.claimed";
        public const string NextKey = "ui.daily_rewards.next";

        /// <param name="index">The tile, 0 (Day 1) to 6 (Day 7).</param>
        /// <param name="today">Today's day of the week, 0 to 6.</param>
        /// <param name="canClaim">Today's reward is still to claim.</param>
        public static DayTileState StateOf(int index, int today, bool canClaim)
        {
            if (index < today)
            {
                return DayTileState.Claimed;
            }

            if (index > today)
            {
                return DayTileState.Upcoming;
            }

            return canClaim ? DayTileState.Today : DayTileState.Claimed;
        }

        /// <summary>Tomorrow's day as the player reads it (1 to <paramref name="days"/>): Day 1 after the last.</summary>
        public static int NextDayNumber(int today, int days) => days <= 0 ? 1 : ((today + 1) % days) + 1;
    }
}
