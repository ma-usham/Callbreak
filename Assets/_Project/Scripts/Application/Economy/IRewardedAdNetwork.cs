using System;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// A rewarded-ad network's SDK (port), as <see cref="PreloadedRewardedAds"/> drives it: get ready
    /// (the player's consent first, then the SDK), load one ad, show it. Every task completes, and
    /// every callback comes, on the main thread; nothing throws: a failure is a false, a null or the
    /// failed callback.
    /// </summary>
    public interface IRewardedAdNetwork
    {
        /// <summary>
        /// Gets the network ready to request ads: the player's consent where the law asks for it (a
        /// consent form may show), then the SDK. True when ads may be requested; false when they may
        /// not yet (no consent, the form failed, no network, no ad unit), so it can be asked again.
        /// </summary>
        Task<bool> StartAsync();

        /// <summary>Loads one ad; null when none came (no fill, no network, no consent).</summary>
        Task<IRewardedAd> LoadAsync();
    }

    /// <summary>One loaded rewarded ad, shown at most once and then destroyed.</summary>
    public interface IRewardedAd
    {
        /// <summary>The ad can still be shown: not shown yet and not expired.</summary>
        bool CanShow { get; }

        /// <summary>
        /// Shows the ad full screen. Then <paramref name="rewarded"/> once the player has earned the
        /// reward (usually before the ad is closed) and <paramref name="closed"/> once the player has
        /// closed it; or <paramref name="failed"/>, with the reason, when it can't be shown.
        /// </summary>
        void Show(Action rewarded, Action closed, Action<string> failed);

        /// <summary>Releases the ad.</summary>
        void Destroy();
    }
}
