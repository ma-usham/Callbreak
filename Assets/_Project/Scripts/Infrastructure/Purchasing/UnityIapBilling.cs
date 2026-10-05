using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Callbreak.Infrastructure.Purchasing
{
    /// <summary>
    /// The app store's billing behind <see cref="IStoreBilling"/>, through Unity IAP 5
    /// (com.unity.purchasing 5.4.4): Google Play Billing on Android (the package adds Play Billing
    /// Library 9.0.0 to the Gradle build), the App Store on iOS. It needs no
    /// UnityServices.InitializeAsync. Unity IAP picks the Android store from
    /// Assets/Resources/BillingMode.json, a file it writes and reads itself; without it an Android
    /// build would get Unity's fake store.
    /// <para>
    /// Every event is subscribed before the first Connect, as Unity IAP asks. Products are one-time
    /// and kept (non-consumable). A listing hands back what the store holds for the account:
    /// acknowledged purchases as confirmed, unacknowledged ones as paid, payments still on their way
    /// as awaiting. Unity IAP is told not to replay the unacknowledged ones through OnPurchasePending
    /// as well (ProcessPendingOrdersOnPurchasesFetched(false)): <see cref="StoreNoAds"/> settles them
    /// from the listing. A confirmation of a purchase the store had acknowledged already counts as
    /// done. One connection, one product fetch, one listing and one confirmation per purchase run at
    /// a time; a second request shares the first. A request the store never answers ends after
    /// <see cref="Timeout"/> as a failure.
    /// </para>
    /// <para>
    /// The game's return to the foreground (<see cref="UnityEngine.Application.focusChanged"/>, focus regained)
    /// is passed on as <see cref="Resumed"/>; <see cref="Dispose"/> stops following the focus and
    /// Unity IAP's events. Unity IAP lists the purchases on focus too, into its own cache only, which
    /// raises no event here (its replay of unacknowledged purchases is off).
    /// </para>
    /// Unity IAP calls back on the main thread already; a callback that arrives on another thread is
    /// posted to it. Nothing here throws into the game. The log names products and failure reasons,
    /// never a receipt, an order or a purchase token.
    /// </summary>
    public sealed class UnityIapBilling : IStoreBilling, IDisposable
    {
        private const string LogPrefix = "[IAP] ";

        /// <summary>How long a request may go unanswered before it counts as failed.</summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

        private readonly StoreController _store;
        private readonly SynchronizationContext _mainThread;
        private readonly int _mainThreadId;

        // Keyed by the store's transaction id (on Google Play the purchase token): memory only, never logged.
        private readonly Dictionary<string, TaskCompletionSource<bool>> _confirmations = new Dictionary<string, TaskCompletionSource<bool>>(StringComparer.Ordinal);

        private TaskCompletionSource<bool> _connecting;
        private TaskCompletionSource<StoreProduct> _fetching;
        private string _fetchingId;
        private TaskCompletionSource<IReadOnlyList<StorePurchase>> _listing;
        private string _purchasing;
        private bool _disposed;

        public UnityIapBilling()
        {
            _mainThread = SynchronizationContext.Current;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            try
            {
                _store = UnityIAPServices.StoreController();
                _store.OnStoreConnected += OnStoreConnected;
                _store.OnStoreDisconnected += OnStoreDisconnected;
                _store.OnProductsFetched += OnProductsFetched;
                _store.OnProductsFetchFailed += OnProductsFetchFailed;
                _store.OnPurchasesFetched += OnPurchasesFetched;
                _store.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
                _store.OnPurchasePending += OnPurchasePending;
                _store.OnPurchaseDeferred += OnPurchaseDeferred;
                _store.OnPurchaseFailed += OnPurchaseFailed;
                _store.OnPurchaseConfirmed += OnPurchaseConfirmed;
                _store.ProcessPendingOrdersOnPurchasesFetched(false);
            }
            catch (Exception e)
            {
                // No store at all: every request fails, and No Ads is not for sale.
                LogFailure("Unity IAP did not start", e);
                Unsubscribe();
                _store = null;
            }

            UnityEngine.Application.focusChanged += OnFocusChanged;
        }

        public event Action<StorePurchase> PurchaseUpdated;

        public event Action<string, StorePurchaseFailure> PurchaseFailed;

        public event Action Resumed;

        /// <summary>Stops following the focus and Unity IAP's events.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            UnityEngine.Application.focusChanged -= OnFocusChanged;
            Unsubscribe();
        }

        public Task<bool> ConnectAsync()
        {
            if (_store == null)
            {
                return Task.FromResult(false);
            }

            if (IsConnected())
            {
                return Task.FromResult(true);
            }

            if (_connecting != null)
            {
                return _connecting.Task;
            }

            var connecting = new TaskCompletionSource<bool>();
            _connecting = connecting;
            _ = ConnectAsync(connecting);
            return connecting.Task;
        }

        public Task<StoreProduct> FetchProductAsync(string productId)
        {
            if (_store == null || string.IsNullOrEmpty(productId))
            {
                return Task.FromResult<StoreProduct>(null);
            }

            if (_fetching != null)
            {
                // Unity IAP fetches one list at a time: the same product shares the fetch under way.
                return productId == _fetchingId ? _fetching.Task : Task.FromResult<StoreProduct>(null);
            }

            var fetching = new TaskCompletionSource<StoreProduct>();
            _fetching = fetching;
            _fetchingId = productId;
            try
            {
                _store.FetchProductsWithNoRetries(new List<ProductDefinition> { new ProductDefinition(productId, ProductType.NonConsumable) });
            }
            catch (Exception e)
            {
                LogFailure("The product fetch did not start", e);
                EndFetch(fetching, null);
            }

            _ = TimeOutAsync(fetching.Task, () => EndFetch(fetching, null), "the product");
            return fetching.Task;
        }

        public Task<IReadOnlyList<StorePurchase>> FetchPurchasesAsync()
        {
            if (_store == null)
            {
                return Task.FromResult<IReadOnlyList<StorePurchase>>(null);
            }

            if (_listing != null)
            {
                return _listing.Task;
            }

            var listing = new TaskCompletionSource<IReadOnlyList<StorePurchase>>();
            _listing = listing;
            try
            {
                _store.FetchPurchases();
            }
            catch (Exception e)
            {
                LogFailure("The listing did not start", e);
                EndListing(listing, null);
            }

            _ = TimeOutAsync(listing.Task, () => EndListing(listing, null), "the listing");
            return listing.Task;
        }

        public void Purchase(string productId)
        {
            Product product = null;
            try
            {
                product = _store?.GetProductById(productId);
            }
            catch (Exception e)
            {
                LogFailure("The product could not be read", e);
            }

            if (product == null)
            {
                Debug.LogWarning(LogPrefix + productId + " is not for sale now: the store has not returned it.");
                RaiseFailed(productId, StorePurchaseFailure.Unavailable);
                return;
            }

            _purchasing = productId;
            try
            {
                _store.PurchaseProduct(product);
            }
            catch (Exception e)
            {
                LogFailure("The purchase did not start", e);
                _purchasing = null;
                RaiseFailed(productId, StorePurchaseFailure.Failed);
            }
        }

        public Task<bool> ConfirmAsync(StorePurchase purchase)
        {
            if (purchase != null && purchase.State == StorePurchaseState.Confirmed)
            {
                return Task.FromResult(true);
            }

            string transaction = purchase?.Order is PendingOrder pending ? TransactionOf(pending) : null;
            if (_store == null || string.IsNullOrEmpty(transaction))
            {
                return Task.FromResult(false);
            }

            if (_confirmations.TryGetValue(transaction, out TaskCompletionSource<bool> running))
            {
                return running.Task;
            }

            var confirmation = new TaskCompletionSource<bool>();
            _confirmations[transaction] = confirmation;
            try
            {
                _store.ConfirmPurchase((PendingOrder)purchase.Order);
            }
            catch (Exception e)
            {
                LogFailure("The confirmation did not start", e);
                EndConfirmation(transaction, confirmation, false);
            }

            _ = TimeOutAsync(confirmation.Task, () => EndConfirmation(transaction, confirmation, false), "the confirmation");
            return confirmation.Task;
        }

        /// <summary>Connects, and ends <paramref name="connecting"/> once Unity IAP has connected or given up.</summary>
        private async Task ConnectAsync(TaskCompletionSource<bool> connecting)
        {
            try
            {
                // Unity IAP ends this task once the store is connected, or once it has given up (after
                // its own retries), which it also reports through OnStoreDisconnected.
                Task connect = _store.Connect();
                if (await Task.WhenAny(connect, Task.Delay(Timeout)) != connect)
                {
                    Debug.LogWarning(LogPrefix + "The store did not answer the connection in " + Timeout.TotalSeconds.ToString("0") + " s.");
                }
            }
            catch (Exception e)
            {
                LogFailure("The connection failed", e);
            }

            if (_connecting == connecting)
            {
                _connecting = null;
            }

            connecting.TrySetResult(IsConnected());
        }

        private bool IsConnected()
        {
            try
            {
                return _store != null && _store.GetConnectionState() == ConnectionState.Connected;
            }
            catch (Exception e)
            {
                LogFailure("The connection could not be read", e);
                return false;
            }
        }

        /// <summary>Unity's focus change, on the main thread: a regained focus is passed on as <see cref="Resumed"/>.</summary>
        private void OnFocusChanged(bool focused)
        {
            if (focused && !_disposed)
            {
                OnMainThread(() => Resumed?.Invoke());
            }
        }

        private void Unsubscribe()
        {
            if (_store == null)
            {
                return;
            }

            try
            {
                _store.OnStoreConnected -= OnStoreConnected;
                _store.OnStoreDisconnected -= OnStoreDisconnected;
                _store.OnProductsFetched -= OnProductsFetched;
                _store.OnProductsFetchFailed -= OnProductsFetchFailed;
                _store.OnPurchasesFetched -= OnPurchasesFetched;
                _store.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;
                _store.OnPurchasePending -= OnPurchasePending;
                _store.OnPurchaseDeferred -= OnPurchaseDeferred;
                _store.OnPurchaseFailed -= OnPurchaseFailed;
                _store.OnPurchaseConfirmed -= OnPurchaseConfirmed;
            }
            catch (Exception e)
            {
                LogFailure("Unity IAP's events were not left", e);
            }
        }

        private void OnStoreConnected()
        {
            OnMainThread(() => Debug.Log(LogPrefix + "Connected to the store."));
        }

        private void OnStoreDisconnected(StoreConnectionFailureDescription failure)
        {
            OnMainThread(() => Debug.LogWarning(LogPrefix + "The store is not connected: " + failure?.Message));
        }

        private void OnProductsFetched(List<Product> products)
        {
            OnMainThread(() =>
            {
                TaskCompletionSource<StoreProduct> fetching = _fetching;
                if (fetching == null)
                {
                    return;
                }

                string id = _fetchingId;
                Product product = products?.Find(p => IdOf(p) == id || p?.uSku == id);
                string price = product?.metadata?.localizedPriceString;
                if (product == null || !product.availableToPurchase || string.IsNullOrEmpty(price))
                {
                    Debug.LogWarning(LogPrefix + "The store did not return " + id + " for sale.");
                    EndFetch(fetching, null);
                    return;
                }

                EndFetch(fetching, new StoreProduct(id, price));
            });
        }

        private void OnProductsFetchFailed(ProductFetchFailed failure)
        {
            OnMainThread(() =>
            {
                TaskCompletionSource<StoreProduct> fetching = _fetching;
                if (fetching == null)
                {
                    return;
                }

                Debug.LogWarning(LogPrefix + "The store did not return " + _fetchingId + ": " + failure?.FailureReason);
                EndFetch(fetching, null);
            });
        }

        private void OnPurchasesFetched(Orders orders)
        {
            OnMainThread(() =>
            {
                var purchases = new List<StorePurchase>();
                if (orders != null)
                {
                    Add(purchases, orders.ConfirmedOrders, StorePurchaseState.Confirmed);
                    Add(purchases, orders.PendingOrders, StorePurchaseState.Paid);
                    Add(purchases, orders.DeferredOrders, StorePurchaseState.AwaitingPayment);
                }

                TaskCompletionSource<IReadOnlyList<StorePurchase>> listing = _listing;
                if (listing == null)
                {
                    // Unity IAP also hands a purchase it found acknowledged already through here,
                    // alone and unasked: that is news of the purchase, not a listing of the account.
                    foreach (StorePurchase purchase in purchases)
                    {
                        RaiseUpdated(purchase);
                    }

                    return;
                }

                EndListing(listing, orders == null ? null : purchases);
            });
        }

        private void OnPurchasesFetchFailed(PurchasesFetchFailureDescription failure)
        {
            OnMainThread(() =>
            {
                TaskCompletionSource<IReadOnlyList<StorePurchase>> listing = _listing;
                if (listing == null)
                {
                    return;
                }

                Debug.LogWarning(LogPrefix + "The store did not list the purchases: " + failure?.FailureReason);
                EndListing(listing, null);
            });
        }

        private void OnPurchasePending(PendingOrder order)
        {
            OnMainThread(() => Deliver(order, StorePurchaseState.Paid));
        }

        private void OnPurchaseDeferred(DeferredOrder order)
        {
            OnMainThread(() => Deliver(order, StorePurchaseState.AwaitingPayment));
        }

        private void OnPurchaseFailed(FailedOrder order)
        {
            OnMainThread(() =>
            {
                // A failure while a purchase is under way is that purchase's; the store's own record of
                // the product can be a stand-in ("InvalidProduct") when its cache missed it.
                string id = _purchasing ?? FirstIdOf(order);
                _purchasing = null;
                PurchaseFailureReason reason = order?.FailureReason ?? PurchaseFailureReason.Unknown;
                Debug.Log(LogPrefix + "The purchase of " + (id ?? "a product") + " did not go through: " + reason + ".");
                RaiseFailed(id, Map(reason));
            });
        }

        private void OnPurchaseConfirmed(Order order)
        {
            OnMainThread(() =>
            {
                string transaction = TransactionOf(order);
                if (string.IsNullOrEmpty(transaction) || !_confirmations.TryGetValue(transaction, out TaskCompletionSource<bool> confirmation))
                {
                    return;
                }

                // A purchase acknowledged already (by an earlier confirmation) is as good as confirmed now.
                var failed = order as FailedOrder;
                bool confirmed = order is ConfirmedOrder || (failed != null && failed.FailureReason == PurchaseFailureReason.DuplicateTransaction);
                if (!confirmed)
                {
                    Debug.LogWarning(LogPrefix + "The store did not take the confirmation of " + (FirstIdOf(order) ?? "a purchase") + ": " + failed?.FailureReason + ".");
                }

                EndConfirmation(transaction, confirmation, confirmed);
            });
        }

        /// <summary>Hands each product of an order on as a purchase in <paramref name="state"/>.</summary>
        private void Deliver(Order order, StorePurchaseState state)
        {
            foreach (string id in IdsOf(order))
            {
                if (id == _purchasing)
                {
                    _purchasing = null;
                }

                RaiseUpdated(new StorePurchase(id, state, order));
            }
        }

        private void EndFetch(TaskCompletionSource<StoreProduct> fetching, StoreProduct product)
        {
            if (_fetching == fetching)
            {
                _fetching = null;
                _fetchingId = null;
            }

            fetching.TrySetResult(product);
        }

        private void EndListing(TaskCompletionSource<IReadOnlyList<StorePurchase>> listing, IReadOnlyList<StorePurchase> purchases)
        {
            if (_listing == listing)
            {
                _listing = null;
            }

            listing.TrySetResult(purchases);
        }

        private void EndConfirmation(string transaction, TaskCompletionSource<bool> confirmation, bool confirmed)
        {
            if (_confirmations.TryGetValue(transaction, out TaskCompletionSource<bool> running) && running == confirmation)
            {
                _confirmations.Remove(transaction);
            }

            confirmation.TrySetResult(confirmed);
        }

        /// <summary>Runs <paramref name="end"/> when <paramref name="request"/> has had no answer for <see cref="Timeout"/>.</summary>
        private static async Task TimeOutAsync(Task request, Action end, string what)
        {
            if (request.IsCompleted || await Task.WhenAny(request, Task.Delay(Timeout)) == request)
            {
                return;
            }

            Debug.LogWarning(LogPrefix + "The store did not answer " + what + " in " + Timeout.TotalSeconds.ToString("0") + " s.");
            end();
        }

        private void RaiseUpdated(StorePurchase purchase)
        {
            try
            {
                PurchaseUpdated?.Invoke(purchase);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void RaiseFailed(string productId, StorePurchaseFailure failure)
        {
            try
            {
                PurchaseFailed?.Invoke(productId, failure);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        /// <summary>Runs a callback's work on the main thread: at once when it is there, else posted to it.</summary>
        private void OnMainThread(Action action)
        {
            if (_mainThread == null || Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                Run(action);
                return;
            }

            _mainThread.Post(_ => Run(action), null);
        }

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private static StorePurchaseFailure Map(PurchaseFailureReason reason)
        {
            switch (reason)
            {
                case PurchaseFailureReason.UserCancelled:
                case PurchaseFailureReason.OrderCancelled:
                    return StorePurchaseFailure.Cancelled;

                // Google Play's ITEM_ALREADY_OWNED.
                case PurchaseFailureReason.DuplicateTransaction:
                    return StorePurchaseFailure.AlreadyOwned;

                case PurchaseFailureReason.PurchasingUnavailable:
                case PurchaseFailureReason.ProductUnavailable:
                case PurchaseFailureReason.StoreNotConnected:
                case PurchaseFailureReason.NotSupported:
                case PurchaseFailureReason.UserNotAuthenticated:
                    return StorePurchaseFailure.Unavailable;

                default:
                    return StorePurchaseFailure.Failed;
            }
        }

        private static void Add<T>(List<StorePurchase> purchases, IReadOnlyList<T> orders, StorePurchaseState state)
            where T : Order
        {
            if (orders == null)
            {
                return;
            }

            foreach (T order in orders)
            {
                foreach (string id in IdsOf(order))
                {
                    purchases.Add(new StorePurchase(id, state, order));
                }
            }
        }

        /// <summary>The store's id of each product an order holds.</summary>
        private static List<string> IdsOf(Order order)
        {
            var ids = new List<string>();
            IReadOnlyList<CartItem> items = order?.CartOrdered?.Items();
            if (items == null)
            {
                return ids;
            }

            foreach (CartItem item in items)
            {
                string id = IdOf(item?.Product);
                if (!string.IsNullOrEmpty(id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        private static string FirstIdOf(Order order)
        {
            List<string> ids = IdsOf(order);
            return ids.Count > 0 ? ids[0] : null;
        }

        private static string IdOf(Product product)
        {
            string id = product?.definition?.storeSpecificId;
            return string.IsNullOrEmpty(id) ? product?.uSku : id;
        }

        private static string TransactionOf(Order order) => order?.Info?.TransactionID;

        /// <summary>Logs what failed and the kind of exception only: a store's message can carry a purchase token.</summary>
        private static void LogFailure(string what, Exception e)
        {
            Debug.LogWarning(LogPrefix + what + " (" + e.GetType().Name + ").");
        }
    }
}
