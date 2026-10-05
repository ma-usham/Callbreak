using System;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// An interstitial-ad network's SDK (port), as <see cref="PreloadedInterstitialAds"/> drives it:
    /// get ready (the player's consent first, then the SDK), load one ad, show it. Every task
    /// completes, and every callback comes, on the main thread; nothing throws: a failure is a
    /// false, a null or the failed callback.
    /// </summary>
    public interface IInterstitialAdNetwork
    {
        /// <summary>
        /// Gets the network ready to request ads: the player's consent where the law asks for it (a
        /// consent form may show), then the SDK. True when ads may be requested; false when they may
        /// not yet (no consent, the form failed, no network, no ad unit), so it can be asked again.
        /// </summary>
        Task<bool> StartAsync();

        /// <summary>Loads one ad; null when none came (no fill, no network, no consent).</summary>
        Task<IInterstitialAd> LoadAsync();
    }

    /// <summary>One loaded interstitial ad, shown at most once and then destroyed.</summary>
    public interface IInterstitialAd
    {
        /// <summary>The ad can still be shown: not shown yet and not expired.</summary>
        bool CanShow { get; }

        /// <summary>
        /// Shows the ad full screen. Then <paramref name="closed"/> once the player has closed it; or
        /// <paramref name="failed"/>, with the reason, when it can't be shown.
        /// </summary>
        void Show(Action closed, Action<string> failed);

        /// <summary>Releases the ad.</summary>
        void Destroy();
    }
}
