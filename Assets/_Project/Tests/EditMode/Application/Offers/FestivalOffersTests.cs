using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Offers;
using Callbreak.Tests.EditMode.Presentation;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// Festival offers (the owner, 31 Oct 2026; ADR 067): an offer from the backend is claimable while
    /// it is live, the player hasn't claimed it and doesn't own its theme yet. With no config, bad data
    /// or the offer's dates passed there is nothing. Tihar's offer is a gift for one rewarded ad: the ad
    /// watched to the end makes the theme the player's, through the shop's own ownership, not equipped;
    /// no ad, or one skipped, gives nothing. A free offer needs no ad, a discount takes its coins. Each
    /// offer is claimed once, and opens by itself once, both kept in the festival-offers file.
    /// </summary>
    public sealed class FestivalOffersTests
    {
        private static readonly DateTime DuringTihar = new DateTime(2026, 11, 1, 6, 30, 0, DateTimeKind.Utc);

        private InMemorySaveStore _store;
        private Wallet _wallet;
        private CosmeticsService _shop;
        private FakeSource _source;
        private FakeAds _ads;
        private DateTime _now;
        private FestivalOffers _offers;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _wallet = new Wallet(_store);
            _shop = new CosmeticsService(_store, _wallet);
            _source = new FakeSource();
            _source.Set(FestivalOfferReaderTests.Tihar());
            _ads = new FakeAds();
            _now = DuringTihar;
            _offers = NewOffers();
        }

        [TearDown]
        public void TearDown() => _offers.Dispose();

        [Test]
        public void Tihar_IsClaimable_WhileItIsLive()
        {
            Assert.AreEqual("tihar-2026", _offers.Claimable?.Id);
            Assert.AreEqual("tihar-lights", _offers.Claimable.ThemeId);
            Assert.IsTrue(_offers.IsOffered("tihar-lights"));
            Assert.IsFalse(_offers.IsOffered("mithila-garden"));
            Assert.IsFalse(_offers.IsOffered(null));
        }

        [Test]
        public void BeforeOrAfterItsDates_ThereIsNothing()
        {
            _now = new DateTime(2026, 10, 30, 18, 14, 59, DateTimeKind.Utc);
            Assert.IsNull(_offers.Claimable, "a second before 31 Oct in Nepal");

            _now = new DateTime(2026, 10, 30, 18, 15, 0, DateTimeKind.Utc);
            Assert.IsNotNull(_offers.Claimable, "midnight in Nepal");

            _now = new DateTime(2026, 11, 15, 18, 15, 0, DateTimeKind.Utc);
            Assert.IsNull(_offers.Claimable, "16 Nov in Nepal");
            Assert.IsFalse(_offers.IsOffered("tihar-lights"));
        }

        [Test]
        public void ALocalClockTime_IsTurnedIntoUtc()
        {
            _now = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();

            Assert.IsNull(_offers.Claimable);
        }

        [Test]
        public void NoConfig_OrBadData_IsNoOffer()
        {
            _source.Config = null;
            _source.RaiseChanged();
            Assert.IsNull(_offers.Claimable);

            FestivalOfferEntry bad = FestivalOfferReaderTests.Tihar();
            bad.theme = "tihar";
            _source.Set(bad);
            Assert.IsNull(_offers.Claimable);
        }

        [Test]
        public void NewOffersFromTheBackend_AreRead_AndSaidAsAChange()
        {
            _source.Config = null;
            _source.RaiseChanged();
            int changes = 0;
            _offers.Changed += () => changes++;

            _source.Set(FestivalOfferReaderTests.Tihar());

            Assert.AreEqual(1, changes);
            Assert.AreEqual("tihar-2026", _offers.Claimable?.Id);
        }

        [Test]
        public void ATheme_ThePlayerAlreadyOwns_ShowsNothing()
        {
            _wallet.Add(1000);
            int changes = 0;
            _offers.Changed += () => changes++;

            Assert.AreEqual(PurchaseResult.Bought, _shop.Buy(CosmeticKind.Theme, "tihar-lights"));

            Assert.IsNull(_offers.Claimable);
            Assert.IsFalse(_offers.IsOffered("tihar-lights"));
            Assert.AreEqual(1, changes, "the shop's change is the offer's");
        }

        [Test]
        public void OneAdWatched_MakesTheThemeThePlayers_NotEquipped_AndFree()
        {
            int coins = _wallet.Coins;
            int changes = 0;
            _offers.Changed += () => changes++;

            FestivalClaimResult result = Claim();

            Assert.AreEqual(FestivalClaimResult.Claimed, result);
            Assert.AreEqual(1, _ads.Shows);
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Theme, "tihar-lights"));
            Assert.AreEqual("durbar-square", _shop.Equipped(CosmeticKind.Theme), "claiming never equips: the button turns to Equip");
            Assert.AreEqual(coins, _wallet.Coins);
            Assert.AreEqual("tihar-lights", _shop.LastBought(CosmeticKind.Theme), "it comes first in the shop, as a theme just bought");
            Assert.IsNull(_offers.Claimable);
            Assert.GreaterOrEqual(changes, 1);
            CollectionAssert.AreEqual(new[] { "tihar-2026" }, _store.Peek<FestivalOffersData>(FestivalOffers.SaveKey).claimedOfferIds);
        }

        [Test]
        public void NoAdToShow_GivesNothing_AndShowsNone()
        {
            _ads.Available = false;

            Assert.AreEqual(FestivalClaimResult.AdUnavailable, Claim());

            Assert.AreEqual(0, _ads.Shows);
            AssertNothingGiven();
        }

        [Test]
        public void AnAdSkippedOrFailed_GivesNothing_AndTheOfferStays()
        {
            _ads.Completes = false;

            Assert.AreEqual(FestivalClaimResult.AdNotWatched, Claim());

            Assert.AreEqual(1, _ads.Shows);
            AssertNothingGiven();
        }

        [Test]
        public void ACancelledAd_GivesNothing()
        {
            _ads.Cancels = true;

            Assert.IsTrue(_offers.ClaimAsync(CancellationToken.None).IsCanceled);

            AssertNothingGiven();
        }

        [Test]
        public void AFreeOffer_NeedsNoAd()
        {
            FestivalOfferEntry free = FestivalOfferReaderTests.Tihar();
            free.kind = "free";
            _source.Set(free);

            Assert.AreEqual(FestivalClaimResult.Claimed, Claim());

            Assert.AreEqual(0, _ads.Shows);
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Theme, "tihar-lights"));
        }

        [Test]
        public void ADiscount_TakesItsCoins_OrNoneWhenTheyArentThere()
        {
            FestivalOfferEntry discount = FestivalOfferReaderTests.Tihar();
            discount.kind = "discount";
            discount.price = 400;
            _source.Set(discount);
            _wallet.TrySpend(_wallet.Coins - 399);

            Assert.AreEqual(FestivalClaimResult.NotEnoughCoins, Claim());
            AssertNothingGiven();
            Assert.AreEqual(399, _wallet.Coins);

            _wallet.Add(101);
            Assert.AreEqual(FestivalClaimResult.Claimed, Claim());

            Assert.AreEqual(100, _wallet.Coins, "400 of the 500, not the shop's 1,000");
            Assert.AreEqual(0, _ads.Shows);
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Theme, "tihar-lights"));
        }

        [Test]
        public void AnOffer_IsClaimedOnce_AlsoAfterARestart()
        {
            Claim();

            Assert.AreEqual(FestivalClaimResult.NothingToClaim, Claim());
            Assert.AreEqual(1, _ads.Shows);

            // A save where the theme went missing (an old cosmetics file restored) still has the claim.
            _store.Delete(CosmeticsService.SaveKey);
            _shop = new CosmeticsService(_store, _wallet);
            _offers.Dispose();
            _offers = NewOffers();
            Assert.IsNull(_offers.Claimable);
        }

        [Test]
        public void ASecondClaim_WhileTheAdRuns_DoesNothing()
        {
            var showing = new TaskCompletionSource<bool>();
            _ads.Pending = showing;

            Task<FestivalClaimResult> first = _offers.ClaimAsync(CancellationToken.None);
            Assert.AreEqual(FestivalClaimResult.Busy, Claim());
            showing.SetResult(true);

            Assert.IsTrue(first.IsCompleted);
            Assert.AreEqual(FestivalClaimResult.Claimed, first.Result);
            Assert.AreEqual(1, _ads.Shows);
        }

        [Test]
        public void AnAdThatOutlastsTheOffer_StillGivesWhatItWasWatchedFor()
        {
            var showing = new TaskCompletionSource<bool>();
            _ads.Pending = showing;

            Task<FestivalClaimResult> claim = _offers.ClaimAsync(CancellationToken.None);
            _now = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);
            showing.SetResult(true);

            Assert.IsTrue(claim.IsCompleted);
            Assert.AreEqual(FestivalClaimResult.Claimed, claim.Result);
            Assert.IsTrue(_shop.IsOwned(CosmeticKind.Theme, "tihar-lights"));
        }

        [Test]
        public void WithNothingToClaim_NoAdIsShown()
        {
            _now = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

            Assert.AreEqual(FestivalClaimResult.NothingToClaim, Claim());

            Assert.AreEqual(0, _ads.Shows);
        }

        [Test]
        public void TwoLiveOffers_TheFirstWrittenComesFirst_ThenTheNext()
        {
            FestivalOfferEntry mithila = FestivalOfferReaderTests.Tihar("chhath-2026");
            mithila.theme = "mithila-garden";
            _source.Set(FestivalOfferReaderTests.Tihar(), mithila);

            Assert.AreEqual("tihar-2026", _offers.Claimable.Id);
            Claim();

            Assert.AreEqual("chhath-2026", _offers.Claimable?.Id);
        }

        [Test]
        public void ItOpensByItself_OncePerOffer_AlsoAfterARestart()
        {
            Assert.IsTrue(_offers.TakeAutoOpen());
            Assert.IsFalse(_offers.TakeAutoOpen());

            _offers.Dispose();
            _offers = NewOffers();
            Assert.IsFalse(_offers.TakeAutoOpen());

            // Next year's offer is a new one.
            _source.Set(FestivalOfferReaderTests.Tihar("tihar-2027"));
            Assert.IsTrue(_offers.TakeAutoOpen());
        }

        [Test]
        public void ItDoesntOpen_WithNothingToClaim()
        {
            _now = new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc);

            Assert.IsFalse(_offers.TakeAutoOpen());

            _now = DuringTihar;
            Assert.IsTrue(_offers.TakeAutoOpen(), "the opening wasn't used up");
        }

        [Test]
        public void AFileFromANewerVersion_IsNeverOverwritten_AndNothingIsClaimable()
        {
            _offers.Dispose();
            _store.Put(FestivalOffers.SaveKey, new FestivalOffersData { schemaVersion = FestivalOffersData.CurrentVersion + 1 });
            _offers = NewOffers();
            int saves = _store.SavesOf(FestivalOffers.SaveKey);

            Assert.IsNull(_offers.Claimable, "a claim this build couldn't keep could be made again");
            Assert.IsFalse(_offers.TakeAutoOpen());
            Assert.AreEqual(saves, _store.SavesOf(FestivalOffers.SaveKey));
        }

        [Test]
        public void ABrokenFile_ReadsAsNothingClaimedOrOpened()
        {
            _offers.Dispose();
            _store.Put(FestivalOffers.SaveKey, new FestivalOffersData { claimedOfferIds = null, openedOfferIds = new List<string> { null, "" } });
            _offers = NewOffers();

            Assert.AreEqual("tihar-2026", _offers.Claimable?.Id);
            Assert.IsTrue(_offers.TakeAutoOpen());
        }

        [Test]
        public void Disposed_ItNoLongerFollowsTheBackendOrTheShop()
        {
            int changes = 0;
            _offers.Changed += () => changes++;
            _offers.Dispose();

            _source.Set(FestivalOfferReaderTests.Tihar("tihar-2027"));
            _wallet.Add(1000);
            _shop.Buy(CosmeticKind.Theme, "tihar-lights");

            Assert.AreEqual(0, changes);
            Assert.AreEqual(0, _source.Listeners);
        }

        [Test]
        public void Refresh_SaysAChange_WhenAnOfferStartsOrEnds_AndOnlyThen()
        {
            _now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
            _offers.Dispose();
            _offers = NewOffers();
            int changes = 0;
            _offers.Changed += () => changes++;

            _offers.Refresh();
            Assert.AreEqual(0, changes, "still before Tihar");

            _now = DuringTihar;
            _offers.Refresh();
            Assert.AreEqual(1, changes, "Tihar has started");
            _offers.Refresh();
            Assert.AreEqual(1, changes, "nothing new");

            _now = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
            _offers.Refresh();
            Assert.AreEqual(2, changes, "Tihar has ended");
            Assert.IsNull(_offers.Claimable);
        }

        [Test]
        public void Refresh_AfterAClaim_SaysNothingMore()
        {
            _offers.Refresh();
            Claim();
            int changes = 0;
            _offers.Changed += () => changes++;

            _offers.Refresh();

            Assert.AreEqual(0, changes, "the claim said it already");
        }

        [Test]
        public void Refresh_AfterTheBackendChanged_SaysNothingMore()
        {
            _offers.Refresh();
            _source.Set(FestivalOfferReaderTests.Tihar("tihar-2027"));
            int changes = 0;
            _offers.Changed += () => changes++;

            _offers.Refresh();

            Assert.AreEqual(0, changes, "the backend's change said it already");
        }

        [Test]
        public void TheOffersWords_AreInTheUiTable()
        {
            var strings = new TableStrings();

            foreach (string key in new[]
            {
                FestivalOfferReader.DefaultTitleKey, "ui.offer.tihar", "ui.offer.claim.watch_ad", "ui.offer.claim.free",
                "ui.offer.claim.discount", "ui.offer.ad_unavailable", "ui.offer.ad_not_watched", "ui.shop.themes.festival",
                "ui.shop.status.free",
            })
            {
                Assert.IsTrue(strings.Has(key), key);
            }

            Assert.AreEqual("Buy for 400 coins", strings.Format("ui.offer.claim.discount", "400"));
            Assert.AreEqual("Free", strings.Get(Callbreak.Presentation.Screens.ShopDisplay.StatusKey(Callbreak.Presentation.Screens.ShopTileStatus.Offer)));
        }

        private void AssertNothingGiven()
        {
            Assert.IsFalse(_shop.IsOwned(CosmeticKind.Theme, "tihar-lights"));
            Assert.AreEqual("tihar-2026", _offers.Claimable?.Id, "the offer is still there to claim");
            Assert.IsFalse(_store.Peek<FestivalOffersData>(FestivalOffers.SaveKey)?.claimedOfferIds?.Any() ?? false);
        }

        /// <summary>A claim whose ad (if any) is answered at once.</summary>
        private FestivalClaimResult Claim()
        {
            Task<FestivalClaimResult> claim = _offers.ClaimAsync(CancellationToken.None);
            Assert.IsTrue(claim.IsCompleted, "answered at once");
            return claim.Result;
        }

        private FestivalOffers NewOffers() => new FestivalOffers(_source, _shop, _ads, _store, () => _now);

        private sealed class FakeSource : IFestivalOfferSource
        {
            private Action _changed;

            public event Action Changed
            {
                add
                {
                    _changed += value;
                    Listeners++;
                }

                remove
                {
                    _changed -= value;
                    Listeners--;
                }
            }

            public int Listeners { get; private set; }

            public FestivalOffersConfig Config { get; set; }

            public void Set(params FestivalOfferEntry[] entries)
            {
                Config = new FestivalOffersConfig { offers = entries.ToList() };
                RaiseChanged();
            }

            public void RaiseChanged() => _changed?.Invoke();
        }

        private sealed class FakeAds : IRewardedAds
        {
            public bool Available { get; set; } = true;

            public bool Completes { get; set; } = true;

            public bool Cancels { get; set; }

            public TaskCompletionSource<bool> Pending { get; set; }

            public int Shows { get; private set; }

            public bool IsAvailable => Available;

            public Task<bool> ShowAsync(CancellationToken ct)
            {
                Shows++;
                if (Cancels)
                {
                    throw new OperationCanceledException();
                }

                return Pending != null ? Pending.Task : Task.FromResult(Completes);
            }
        }
    }
}
