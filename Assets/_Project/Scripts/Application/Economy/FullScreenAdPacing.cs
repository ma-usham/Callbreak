using System;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// When the last full-screen ad the game chose to show came up, shared by the ads between matches
    /// (<see cref="BetweenMatchAds"/>) and the app open ads on the return to the game
    /// (<see cref="ReturnToGameAds"/>): at most one of either every <see cref="MinInterval"/>, on the
    /// wall clock (the owner, 15 Oct 2026). The Earn Coins ads, which the player asks for, don't count.
    /// It lives in memory, so each launch starts afresh.
    /// </summary>
    public sealed class FullScreenAdPacing
    {
        /// <summary>The least time from one full-screen ad coming up to the next: at most one every three minutes.</summary>
        public static readonly TimeSpan MinInterval = TimeSpan.FromMinutes(3);

        /// <summary>When the last one came up; null while none has this launch.</summary>
        public DateTime? LastAdUtc { get; set; }
    }
}
