using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// A rewarded video ad (port). On phones it is Google AdMob (<see cref="PreloadedRewardedAds"/>
    /// over the AdMob adapter); in the Editor, in the tests and on desktop a stand-in completes
    /// every ad.
    /// </summary>
    public interface IRewardedAds
    {
        /// <summary>An ad can be shown now.</summary>
        bool IsAvailable { get; }

        /// <summary>Shows one ad; true only when it was watched to the end and earned its reward.</summary>
        Task<bool> ShowAsync(CancellationToken ct);
    }

    /// <summary>
    /// The Earn Coins page (mockup 22; designer's spec rewarded-coins-prompts.md): three ads per set,
    /// paying 20, 30 and 50 coins, each credited the moment its ad completes. The set resets five
    /// hours after its first completed ad (wall-clock time, so time away counts), keeping the coins
    /// earned. A failed, skipped or unavailable ad credits nothing and does not advance.
    /// </summary>
    public interface IAdRewards
    {
        event Action Changed;

        /// <summary>The payouts in order: 20, 30, 50.</summary>
        IReadOnlyList<int> Rewards { get; }

        /// <summary>Ads completed in the current set, 0 to <see cref="Rewards"/>.Count.</summary>
        int Watched { get; }

        /// <summary>What the next ad pays; 0 while every ad of the set has been watched.</summary>
        int NextReward { get; }

        /// <summary>When the set resets: its first completed ad + 5 h; null before the set's first ad.</summary>
        DateTime? ResetsAtUtc { get; }

        /// <summary>Shows the next ad and credits its reward; the coins credited, 0 when none were.</summary>
        Task<int> WatchNextAsync(CancellationToken ct);
    }
}
