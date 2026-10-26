using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// A banner-ad network's SDK (port), as <see cref="BannerAds"/> drives it: get ready (the
    /// player's consent first, then the SDK), load one banner, then place it on the screen and take
    /// it off again. Every task completes, and every call comes, on the main thread; nothing throws:
    /// a failure is a false or a null.
    /// </summary>
    public interface IBannerAdNetwork
    {
        /// <summary>
        /// Gets the network ready to request ads: the player's consent where the law asks for it, then
        /// the SDK. True when ads may be requested; false when they may not yet (no consent, no
        /// network, no ad unit), so it can be asked again.
        /// </summary>
        Task<bool> StartAsync();

        /// <summary>Loads one banner, off the screen; null when none came (no fill, no network, no consent).</summary>
        Task<IBannerAd> LoadAsync();
    }

    /// <summary>
    /// One loaded banner. The network refreshes what it shows by itself while it is up. Places are in
    /// screen pixels, from the screen's top-left corner.
    /// </summary>
    public interface IBannerAd
    {
        /// <summary>The banner's width on the screen, in pixels.</summary>
        int Width { get; }

        /// <summary>The banner's height on the screen, in pixels.</summary>
        int Height { get; }

        /// <summary>Puts the banner on the screen with its top-left corner at (<paramref name="x"/>, <paramref name="y"/>), or moves it there.</summary>
        void ShowAt(int x, int y);

        /// <summary>Takes the banner off the screen; it stays loaded.</summary>
        void Hide();

        /// <summary>Takes the banner off the screen and releases it.</summary>
        void Destroy();
    }
}
