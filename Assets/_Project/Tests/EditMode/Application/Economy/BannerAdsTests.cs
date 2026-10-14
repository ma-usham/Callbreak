using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The table's banner over a real network (AdMob on phones): the network starts at once (consent,
    /// then the SDK) and one banner loads, off the screen; a failed start or load is tried again after
    /// 5 s, 10 s, 20 s ... at most two minutes. The banner shows only where the table says it may,
    /// right-aligned in that slot and centred in its height, and only when it fits there; it goes off
    /// the screen when the table says so, and moves with its slot. A player who owns No Ads gets
    /// nothing requested and sees nothing: buying it takes the banner away, a refund loads one again.
    /// Nothing the network does makes it throw, and disposing it releases the banner.
    /// </summary>
    public sealed class BannerAdsTests
    {
        /// <summary>A 320 x 50 dp banner on a phone of density 2.75: 880 x 138 pixels.</summary>
        private const int BannerWidth = 880;

        private const int BannerHeight = 138;

        /// <summary>Left of the Scores button at the top of a 2400 x 1080 screen, room for the banner and a little more.</summary>
        private static readonly BannerSlot Slot = new BannerSlot(1100, 10, 1000, 150);

        private ScriptedNetwork _network;
        private ScriptedNoAds _noAds;
        private List<Wait> _waits;
        private List<string> _log;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            // The network's callbacks resume the banner on the test's own thread, at once, in the
            // Editor as in the dotnet harness.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _network = new ScriptedNetwork();
            _noAds = new ScriptedNoAds();
            _waits = new List<Wait>();
            _log = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [Test]
        public void TheNetwork_StartsAtOnce_ThenOneBannerLoads_OffTheScreen()
        {
            BannerAds ads = NewAds();

            Assert.AreEqual(1, _network.Starts, "consent and the SDK start with the table");
            Assert.AreEqual(0, _network.Loads, "no banner is requested before the network may request ads");

            _network.FinishStart(true);
            Assert.AreEqual(1, _network.Loads);

            var banner = new ScriptedBanner();
            _network.FinishLoad(banner);
            Assert.AreEqual(0, banner.ShowCount, "nothing shows before the table gives it a place");
            Assert.IsFalse(ads.IsShowing);
            Assert.AreEqual(1, _network.Starts);
            Assert.AreEqual(1, _network.Loads, "one banner, not more");
        }

        [Test]
        public void ALoadedBanner_ShowsInItsSlot_RightAligned_AndCentredInItsHeight()
        {
            BannerAds ads = ReadyAds(out ScriptedBanner banner);

            ads.ShowIn(Slot);

            Assert.IsTrue(ads.IsShowing);
            Assert.IsTrue(banner.OnScreen);
            Assert.AreEqual(Slot.X + Slot.Width - BannerWidth, banner.X, "its right edge on the slot's, next to the Scores button");
            Assert.AreEqual(Slot.Y + ((Slot.Height - BannerHeight) / 2), banner.Y);
        }

        [Test]
        public void ASlotGivenBeforeTheBannerLoads_GetsTheBannerOnceItComes()
        {
            BannerAds ads = NewAds();
            ads.ShowIn(Slot);
            Assert.IsFalse(ads.IsShowing, "nothing to show yet");

            _network.FinishStart(true);
            var banner = new ScriptedBanner();
            _network.FinishLoad(banner);

            Assert.IsTrue(ads.IsShowing);
            Assert.IsTrue(banner.OnScreen);
            Assert.AreEqual(Slot.X + Slot.Width - BannerWidth, banner.X);
        }

        [Test]
        public void Hide_TakesTheBannerOffTheScreen_AndShowInBringsTheSameOneBack()
        {
            BannerAds ads = ReadyAds(out ScriptedBanner banner);
            ads.ShowIn(Slot);

            ads.Hide();
            Assert.IsFalse(ads.IsShowing);
            Assert.IsFalse(banner.OnScreen, "under the scorecard, the menu or a dialog, nothing of it shows");
            Assert.AreEqual(0, banner.Destroys, "it stays loaded");

            ads.ShowIn(Slot);
            Assert.IsTrue(banner.OnScreen);
            Assert.AreEqual(1, _network.Loads, "no new banner is requested");
        }

        [Test]
        public void HiddenBeforeTheBannerLoads_ItStaysOffTheScreenWhenItComes()
        {
            BannerAds ads = NewAds();
            ads.ShowIn(Slot);
            ads.Hide();

            _network.FinishStart(true);
            var banner = new ScriptedBanner();
            _network.FinishLoad(banner);

            Assert.IsFalse(ads.IsShowing);
            Assert.AreEqual(0, banner.ShowCount);
        }

        [Test]
        public void TheSameSlotAgain_ChangesNothing_AndAnotherSlot_MovesTheBanner()
        {
            BannerAds ads = ReadyAds(out ScriptedBanner banner);
            ads.ShowIn(Slot);
            ads.ShowIn(Slot);
            ads.ShowIn(Slot);
            Assert.AreEqual(1, banner.ShowCount, "asked every frame, it is placed once");

            // The phone turned round: the notch, and the safe area with it, moved to the other side.
            var moved = new BannerSlot(1000, 10, 1000, 150);
            ads.ShowIn(moved);

            Assert.AreEqual(2, banner.ShowCount);
            Assert.AreEqual(moved.X + moved.Width - BannerWidth, banner.X);
            Assert.IsTrue(ads.IsShowing);
        }

        [TestCase(BannerWidth - 1, 150)]
        [TestCase(1000, BannerHeight - 1)]
        public void ABannerThatDoesNotFitItsSlot_StaysOffTheScreen(int width, int height)
        {
            BannerAds ads = ReadyAds(out ScriptedBanner banner);

            ads.ShowIn(new BannerSlot(1100, 10, width, height));

            Assert.IsFalse(ads.IsShowing, "it would cover the game");
            Assert.AreEqual(0, banner.ShowCount);
            StringAssert.Contains("does not fit", string.Join("\n", _log));
        }

        [Test]
        public void ABannerThatNoLongerFits_GoesOffTheScreen_AndComesBackWhenItFitsAgain()
        {
            BannerAds ads = ReadyAds(out ScriptedBanner banner);
            ads.ShowIn(Slot);

            ads.ShowIn(new BannerSlot(1100, 10, 600, 150));
            Assert.IsFalse(ads.IsShowing);
            Assert.IsFalse(banner.OnScreen);

            ads.ShowIn(Slot);
            Assert.IsTrue(ads.IsShowing);
            Assert.IsTrue(banner.OnScreen);
        }

        [Test]
        public void ASlotTheBannerExactlyFills_ShowsIt()
        {
            BannerAds ads = ReadyAds(out ScriptedBanner banner);

            ads.ShowIn(new BannerSlot(1100, 10, BannerWidth, BannerHeight));

            Assert.IsTrue(ads.IsShowing);
            Assert.AreEqual(1100, banner.X);
            Assert.AreEqual(10, banner.Y);
        }

        [Test]
        public void AStartThatMayNotRequestAds_IsTriedAgain_AfterLongerWaits()
        {
            BannerAds ads = NewAds();
            ads.ShowIn(Slot);

            _network.FinishStart(false);
            Assert.AreEqual(0, _network.Loads);
            Assert.AreEqual(TimeSpan.FromSeconds(5), LastWait().Length);

            LastWait().Finish();
            Assert.AreEqual(2, _network.Starts);
            _network.FinishStart(false);
            Assert.AreEqual(TimeSpan.FromSeconds(10), LastWait().Length);

            LastWait().Finish();
            _network.FinishStart(true);
            _network.FinishLoad(new ScriptedBanner());
            Assert.AreEqual(3, _network.Starts);
            Assert.AreEqual(1, _network.Loads);
            Assert.IsTrue(ads.IsShowing);
        }

        [Test]
        public void ALoadWithoutABanner_IsTriedAgain_WithoutStartingTheNetworkAgain()
        {
            BannerAds ads = NewAds();
            ads.ShowIn(Slot);
            _network.FinishStart(true);

            _network.FinishLoad(null);
            Assert.IsFalse(ads.IsShowing);
            Assert.AreEqual(TimeSpan.FromSeconds(5), LastWait().Length);

            LastWait().Finish();
            _network.FinishLoad(null);
            Assert.AreEqual(TimeSpan.FromSeconds(10), LastWait().Length);

            LastWait().Finish();
            _network.FinishLoad(new ScriptedBanner());
            Assert.IsTrue(ads.IsShowing);
            Assert.AreEqual(1, _network.Starts, "the network starts once");
            Assert.AreEqual(3, _network.Loads);
        }

        [Test]
        public void TheRetryWait_Doubles_UpToTwoMinutes()
        {
            CollectionAssert.AreEqual(
                new[] { 5d, 10d, 20d, 40d, 80d, 120d, 120d },
                new[] { 1, 2, 3, 4, 5, 6, 50 }.Select(n => BannerAds.RetryDelay(n).TotalSeconds));
        }

        [Test]
        public void APlayerWhoOwnsNoAds_GetsNothingRequested_AndSeesNothing()
        {
            _noAds.Owned = true;
            BannerAds ads = NewAds();

            ads.ShowIn(Slot);

            Assert.IsFalse(ads.IsShowing);
            Assert.AreEqual(0, _network.Starts, "not even consent and the SDK");
            Assert.AreEqual(0, _network.Loads);
            Assert.AreEqual(0, _waits.Count, "nothing is waiting to try again");
        }

        [Test]
        public void BuyingNoAds_TakesTheBannerAway_AndRequestsNoMore()
        {
            BannerAds ads = ReadyAds(out ScriptedBanner banner);
            ads.ShowIn(Slot);

            _noAds.Buy();

            Assert.IsFalse(ads.IsShowing);
            Assert.IsFalse(banner.OnScreen, "gone at once");
            Assert.AreEqual(1, banner.Destroys, "and released");
            ads.ShowIn(Slot);
            Assert.IsFalse(ads.IsShowing, "the table asking again brings nothing");
            Assert.AreEqual(1, _network.Loads, "no other banner is requested");
        }

        [Test]
        public void BuyingNoAds_WhileWaitingToTryAgain_StopsTheTries()
        {
            BannerAds ads = NewAds();
            _network.FinishStart(true);
            _network.FinishLoad(null);
            Wait wait = LastWait();

            _noAds.Buy();

            Assert.IsTrue(wait.Cancelled, "the wait is over");
            Assert.AreEqual(1, _network.Loads);
            Assert.AreEqual(1, _waits.Count, "and no other try is planned");
            Assert.IsFalse(ads.IsShowing);
        }

        [Test]
        public void BuyingNoAds_WhileTheNetworkStarts_RequestsNoBanner()
        {
            BannerAds ads = NewAds();

            _noAds.Buy();
            _network.FinishStart(true);

            Assert.AreEqual(0, _network.Loads);
            Assert.AreEqual(0, _waits.Count);
            Assert.IsFalse(ads.IsShowing);
        }

        [Test]
        public void BuyingNoAds_WhileTheBannerLoads_ReleasesItWhenItComes_WithoutShowingIt()
        {
            BannerAds ads = NewAds();
            ads.ShowIn(Slot);
            _network.FinishStart(true);

            _noAds.Buy();
            var banner = new ScriptedBanner();
            _network.FinishLoad(banner);

            Assert.AreEqual(1, banner.Destroys);
            Assert.AreEqual(0, banner.ShowCount);
            Assert.IsFalse(ads.IsShowing);
            Assert.AreEqual(0, _waits.Count);
        }

        [Test]
        public void ARefund_LoadsABannerAgain_AndShowsItWhereTheTableWantsIt()
        {
            _noAds.Owned = true;
            BannerAds ads = NewAds();
            ads.ShowIn(Slot);

            _noAds.Refund();

            Assert.AreEqual(1, _network.Starts);
            _network.FinishStart(true);
            var banner = new ScriptedBanner();
            _network.FinishLoad(banner);
            Assert.IsTrue(ads.IsShowing);
            Assert.IsTrue(banner.OnScreen);
        }

        [Test]
        public void BuyingThenARefund_WhileTheBannerLoads_KeepsThatBanner_AndLoadsNoSecond()
        {
            BannerAds ads = NewAds();
            ads.ShowIn(Slot);
            _network.FinishStart(true);

            _noAds.Buy();
            _noAds.Refund();
            var banner = new ScriptedBanner();
            _network.FinishLoad(banner);

            Assert.AreEqual(0, banner.Destroys, "wanted again by the time it came");
            Assert.IsTrue(ads.IsShowing);
            Assert.AreEqual(1, _network.Loads, "one load at a time");
        }

        [Test]
        public void ANoAdsThatCannotBeAsked_GetsNothingRequested()
        {
            _noAds.ThrowOnOwned = true;
            BannerAds ads = NewAds();

            Assert.DoesNotThrow(() => ads.ShowIn(Slot));
            Assert.IsFalse(ads.IsShowing);
            Assert.AreEqual(0, _network.Starts, "a paying player must never see one");
            StringAssert.Contains("No Ads", string.Join("\n", _log));
        }

        [Test]
        public void ANetworkThatThrows_NeverThrowsIntoTheGame()
        {
            _network.ThrowOnStart = true;
            BannerAds ads = null;
            Assert.DoesNotThrow(() => ads = NewAds());
            Assert.DoesNotThrow(() => ads.ShowIn(Slot));
            Assert.IsFalse(ads.IsShowing);

            _network.ThrowOnStart = false;
            _network.ThrowOnLoad = true;
            BannerAds second = null;
            Assert.DoesNotThrow(() => second = NewAds());
            Assert.DoesNotThrow(() => _network.FinishStart(true));
            Assert.IsFalse(second.IsShowing);
        }

        [Test]
        public void ABannerThatThrows_NeverThrowsIntoTheGame()
        {
            BannerAds ads = ReadyAds(out ScriptedBanner banner);
            banner.ThrowOnShow = true;

            Assert.DoesNotThrow(() => ads.ShowIn(Slot));
            Assert.IsFalse(ads.IsShowing, "a banner that could not be placed is not up");

            banner.ThrowOnShow = false;
            banner.ThrowOnSize = true;
            Assert.DoesNotThrow(() => ads.ShowIn(new BannerSlot(0, 0, 1000, 150)));
            Assert.IsFalse(ads.IsShowing);

            banner.ThrowOnSize = false;
            ads.ShowIn(Slot);
            Assert.IsTrue(ads.IsShowing);
            banner.ThrowOnHide = true;
            banner.ThrowOnDestroy = true;
            Assert.DoesNotThrow(() => ads.Hide());
            Assert.DoesNotThrow(() => ads.Dispose());
        }

        [Test]
        public void Dispose_ReleasesTheBanner_AndStopsTheTries()
        {
            BannerAds ads = ReadyAds(out ScriptedBanner banner);
            ads.ShowIn(Slot);

            ads.Dispose();

            Assert.IsFalse(banner.OnScreen);
            Assert.AreEqual(1, banner.Destroys);
            Assert.IsFalse(ads.IsShowing);
            ads.ShowIn(Slot);
            Assert.IsFalse(banner.OnScreen, "a disposed banner shows nothing");
            Assert.DoesNotThrow(ads.Dispose);
            Assert.AreEqual(1, banner.Destroys);
        }

        [Test]
        public void Dispose_WhileWaitingToTryAgain_EndsTheWait()
        {
            BannerAds ads = NewAds();
            _network.FinishStart(true);
            _network.FinishLoad(null);

            ads.Dispose();

            Assert.IsTrue(LastWait().Cancelled);
            Assert.AreEqual(1, _network.Loads);
        }

        [Test]
        public void ABannerThatArrivesAfterDispose_IsReleased()
        {
            BannerAds ads = NewAds();
            ads.ShowIn(Slot);
            _network.FinishStart(true);

            ads.Dispose();
            var banner = new ScriptedBanner();
            _network.FinishLoad(banner);

            Assert.AreEqual(1, banner.Destroys);
            Assert.AreEqual(0, banner.ShowCount);
        }

        [Test]
        public void Dispose_StopsListeningToNoAds()
        {
            BannerAds ads = NewAds();
            Assert.AreEqual(1, _noAds.Listeners);

            ads.Dispose();
            _noAds.Buy();
            _noAds.Refund();

            Assert.AreEqual(0, _noAds.Listeners);
            Assert.AreEqual(1, _network.Starts, "nothing starts again");
        }

        [Test]
        public void Constructor_RefusesMissingParts()
        {
            Assert.Throws<ArgumentNullException>(() => new BannerAds(null, _noAds));
            Assert.Throws<ArgumentNullException>(() => new BannerAds(_network, null));
            Assert.AreEqual(0, _noAds.Listeners, "a refused one listens to nothing");
            Assert.AreEqual(0, _network.Starts);
        }

        [Test]
        public void TheStandIn_NeverHasABanner()
        {
            var standIn = new StandInBannerAds();

            standIn.ShowIn(Slot);
            Assert.IsFalse(standIn.IsShowing);
            Assert.DoesNotThrow(standIn.Hide);
        }

        [Test]
        public void Slots_AreEqual_ByTheirRectangle()
        {
            Assert.AreEqual(new BannerSlot(1, 2, 3, 4), new BannerSlot(1, 2, 3, 4));
            Assert.AreEqual(new BannerSlot(1, 2, 3, 4).GetHashCode(), new BannerSlot(1, 2, 3, 4).GetHashCode());
            Assert.AreNotEqual(new BannerSlot(1, 2, 3, 4), new BannerSlot(1, 2, 3, 5));
            Assert.AreNotEqual(new BannerSlot(1, 2, 3, 4), new BannerSlot(0, 2, 3, 4));
        }

        private BannerAds NewAds() => new BannerAds(_network, _noAds, _log.Add, Delay);

        private BannerAds ReadyAds(out ScriptedBanner banner)
        {
            BannerAds ads = NewAds();
            _network.FinishStart(true);
            banner = new ScriptedBanner();
            _network.FinishLoad(banner);
            Assert.IsFalse(ads.IsShowing);
            return ads;
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

        private sealed class ScriptedNetwork : IBannerAdNetwork
        {
            private TaskCompletionSource<bool> _start;
            private TaskCompletionSource<IBannerAd> _load;

            public int Starts { get; private set; }

            public int Loads { get; private set; }

            public bool ThrowOnStart { get; set; }

            public bool ThrowOnLoad { get; set; }

            public Task<bool> StartAsync()
            {
                Starts++;
                if (ThrowOnStart)
                {
                    throw new InvalidOperationException("a broken SDK");
                }

                _start = new TaskCompletionSource<bool>();
                return _start.Task;
            }

            public Task<IBannerAd> LoadAsync()
            {
                Loads++;
                if (ThrowOnLoad)
                {
                    throw new InvalidOperationException("a broken SDK");
                }

                _load = new TaskCompletionSource<IBannerAd>();
                return _load.Task;
            }

            public void FinishStart(bool ready) => _start.SetResult(ready);

            public void FinishLoad(IBannerAd banner) => _load.SetResult(banner);
        }

        private sealed class ScriptedBanner : IBannerAd
        {
            public int Width => ThrowOnSize ? throw new InvalidOperationException("a broken SDK") : BannerWidth;

            public int Height => ThrowOnSize ? throw new InvalidOperationException("a broken SDK") : BannerHeight;

            public bool OnScreen { get; private set; }

            public int X { get; private set; }

            public int Y { get; private set; }

            public int ShowCount { get; private set; }

            public int Destroys { get; private set; }

            public bool ThrowOnShow { get; set; }

            public bool ThrowOnSize { get; set; }

            public bool ThrowOnHide { get; set; }

            public bool ThrowOnDestroy { get; set; }

            public void ShowAt(int x, int y)
            {
                if (ThrowOnShow)
                {
                    throw new InvalidOperationException("a broken SDK");
                }

                ShowCount++;
                OnScreen = true;
                X = x;
                Y = y;
            }

            public void Hide()
            {
                if (ThrowOnHide)
                {
                    throw new InvalidOperationException("a broken SDK");
                }

                OnScreen = false;
            }

            public void Destroy()
            {
                Destroys++;
                OnScreen = false;
                if (ThrowOnDestroy)
                {
                    throw new InvalidOperationException("a broken SDK");
                }
            }
        }

        /// <summary>No Ads as the store keeps it: bought or refunded by the test, which says so through Changed.</summary>
        private sealed class ScriptedNoAds : INoAds
        {
            private Action _changed;
            private bool _owned;

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

            public bool ThrowOnOwned { get; set; }

            public bool Owned
            {
                get => ThrowOnOwned ? throw new InvalidOperationException("the store is broken") : _owned;
                set => _owned = value;
            }

            public bool CanBuy => !_owned;

            public string Price => "$0.99";

            public Task<NoAdsPurchase> BuyAsync(CancellationToken ct) => Task.FromResult(NoAdsPurchase.Unavailable);

            public void Buy()
            {
                _owned = true;
                _changed?.Invoke();
            }

            public void Refund()
            {
                _owned = false;
                _changed?.Invoke();
            }
        }
    }
}
