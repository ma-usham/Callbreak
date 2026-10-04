using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using UnityEngine;

namespace Callbreak.Infrastructure.Services
{
    /// <summary>
    /// A STAND-IN for a rewarded-ad network, for the Editor, the tests and desktop builds: it shows
    /// no ad at all, and every "ad" completes and earns its reward after <see cref="DelaySeconds"/>,
    /// so the Earn Coins page can be played end to end without one. Phones get Google AdMob instead
    /// (ProjectLifetimeScope.RegisterRewardedAds; Docs/Setup/AdMob.md). The Editor and development
    /// builds log each stand-in ad, so it is never taken for a real one.
    /// </summary>
    public sealed class StandInRewardedAds : IRewardedAds
    {
        /// <summary>How long a stand-in "ad" takes, so the page's waiting state can be seen.</summary>
        public const float DelaySeconds = 1.5f;

        private readonly bool _log;

        public StandInRewardedAds()
        {
            _log = Debug.isDebugBuild;
        }

        public bool IsAvailable => true;

        public async Task<bool> ShowAsync(CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(DelaySeconds), ct);
            if (_log)
            {
                Debug.Log("[StandInRewardedAds] A stand-in ad completed: no ad network is connected, so no ad was shown.");
            }

            return true;
        }
    }
}
