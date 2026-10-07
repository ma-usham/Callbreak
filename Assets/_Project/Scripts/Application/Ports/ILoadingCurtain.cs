using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Ports
{
    /// <summary>
    /// The full-screen curtain scene changes happen behind (GDD §21 startup sequence, §23 curtain
    /// layer): it covers the screen before a scene unloads and uncovers the next one once it is
    /// ready, and it swallows input while it covers.
    /// </summary>
    public interface ILoadingCurtain
    {
        bool IsCovering { get; }

        Task CoverAsync(CancellationToken ct);

        Task UncoverAsync(CancellationToken ct);

        /// <summary>
        /// Covers the screen in black, with only the loading emblem in the middle (its ring turning
        /// round a gold spade), while another app's window is over the game: the store's purchase
        /// sheet, which can turn the game to portrait behind it (ADR 042). It swallows input like
        /// the curtain. A scene change already under the curtain needs no black cover.
        /// </summary>
        Task CoverDarkAsync(CancellationToken ct);

        /// <summary>
        /// Takes the black cover away once the screen is landscape again (after a moment at most).
        /// Does nothing when the curtain doesn't show the black cover, so a scene change that took
        /// it over meanwhile keeps its curtain.
        /// </summary>
        Task UncoverDarkAsync(CancellationToken ct);
    }
}
