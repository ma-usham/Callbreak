using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// No Ads (port): the one-time purchase (store product <see cref="NoAdsProduct.Id"/>, $0.99; the
    /// owner, 5 Oct 2026) that removes the ads between matches. The Earn Coins videos stay, as the
    /// player chooses those. On phones it is the store's (Unity IAP over Google Play Billing); in the
    /// Editor, in the tests and on desktop a stand-in. Everything happens on the main thread,
    /// <see cref="Changed"/> included.
    /// </summary>
    public interface INoAds
    {
        /// <summary><see cref="Owned"/>, <see cref="CanBuy"/> or <see cref="Price"/> changed.</summary>
        event Action Changed;

        /// <summary>The player owns No Ads: bought here, or restored by the store for their account.</summary>
        bool Owned { get; }

        /// <summary>The store is ready to sell it, it isn't owned, and no purchase is under way.</summary>
        bool CanBuy { get; }

        /// <summary>The store's price in the player's currency ("$0.99"); null until the store has said.</summary>
        string Price { get; }

        /// <summary>
        /// Opens the store's purchase sheet and completes with what became of it. It never throws, but
        /// for the <see cref="OperationCanceledException"/> of a cancelled token.
        /// </summary>
        Task<NoAdsPurchase> BuyAsync(CancellationToken ct);
    }

    /// <summary>What became of a No Ads purchase.</summary>
    public enum NoAdsPurchase
    {
        /// <summary>Bought now: <see cref="INoAds.Owned"/> is true.</summary>
        Bought,

        /// <summary>It was owned already (bought before, maybe on another device); nothing was charged.</summary>
        AlreadyOwned,

        /// <summary>The player closed the store's sheet.</summary>
        Cancelled,

        /// <summary>Paid by a method that settles later (e.g. cash); No Ads arrives once it does.</summary>
        Pending,

        /// <summary>The store can't sell it now (no Play Store, offline, the product unknown).</summary>
        Unavailable,

        /// <summary>The store reported an error.</summary>
        Failed,
    }

    /// <summary>The No Ads product as the stores know it (Play Console: Monetize with Play, One-time products).</summary>
    public static class NoAdsProduct
    {
        /// <summary>Its product id. Play never lets an id change or be used again, so this stays.</summary>
        public const string Id = "no_ads";
    }
}
