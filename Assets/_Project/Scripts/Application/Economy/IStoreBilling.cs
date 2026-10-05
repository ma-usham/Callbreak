using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// An app store's billing (port), as <see cref="StoreNoAds"/> drives it: connect, read a product
    /// and its price, list the purchases the player's account holds, open a purchase, and confirm a
    /// paid one (Google Play's acknowledgement: Google refunds a purchase left unconfirmed for three
    /// days). It sells one-time products that the player keeps. On phones it is Unity IAP over Google
    /// Play Billing (Infrastructure/Purchasing).
    /// <para>
    /// Every task completes, and every event comes, on the main thread. Nothing throws: a failure is
    /// a false, a null or <see cref="PurchaseFailed"/>.
    /// </para>
    /// </summary>
    public interface IStoreBilling
    {
        /// <summary>
        /// A purchase was made, or moved on, outside a listing: what became of <see cref="Purchase"/>,
        /// a payment that settled later, or a purchase the store delivered by itself.
        /// </summary>
        event Action<StorePurchase> PurchaseUpdated;

        /// <summary>A purchase did not go through: the product's id (null when the store didn't say), and why.</summary>
        event Action<string, StorePurchaseFailure> PurchaseFailed;

        /// <summary>
        /// The game is back in front of the player (it regained the focus). The store may have news by
        /// now that only a listing shows: a payment that settled, a purchase made outside the game, a
        /// refund. Google asks apps to list the purchases again then.
        /// </summary>
        event Action Resumed;

        /// <summary>Connects to the store: true once connected; false when it can't be now (no store app, offline). Asking again tries again.</summary>
        Task<bool> ConnectAsync();

        /// <summary>The product as the store sells it; null when the store doesn't know it, or couldn't say.</summary>
        Task<StoreProduct> FetchProductAsync(string productId);

        /// <summary>
        /// Every purchase the store holds for the player's account: the paid ones, and the ones
        /// awaiting their payment. A refunded purchase is no longer among them. Null when the store
        /// couldn't list them.
        /// </summary>
        Task<IReadOnlyList<StorePurchase>> FetchPurchasesAsync();

        /// <summary>
        /// Opens the store's purchase sheet for the product. What became of it comes as
        /// <see cref="PurchaseUpdated"/> or <see cref="PurchaseFailed"/>, maybe before this returns.
        /// </summary>
        void Purchase(string productId);

        /// <summary>Confirms a paid purchase to the store: true once the store has it, or had it already.</summary>
        Task<bool> ConfirmAsync(StorePurchase purchase);
    }

    /// <summary>A product as the store sells it.</summary>
    public sealed class StoreProduct
    {
        public StoreProduct(string id, string price)
        {
            Id = id;
            Price = price;
        }

        public string Id { get; }

        /// <summary>Its price in the player's currency, as the store writes it ("$0.99").</summary>
        public string Price { get; }
    }

    /// <summary>One purchase of a product, and where it stands.</summary>
    public sealed class StorePurchase
    {
        /// <param name="order">The adapter's own record of the purchase, handed back to <see cref="IStoreBilling.ConfirmAsync"/>.</param>
        public StorePurchase(string productId, StorePurchaseState state, object order = null)
        {
            ProductId = productId;
            State = state;
            Order = order;
        }

        public string ProductId { get; }

        public StorePurchaseState State { get; }

        /// <summary>The adapter's own record of the purchase. Opaque here, and never logged: it holds the store's receipt.</summary>
        public object Order { get; }
    }

    /// <summary>Where a purchase stands.</summary>
    public enum StorePurchaseState
    {
        /// <summary>Paid, and not yet confirmed to the store: grant it, then confirm it.</summary>
        Paid,

        /// <summary>Paid and confirmed.</summary>
        Confirmed,

        /// <summary>Waiting for a payment that settles later (e.g. cash): it grants nothing yet.</summary>
        AwaitingPayment,
    }

    /// <summary>Why a purchase did not go through.</summary>
    public enum StorePurchaseFailure
    {
        /// <summary>The player closed the store's sheet.</summary>
        Cancelled,

        /// <summary>The player's account owns the product already.</summary>
        AlreadyOwned,

        /// <summary>The store can't sell it now: not connected, no store, or the product unknown.</summary>
        Unavailable,

        /// <summary>Anything else.</summary>
        Failed,
    }
}
