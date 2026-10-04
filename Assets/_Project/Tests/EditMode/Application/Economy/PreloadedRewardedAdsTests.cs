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
    /// The Earn Coins ads over a real network (AdMob on phones): the network starts at once (consent,
    /// then the SDK) and one ad is kept loaded; a failed start or load is tried again after 5 s,
    /// 10 s, 20 s ... at most two minutes; an expired ad is replaced; a show is true only when the ad
    /// paid its reward, even when the network reports the reward just after the close; nothing the
    /// network does makes it throw.
    /// </summary>
    public sealed class PreloadedRewardedAdsTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);

        private ScriptedNetwork _network;
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
            PreloadedRewardedAds ads = NewAds();

            Assert.AreEqual(1, _network.Starts, "consent and the SDK start with the ads, before any tap");
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
            PreloadedRewardedAds ads = NewAds();

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
            PreloadedRewardedAds ads = NewAds();
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
            PreloadedRewardedAds ads = NewAds();
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
                new[] { 1, 2, 3, 4, 5, 6, 50 }.Select(n => PreloadedRewardedAds.RetryDelay(n).TotalSeconds));
        }

        [Test]
        public void AnAdWatchedToItsReward_IsTrue_AndTheNextAdLoads()
        {
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);

            Task<bool> show = ads.ShowAsync(CancellationToken.None);
            Assert.AreEqual(1, ad.Shows);
            Assert.IsFalse(show.IsCompleted, "the ad is on screen");
            Assert.IsFalse(ads.IsAvailable, "the shown ad is not offered again");

            ad.Rewarded();
            Assert.IsFalse(show.IsCompleted, "done when the ad has gone away");
            ad.Closed();

            Assert.IsTrue(Over(show));
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads, "the next ad loads after the show");
            _network.FinishLoad(new ScriptedAd());
            Assert.IsTrue(ads.IsAvailable);
        }

        [Test]
        public void AnAdClosedEarly_IsFalse_AfterTheGraceForALateReward()
        {
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);
            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            ad.Closed();
            Assert.IsFalse(show.IsCompleted, "a reward may still come");
            Assert.AreEqual(PreloadedRewardedAds.LateRewardGrace, LastWait().Length);

            LastWait().Finish();
            Assert.IsFalse(Over(show));
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads);
        }

        [Test]
        public void ARewardReportedJustAfterTheClose_StillCounts()
        {
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);
            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            ad.Closed();
            ad.Rewarded();

            Assert.IsTrue(show.IsCompleted);
            Assert.IsTrue(Over(show));
            LastWait().Finish();
            Assert.IsTrue(Over(show), "the grace ending changes nothing");
        }

        [Test]
        public void AnAdThatCannotBeShown_IsFalse_AndTheNextAdLoads()
        {
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);
            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            ad.Failed("the app was in the background");

            Assert.IsFalse(Over(show));
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads);
            StringAssert.Contains("the app was in the background", string.Join("\n", _log));
        }

        [Test]
        public void AnAdThatThrowsWhenShown_IsFalse()
        {
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);
            ad.ThrowOnShow = true;

            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            Assert.IsTrue(show.IsCompleted);
            Assert.IsFalse(Over(show));
            Assert.AreEqual(2, _network.Loads);
        }

        [Test]
        public void ShowingWithoutAnAd_IsFalse()
        {
            PreloadedRewardedAds ads = NewAds();

            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            Assert.IsTrue(show.IsCompleted);
            Assert.IsFalse(Over(show));
        }

        [Test]
        public void ASecondShow_WhileAnAdPlays_IsFalse()
        {
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);
            Task<bool> first = ads.ShowAsync(CancellationToken.None);

            Task<bool> second = ads.ShowAsync(CancellationToken.None);

            Assert.IsTrue(second.IsCompleted);
            Assert.IsFalse(Over(second));
            Assert.AreEqual(1, ad.Shows);
            ad.Rewarded();
            ad.Closed();
            Assert.IsTrue(Over(first));
        }

        [Test]
        public void ACancelledWait_EndsInOperationCanceled_AndTheNextAdLoads()
        {
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);
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
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);

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
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);

            _now = Start + PreloadedRewardedAds.MaxAdAge - TimeSpan.FromSeconds(1);
            Assert.IsTrue(ads.IsAvailable);

            _now = Start + PreloadedRewardedAds.MaxAdAge;
            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            Assert.IsFalse(Over(show), "an expired ad is not shown");
            Assert.AreEqual(0, ad.Shows);
            Assert.AreEqual(1, ad.Destroys);
            Assert.AreEqual(2, _network.Loads);
        }

        [Test]
        public void ANetworkThatThrows_NeverThrowsIntoTheGame()
        {
            _network.ThrowOnStart = true;
            PreloadedRewardedAds ads = NewAds();

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
            PreloadedRewardedAds ads = ReadyAds(out ScriptedAd ad);

            ads.Dispose();

            Assert.AreEqual(1, ad.Destroys);
            Assert.IsFalse(ads.IsAvailable);
            Assert.IsFalse(Over(ads.ShowAsync(CancellationToken.None)));
            Assert.AreEqual(1, _network.Loads, "nothing loads after it is disposed");

            PreloadedRewardedAds retrying = NewAds();
            _network.FinishStart(false);
            Wait wait = LastWait();
            retrying.Dispose();
            Assert.IsTrue(wait.Cancelled);
            Assert.AreEqual(2, _network.Starts, "no further try after it is disposed");
        }

        /// <summary>The result of a show that must be over by now (never waits for one that isn't).</summary>
        private static bool Over(Task<bool> show)
        {
            Assert.IsTrue(show.IsCompleted, "the show is over");
            return show.Result;
        }

        private PreloadedRewardedAds NewAds() => new PreloadedRewardedAds(_network, () => _now, _log.Add, Delay);

        private PreloadedRewardedAds ReadyAds(out ScriptedAd ad)
        {
            PreloadedRewardedAds ads = NewAds();
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

        private sealed class ScriptedNetwork : IRewardedAdNetwork
        {
            private TaskCompletionSource<bool> _start;
            private TaskCompletionSource<IRewardedAd> _load;

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

            public Task<IRewardedAd> LoadAsync()
            {
                Loads++;
                if (ThrowOnLoad)
                {
                    throw new InvalidOperationException("a broken SDK");
                }

                _load = new TaskCompletionSource<IRewardedAd>();
                return _load.Task;
            }

            public void FinishStart(bool ready) => _start.SetResult(ready);

            public void FinishLoad(IRewardedAd ad) => _load.SetResult(ad);
        }

        private sealed class ScriptedAd : IRewardedAd
        {
            private Action _rewarded;
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

            public void Show(Action rewarded, Action closed, Action<string> failed)
            {
                Shows++;
                if (ThrowOnShow)
                {
                    throw new InvalidOperationException("a broken SDK");
                }

                _rewarded = rewarded;
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

            public void Rewarded() => _rewarded();

            public void Closed() => _closed();

            public void Failed(string reason) => _failed(reason);
        }
    }
}
