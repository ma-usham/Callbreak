using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// No Ads over an app store (Google Play on phones; the owner, 5 Oct 2026). It connects at once,
    /// reads the price, then lists the account's purchases, trying again after 5 s, 10 s ... at most
    /// two minutes. Ownership is saved under its own key: from a purchase the store confirms, or from
    /// a listing that has No Ads (a reinstall, a new phone), and only then is the store told
    /// (Google refunds what is left unconfirmed for three days). A purchase awaiting its payment
    /// grants nothing until it is paid, now or on a later start. A listing without No Ads takes it
    /// away (a refund); a listing that fails never does. Back in the game, the account is listed again
    /// when that can bring news (not owned, or a purchase still to confirm), at most once every 30 s
    /// and never while the purchase sheet is up. One purchase runs at a time, and nothing the store
    /// does makes it throw.
    /// </summary>
    public sealed class StoreNoAdsTests
    {
        private const string Price = "$0.99";

        private static readonly DateTime Start = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStore _saves;
        private ScriptedBilling _billing;
        private List<Wait> _waits;
        private List<string> _log;
        private DateTime _now;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            // The store's answers resume No Ads on the test's own thread, at once, in the Editor as in
            // the dotnet harness.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _saves = new InMemorySaveStore();
            _billing = new ScriptedBilling(_saves);
            _waits = new List<Wait>();
            _log = new List<string>();
            _now = Start;
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [Test]
        public void TheStore_IsAskedAtOnce_ForThePrice_ThenForTheAccountsPurchases()
        {
            StoreNoAds noAds = NewNoAds();
            int changed = 0;
            noAds.Changed += () => changed++;

            Assert.AreEqual(1, _billing.Connects, "it connects at once, before any tap");
            Assert.AreEqual(0, _billing.ProductFetches);
            Assert.IsFalse(noAds.Owned);
            Assert.IsFalse(noAds.CanBuy);
            Assert.IsNull(noAds.Price, "no price until the store has said");

            _billing.FinishConnect(true);
            Assert.AreEqual(1, _billing.ProductFetches);
            Assert.AreEqual(NoAdsProduct.Id, _billing.FetchedProductId);
            Assert.AreEqual(0, _billing.Listings, "the product first, so the store knows it when it lists the purchases");

            _billing.FinishProduct(Price);
            Assert.AreEqual(Price, noAds.Price);
            Assert.AreEqual(1, changed);
            Assert.IsFalse(noAds.CanBuy, "not before the account is listed: it may own No Ads already");
            Assert.AreEqual(1, _billing.Listings);

            _billing.FinishListing();
            Assert.IsTrue(noAds.CanBuy);
            Assert.IsFalse(noAds.Owned);
            Assert.AreEqual(2, changed);
            Assert.AreEqual(0, _waits.Count, "nothing to try again");
            Assert.AreEqual(0, _saves.SaveCount, "nothing to save");
        }

        [Test]
        public void AConnectionThatFails_IsTriedAgain_AfterLongerWaits()
        {
            StoreNoAds noAds = NewNoAds();

            _billing.FinishConnect(false);
            Assert.AreEqual(0, _billing.ProductFetches);
            Assert.AreEqual(TimeSpan.FromSeconds(5), LastWait().Length);

            LastWait().Finish();
            Assert.AreEqual(2, _billing.Connects);
            _billing.FinishConnect(false);
            Assert.AreEqual(TimeSpan.FromSeconds(10), LastWait().Length);

            LastWait().Finish();
            _billing.FinishConnect(true);
            _billing.FinishProduct(Price);
            _billing.FinishListing();
            Assert.AreEqual(3, _billing.Connects);
            Assert.IsTrue(noAds.CanBuy);
            Assert.AreEqual(2, _waits.Count);
        }

        [Test]
        public void TheRetryWait_Doubles_UpToTwoMinutes()
        {
            CollectionAssert.AreEqual(
                new[] { 5d, 10d, 20d, 40d, 80d, 120d, 120d },
                new[] { 1, 2, 3, 4, 5, 6, 50 }.Select(n => StoreNoAds.RetryDelay(n).TotalSeconds));
        }

        [Test]
        public void AProductTheStoreDoesNotSell_LeavesNothingToBuy_AndIsAskedForAgain()
        {
            StoreNoAds noAds = NewNoAds();
            _billing.FinishConnect(true);

            _billing.FinishProduct(null);
            Assert.AreEqual(1, _billing.Listings, "the account is listed all the same: it may own No Ads");
            _billing.FinishListing();
            Assert.IsFalse(noAds.CanBuy);
            Assert.IsNull(noAds.Price);
            Assert.AreEqual(TimeSpan.FromSeconds(5), LastWait().Length);

            LastWait().Finish();
            _billing.FinishConnect(true);
            Assert.AreEqual(2, _billing.ProductFetches);
            _billing.FinishProduct(Price);
            Assert.AreEqual(1, _billing.Listings, "listed once is enough");
            Assert.IsTrue(noAds.CanBuy);
            Assert.AreEqual(Price, noAds.Price);
        }

        [Test]
        public void ASavedPurchase_TurnsTheAdsOffFromTheStart_BeforeTheStoreAnswers()
        {
            _saves.Put(StoreNoAds.SaveKey, new NoAdsData { owned = true });

            StoreNoAds noAds = NewNoAds();
            Assert.IsTrue(noAds.Owned, "offline too");
            Assert.IsFalse(noAds.CanBuy);

            _billing.FinishConnect(false);
            Assert.IsTrue(noAds.Owned, "a store that can't be reached takes nothing away");
        }

        [Test]
        public void AListingWithNoAdsPaidFor_MakesItOwned_AndSavesIt()
        {
            StoreNoAds noAds = NewNoAds();
            int changed = 0;
            noAds.Changed += () => changed++;

            // A reinstall, or a new phone: the store lists what the account bought.
            _billing.FinishConnect(true);
            _billing.FinishProduct(Price);
            _billing.FinishListing(NoAds(StorePurchaseState.Confirmed));

            Assert.IsTrue(noAds.Owned);
            Assert.IsFalse(noAds.CanBuy);
            Assert.AreEqual(2, changed, "the price, then the ownership");
            Assert.IsTrue(_saves.Peek<NoAdsData>(StoreNoAds.SaveKey).owned);
            Assert.AreEqual(0, _billing.Confirms.Count, "the store has its confirmation already");
            Assert.IsTrue(NewNoAds(new ScriptedBilling(_saves)).Owned, "and it survives a restart");
        }

        [Test]
        public void AListedPurchaseNotYetConfirmed_IsSavedFirst_ThenConfirmed()
        {
            // E.g. the game was killed between a purchase and its confirmation.
            StorePurchase paid = NoAds(StorePurchaseState.Paid);

            StoreNoAds noAds = ReadyNoAds(paid);

            Assert.IsTrue(noAds.Owned);
            Assert.AreSame(paid, _billing.Confirms.Single());
            Assert.IsTrue(_billing.SavedOwnedAtConfirm.Single(), "saved before the store is told");
            _billing.FinishConfirm(true);
            Assert.AreEqual(0, _waits.Count);
        }

        [Test]
        public void AListingWithoutNoAds_TakesItAway_AsAfterARefund()
        {
            _saves.Put(StoreNoAds.SaveKey, new NoAdsData { owned = true });
            StoreNoAds noAds = NewNoAds();
            int changed = 0;
            noAds.Changed += () => changed++;

            _billing.FinishConnect(true);
            _billing.FinishProduct(Price);
            _billing.FinishListing();

            Assert.IsFalse(noAds.Owned);
            Assert.IsTrue(noAds.CanBuy, "it can be bought again");
            Assert.AreEqual(2, changed, "the price, then the ownership");
            Assert.IsFalse(_saves.Peek<NoAdsData>(StoreNoAds.SaveKey).owned);
        }

        [Test]
        public void AListingThatFails_TakesNothingAway_AndIsTriedAgain()
        {
            _saves.Put(StoreNoAds.SaveKey, new NoAdsData { owned = true });
            StoreNoAds noAds = NewNoAds();
            _billing.FinishConnect(true);
            _billing.FinishProduct(Price);

            _billing.FailListing();
            Assert.IsTrue(noAds.Owned, "an error is not a refund");
            Assert.AreEqual(0, _saves.SavesOf(StoreNoAds.SaveKey));
            Assert.AreEqual(TimeSpan.FromSeconds(5), LastWait().Length);

            LastWait().Finish();
            _billing.FinishConnect(true);
            Assert.AreEqual(1, _billing.ProductFetches, "the price is known already");
            Assert.AreEqual(2, _billing.Listings);
            _billing.FinishListing(NoAds(StorePurchaseState.Confirmed));
            Assert.IsTrue(noAds.Owned);
        }

        [Test]
        public void AListedPaymentStillOnItsWay_GrantsNothing_AndStartsNoSecondPurchase()
        {
            StoreNoAds noAds = ReadyNoAds(NoAds(StorePurchaseState.AwaitingPayment));

            Assert.IsFalse(noAds.Owned);
            Assert.IsFalse(noAds.CanBuy, "a purchase is under way: its payment");
            Assert.AreEqual(NoAdsPurchase.Pending, Over(noAds.BuyAsync(CancellationToken.None)));
            Assert.AreEqual(0, _billing.Purchases);
            Assert.AreEqual(0, _saves.SaveCount);
            Assert.AreEqual(0, _billing.Confirms.Count);
        }

        [Test]
        public void Buy_OpensTheStoresSheetOnce_AndAPaidPurchaseIsSavedThenConfirmed()
        {
            StoreNoAds noAds = ReadyNoAds();
            int changed = 0;
            noAds.Changed += () => changed++;

            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);
            Assert.AreEqual(1, _billing.Purchases);
            Assert.AreEqual(NoAdsProduct.Id, _billing.PurchasedProductId);
            Assert.IsFalse(buy.IsCompleted, "the sheet is up");
            Assert.IsFalse(noAds.CanBuy, "one purchase at a time");
            Assert.AreEqual(1, changed);

            StorePurchase paid = NoAds(StorePurchaseState.Paid);
            _billing.Deliver(paid);
            Assert.AreEqual(NoAdsPurchase.Bought, Over(buy));
            Assert.IsTrue(noAds.Owned);
            Assert.IsFalse(noAds.CanBuy);
            Assert.AreEqual(2, changed);
            Assert.AreSame(paid, _billing.Confirms.Single());
            Assert.IsTrue(_billing.SavedOwnedAtConfirm.Single(), "saved before the store is told");

            _billing.FinishConfirm(true);
            Assert.AreEqual(0, _waits.Count);
            Assert.IsTrue(NewNoAds(new ScriptedBilling(_saves)).Owned, "and it survives a restart");
        }

        [Test]
        public void AClosedSheet_IsCancelled_AndChangesNothing()
        {
            StoreNoAds noAds = ReadyNoAds();
            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);

            _billing.Fail(NoAdsProduct.Id, StorePurchaseFailure.Cancelled);

            Assert.AreEqual(NoAdsPurchase.Cancelled, Over(buy));
            Assert.IsFalse(noAds.Owned);
            Assert.IsTrue(noAds.CanBuy);
            Assert.AreEqual(0, _saves.SaveCount);
            Assert.AreEqual(0, _billing.Confirms.Count);
            Assert.AreEqual(1, _billing.Listings, "nothing to list again");
        }

        [Test]
        public void APurchaseAwaitingItsPayment_IsPending_AndIsGrantedOnceItIsPaid()
        {
            StoreNoAds noAds = ReadyNoAds();
            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);

            _billing.Deliver(NoAds(StorePurchaseState.AwaitingPayment));
            Assert.AreEqual(NoAdsPurchase.Pending, Over(buy));
            Assert.IsFalse(noAds.Owned, "nothing until it is paid");
            Assert.IsFalse(noAds.CanBuy);
            Assert.AreEqual(0, _saves.SaveCount);
            Assert.AreEqual(NoAdsPurchase.Pending, Over(noAds.BuyAsync(CancellationToken.None)), "no second purchase while it waits");
            Assert.AreEqual(1, _billing.Purchases);

            // The payment settles while the game runs.
            StorePurchase paid = NoAds(StorePurchaseState.Paid);
            _billing.Deliver(paid);
            Assert.IsTrue(noAds.Owned);
            Assert.AreSame(paid, _billing.Confirms.Single());
            Assert.IsTrue(_billing.SavedOwnedAtConfirm.Single());
        }

        [Test]
        public void APaymentThatSettledWhileTheGameWasClosed_IsGrantedOnTheNextStart()
        {
            StoreNoAds first = ReadyNoAds();
            Task<NoAdsPurchase> buy = first.BuyAsync(CancellationToken.None);
            _billing.Deliver(NoAds(StorePurchaseState.AwaitingPayment));
            Assert.AreEqual(NoAdsPurchase.Pending, Over(buy));
            first.Dispose();

            _billing = new ScriptedBilling(_saves);
            StorePurchase paid = NoAds(StorePurchaseState.Paid);
            StoreNoAds next = ReadyNoAds(paid);

            Assert.IsTrue(next.Owned);
            Assert.AreSame(paid, _billing.Confirms.Single());
            Assert.IsTrue(_billing.SavedOwnedAtConfirm.Single());
        }

        [Test]
        public void APurchaseTheAccountOwnsAlready_IsListed_AndGrantedWithoutACharge()
        {
            StoreNoAds noAds = ReadyNoAds();
            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);

            _billing.Fail(NoAdsProduct.Id, StorePurchaseFailure.AlreadyOwned);
            Assert.AreEqual(2, _billing.Listings, "the store says it is owned: the account is listed to see it");
            Assert.IsFalse(buy.IsCompleted);

            _billing.FinishListing(NoAds(StorePurchaseState.Confirmed));
            Assert.AreEqual(NoAdsPurchase.AlreadyOwned, Over(buy));
            Assert.IsTrue(noAds.Owned);
            Assert.IsTrue(_saves.Peek<NoAdsData>(StoreNoAds.SaveKey).owned);
        }

        [Test]
        public void AnAlreadyOwnedThatTheListingDoesNotShow_Fails()
        {
            StoreNoAds noAds = ReadyNoAds();
            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);

            _billing.Fail(NoAdsProduct.Id, StorePurchaseFailure.AlreadyOwned);
            _billing.FinishListing();

            Assert.AreEqual(NoAdsPurchase.Failed, Over(buy));
            Assert.IsFalse(noAds.Owned);
            Assert.IsTrue(noAds.CanBuy);
        }

        [Test]
        public void AStoreThatCannotSellNow_IsUnavailable_AndIsConnectedAgain()
        {
            StoreNoAds noAds = ReadyNoAds();
            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);

            _billing.Fail(NoAdsProduct.Id, StorePurchaseFailure.Unavailable);
            Assert.AreEqual(NoAdsPurchase.Unavailable, Over(buy));
            Assert.IsFalse(noAds.CanBuy);
            Assert.AreEqual(Price, noAds.Price, "the price stays as the store gave it");

            Assert.AreEqual(2, _billing.Connects, "it connects again at once");
            _billing.FinishConnect(true);
            Assert.AreEqual(2, _billing.ProductFetches);
            _billing.FinishProduct(Price);
            Assert.IsTrue(noAds.CanBuy);
            Assert.AreEqual(1, _billing.Listings, "the account was listed already");
        }

        [Test]
        public void AFailedPurchase_Fails_AndTheAccountIsListedAgain_InCaseItWasPaid()
        {
            StoreNoAds noAds = ReadyNoAds();
            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);

            _billing.Fail(NoAdsProduct.Id, StorePurchaseFailure.Failed);
            Assert.AreEqual(NoAdsPurchase.Failed, Over(buy));
            Assert.IsTrue(noAds.CanBuy);

            _billing.FinishConnect(true);
            Assert.AreEqual(2, _billing.Listings);
            StorePurchase paid = NoAds(StorePurchaseState.Paid);
            _billing.FinishListing(paid);
            Assert.IsTrue(noAds.Owned, "paid after all");
            Assert.AreSame(paid, _billing.Confirms.Single());
        }

        [Test]
        public void AListingMadeBeforeAPurchase_DoesNotTakeItAway()
        {
            StoreNoAds noAds = ReadyNoAds();
            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);
            _billing.Fail(NoAdsProduct.Id, StorePurchaseFailure.Failed);
            Over(buy);
            _billing.FinishConnect(true);
            Assert.AreEqual(2, _billing.Listings, "the account is listed again...");

            _billing.Deliver(NoAds(StorePurchaseState.Paid));
            Assert.IsTrue(noAds.Owned, "...when the purchase comes through after all");

            _billing.FinishListing();
            Assert.IsTrue(noAds.Owned, "a listing older than the purchase takes nothing away");
        }

        [Test]
        public void Buy_BeforeTheStoreIsReady_IsUnavailable_WithoutASheet()
        {
            StoreNoAds noAds = NewNoAds();

            Assert.AreEqual(NoAdsPurchase.Unavailable, Over(noAds.BuyAsync(CancellationToken.None)));
            Assert.AreEqual(0, _billing.Purchases);
            Assert.AreEqual(1, _billing.Connects, "the connection under way is not hurried");
        }

        [Test]
        public void Buy_WhenOwned_IsAlreadyOwned_WithoutASheet()
        {
            _saves.Put(StoreNoAds.SaveKey, new NoAdsData { owned = true });
            StoreNoAds noAds = NewNoAds();

            Assert.AreEqual(NoAdsPurchase.AlreadyOwned, Over(noAds.BuyAsync(CancellationToken.None)));
            Assert.AreEqual(0, _billing.Purchases);
        }

        [Test]
        public void TwoBuysAtOnce_OpenOneSheet_AndBothGetWhatBecameOfIt()
        {
            StoreNoAds noAds = ReadyNoAds();

            Task<NoAdsPurchase> first = noAds.BuyAsync(CancellationToken.None);
            Task<NoAdsPurchase> second = noAds.BuyAsync(CancellationToken.None);
            Assert.AreEqual(1, _billing.Purchases);

            _billing.Deliver(NoAds(StorePurchaseState.Paid));
            Assert.AreEqual(NoAdsPurchase.Bought, Over(first));
            Assert.AreEqual(NoAdsPurchase.Bought, Over(second));
            Assert.AreEqual(1, _billing.Confirms.Count);
        }

        [Test]
        public void ACallerThatStopsWaiting_IsCancelled_ButThePurchaseStillCountsOncePaid()
        {
            StoreNoAds noAds = ReadyNoAds();
            using (var cancel = new CancellationTokenSource())
            {
                Task<NoAdsPurchase> buy = noAds.BuyAsync(cancel.Token);

                cancel.Cancel();
                Assert.IsTrue(buy.IsCanceled);
                Assert.IsFalse(noAds.CanBuy, "the sheet is still up");

                _billing.Deliver(NoAds(StorePurchaseState.Paid));
                Assert.IsTrue(noAds.Owned);
                Assert.AreEqual(1, _billing.Confirms.Count);
            }
        }

        [Test]
        public void Buy_WithACancelledToken_IsCancelled_WithoutThrowingAtTheCaller_OrASheet()
        {
            StoreNoAds noAds = ReadyNoAds();
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();
                Task<NoAdsPurchase> buy = null;

                Assert.DoesNotThrow(() => buy = noAds.BuyAsync(cancel.Token));
                Assert.IsTrue(buy.IsCanceled);
                Assert.AreEqual(0, _billing.Purchases);
                Assert.IsTrue(noAds.CanBuy);
            }
        }

        [Test]
        public void AConfirmationTheStoreDidNotTake_IsTriedAgain_ByListingAgain_AfterLongerWaits()
        {
            StoreNoAds noAds = ReadyNoAds(NoAds(StorePurchaseState.Paid));

            _billing.FinishConfirm(false);
            Assert.IsTrue(noAds.Owned, "the player keeps it meanwhile");
            Assert.AreEqual(TimeSpan.FromSeconds(5), LastWait().Length);

            LastWait().Finish();
            _billing.FinishConnect(true);
            Assert.AreEqual(2, _billing.Listings);
            _billing.FinishListing(NoAds(StorePurchaseState.Paid));
            Assert.AreEqual(2, _billing.Confirms.Count);
            _billing.FinishConfirm(false);
            Assert.AreEqual(TimeSpan.FromSeconds(10), LastWait().Length);

            LastWait().Finish();
            _billing.FinishConnect(true);
            _billing.FinishListing(NoAds(StorePurchaseState.Confirmed));
            Assert.AreEqual(2, _billing.Confirms.Count, "confirmed meanwhile: nothing left to confirm");
            Assert.AreEqual(2, _waits.Count);
            Assert.IsTrue(noAds.Owned);
        }

        [Test]
        public void AStoreThatAnswersBeforePurchaseReturns_StillEndsThePurchase()
        {
            StoreNoAds noAds = ReadyNoAds();
            _billing.DuringPurchase = () => _billing.Fail(NoAdsProduct.Id, StorePurchaseFailure.Cancelled);

            Assert.AreEqual(NoAdsPurchase.Cancelled, Over(noAds.BuyAsync(CancellationToken.None)));
            Assert.IsTrue(noAds.CanBuy);
        }

        [Test]
        public void ABrokenStore_NeverMakesItThrow()
        {
            _billing.ThrowOnConnect = true;
            StoreNoAds noAds = null;

            Assert.DoesNotThrow(() => noAds = NewNoAds());
            Assert.AreEqual(TimeSpan.FromSeconds(5), LastWait().Length, "a try that broke is tried again");

            _billing.ThrowOnConnect = false;
            LastWait().Finish();
            _billing.FinishConnect(true);
            _billing.FinishProduct(Price);
            _billing.FinishListing();
            Assert.IsTrue(noAds.CanBuy);

            _billing.ThrowOnPurchase = true;
            Task<NoAdsPurchase> buy = null;
            Assert.DoesNotThrow(() => buy = noAds.BuyAsync(CancellationToken.None));
            Assert.AreEqual(NoAdsPurchase.Failed, Over(buy));
            Assert.IsTrue(noAds.CanBuy);
        }

        [Test]
        public void AFileFromANewerVersion_IsNeverOverwritten_AndAPurchaseIsStillConfirmed()
        {
            _saves.Put(StoreNoAds.SaveKey, new NoAdsData { schemaVersion = NoAdsData.CurrentVersion + 1 });

            StoreNoAds noAds = ReadyNoAds(NoAds(StorePurchaseState.Paid));

            Assert.IsTrue(noAds.Owned);
            Assert.AreEqual(0, _saves.SavesOf(StoreNoAds.SaveKey), "a newer app's file is never overwritten");
            Assert.AreEqual(1, _billing.Confirms.Count, "the store keeps the record, so the purchase is kept");
        }

        [Test]
        public void OtherProducts_AreNotNoAds()
        {
            StoreNoAds noAds = ReadyNoAds(new StorePurchase("coins_100", StorePurchaseState.Paid, new object()));
            Assert.IsFalse(noAds.Owned);
            Assert.AreEqual(0, _billing.Confirms.Count);

            _billing.Deliver(new StorePurchase("coins_100", StorePurchaseState.Paid, new object()));
            Assert.IsFalse(noAds.Owned);

            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);
            _billing.Fail("coins_100", StorePurchaseFailure.Cancelled);
            Assert.IsFalse(buy.IsCompleted, "another product's failure is not this purchase's");
            _billing.Fail(null, StorePurchaseFailure.Cancelled);
            Assert.AreEqual(NoAdsPurchase.Cancelled, Over(buy), "a failure the store didn't place belongs to the purchase under way");
        }

        [Test]
        public void AChangedListenerThatThrows_BreaksNothing()
        {
            StoreNoAds noAds = ReadyNoAds();
            noAds.Changed += () => throw new InvalidOperationException("a broken screen");
            Task<NoAdsPurchase> buy = null;

            Assert.DoesNotThrow(() => buy = noAds.BuyAsync(CancellationToken.None));
            Assert.DoesNotThrow(() => _billing.Deliver(NoAds(StorePurchaseState.Paid)));
            Assert.AreEqual(NoAdsPurchase.Bought, Over(buy));
            Assert.IsTrue(noAds.Owned);
            Assert.IsTrue(_log.Any(line => line.Contains("a broken screen")), "the failure is logged");
        }

        [Test]
        public void BackInTheGame_WithoutNoAds_TheAccountIsListedAgain_AndAPurchaseMadeElsewhereCounts()
        {
            StoreNoAds noAds = ReadyNoAds();

            _billing.Resume();
            _billing.FinishConnect(true);
            Assert.AreEqual(2, _billing.Listings, "not owned: a listing may bring news");
            Assert.AreEqual(1, _billing.ProductFetches, "the price is known already");

            // E.g. a promo code redeemed in the Play Store while the game was in the background.
            _billing.FinishListing(NoAds(StorePurchaseState.Confirmed));
            Assert.IsTrue(noAds.Owned);
            Assert.IsTrue(_saves.Peek<NoAdsData>(StoreNoAds.SaveKey).owned);
        }

        [Test]
        public void BackInTheGame_TheAccountIsListedAtMostOnceEvery30Seconds()
        {
            Assert.AreEqual(TimeSpan.FromSeconds(30), StoreNoAds.RefreshInterval);
            ReadyNoAds();
            _billing.Resume();
            _billing.FinishConnect(true);
            _billing.FinishListing();

            _now += TimeSpan.FromSeconds(29);
            _billing.Resume();
            Assert.AreEqual(2, _billing.Connects, "29 s later: nothing");
            Assert.AreEqual(2, _billing.Listings);

            _now += TimeSpan.FromSeconds(1);
            _billing.Resume();
            _billing.FinishConnect(true);
            Assert.AreEqual(3, _billing.Listings, "30 s after the last one");
        }

        [Test]
        public void BackInTheGame_AnOwnerWithNothingToConfirm_IsNotListedAgain()
        {
            ReadyNoAds(NoAds(StorePurchaseState.Confirmed));

            _billing.Resume();

            Assert.AreEqual(1, _billing.Connects);
            Assert.AreEqual(1, _billing.Listings);
        }

        [Test]
        public void BackInTheGame_OnceTheStoreHasTheConfirmation_NothingIsListed()
        {
            ReadyNoAds(NoAds(StorePurchaseState.Paid));
            _billing.FinishConfirm(true);

            _billing.Resume();

            Assert.AreEqual(1, _billing.Listings);
        }

        [Test]
        public void BackInTheGame_APurchaseStillToConfirm_IsListedAgain_AndARefundTakesNoAdsAway()
        {
            StoreNoAds noAds = ReadyNoAds(NoAds(StorePurchaseState.Paid));
            _billing.FinishConfirm(false);
            Assert.IsTrue(noAds.Owned);

            _billing.Resume();
            _billing.FinishConnect(true);
            Assert.AreEqual(2, _billing.Listings, "owned, but its confirmation is still owed");

            // Google refunded the purchase that was never confirmed.
            _billing.FinishListing();
            Assert.IsFalse(noAds.Owned);
            Assert.IsFalse(_saves.Peek<NoAdsData>(StoreNoAds.SaveKey).owned);
            Assert.IsTrue(noAds.CanBuy, "it can be bought again");
        }

        [Test]
        public void BackInTheGame_APaymentThatSettledMeanwhile_IsGranted_AndConfirmed()
        {
            StoreNoAds noAds = ReadyNoAds(NoAds(StorePurchaseState.AwaitingPayment));

            _billing.Resume();
            _billing.FinishConnect(true);
            StorePurchase paid = NoAds(StorePurchaseState.Paid);
            _billing.FinishListing(paid);

            Assert.IsTrue(noAds.Owned);
            Assert.AreSame(paid, _billing.Confirms.Single());
            Assert.IsTrue(_billing.SavedOwnedAtConfirm.Single(), "saved before the store is told");
        }

        [Test]
        public void BackInTheGame_APaymentThatNeverCame_LetsNoAdsBeBoughtAgain()
        {
            StoreNoAds noAds = ReadyNoAds(NoAds(StorePurchaseState.AwaitingPayment));
            Assert.IsFalse(noAds.CanBuy);

            _billing.Resume();
            _billing.FinishConnect(true);
            _billing.FinishListing();

            Assert.IsFalse(noAds.Owned);
            Assert.IsTrue(noAds.CanBuy);
        }

        [Test]
        public void BackInTheGame_WhileThePurchaseSheetIsUp_NothingIsListed_NorIsTheWaitUsedUp()
        {
            StoreNoAds noAds = ReadyNoAds();
            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);

            // Google's sheet takes the focus as it opens, and gives it back as it closes.
            _billing.Resume();
            Assert.AreEqual(1, _billing.Connects);
            Assert.AreEqual(1, _billing.Listings);

            _billing.Fail(NoAdsProduct.Id, StorePurchaseFailure.Cancelled);
            Over(buy);
            _billing.Resume();
            _billing.FinishConnect(true);
            Assert.AreEqual(2, _billing.Listings, "the skipped return did not count toward the 30 s");
        }

        [Test]
        public void BackInTheGame_WhileItIsStillGettingReady_NothingMoreStarts()
        {
            NewNoAds();

            _billing.Resume();
            Assert.AreEqual(1, _billing.Connects, "the start-up's own connection goes on");

            _billing.FinishConnect(true);
            _billing.FinishProduct(Price);
            _billing.FinishListing();
            Assert.AreEqual(1, _billing.Listings);
        }

        [Test]
        public void BackInTheGame_AClockSetBack_DoesNotStopTheListings()
        {
            ReadyNoAds();
            _billing.Resume();
            _billing.FinishConnect(true);
            _billing.FinishListing();

            _now -= TimeSpan.FromHours(1);
            _billing.Resume();
            _billing.FinishConnect(true);

            Assert.AreEqual(3, _billing.Listings);
        }

        [Test]
        public void Dispose_StopsTryingAgain_AndHearsTheStoreNoMore()
        {
            StoreNoAds noAds = NewNoAds();
            _billing.FinishConnect(false);
            Wait wait = LastWait();

            noAds.Dispose();

            Assert.IsTrue(wait.Cancelled);
            Assert.AreEqual(1, _billing.Connects, "no further try after it is disposed");
            Assert.IsFalse(_billing.HasListeners);
        }

        /// <summary>The result of a purchase that must be over by now (never waits for one that isn't).</summary>
        private static NoAdsPurchase Over(Task<NoAdsPurchase> buy)
        {
            Assert.IsTrue(buy.IsCompleted, "the purchase is over");
            return buy.Result;
        }

        private static StorePurchase NoAds(StorePurchaseState state) => new StorePurchase(NoAdsProduct.Id, state, new object());

        private StoreNoAds NewNoAds() => NewNoAds(_billing);

        private StoreNoAds NewNoAds(ScriptedBilling billing) => new StoreNoAds(billing, _saves, () => _now, _log.Add, Delay);

        /// <summary>No Ads with the store connected, the price read and the account listed with <paramref name="listed"/>.</summary>
        private StoreNoAds ReadyNoAds(params StorePurchase[] listed)
        {
            StoreNoAds noAds = NewNoAds();
            _billing.FinishConnect(true);
            _billing.FinishProduct(Price);
            _billing.FinishListing(listed);
            return noAds;
        }

        private Task Delay(TimeSpan length, CancellationToken ct)
        {
            var wait = new Wait(length);
            ct.Register(wait.Cancel);
            _waits.Add(wait);
            return wait.Task;
        }

        private Wait LastWait() => _waits[_waits.Count - 1];

        private sealed class Wait
        {
            private readonly TaskCompletionSource<bool> _done = new TaskCompletionSource<bool>();

            public Wait(TimeSpan length)
            {
                Length = length;
            }

            public TimeSpan Length { get; }

            public Task Task => _done.Task;

            public bool Cancelled => _done.Task.IsCanceled;

            public void Finish() => _done.TrySetResult(true);

            public void Cancel() => _done.TrySetCanceled();
        }

        /// <summary>A store whose every answer the test gives, when it chooses.</summary>
        private sealed class ScriptedBilling : IStoreBilling
        {
            private readonly InMemorySaveStore _saves;
            private readonly List<TaskCompletionSource<bool>> _confirmations = new List<TaskCompletionSource<bool>>();
            private TaskCompletionSource<bool> _connect;
            private TaskCompletionSource<StoreProduct> _product;
            private TaskCompletionSource<IReadOnlyList<StorePurchase>> _listing;

            public ScriptedBilling(InMemorySaveStore saves)
            {
                _saves = saves;
            }

            public event Action<StorePurchase> PurchaseUpdated;

            public event Action<string, StorePurchaseFailure> PurchaseFailed;

            public event Action Resumed;

            public int Connects { get; private set; }

            public int ProductFetches { get; private set; }

            public string FetchedProductId { get; private set; }

            public int Listings { get; private set; }

            public int Purchases { get; private set; }

            public string PurchasedProductId { get; private set; }

            public List<StorePurchase> Confirms { get; } = new List<StorePurchase>();

            /// <summary>For each confirmation, whether No Ads was saved as owned when it was asked for.</summary>
            public List<bool> SavedOwnedAtConfirm { get; } = new List<bool>();

            public bool ThrowOnConnect { get; set; }

            public bool ThrowOnPurchase { get; set; }

            /// <summary>Runs inside <see cref="Purchase"/>: a store that answers before it returns.</summary>
            public Action DuringPurchase { get; set; }

            public bool HasListeners => PurchaseUpdated != null || PurchaseFailed != null || Resumed != null;

            public Task<bool> ConnectAsync()
            {
                Connects++;
                if (ThrowOnConnect)
                {
                    throw new InvalidOperationException("a broken store");
                }

                _connect = new TaskCompletionSource<bool>();
                return _connect.Task;
            }

            public Task<StoreProduct> FetchProductAsync(string productId)
            {
                ProductFetches++;
                FetchedProductId = productId;
                _product = new TaskCompletionSource<StoreProduct>();
                return _product.Task;
            }

            public Task<IReadOnlyList<StorePurchase>> FetchPurchasesAsync()
            {
                Listings++;
                _listing = new TaskCompletionSource<IReadOnlyList<StorePurchase>>();
                return _listing.Task;
            }

            public void Purchase(string productId)
            {
                Purchases++;
                PurchasedProductId = productId;
                if (ThrowOnPurchase)
                {
                    throw new InvalidOperationException("a broken store");
                }

                DuringPurchase?.Invoke();
            }

            public Task<bool> ConfirmAsync(StorePurchase purchase)
            {
                Confirms.Add(purchase);
                SavedOwnedAtConfirm.Add(_saves.Peek<NoAdsData>(StoreNoAds.SaveKey)?.owned == true);
                var confirmation = new TaskCompletionSource<bool>();
                _confirmations.Add(confirmation);
                return confirmation.Task;
            }

            public void FinishConnect(bool connected) => _connect.SetResult(connected);

            public void FinishProduct(string price) => _product.SetResult(price == null ? null : new StoreProduct(NoAdsProduct.Id, price));

            public void FinishListing(params StorePurchase[] purchases) => _listing.SetResult(purchases);

            public void FailListing() => _listing.SetResult(null);

            public void FinishConfirm(bool confirmed) => _confirmations[_confirmations.Count - 1].SetResult(confirmed);

            public void Deliver(StorePurchase purchase) => PurchaseUpdated?.Invoke(purchase);

            public void Fail(string productId, StorePurchaseFailure failure) => PurchaseFailed?.Invoke(productId, failure);

            /// <summary>The game comes back in front of the player.</summary>
            public void Resume() => Resumed?.Invoke();
        }
    }
}
