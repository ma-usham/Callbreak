using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// No game sound under an ad (the owner, 8 Oct 2026: "when the ad plays the game sound also
    /// plays. it shouldnt happen"). On Android the game keeps running under a full-screen ad, so
    /// while a show of the Earn Coins ads or of the ads between matches is under way the game's audio
    /// is held silent, and it comes back once the show is over, however it ends: watched, skipped,
    /// closed, failed, thrown or cancelled. A show that is over at once (no ad loaded, one already
    /// up, an ad that throws when shown, No Ads owned) silences nothing. Each show lets go of its own
    /// hold only, once, so two ads at once can't bring the sound back early. Results, faults and
    /// cancellation pass through unchanged. The player's settings are the audio service's own and
    /// are never touched here (AudioServiceSilenceTests).
    /// </summary>
    public sealed class QuietAdsTests
    {
        private ScriptedSilencer _audio;
        private ScriptedShows _shows;
        private List<string> _log;
        private List<Wait> _waits;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            // The ads' callbacks resume the shows on the test's own thread, at once, in the Editor as
            // in the dotnet harness.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _audio = new ScriptedSilencer();
            _shows = new ScriptedShows();
            _log = new List<string>();
            _waits = new List<Wait>();
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [Test]
        public void ARewardedAdOnScreen_SilencesTheGame_UntilItIsWatched()
        {
            var ads = new QuietRewardedAds(_shows, _audio, _log.Add);

            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            Assert.IsFalse(show.IsCompleted, "the ad is on screen");
            Assert.AreEqual(1, _audio.Held, "the game is silent under the ad");

            _shows.Finish(true);

            Assert.IsTrue(Over(show), "the reward passes through");
            Assert.AreEqual(0, _audio.Held, "the sound is back");
            Assert.AreEqual(1, _audio.Silences);
            Assert.AreEqual(0, _audio.DoubleReleases);
        }

        [Test]
        public void ASkippedRewardedAd_BringsTheSoundBack_AndPaysNothing()
        {
            var ads = new QuietRewardedAds(_shows, _audio, _log.Add);
            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            _shows.Finish(false);

            Assert.IsFalse(Over(show));
            Assert.AreEqual(0, _audio.Held);
        }

        [Test]
        public void AnInterstitialOnScreen_SilencesTheGame_UntilItHasGone()
        {
            var ads = new QuietInterstitialAds(_shows, _audio, _log.Add);

            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            Assert.IsFalse(show.IsCompleted);
            Assert.AreEqual(1, _audio.Held, "the game is silent under the ad");

            _shows.Finish(true);

            Assert.IsTrue(Over(show), "shown passes through");
            Assert.AreEqual(0, _audio.Held, "the sound is back");
        }

        [Test]
        public void AShowThatFaults_BringsTheSoundBack_AndFaultsAsBefore()
        {
            var rewarded = new QuietRewardedAds(_shows, _audio, _log.Add);
            Task<bool> show = rewarded.ShowAsync(CancellationToken.None);
            var broken = new InvalidOperationException("a broken ad");

            _shows.Fault(broken);

            Assert.IsTrue(show.IsFaulted);
            Assert.AreSame(broken, show.Exception.InnerException);
            Assert.AreEqual(0, _audio.Held);

            var between = new QuietInterstitialAds(_shows, _audio, _log.Add);
            show = between.ShowAsync(CancellationToken.None);
            _shows.Fault(broken);

            Assert.IsTrue(show.IsFaulted);
            Assert.AreEqual(0, _audio.Held);
            Assert.AreEqual(2, _audio.Silences);
        }

        [Test]
        public void ACancelledWait_BringsTheSoundBack_AndEndsInOperationCanceled()
        {
            var rewarded = new QuietRewardedAds(_shows, _audio, _log.Add);
            var cancel = new CancellationTokenSource();
            Task<bool> show = rewarded.ShowAsync(cancel.Token);
            Assert.AreEqual(1, _audio.Held);

            cancel.Cancel();

            Assert.IsTrue(show.IsCanceled);
            Assert.AreEqual(0, _audio.Held, "a wait that stops never leaves the game silent");

            var between = new QuietInterstitialAds(_shows, _audio, _log.Add);
            cancel = new CancellationTokenSource();
            show = between.ShowAsync(cancel.Token);
            cancel.Cancel();

            Assert.IsTrue(show.IsCanceled);
            Assert.AreEqual(0, _audio.Held);
        }

        [Test]
        public void AShowThatIsOverAtOnce_SilencesNothing()
        {
            _shows.OverAtOnce = true;
            var rewarded = new QuietRewardedAds(_shows, _audio, _log.Add);
            var between = new QuietInterstitialAds(_shows, _audio, _log.Add);

            Assert.IsFalse(Over(rewarded.ShowAsync(CancellationToken.None)));
            Assert.IsFalse(Over(between.ShowAsync(CancellationToken.None)));

            Assert.AreEqual(2, _shows.Shows, "each show was asked for");
            Assert.AreEqual(0, _audio.Silences, "no ad came up, so the game was never silenced");
        }

        [Test]
        public void AnAlreadyCancelledToken_SilencesNothing()
        {
            var cancelled = new CancellationToken(true);

            Assert.IsTrue(new QuietRewardedAds(_shows, _audio).ShowAsync(cancelled).IsCanceled);
            Assert.IsTrue(new QuietInterstitialAds(_shows, _audio).ShowAsync(cancelled).IsCanceled);

            Assert.AreEqual(0, _audio.Silences);
        }

        [Test]
        public void TwoAdsAtOnce_KeepTheGameSilent_UntilBothHaveGone()
        {
            var first = new ScriptedShows();
            var second = new ScriptedShows();
            Task<bool> rewarded = new QuietRewardedAds(first, _audio, _log.Add).ShowAsync(CancellationToken.None);
            Task<bool> between = new QuietInterstitialAds(second, _audio, _log.Add).ShowAsync(CancellationToken.None);
            Assert.AreEqual(2, _audio.Held);

            first.Finish(true);

            Assert.IsTrue(Over(rewarded));
            Assert.AreEqual(1, _audio.Held, "the other ad is still up: still silent");

            second.Finish(true);

            Assert.IsTrue(Over(between));
            Assert.AreEqual(0, _audio.Held);
            Assert.AreEqual(0, _audio.DoubleReleases, "each show let go of its own hold, once");
        }

        [Test]
        public void ASilencerThatThrows_NeverStopsTheAd()
        {
            _audio.ThrowOnSilence = true;
            var ads = new QuietRewardedAds(_shows, _audio, _log.Add);

            Task<bool> show = ads.ShowAsync(CancellationToken.None);
            _shows.Finish(true);

            Assert.IsTrue(Over(show), "the ad still shows and pays");
            StringAssert.Contains("could not be silenced", string.Join("\n", _log));
        }

        [Test]
        public void ASilenceThatThrowsWhenLetGo_StillEndsTheShow()
        {
            _audio.ThrowOnRestore = true;
            var ads = new QuietInterstitialAds(_shows, _audio, _log.Add);

            Task<bool> show = ads.ShowAsync(CancellationToken.None);
            _shows.Finish(true);

            Assert.IsTrue(Over(show));
            StringAssert.Contains("could not be brought back", string.Join("\n", _log));
        }

        [Test]
        public void IsAvailable_IsTheWrappedAds()
        {
            var rewarded = new QuietRewardedAds(_shows, _audio);
            var between = new QuietInterstitialAds(_shows, _audio);

            _shows.Available = true;
            Assert.IsTrue(rewarded.IsAvailable);
            Assert.IsTrue(between.IsAvailable);

            _shows.Available = false;
            Assert.IsFalse(rewarded.IsAvailable);
            Assert.IsFalse(between.IsAvailable);
            Assert.AreEqual(0, _audio.Silences, "asking silences nothing");
        }

        [Test]
        public void Dispose_DisposesTheWrappedAds()
        {
            new QuietRewardedAds(_shows, _audio).Dispose();
            new QuietInterstitialAds(_shows, _audio).Dispose();

            Assert.AreEqual(2, _shows.Disposes);
            Assert.DoesNotThrow(() => new QuietRewardedAds(new UndisposableShows(), _audio).Dispose());
            Assert.DoesNotThrow(() => new QuietInterstitialAds(new UndisposableShows(), _audio).Dispose());
        }

        [Test]
        public void Constructors_RefuseMissingParts()
        {
            Assert.Throws<ArgumentNullException>(() => new QuietRewardedAds(null, _audio));
            Assert.Throws<ArgumentNullException>(() => new QuietRewardedAds(_shows, null));
            Assert.Throws<ArgumentNullException>(() => new QuietInterstitialAds(null, _audio));
            Assert.Throws<ArgumentNullException>(() => new QuietInterstitialAds(_shows, null));
        }

        // Over the real policies and a scripted network: the ways an AdMob ad ends on a phone.

        [Test]
        public void OverTheNetwork_AWatchedRewardedAd_IsSilentFromShowToClose()
        {
            var network = new ScriptedNetwork();
            var ad = network.Next;
            var ads = new QuietRewardedAds(new PreloadedRewardedAds(network, () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);
            Assert.IsTrue(ads.IsAvailable);

            Task<bool> show = ads.ShowAsync(CancellationToken.None);
            Assert.AreEqual(1, ad.Shows);
            Assert.AreEqual(1, _audio.Held);

            ad.Rewarded();
            Assert.AreEqual(1, _audio.Held, "earned, but the ad is still up");
            ad.Closed();

            Assert.IsTrue(Over(show));
            Assert.AreEqual(0, _audio.Held, "the sound is back as the ad closes");
        }

        [Test]
        public void OverTheNetwork_ASkippedRewardedAd_BringsTheSoundBack_OnceItsShowIsOver()
        {
            var network = new ScriptedNetwork();
            var ad = network.Next;
            var ads = new QuietRewardedAds(new PreloadedRewardedAds(network, () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);
            Task<bool> show = ads.ShowAsync(CancellationToken.None);

            ad.Closed();
            Assert.AreEqual(1, _waits.Count, "the show waits a moment for a reward reported late");
            _waits[0].Finish();

            Assert.IsFalse(Over(show));
            Assert.AreEqual(0, _audio.Held);
        }

        [Test]
        public void OverTheNetwork_AnAdThatFailsToShow_BringsTheSoundBack()
        {
            var network = new ScriptedNetwork();
            var rewardedAd = network.Next;
            var rewarded = new QuietRewardedAds(new PreloadedRewardedAds(network, () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);
            Task<bool> show = rewarded.ShowAsync(CancellationToken.None);

            rewardedAd.Failed("the app was in the background");

            Assert.IsFalse(Over(show));
            Assert.AreEqual(0, _audio.Held);

            var betweenAd = network.Next;
            var between = new QuietInterstitialAds(new PreloadedInterstitialAds(network, new StandInNoAds(), () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);
            show = between.ShowAsync(CancellationToken.None);
            Assert.AreEqual(1, _audio.Held);

            betweenAd.Failed("the app was in the background");

            Assert.IsFalse(Over(show));
            Assert.AreEqual(0, _audio.Held);
        }

        [Test]
        public void OverTheNetwork_AClosedInterstitial_BringsTheSoundBack()
        {
            var network = new ScriptedNetwork();
            var ad = network.Next;
            var ads = new QuietInterstitialAds(new PreloadedInterstitialAds(network, new StandInNoAds(), () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);

            Task<bool> show = ads.ShowAsync(CancellationToken.None);
            Assert.AreEqual(1, ad.Shows);
            Assert.AreEqual(1, _audio.Held);

            ad.Closed();

            Assert.IsTrue(Over(show));
            Assert.AreEqual(0, _audio.Held);
        }

        [Test]
        public void OverTheNetwork_AnAdThatThrowsWhenShown_SilencesNothing()
        {
            var network = new ScriptedNetwork();
            network.Next.ThrowOnShow = true;
            var rewarded = new QuietRewardedAds(new PreloadedRewardedAds(network, () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);

            Assert.IsFalse(Over(rewarded.ShowAsync(CancellationToken.None)));

            network.Next.ThrowOnShow = true;
            var between = new QuietInterstitialAds(new PreloadedInterstitialAds(network, new StandInNoAds(), () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);

            Assert.IsFalse(Over(between.ShowAsync(CancellationToken.None)));
            Assert.AreEqual(0, _audio.Silences);
        }

        [Test]
        public void OverTheNetwork_NoAdLoaded_OrNoAdsOwned_SilencesNothing()
        {
            var network = new ScriptedNetwork { Empty = true };
            var rewarded = new QuietRewardedAds(new PreloadedRewardedAds(network, () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);
            var between = new QuietInterstitialAds(new PreloadedInterstitialAds(network, new StandInNoAds(), () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);

            Assert.IsFalse(Over(rewarded.ShowAsync(CancellationToken.None)));
            Assert.IsFalse(Over(between.ShowAsync(CancellationToken.None)));

            var owned = new StandInNoAds();
            _ = owned.BuyAsync(CancellationToken.None);
            var full = new ScriptedNetwork();
            var paid = new QuietInterstitialAds(new PreloadedInterstitialAds(full, owned, () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);

            Assert.IsFalse(Over(paid.ShowAsync(CancellationToken.None)));
            Assert.AreEqual(0, _audio.Silences);
        }

        [Test]
        public void OverTheNetwork_ACancelledWait_BringsTheSoundBack()
        {
            var network = new ScriptedNetwork();
            var rewardedAd = network.Next;
            var rewarded = new QuietRewardedAds(new PreloadedRewardedAds(network, () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);
            var cancel = new CancellationTokenSource();
            Task<bool> show = rewarded.ShowAsync(cancel.Token);
            Assert.AreEqual(1, _audio.Held);

            cancel.Cancel();

            Assert.IsTrue(show.IsCanceled);
            Assert.AreEqual(1, rewardedAd.Destroys, "the ad is let go");
            Assert.AreEqual(0, _audio.Held);

            var betweenAd = network.Next;
            var between = new QuietInterstitialAds(new PreloadedInterstitialAds(network, new StandInNoAds(), () => DateTime.UtcNow, _log.Add, Delay), _audio, _log.Add);
            cancel = new CancellationTokenSource();
            show = between.ShowAsync(cancel.Token);
            Assert.AreEqual(1, _audio.Held);

            cancel.Cancel();

            Assert.IsTrue(show.IsCanceled);
            Assert.AreEqual(1, betweenAd.Destroys);
            Assert.AreEqual(0, _audio.Held);
        }

        /// <summary>The result of a show that must be over by now (never waits for one that isn't).</summary>
        private static bool Over(Task<bool> show)
        {
            Assert.IsTrue(show.IsCompleted, "the show is over");
            return show.Result;
        }

        private Task Delay(TimeSpan length, CancellationToken ct)
        {
            var wait = new Wait();
            ct.Register(wait.Cancel);
            _waits.Add(wait);
            return wait.Task;
        }

        private sealed class Wait
        {
            private readonly TaskCompletionSource<bool> _done = new TaskCompletionSource<bool>();

            public Task Task => _done.Task;

            public void Finish() => _done.TrySetResult(true);

            public void Cancel() => _done.TrySetCanceled();
        }

        /// <summary>The game's audio: counts the holds taken and let go, and each handle let go twice.</summary>
        private sealed class ScriptedSilencer : IAudioSilencer
        {
            public int Silences { get; private set; }

            public int Releases { get; private set; }

            public int DoubleReleases { get; private set; }

            /// <summary>Holds not let go yet: the game is silent while there is one.</summary>
            public int Held => Silences - Releases;

            public bool ThrowOnSilence { get; set; }

            public bool ThrowOnRestore { get; set; }

            public IDisposable Silence()
            {
                if (ThrowOnSilence)
                {
                    throw new InvalidOperationException("a broken audio system");
                }

                Silences++;
                return new Hold(this);
            }

            private sealed class Hold : IDisposable
            {
                private readonly ScriptedSilencer _owner;
                private bool _released;

                public Hold(ScriptedSilencer owner)
                {
                    _owner = owner;
                }

                public void Dispose()
                {
                    if (_released)
                    {
                        _owner.DoubleReleases++;
                        return;
                    }

                    _released = true;
                    _owner.Releases++;
                    if (_owner.ThrowOnRestore)
                    {
                        throw new InvalidOperationException("a broken audio system");
                    }
                }
            }
        }

        /// <summary>Ads of either kind whose shows the test ends.</summary>
        private class UndisposableShows : IRewardedAds, IInterstitialAds
        {
            private TaskCompletionSource<bool> _show;

            public bool Available { get; set; }

            public bool OverAtOnce { get; set; }

            public int Shows { get; private set; }

            public bool IsAvailable => Available;

            public Task<bool> ShowAsync(CancellationToken ct)
            {
                Shows++;
                if (ct.IsCancellationRequested)
                {
                    return Task.FromCanceled<bool>(ct);
                }

                if (OverAtOnce)
                {
                    return Task.FromResult(false);
                }

                var show = new TaskCompletionSource<bool>();
                _show = show;
                ct.Register(() => show.TrySetCanceled());
                return show.Task;
            }

            public void Finish(bool result) => _show.SetResult(result);

            public void Fault(Exception e) => _show.SetException(e);
        }

        private sealed class ScriptedShows : UndisposableShows, IDisposable
        {
            public int Disposes { get; private set; }

            public void Dispose() => Disposes++;
        }

        /// <summary>A network of either kind that is ready at once and hands out <see cref="Next"/>, or no ad while <see cref="Empty"/>.</summary>
        private sealed class ScriptedNetwork : IRewardedAdNetwork, IInterstitialAdNetwork
        {
            public ScriptedNetwork()
            {
                Next = new ScriptedAd();
            }

            /// <summary>The ad the next load brings; a new one is made ready behind it.</summary>
            public ScriptedAd Next { get; private set; }

            public bool Empty { get; set; }

            public Task<bool> StartAsync() => Task.FromResult(true);

            Task<IRewardedAd> IRewardedAdNetwork.LoadAsync() => Task.FromResult<IRewardedAd>(Take());

            Task<IInterstitialAd> IInterstitialAdNetwork.LoadAsync() => Task.FromResult<IInterstitialAd>(Take());

            private ScriptedAd Take()
            {
                if (Empty)
                {
                    return null;
                }

                ScriptedAd ad = Next;
                Next = new ScriptedAd();
                return ad;
            }
        }

        private sealed class ScriptedAd : IRewardedAd, IInterstitialAd
        {
            private Action _rewarded;
            private Action _closed;
            private Action<string> _failed;

            public bool CanShow => Shows == 0;

            public int Shows { get; private set; }

            public int Destroys { get; private set; }

            public bool ThrowOnShow { get; set; }

            public void Show(Action rewarded, Action closed, Action<string> failed)
            {
                Show(closed, failed);
                _rewarded = rewarded;
            }

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

            public void Destroy() => Destroys++;

            public void Rewarded() => _rewarded();

            public void Closed() => _closed();

            public void Failed(string reason) => _failed(reason);
        }
    }
}
