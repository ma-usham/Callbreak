using System;

namespace Callbreak.Application.Economy
{
    /// <summary>no-ads.json: whether the player owns No Ads. Plain serializable data read and written by <see cref="StoreNoAds"/>.</summary>
    [Serializable]
    public sealed class NoAdsData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;

        /// <summary>The player owns No Ads: bought here, or listed by the store for their account.</summary>
        public bool owned;
    }
}
