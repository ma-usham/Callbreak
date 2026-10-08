using System;

namespace Callbreak.Application.Economy
{
    /// <summary>daily-rewards.json: the week of login rewards in progress. Plain serializable data read and written by <see cref="DailyRewards"/>.</summary>
    [Serializable]
    public sealed class DailyRewardsData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;

        /// <summary>The calendar day of the last claim on the device's clock (<see cref="DailyRewards.DayNumber"/>); 0 before the first.</summary>
        public int lastClaimDay;

        /// <summary>Which day of the week that claim paid, 0 (Day 1) to 6 (Day 7).</summary>
        public int lastClaimIndex;

        /// <summary>The calendar day the page last opened by itself; 0 before the first time.</summary>
        public int autoOpenDay;
    }
}
