using System;

namespace Callbreak.Application.Economy
{
    /// <summary>ad-rewards.json: the Earn Coins set in progress. Plain serializable data read and written by <see cref="AdRewards"/>.</summary>
    [Serializable]
    public sealed class AdRewardsData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;

        /// <summary>Ads completed in the set, 0 to 3. A set whose deadline has passed reads as a new one.</summary>
        public int watched;

        /// <summary>When the set resets (its first completed ad + 5 h), in Unix seconds (UTC); 0 before its first ad.</summary>
        public long resetsAtUnixSeconds;
    }
}
