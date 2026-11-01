using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Offers
{
    /// <summary>What claiming a festival offer did (<see cref="IFestivalOffers.ClaimAsync"/>).</summary>
    public enum FestivalClaimResult
    {
        /// <summary>The theme is the player's (owned, not equipped).</summary>
        Claimed,

        /// <summary>No offer is live and unclaimed for a theme the player doesn't own.</summary>
        NothingToClaim,

        /// <summary>The offer needs an ad and none can be shown now.</summary>
        AdUnavailable,

        /// <summary>The ad was skipped or failed.</summary>
        AdNotWatched,

        /// <summary>A discount the wallet can't pay.</summary>
        NotEnoughCoins,

        /// <summary>A claim is already under way.</summary>
        Busy,
    }

    /// <summary>
    /// The festival offers from the backend, for the main menu and the shop (the owner, 31 Oct 2026;
    /// ADR 067): the offer the player can claim now, if any; its one opening by itself on the main
    /// menu; and claiming it, which makes its theme the player's through the shop's own ownership. Time
    /// moves offers in and out without an event: a page reads <see cref="Claimable"/> when it shows.
    /// </summary>
    public interface IFestivalOffers
    {
        /// <summary>The offers from the backend, a claim, or the player's themes changed.</summary>
        event Action Changed;

        /// <summary>
        /// The first offer, as the backend lists them, that is live now, not claimed by this player,
        /// and whose theme the player doesn't own; null when there is none.
        /// </summary>
        FestivalOffer Claimable { get; }

        /// <summary>The claimable offer is for this theme (its shop tile says Festival).</summary>
        bool IsOffered(string themeId);

        /// <summary>
        /// True once per offer, the first time it is asked while the offer is claimable, and kept across
        /// restarts: the main menu opens the offer by itself then.
        /// </summary>
        bool TakeAutoOpen();

        /// <summary>
        /// Claims <see cref="Claimable"/>: a free offer at once; a watch-ad one when its ad is watched to
        /// the end; a discount when the wallet pays its price. A cancelled ad ends in
        /// <see cref="OperationCanceledException"/>, giving nothing.
        /// </summary>
        Task<FestivalClaimResult> ClaimAsync(CancellationToken ct);
    }
}
