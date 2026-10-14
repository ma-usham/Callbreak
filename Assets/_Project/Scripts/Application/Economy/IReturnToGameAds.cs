using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The app open ads (the owner, 15 Oct 2026): a full-screen ad as the player comes back to the
    /// game on the bare main menu, when one is due (<see cref="ReturnToGameAds"/>). No Ads
    /// (<see cref="INoAds"/>) removes them.
    /// </summary>
    public interface IReturnToGameAds
    {
        /// <summary>The game went to the background at <paramref name="leftUtc"/> (the wall clock, UTC): the time away starts then.</summary>
        void Left(DateTime leftUtc);

        /// <summary>
        /// The game came back on the bare main menu: shows an ad when one is due and loaded, and
        /// completes once it has gone away; at once when none is due or none is loaded, as the game
        /// never waits for one. True when an ad was shown. Each time away gives one chance. It never
        /// throws, but for the <see cref="System.OperationCanceledException"/> of a cancelled token.
        /// </summary>
        Task<bool> ShowIfDueAsync(CancellationToken ct);
    }
}
