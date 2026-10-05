using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Persistence;
using Callbreak.Application.Ports;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// <see cref="INoAds"/> over an app store (<see cref="IStoreBilling"/>: Unity IAP over Google Play
    /// Billing on phones) and its own <c>no-ads</c> save file, which keeps the ads off from the first
    /// frame, offline too. The store has the last word on ownership.
    /// <para>
    /// It gets ready at once: it connects, reads the price, then lists the purchases of the player's
    /// account. A try that fails is tried again after <see cref="FirstRetryDelay"/>, each wait twice
    /// the last, up to <see cref="MaxRetryDelay"/>. A listing with No Ads paid for (a reinstall, a new
    /// phone, a payment that settled while the game was closed) makes it owned; a listing without it
    /// (a refund) makes it not owned, unless a purchase came in while the listing was on its way. A
    /// listing that fails changes nothing. It can be bought once the account has been listed.
    /// </para>
    /// <para>
    /// A paid purchase is saved first and only then confirmed to the store (Google refunds one left
    /// unconfirmed for three days). A confirmation the store didn't take is tried again by listing the
    /// account again, after the same waits, until the store has it. A purchase awaiting its payment
    /// grants nothing, and no second purchase starts while it waits; it is granted once paid, now or
    /// on a later start. A purchase that failed lists the account again, in case it was paid after all.
    /// </para>
    /// <para>
    /// Back in the game (<see cref="IStoreBilling.Resumed"/>), the account is listed again when that
    /// can bring news: No Ads isn't owned (a payment that settled meanwhile, a purchase made outside
    /// the game), or a paid purchase still waits for its confirmation (a refund would show). At most
    /// once every <see cref="RefreshInterval"/>; never while a purchase is under way, as the store's
    /// sheet takes the focus and gives it back; never while getting ready goes on, which lists anyway.
    /// </para>
    /// One purchase runs at a time: a second <see cref="BuyAsync"/> shares the first one's outcome.
    /// Nothing here throws, but for the <see cref="OperationCanceledException"/> of a cancelled token,
    /// which ends that caller's wait and not the purchase. Everything happens on the main thread, as
    /// the store's tasks and events do, <see cref="Changed"/> included. A file from a newer app version
    /// is ignored and never overwritten: ownership then lives in memory and comes from the store's
    /// listing at each start, so a purchase is still confirmed, the store keeping the record.
    /// </summary>
    public sealed class StoreNoAds : INoAds, IDisposable
    {
        public const string SaveKey = "no-ads";

        /// <summary>The wait before the first retry; each retry after it waits twice as long.</summary>
        public static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5);

        /// <summary>The longest wait between two tries.</summary>
        public static readonly TimeSpan MaxRetryDelay = TimeSpan.FromMinutes(2);

        /// <summary>The least time between two listings on the game's return to the foreground.</summary>
        public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

        private readonly IStoreBilling _billing;
        private readonly ISaveStore _saves;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private readonly NoAdsData _data;
        private readonly bool _readOnly;

        private string _price;
        private bool _forSale;
        private bool _listed;
        private bool _listAgain = true;
        private bool _awaitingPayment;
        private bool _unconfirmed;
        private DateTime? _lastRefreshUtc;
        private bool _saved = true;
        private int _grants;
        private int _confirmFailures;
        private bool _gettingReady;
        private bool _disposed;
        private TaskCompletionSource<NoAdsPurchase> _purchase;

        /// <param name="utcNow">The clock that spaces the listings on the game's return.</param>
        /// <param name="log">Where its progress goes, a line each (no player data, no receipts); nowhere when null.</param>
        /// <param name="delay">The retry waits; <see cref="Task.Delay(TimeSpan, CancellationToken)"/> when null.</param>
        public StoreNoAds(
            IStoreBilling billing,
            ISaveStore saves,
            Func<DateTime> utcNow,
            Action<string> log = null,
            Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            _billing = billing ?? throw new ArgumentNullException(nameof(billing));
            _saves = saves ?? throw new ArgumentNullException(nameof(saves));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _log = log ?? (_ => { });
            _delay = delay ?? Task.Delay;
            NoAdsData loaded = VersionedSave.Load<NoAdsData>(saves, SaveKey, NoAdsData.CurrentVersion, d => d.schemaVersion, out _readOnly);
            _data = loaded ?? new NoAdsData();
            _data.schemaVersion = NoAdsData.CurrentVersion;
            _billing.PurchaseUpdated += OnPurchaseUpdated;
            _billing.PurchaseFailed += OnPurchaseFailed;
            _billing.Resumed += OnResumed;
            GetReady();
        }

        public event Action Changed;

        public bool Owned => _data.owned;

        public bool CanBuy => _forSale && _listed && !_data.owned && _purchase == null && !_awaitingPayment && !_disposed;

        public string Price => _price;

        /// <summary>The wait after <paramref name="failures"/> failed tries in a row: 5 s, 10 s, 20 s ... at most two minutes.</summary>
        public static TimeSpan RetryDelay(int failures)
        {
            int doublings = Math.Max(0, Math.Min(failures - 1, 16));
            double seconds = FirstRetryDelay.TotalSeconds * Math.Pow(2, doublings);
            return TimeSpan.FromSeconds(Math.Min(seconds, MaxRetryDelay.TotalSeconds));
        }

        public Task<NoAdsPurchase> BuyAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled<NoAdsPurchase>(ct);
            }

            if (Owned)
            {
                return Task.FromResult(NoAdsPurchase.AlreadyOwned);
            }

            TaskCompletionSource<NoAdsPurchase> purchase = _purchase;
            if (purchase == null)
            {
                if (_awaitingPayment)
                {
                    return Task.FromResult(NoAdsPurchase.Pending);
                }

                if (!_forSale || !_listed || _disposed)
                {
                    // Getting ready goes on, or starts again; CanBuy says when it is done.
                    GetReady();
                    return Task.FromResult(NoAdsPurchase.Unavailable);
                }

                purchase = new TaskCompletionSource<NoAdsPurchase>();
                Change(() => _purchase = purchase);
                try
                {
                    _billing.Purchase(NoAdsProduct.Id);
                }
                catch (Exception e)
                {
                    _log("No Ads: the store's sheet did not open: " + e.Message);
                    Finish(NoAdsPurchase.Failed);
                }
            }

            return WaitAsync(purchase.Task, ct);
        }

        /// <summary>Stops trying again and stops hearing the store.</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetime.Cancel();
            _billing.PurchaseUpdated -= OnPurchaseUpdated;
            _billing.PurchaseFailed -= OnPurchaseFailed;
            _billing.Resumed -= OnResumed;
            Finish(NoAdsPurchase.Unavailable);
        }

        /// <summary>Starts getting ready (connect, the price, the account's purchases), unless that is under way.</summary>
        private void GetReady()
        {
            if (_gettingReady || _disposed)
            {
                return;
            }

            _gettingReady = true;
            _ = GetReadyAsync();
        }

        /// <summary>Tries until the store sells No Ads and the account is listed, waiting longer after each failure.</summary>
        private async Task GetReadyAsync()
        {
            int failures = 0;
            try
            {
                while (!_disposed)
                {
                    if (await TryGetReadyAsync())
                    {
                        return;
                    }

                    failures++;
                    TimeSpan wait = RetryDelay(failures);
                    _log("No Ads: the store is not ready; trying again in " + wait.TotalSeconds.ToString("0") + " s.");
                    await _delay(wait, _lifetime.Token);
                }
            }
            catch (OperationCanceledException)
            {
                // Disposed while waiting.
            }
            catch (Exception e)
            {
                // A broken wait: the next purchase starts over.
                _log("No Ads: getting ready stopped: " + e.Message);
            }
            finally
            {
                _gettingReady = false;
            }
        }

        /// <summary>
        /// One try: connect, then read the price unless it is known, then list the account if it is
        /// due. True when the store sells No Ads and the account is listed.
        /// </summary>
        private async Task<bool> TryGetReadyAsync()
        {
            try
            {
                if (!await _billing.ConnectAsync() || _disposed)
                {
                    return false;
                }

                if (!_forSale)
                {
                    // The product first: the store then knows it when it lists the purchases.
                    StoreProduct product = await _billing.FetchProductAsync(NoAdsProduct.Id);
                    if (product != null && !_disposed)
                    {
                        Change(() =>
                        {
                            _price = product.Price;
                            _forSale = true;
                        });
                    }
                }

                if (_listAgain && !_disposed)
                {
                    _listAgain = false;
                    if (!await ListAsync())
                    {
                        _listAgain = true;
                    }
                }

                return _forSale && !_listAgain;
            }
            catch (Exception e)
            {
                _log("No Ads: the store failed: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Lists the account's purchases and takes No Ads' ownership from them, then confirms a paid
        /// one the store hasn't had confirmed. False when the store couldn't list them: then nothing
        /// changes.
        /// </summary>
        private async Task<bool> ListAsync()
        {
            int grants = _grants;
            IReadOnlyList<StorePurchase> purchases;
            try
            {
                purchases = await _billing.FetchPurchasesAsync();
            }
            catch (Exception e)
            {
                _log("No Ads: the purchases were not listed: " + e.Message);
                return false;
            }

            if (purchases == null || _disposed)
            {
                return false;
            }

            var paid = new List<StorePurchase>();
            bool awaitingPayment = false;
            foreach (StorePurchase purchase in purchases)
            {
                if (purchase == null || purchase.ProductId != NoAdsProduct.Id)
                {
                    continue;
                }

                if (purchase.State == StorePurchaseState.AwaitingPayment)
                {
                    awaitingPayment = true;
                }
                else
                {
                    paid.Add(purchase);
                }
            }

            bool saved = true;
            Change(() =>
            {
                _listed = true;
                if (paid.Count > 0)
                {
                    _awaitingPayment = false;
                    _unconfirmed = paid.Exists(purchase => purchase.State == StorePurchaseState.Paid);
                    saved = SetOwned(true);
                }
                else if (_grants == grants)
                {
                    // Refunded, or never bought on this account. A purchase granted while this listing
                    // was on its way is newer than the listing, and stays.
                    _awaitingPayment = awaitingPayment;
                    _unconfirmed = false;
                    SetOwned(false);
                }
            });

            if (saved)
            {
                foreach (StorePurchase purchase in paid)
                {
                    if (purchase.State == StorePurchaseState.Paid)
                    {
                        _ = ConfirmAsync(purchase);
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Confirms a paid purchase, saved already, to the store. One the store didn't take is tried
        /// again by listing the account again after a wait, which confirms it again.
        /// </summary>
        private async Task ConfirmAsync(StorePurchase purchase)
        {
            bool confirmed;
            try
            {
                confirmed = await _billing.ConfirmAsync(purchase);
            }
            catch (Exception e)
            {
                _log("No Ads: the purchase was not confirmed: " + e.Message);
                confirmed = false;
            }

            if (confirmed)
            {
                _confirmFailures = 0;
                _unconfirmed = false;
                return;
            }

            if (_disposed)
            {
                return;
            }

            _confirmFailures++;
            TimeSpan wait = RetryDelay(_confirmFailures);
            _log("No Ads: the store did not take the confirmation; listing again in " + wait.TotalSeconds.ToString("0") + " s.");
            try
            {
                await _delay(wait, _lifetime.Token);
            }
            catch (Exception)
            {
                // Disposed while waiting, or a broken wait: the next start lists the account again.
                return;
            }

            _listAgain = true;
            GetReady();
        }

        private void OnPurchaseUpdated(StorePurchase purchase)
        {
            if (_disposed || purchase == null || purchase.ProductId != NoAdsProduct.Id)
            {
                return;
            }

            if (purchase.State == StorePurchaseState.AwaitingPayment)
            {
                Change(() => _awaitingPayment = true);
                Finish(NoAdsPurchase.Pending);
                return;
            }

            _grants++;
            bool saved = true;
            Change(() =>
            {
                _awaitingPayment = false;
                _unconfirmed = purchase.State == StorePurchaseState.Paid;
                saved = SetOwned(true);
            });

            if (saved && purchase.State == StorePurchaseState.Paid)
            {
                _ = ConfirmAsync(purchase);
            }

            Finish(NoAdsPurchase.Bought);
        }

        /// <summary>
        /// The game is back in front of the player: lists the account again when that can bring news,
        /// at most once every <see cref="RefreshInterval"/> (a clock set back starts the interval again
        /// rather than stop the listings).
        /// </summary>
        private void OnResumed()
        {
            if (_disposed || _purchase != null || _gettingReady || (Owned && !_unconfirmed))
            {
                return;
            }

            DateTime now = _utcNow();
            if (_lastRefreshUtc.HasValue && now >= _lastRefreshUtc.Value && now - _lastRefreshUtc.Value < RefreshInterval)
            {
                return;
            }

            _lastRefreshUtc = now;
            _listAgain = true;
            GetReady();
        }

        private void OnPurchaseFailed(string productId, StorePurchaseFailure failure)
        {
            // A failure without a product is the purchase under way's; another product's is not.
            if (_disposed || _purchase == null || (productId != null && productId != NoAdsProduct.Id))
            {
                return;
            }

            switch (failure)
            {
                case StorePurchaseFailure.Cancelled:
                    Finish(NoAdsPurchase.Cancelled);
                    break;

                case StorePurchaseFailure.AlreadyOwned:
                    // The account owns it, though no listing has said so yet: a listing grants it.
                    _ = FinishFromListingAsync();
                    break;

                case StorePurchaseFailure.Unavailable:
                    Change(() => _forSale = false);
                    Finish(NoAdsPurchase.Unavailable);
                    GetReady();
                    break;

                default:
                    // A purchase can fail on its way back and still be paid: a listing would show it.
                    Finish(NoAdsPurchase.Failed);
                    _listAgain = true;
                    GetReady();
                    break;
            }
        }

        /// <summary>Lists the account, then ends the purchase under way with what the listing showed.</summary>
        private async Task FinishFromListingAsync()
        {
            bool listed = await ListAsync();
            Finish(Owned ? NoAdsPurchase.AlreadyOwned : _awaitingPayment ? NoAdsPurchase.Pending : NoAdsPurchase.Failed);
            if (!listed)
            {
                _listAgain = true;
                GetReady();
            }
        }

        /// <summary>Ends the purchase under way, if any, with <paramref name="result"/>, after its state has changed.</summary>
        private void Finish(NoAdsPurchase result)
        {
            TaskCompletionSource<NoAdsPurchase> purchase = _purchase;
            if (purchase == null)
            {
                return;
            }

            Change(() => _purchase = null);
            purchase.TrySetResult(result);
        }

        /// <summary>
        /// Sets whether No Ads is owned and saves that, unless it is saved already. False when the
        /// save failed: a purchase is then not confirmed, and the next listing tries again.
        /// </summary>
        private bool SetOwned(bool owned)
        {
            if (_data.owned == owned && _saved)
            {
                return true;
            }

            _data.owned = owned;
            if (_readOnly)
            {
                // A newer app's file is never overwritten; the store's record stands in for it.
                return true;
            }

            try
            {
                _saves.Save(SaveKey, _data);
                _saved = true;
            }
            catch (Exception e)
            {
                _saved = false;
                _log("No Ads: not saved: " + e.Message);
            }

            return _saved;
        }

        /// <summary>Applies <paramref name="change"/>, then raises <see cref="Changed"/> if Owned, CanBuy or Price moved.</summary>
        private void Change(Action change)
        {
            bool owned = Owned;
            bool canBuy = CanBuy;
            string price = Price;
            change();
            if (owned == Owned && canBuy == CanBuy && price == Price)
            {
                return;
            }

            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                _log("No Ads: a Changed listener failed: " + e);
            }
        }

        /// <summary>
        /// What becomes of the purchase, for one caller: the purchase's own task when the caller can't
        /// cancel (every such caller shares it); otherwise a wait that a cancelled token ends, and not
        /// the purchase.
        /// </summary>
        private static Task<NoAdsPurchase> WaitAsync(Task<NoAdsPurchase> outcome, CancellationToken ct)
        {
            return !ct.CanBeCanceled || outcome.IsCompleted ? outcome : WaitOrCancelAsync(outcome, ct);
        }

        private static async Task<NoAdsPurchase> WaitOrCancelAsync(Task<NoAdsPurchase> outcome, CancellationToken ct)
        {
            var cancelled = new TaskCompletionSource<bool>();
            using (ct.Register(() => cancelled.TrySetResult(true)))
            {
                if (await Task.WhenAny(outcome, cancelled.Task) != outcome)
                {
                    throw new OperationCanceledException(ct);
                }
            }

            return await outcome;
        }
    }
}
