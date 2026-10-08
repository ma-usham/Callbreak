using System;
using System.Collections.Generic;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The weekly login rewards (mockup 26, the designer's 26-daily-rewards-prompt.md; the owner,
    /// 8 Oct 2026): seven days paying 10, 15, 20, 25, 30, 40 and 60 coins, one claim per calendar day.
    /// Claiming on consecutive days walks the week; a missed day starts it again at Day 1, and after
    /// Day 7 a new week starts. The Daily Rewards page opens by itself once a day while a reward is
    /// ready, and from the main menu's button below No Ads.
    /// </summary>
    public interface IDailyRewards
    {
        /// <summary>A claim was paid. A new day comes without an event: a page reads the properties again.</summary>
        event Action Changed;

        /// <summary>What each day of the week pays, Day 1 first.</summary>
        IReadOnlyList<int> Rewards { get; }

        /// <summary>Today's day of the week, 0 (Day 1) to 6 (Day 7): the one to claim, or the one claimed today.</summary>
        int Day { get; }

        /// <summary>Today's reward is ready: <see cref="Day"/> has not been claimed today.</summary>
        bool CanClaim { get; }

        /// <summary>Pays today's reward into the wallet; the coins credited, 0 when there was nothing to claim.</summary>
        int Claim();

        /// <summary>
        /// True at most once per calendar day, and only while today's reward is ready: the page should
        /// open by itself now. Saying true counts as the day's opening, also across restarts.
        /// </summary>
        bool TakeAutoOpen();
    }
}
