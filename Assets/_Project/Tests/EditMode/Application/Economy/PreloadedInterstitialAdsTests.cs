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
    /// The ads between matches over a real network (AdMob on phones), kept as the Earn Coins ads are:
    /// the network starts at once (consent, then the SDK) and one ad is kept loaded; a failed start or
    /// load is tried again after 5 s, 10 s, 20 s ... at most two minutes; an expired ad is replaced;
    /// a show is over once the ad has gone away, true when it was shown; one shows at a time; nothing
    /// the network does makes it throw. A player who owns No Ads gets nothing requested: buying it
    /// lets the loaded ad go and stops loading, and a refund starts loading again.
    /// </summary>
    public sealed class PreloadedInterstitialAdsTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc);

        private ScriptedNetwork _network;
        private ScriptedNoAds _noAds;
        private List<Wait> _waits;
        private List<string> _log;
        private DateTime _now;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            // The network's callbacks resume the ads on the test's own thread, at once, in the Editor
            // as in the dotnet harness.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _network = new ScriptedNetwork();
            _noAds = new ScriptedNoAds();
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
        public void TheNetwork_StartsAtOnce_ThenOneAdLoads()
        {
            PreloadedInterstitialAds ads = NewAds();

            Assert.AreEqual(1, _network.Starts, "consent and the SDK start with the ads, before any match ends");
            Assert.AreEqual(0, _network.Loads, "no ad is requested before the network may request ads");
            Assert.IsFalse(ads.IsAvailable);

            _network.FinishStart(true);
            Assert.AreEqual(1, _network.Loads);
            Assert.IsFalse(ads.IsAvailable, "the ad is still loading");

            _network.FinishLoad(new ScriptedAd());
            Assert.IsTrue(ads.IsAvailable);
            Assert.AreEqual(1, _network.Starts);
            Assert.AreEqual(1, _network.Loads, "one ad is kept, not more");
        }

        [Test]
        public void AStartThatMayNotRequestAds_IsTriedAgain_AfterLongerWaits()
        {
            PreloadedInterstitialAds ads = NewAds();

            _network.FinishStart(false);
            Assert.AreEqual(0, _network.Loads);
            Assert.AreEqual(TimeSpan.FromSeconds(5), LastWait().Length);

            LastWait().Finish();
            Assert.AreEqual(2, _network.Starts);
            _network.FinishStart(false);
            Assert.AreEqual(TimeSpan.FromSeconds(10), LastWait().Length);

            LastWait().Finish();
            _network.FinishStart(true);
            Assert.AreEqual(3, _network.Starts);
            Assert.AreEqual(1, _network.Loads);
            _network.FinishLoad(new ScriptedAd());
            Assert.IsTrue(ads.IsAvailable);
        }

        [Test]
        public void ALoadWithoutAnAd_IsTriedAgain_WithoutStartingTheNetworkAgain()
        {
            PreloadedInterstitialAds ads = NewAds();
            _network.FinishStart(true);

            _network.FinishLoad(null);
            Assert.IsFalse(ads.IsAvailable);
            Assert.AreEqual(TimeSpan.FromSeconds(5), LastWait().Length);

            LastWait().Finish();
            _network.FinishLoad(null);
            Assert.AreEqual(TimeSpan.FromSeconds(10), LastWait().Length);

            LastWait().Finish();
            _network.FinishLoad(new ScriptedAd());
            Assert.IsTrue(ads.IsAvailable);
            Assert.AreEqual(1, _network.Starts, "the network starts once");
            Assert.AreEqual(3, _network.Loads);
        }

        [Test]
        public void IsAvailable_WhileWaitingToRetry_DoesNotHurryTheNextTry()
        {
            PreloadedInterstitialAds ads = NewAds();
            _network.FinishStart(true);
            _network.FinishLoad(null);

            Assert.IsFalse(ads.IsAvailable);
            Assert.IsFalse(ads.IsAvailable);

            Assert.AreEqual(1, _network.Loads);
            Assert.AreEqual(1, _waits.Count);
        }

        [Test]
        public void TheRetryWait_Doubles_UpToTwoMinutes()
        {
            CollectionAssert.AreEqual(
                new[] { 5d, 10d, 20d, 40d, 80d, 120d, 120d },
                new[] { 1, 2, 3, 4, 5, 6, 50 }.Select(n => PreloadedInterstitialAds.RetryDelay(n).TotalSeconds));
        }

        [Test]
        public void AShownAd_IsTrue_OnceClosed_AndTheNextAdLoads()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);

            Task<bool> show = ads.ShowAsync(CancellationToken.None);
            Assert.AreEqual(1, ad.Shows);
            Assert.IsFalse(show.IsCompleted, "the ad is on screen");
            Assert.IsFalse(ads.IsAvailable, "the shown ad is not offered again");

            ad.Closed();

            Assert.IsTrue(Over(show));
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads, "the next ad loads after the show");
            _network.FinishLoad(new ScriptedAd());
            Assert.IsTrue(ads.IsAvailable);
        }

        [Test]
        public void AnAdThatCannotBeShown_IsFalse_AndTheNextAdLoads()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);
            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            ad.Failed("the app was in the background");

            Assert.IsFalse(Over(show));
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads);
            StringAssert.Contains("the app was in the background", string.Join("\n", _log));
        }

        [Test]
        public void AnAdThatReportsTwice_EndsItsShowOnce()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);
            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            ad.Closed();
            Assert.DoesNotThrow(() => ad.Failed("reported after the close"));
            Assert.DoesNotThrow(() => ad.Closed());

            Assert.IsTrue(Over(show));
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads);
        }

        [Test]
        public void AnAdThatThrowsWhenShown_IsFalse()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);
            ad.ThrowOnShow = true;

            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            Assert.IsTrue(show.IsCompleted);
            Assert.IsFalse(Over(show));
            Assert.AreEqual(2, _network.Loads);
        }

        [Test]
        public void ShowingWithoutAnAd_IsFalse()
        {
            PreloadedInterstitialAds ads = NewAds();

            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            Assert.IsTrue(show.IsCompleted);
            Assert.IsFalse(Over(show));
        }

        [Test]
        public void ASecondShow_WhileAnAdIsUp_IsFalse()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);
            Task<bool> first = ads.ShowAsync(CancellationToken.None);

            Task<bool> second = ads.ShowAsync(CancellationToken.None);

            Assert.IsTrue(second.IsCompleted);
            Assert.IsFalse(Over(second));
            Assert.AreEqual(1, ad.Shows);
            Assert.AreEqual(1, _network.Loads, "the next ad loads only once this one has gone");
            ad.Closed();
            Assert.IsTrue(Over(first));
        }

        [Test]
        public void ACancelledWait_EndsInOperationCanceled_AndTheNextAdLoads()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);
            var cancel = new CancellationTokenSource();
            Task<bool> show = ads.ShowAsync(cancel.Token);

            cancel.Cancel();

            Assert.IsTrue(show.IsCanceled);
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads);
            Assert.IsTrue(ads.ShowAsync(cancel.Token).IsCanceled, "an already cancelled token");
        }

        [Test]
        public void AnAdTheNetworkCanNoLongerShow_IsReplaced()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);

            ad.CanShow = false;

            Assert.IsFalse(ads.IsAvailable);
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads);
            _network.FinishLoad(new ScriptedAd());
            Assert.IsTrue(ads.IsAvailable);
        }

        [Test]
        public void AnAdLoadedAlmostAnHourAgo_IsReplaced()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);

            _now = Start + PreloadedInterstitialAds.MaxAdAge - TimeSpan.FromSeconds(1);
            Assert.IsTrue(ads.IsAvailable);

            _now = Start + PreloadedInterstitialAds.MaxAdAge;
            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            Assert.IsFalse(Over(show), "an expired ad is not shown");
            Assert.AreEqual(0, ad.Shows);
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads);
            Assert.AreEqual(TimeSpan.FromMinutes(55), PreloadedInterstitialAds.MaxAdAge);
        }

        [Test]
        public void ANetworkThatThrows_NeverThrowsIntoTheGame()
        {
            _network.ThrowOnStart = true;
            PreloadedInterstitialAds ads = NewAds();

            Assert.IsFalse(ads.IsAvailable, "the throw stopped loading; asking starts it again");
            Assert.AreEqual(2, _network.Starts);

            _network.ThrowOnStart = false;
            _network.ThrowOnLoad = true;
            Assert.IsFalse(ads.IsAvailable);
            _network.FinishStart(true);
            Assert.DoesNotThrow(() => _ = ads.IsAvailable);
            Assert.IsFalse(Over(ads.ShowAsync(CancellationToken.None)));
            Assert.AreEqual(3, _network.Loads, "each ask tried again, none threw");

            _network.ThrowOnLoad = false;
            Assert.IsFalse(ads.IsAvailable);
            var ad = new ScriptedAd { ThrowOnDestroy = true, ThrowOnCanShow = true };
            _network.FinishLoad(ad);
            Assert.IsFalse(ads.IsAvailable, "an ad that can't be checked counts as expired");
            Assert.AreEqual(1, ad.Destroys);
        }

        [Test]
        public void Dispose_StopsTheRetries_AndReleasesTheAd()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);

            ads.Dispose();

            Assert.AreEqual(1, ad.Destroys);
            Assert.IsFalse(ads.IsAvailable);
            Assert.IsFalse(Over(ads.ShowAsync(CancellationToken.None)));
            Assert.AreEqual(1, _network.Loads, "nothing loads after it is disposed");

            PreloadedInterstitialAds retrying = NewAds();
            _network.FinishStart(false);
            Wait wait = LastWait();
            retrying.Dispose();
            Assert.IsTrue(wait.Cancelled);
            Assert.AreEqual(2, _network.Starts, "no further try after it is disposed");
        }

        [Test]
        public void AnAdThatArrivesAfterDispose_IsReleased()
        {
            PreloadedInterstitialAds ads = NewAds();
            _network.FinishStart(true);

            ads.Dispose();
            var late = new ScriptedAd();
            _network.FinishLoad(late);

            Assert.AreEqual(1, late.Destroys);
            Assert.IsFalse(ads.IsAvailable);
        }

        [Test]
        public void APlayerWhoOwnsNoAds_GetsNothingRequested()
        {
            _noAds.Owned = true;
            PreloadedInterstitialAds ads = NewAds();

            Assert.IsFalse(ads.IsAvailable);
            Assert.IsFalse(Over(ads.ShowAsync(CancellationToken.None)));

            Assert.AreEqual(0, _network.Starts, "not even consent and the SDK");
            Assert.AreEqual(0, _network.Loads);
            Assert.AreEqual(0, _waits.Count, "nothing is waiting to try again");
        }

        [Test]
        public void BuyingNoAds_LetsTheLoadedAdGo_AndRequestsNoMore()
        {
            PreloadedInterstitialAds ads = ReadyAds(out ScriptedAd ad);

            _noAds.Buy();

            Assert.AreEqual(1, ad.Destroys, "the loaded ad is let go at once");
            Assert.IsFalse(ads.IsAvailable);
            Assert.IsFalse(Over(ads.ShowAsync(CancellationToken.None)));
            Assert.AreEqual(0, ad.Shows);
            Assert.AreEqual(1, _network.Loads, "no other ad is requested");
        }

        [Test]
        public void BuyingNoAds_WhileWaitingToTryAgain_StopsTheTries()
        {
            PreloadedInterstitialAds ads = NewAds();
            _network.FinishStart(true);
            _network.FinishLoad(null);
            Wait wait = LastWait();

            _noAds.Buy();

            Assert.IsTrue(wait.Cancelled, "the wait is over");
            Assert.IsFalse(ads.IsAvailable);
            Assert.AreEqual(1, _network.Loads);
            Assert.AreEqual(1, _waits.Count, "and no other try is planned");
        }

        [Test]
        public void BuyingNoAds_WhileTheNetworkStarts_RequestsNoAd()
        {
            PreloadedInterstitialAds ads = NewAds();

            _noAds.Buy();
            _network.FinishStart(true);

            Assert.AreEqual(0, _network.Loads);
            Assert.IsFalse(ads.IsAvailable);
            Assert.AreEqual(0, _waits.Count);
        }

        [Test]
        public void BuyingNoAds_WhileAnAdLoads_LetsThatAdGoWhenItComes()
        {
            PreloadedInterstitialAds ads = NewAds();
            _network.FinishStart(true);

            _noAds.Buy();
            var ad = new ScriptedAd();
            _network.FinishLoad(ad);

            Assert.AreEqual(1, ad.Destroys);
            Assert.IsFalse(ads.IsAvailable);
            Assert.AreEqual(1, _network.Loads);
            Assert.AreEqual(0, _waits.Count);
        }

        [Test]
        public void ARefund_StartsLoadingAgain()
        {
            _noAds.Owned = true;
            PreloadedInterstitialAds ads = NewAds();

            _noAds.Refund();

            Assert.AreEqual(1, _network.Starts);
            _network.FinishStart(true);
            Assert.AreEqual(1, _network.Loads);
            _network.FinishLoad(new ScriptedAd());
            Assert.IsTrue(ads.IsAvailable);
        }

        [Test]
        public void BuyingThenARefund_WhileAnAdLoads_KeepsThatAd_AndLoadsNoSecond()
        {
            PreloadedInterstitialAds ads = NewAds();
            _network.FinishStart(true);

            _noAds.Buy();
            _noAds.Refund();
            var ad = new ScriptedAd();
            _network.FinishLoad(ad);

            Assert.AreEqual(0, ad.Destroys, "wanted again by the time it came");
            Assert.IsTrue(ads.IsAvailable);
            Assert.AreEqual(1, _network.Loads, "one load at a time");
        }

        [Test]
        public void ANoAdsThatCannotBeAsked_GetsNothingRequested()
        {
            _noAds.ThrowOnOwned = true;
            PreloadedInterstitialAds ads = NewAds();

            Assert.DoesNotThrow(() => _ = ads.IsAvailable);
            Assert.IsFalse(ads.IsAvailable);
            Assert.AreEqual(0, _network.Starts, "a paying player must never see one");
            StringAssert.Contains("No Ads", string.Join("\n", _log));
        }

        [Test]
        public void Dispose_StopsListeningToNoAds()
        {
            PreloadedInterstitialAds ads = NewAds();
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
            Assert.Throws<ArgumentNullException>(() => new PreloadedInterstitialAds(null, _noAds, () => _now));
            Assert.Throws<ArgumentNullException>(() => new PreloadedInterstitialAds(_network, null, () => _now));
            Assert.Throws<ArgumentNullException>(() => new PreloadedInterstitialAds(_network, _noAds, null));
            Assert.AreEqual(0, _noAds.Listeners, "a refused one listens to nothing");
        }

        /// <summary>The result of a show that must be over by now (never waits for one that isn't).</summary>
        private static bool Over(Task<bool> show)
        {
            Assert.IsTrue(show.IsCompleted, "the show is over");
            return show.Result;
        }

        private PreloadedInterstitialAds NewAds() => new PreloadedInterstitialAds(_network, _noAds, () => _now, _log.Add, Delay);

        private PreloadedInterstitialAds ReadyAds(out ScriptedAd ad)
        {
            PreloadedInterstitialAds ads = NewAds();
            _network.FinishStart(true);
            ad = new ScriptedAd();
            _network.FinishLoad(ad);
            Assert.IsTrue(ads.IsAvailable);
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

        private sealed class ScriptedNetwork : IInterstitialAdNetwork
        {
            private TaskCompletionSource<bool> _start;
            private TaskCompletionSource<IInterstitialAd> _load;

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

            public Task<IInterstitialAd> LoadAsync()
            {
                Loads++;
                if (ThrowOnLoad)
                {
                    throw new InvalidOperationException("a broken SDK");
                }

                _load = new TaskCompletionSource<IInterstitialAd>();
                return _load.Task;
            }

            public void FinishStart(bool ready) => _start.SetResult(ready);

            public void FinishLoad(IInterstitialAd ad) => _load.SetResult(ad);
        }

        private sealed class ScriptedAd : IInterstitialAd
        {
            private Action _closed;
            private Action<string> _failed;
            private bool _canShow = true;

            public bool CanShow
            {
                get => ThrowOnCanShow ? throw new InvalidOperationException("a broken SDK") : _canShow;
                set => _canShow = value;
            }

            public int Shows { get; private set; }

            public int Destroys { get; private set; }

            public bool ThrowOnShow { get; set; }

            public bool ThrowOnCanShow { get; set; }

            public bool ThrowOnDestroy { get; set; }

            public void Show(Action closed, Action<string> failed)
            {
                Shows++;
                if (ThrowOnShow)
                {
                    throw new InvalidOperationException("a broken SDK");
                }

                _closed = closed;
                _failed = failed;
            }

            public void Destroy()
            {
                Destroys++;
                if (ThrowOnDestroy)
                {
                    throw new InvalidOperationException("a broken SDK");
                }
            }

            public void Closed() => _closed();

            public void Failed(string reason) => _failed(reason);
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

            public bool Owned
            {
                get => ThrowOnOwned ? throw new InvalidOperationException("a broken store") : _owned;
                set => _owned = value;
            }

            public bool ThrowOnOwned { get; set; }

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
