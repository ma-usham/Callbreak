using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Screens;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The main menu's No Ads button (the owner, 5 Oct 2026): it shows while the store can sell No Ads
    /// and goes once it is owned, following the store, and it stays while its own purchase is under
    /// way. A tap opens the store's sheet once however often it is tapped meanwhile; a toast then says
    /// what became of the purchase (thanks, pending, the store isn't available), or nothing when the
    /// sheet was closed. The copy is the shipped string table's.
    /// </summary>
    public sealed class NoAdsOfferTests
    {
        private FakeNoAds _store;
        private RecordingToast _toast;
        private TableStrings _strings;
        private NoAdsOffer _offer;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            // A purchase the test answers resumes the offer on the test's own thread, at once, in the
            // Editor as in the dotnet harness.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _store = new FakeNoAds();
            _toast = new RecordingToast();
            _strings = new TableStrings();
            _offer = new NoAdsOffer(_store, _toast, _strings);
            _offer.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _offer.Dispose();
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [Test]
        public void TheButton_ShowsWhileNoAdsCanBeBought()
        {
            Assert.IsTrue(_offer.Visible);
            Assert.IsFalse(_offer.Buying);
        }

        [Test]
        public void TheButton_IsHidden_WhileTheStoreCannotSellIt()
        {
            _store.Set(canBuy: false, owned: false);

            Assert.IsFalse(_offer.Visible);
        }

        [Test]
        public void TheButton_FollowsTheStore_AndGoesOnceNoAdsIsOwned()
        {
            int changed = 0;
            _offer.Changed += () => changed++;

            _store.Set(canBuy: false, owned: false);
            Assert.IsFalse(_offer.Visible, "the store can't sell it now (offline, no Play Store)");

            _store.Set(canBuy: true, owned: false);
            Assert.IsTrue(_offer.Visible, "the store is back");

            _store.Set(canBuy: false, owned: true);
            Assert.IsFalse(_offer.Visible, "owned: restored by the store for the player's account");
            Assert.AreEqual(3, changed, "every change of the store's is passed on");
        }

        [Test]
        public void ATap_OpensTheStoreSheetOnce_WhileItIsOpenFurtherTapsDoNothing()
        {
            Task first = _offer.TapAsync(CancellationToken.None);
            Task second = _offer.TapAsync(CancellationToken.None);

            Assert.AreEqual(1, _store.Purchases, "one sheet");
            Assert.IsTrue(_offer.Buying);
            Assert.IsTrue(second.IsCompleted, "the second tap does nothing");
            Assert.IsFalse(first.IsCompleted, "the first waits for the sheet");

            _store.Answer(NoAdsPurchase.Cancelled);

            Assert.IsTrue(first.IsCompleted);
            Assert.IsFalse(_offer.Buying);
            Assert.AreEqual(1, _store.Purchases);
        }

        [Test]
        public void TheButton_StaysWhileItsPurchaseIsUnderWay_ThenFollowsTheStoreAgain()
        {
            Task tap = _offer.TapAsync(CancellationToken.None);

            // The store sells nothing else while a purchase is under way.
            _store.Set(canBuy: false, owned: false);
            Assert.IsTrue(_offer.Visible, "it doesn't vanish under the finger");

            int changed = 0;
            _offer.Changed += () => changed++;
            _store.Answer(NoAdsPurchase.Pending);

            Assert.IsTrue(tap.IsCompleted);
            Assert.AreEqual(1, changed, "the end of the purchase is a change");
            Assert.IsFalse(_offer.Visible, "a pending payment can't be bought again");
        }

        [Test]
        public void Bought_ThanksThePlayer_AndTheButtonGoes()
        {
            Task tap = _offer.TapAsync(CancellationToken.None);
            _store.Set(canBuy: false, owned: true);
            _store.Answer(NoAdsPurchase.Bought);

            Assert.IsTrue(tap.IsCompleted);
            Assert.IsFalse(tap.IsFaulted);
            AssertToasts("ui.main_menu.no_ads.bought");
            Assert.IsFalse(_offer.Visible);
        }

        [TestCase(NoAdsPurchase.Bought, "ui.main_menu.no_ads.bought")]
        [TestCase(NoAdsPurchase.AlreadyOwned, "ui.main_menu.no_ads.already_owned")]
        [TestCase(NoAdsPurchase.Pending, "ui.main_menu.no_ads.pending")]
        [TestCase(NoAdsPurchase.Unavailable, "ui.main_menu.no_ads.unavailable")]
        [TestCase(NoAdsPurchase.Failed, "ui.main_menu.no_ads.unavailable")]
        public void WhatBecameOfThePurchase_IsSaidInOneToast(NoAdsPurchase purchase, string key)
        {
            Task tap = _offer.TapAsync(CancellationToken.None);
            _store.Answer(purchase);

            Assert.IsTrue(tap.IsCompleted);
            Assert.IsFalse(tap.IsFaulted);
            AssertToasts(key);
        }

        [Test]
        public void ClosingTheSheet_SaysNothing_AndTheNextTapOpensItAgain()
        {
            Task tap = _offer.TapAsync(CancellationToken.None);
            _store.Answer(NoAdsPurchase.Cancelled);

            Assert.IsTrue(tap.IsCompleted);
            CollectionAssert.IsEmpty(_toast.Shown, "the player closed the sheet: nothing to say");
            Assert.IsTrue(_offer.Visible);

            _offer.TapAsync(CancellationToken.None);
            Assert.AreEqual(2, _store.Purchases);
        }

        [Test]
        public void ATap_WhileNoAdsCannotBeBought_DoesNothing()
        {
            _store.Set(canBuy: false, owned: false);
            Task tap = _offer.TapAsync(CancellationToken.None);

            Assert.IsTrue(tap.IsCompleted);
            Assert.AreEqual(0, _store.Purchases);

            _store.Set(canBuy: false, owned: true);
            _offer.TapAsync(CancellationToken.None);

            Assert.AreEqual(0, _store.Purchases, "owned: nothing to buy");
            CollectionAssert.IsEmpty(_toast.Shown);
        }

        [Test]
        public void ACancelledWait_EndsQuietly_AndFreesTheButton()
        {
            using (var leaving = new CancellationTokenSource())
            {
                Task tap = _offer.TapAsync(leaving.Token);
                leaving.Cancel();

                Assert.IsTrue(tap.IsCanceled, "the menu stopped waiting for the store");
                Assert.IsFalse(_offer.Buying);
                CollectionAssert.IsEmpty(_toast.Shown);
            }
        }

        [Test]
        public void AStoreThatThrows_SaysItIsNotAvailable_AndPassesTheErrorOn()
        {
            _store.Throw = new InvalidOperationException("a broken store adapter");

            Task tap = _offer.TapAsync(CancellationToken.None);

            Assert.IsTrue(tap.IsFaulted);
            Assert.IsInstanceOf<InvalidOperationException>(tap.Exception.InnerException);
            AssertToasts("ui.main_menu.no_ads.unavailable");
            Assert.IsFalse(_offer.Buying);
            Assert.IsTrue(_offer.Visible, "the player may try again");
        }

        [Test]
        public void Dispose_StopsFollowingTheStore()
        {
            Assert.AreEqual(1, _store.Listeners);

            _offer.Dispose();
            _offer.Dispose();

            Assert.AreEqual(0, _store.Listeners);
        }

        /// <summary>Exactly these toasts were shown, in the table's words (which exist).</summary>
        private void AssertToasts(params string[] keys)
        {
            var expected = new List<string>();
            foreach (string key in keys)
            {
                Assert.IsTrue(_strings.Has(key), key + " is in UI.csv");
                expected.Add(_strings.Get(key));
            }

            CollectionAssert.AreEqual(expected, _toast.Shown);
        }

        /// <summary>A store whose sheet stays open until the test answers it.</summary>
        private sealed class FakeNoAds : INoAds
        {
            private TaskCompletionSource<NoAdsPurchase> _sheet;

            public event Action Changed;

            public bool Owned { get; private set; }

            public bool CanBuy { get; private set; } = true;

            public string Price => "$0.99";

            public int Purchases { get; private set; }

            public Exception Throw { get; set; }

            public int Listeners => Changed == null ? 0 : Changed.GetInvocationList().Length;

            public Task<NoAdsPurchase> BuyAsync(CancellationToken ct)
            {
                Purchases++;
                if (Throw != null)
                {
                    throw Throw;
                }

                var sheet = new TaskCompletionSource<NoAdsPurchase>();
                ct.Register(() => sheet.TrySetCanceled(ct));
                _sheet = sheet;
                return sheet.Task;
            }

            public void Answer(NoAdsPurchase purchase) => _sheet.SetResult(purchase);

            public void Set(bool canBuy, bool owned)
            {
                CanBuy = canBuy;
                Owned = owned;
                Changed?.Invoke();
            }
        }

        private sealed class RecordingToast : IToast
        {
            public List<string> Shown { get; } = new List<string>();

            public void Show(string text) => Shown.Add(text);
        }
    }
}
