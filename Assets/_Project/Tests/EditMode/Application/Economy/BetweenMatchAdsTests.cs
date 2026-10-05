using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Profile;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// When an ad shows between matches (the owner, 5 Oct 2026): as the player leaves a finished
    /// match's results for a new offline match, a Quick or Stake Match search or the main menu, and
    /// never on the way back to a private room's lobby; never after the player's very first match,
    /// never for a player who owns No Ads, at most one every three minutes on the wall clock (a clock
    /// set back starts them again), and never waiting for an ad that isn't loaded. The player goes
    /// on once the ad has gone away. Nothing it is given makes it throw.
    /// </summary>
    public sealed class BetweenMatchAdsTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 5, 18, 0, 0, DateTimeKind.Utc);

        private StatsService _stats;
        private ScriptedAds _ads;
        private ScriptedNoAds _noAds;
        private List<string> _log;
        private DateTime _now;
        private int _matches;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            // An ad the test closes resumes the rule on the test's own thread, at once, in the Editor
            // as in the dotnet harness.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _stats = new StatsService(new InMemorySaveStore());
            _ads = new ScriptedAds();
            _noAds = new ScriptedNoAds();
            _log = new List<string>();
            _now = Start;
            _matches = 0;
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [TestCase(AfterMatch.NewMatch)]
        [TestCase(AfterMatch.Search)]
        [TestCase(AfterMatch.MainMenu)]
        public void LeavingAFinishedMatch_ShowsAnAd_AndThePlayerGoesOnOnceItHasGoneAway(AfterMatch next)
        {
            BetweenMatchAds ads = NewAds(played: 2);

            Task<bool> leaving = ads.ShowIfDueAsync(next, CancellationToken.None);

            Assert.AreEqual(1, _ads.Shows, "the ad shows before " + next);
            Assert.IsFalse(leaving.IsCompleted, "the player goes on once the ad has gone away");
            _ads.Close();
            Assert.IsTrue(Over(leaving), "an ad was shown");
        }

        [Test]
        public void BackToAPrivateLobby_ShowsNoAd_TheMainMenuDoes()
        {
            BetweenMatchAds ads = NewAds(played: 5);

            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.PrivateLobby, CancellationToken.None)), "the friends wait in the lobby");
            Assert.AreEqual(0, _ads.Shows);

            Assert.IsTrue(ShowsAd(ads, AfterMatch.MainMenu), "leaving the room for the main menu does");
        }

        [Test]
        public void ThePlayersVeryFirstMatch_ShowsNoAd_TheSecondDoes()
        {
            BetweenMatchAds ads = NewAds(played: 0);
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)), "no match counted yet");

            Play();
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.NewMatch, CancellationToken.None)), "after the first match ever");
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)));
            Assert.AreEqual(0, _ads.Shows);

            Play();
            Assert.IsTrue(ShowsAd(ads, AfterMatch.NewMatch), "after the second");
        }

        [Test]
        public void AFirstMatchLeftEarly_IsTheFirstMatch_SoTheNextFinishedOneShowsAnAd()
        {
            BetweenMatchAds ads = NewAds(played: 0);
            _stats.RecordMatch("left-early", 4, 0, abandoned: true);

            Play();

            Assert.IsTrue(ShowsAd(ads, AfterMatch.MainMenu), "the statistics count every match played, finished or left");
        }

        [Test]
        public void APlayerWhoOwnsNoAds_NeverSeesOne()
        {
            BetweenMatchAds ads = NewAds(played: 9);
            _noAds.Owned = true;

            foreach (AfterMatch next in new[] { AfterMatch.NewMatch, AfterMatch.Search, AfterMatch.MainMenu })
            {
                Assert.IsFalse(Over(ads.ShowIfDueAsync(next, CancellationToken.None)), next.ToString());
                _now += TimeSpan.FromHours(1);
            }

            Assert.AreEqual(0, _ads.Shows);
        }

        [Test]
        public void NoAdsBoughtDuringTheSession_EndsTheAdsAtOnce()
        {
            BetweenMatchAds ads = NewAds(played: 3);
            Assert.IsTrue(ShowsAd(ads, AfterMatch.MainMenu));

            _now += TimeSpan.FromMinutes(10);
            _noAds.Owned = true;

            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)));
            Assert.AreEqual(1, _ads.Shows);
        }

        [Test]
        public void AtMostOneAd_EveryThreeMinutes_FromWhenTheLastCameUp()
        {
            BetweenMatchAds ads = NewAds(played: 3);
            Task<bool> first = ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None);
            _now += TimeSpan.FromSeconds(30);
            _ads.Close();
            Assert.IsTrue(Over(first));

            _now = Start + BetweenMatchAds.MinInterval - TimeSpan.FromSeconds(1);
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.NewMatch, CancellationToken.None)), "2 min 59 s after the last ad came up");
            Assert.AreEqual(1, _ads.Shows);

            _now = Start + BetweenMatchAds.MinInterval;
            Assert.IsTrue(ShowsAd(ads, AfterMatch.NewMatch), "3 minutes after it");
            Assert.AreEqual(TimeSpan.FromMinutes(3), BetweenMatchAds.MinInterval);
        }

        [Test]
        public void NoAdLoaded_ThePlayerGoesOnAtOnce_AndTheThreeMinutesDoNotStart()
        {
            BetweenMatchAds ads = NewAds(played: 3);
            _ads.IsAvailable = false;

            Task<bool> leaving = ads.ShowIfDueAsync(AfterMatch.Search, CancellationToken.None);

            Assert.IsTrue(leaving.IsCompleted, "nothing waits for an ad");
            Assert.IsFalse(leaving.Result);
            Assert.AreEqual(0, _ads.Shows);

            _ads.IsAvailable = true;
            _now += TimeSpan.FromMinutes(1);
            Assert.IsTrue(ShowsAd(ads, AfterMatch.Search), "the next exit may show one");
        }

        [Test]
        public void AnAdThatCouldNotBeShown_DoesNotStartTheThreeMinutes()
        {
            BetweenMatchAds ads = NewAds(played: 3);
            Task<bool> leaving = ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None);

            _ads.Fail();

            Assert.IsFalse(Over(leaving));
            _now += TimeSpan.FromMinutes(1);
            Assert.IsTrue(ShowsAd(ads, AfterMatch.MainMenu));
        }

        [Test]
        public void ASecondExit_WhileTheAdIsUp_ShowsNoOther()
        {
            BetweenMatchAds ads = NewAds(played: 3);
            Task<bool> first = ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None);

            Task<bool> second = ads.ShowIfDueAsync(AfterMatch.Search, CancellationToken.None);

            Assert.IsTrue(second.IsCompleted);
            Assert.IsFalse(second.Result);
            Assert.AreEqual(1, _ads.Shows);
            _ads.Close();
            Assert.IsTrue(Over(first));
        }

        [Test]
        public void AClockSetBack_StartsTheThreeMinutesAgain()
        {
            BetweenMatchAds ads = NewAds(played: 3);
            Assert.IsTrue(ShowsAd(ads, AfterMatch.MainMenu));

            DateTime setBack = Start - TimeSpan.FromHours(1);
            _now = setBack;
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)), "the clock went back past the last ad");

            _now = setBack + BetweenMatchAds.MinInterval - TimeSpan.FromSeconds(1);
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)));

            _now = setBack + BetweenMatchAds.MinInterval;
            Assert.IsTrue(ShowsAd(ads, AfterMatch.MainMenu), "three minutes after the clock was found set back");
            Assert.AreEqual(2, _ads.Shows);
        }

        [Test]
        public void ACancelledToken_IsCancelled_AndAnAdItStoppedWaitingFor_StillCounts()
        {
            BetweenMatchAds ads = NewAds(played: 3);
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Task<bool> before = null;
                Assert.DoesNotThrow(() => before = ads.ShowIfDueAsync(AfterMatch.MainMenu, cancelled.Token));
                Assert.IsTrue(before.IsCanceled, "an already cancelled token");
                Assert.AreEqual(0, _ads.Shows);
            }

            using (var cancel = new CancellationTokenSource())
            {
                Task<bool> during = ads.ShowIfDueAsync(AfterMatch.MainMenu, cancel.Token);
                cancel.Cancel();
                Assert.IsTrue(during.IsCanceled);
            }

            _now += TimeSpan.FromMinutes(1);
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)), "the ad may still have been up");
            Assert.AreEqual(1, _ads.Shows);
        }

        [Test]
        public void BrokenParts_NeverThrowIntoTheGame_AndShowNoAd()
        {
            BetweenMatchAds ads = NewAds(played: 3);

            _ads.ThrowOnAvailable = true;
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)), "an ad network that can't be asked");
            _ads.ThrowOnAvailable = false;

            _ads.ThrowOnShow = true;
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)), "an ad that throws as it shows");
            _ads.ThrowOnShow = false;

            _noAds.ThrowOnOwned = true;
            Assert.IsFalse(Over(ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)), "a No Ads that can't be asked");
            _noAds.ThrowOnOwned = false;

            var broken = new BetweenMatchAds(_ads, _stats, _noAds, () => throw new InvalidOperationException("a broken clock"), _log.Add);
            Assert.IsFalse(Over(broken.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)), "a clock that throws");

            Assert.AreEqual(1, _ads.Shows, "only the ad that threw was tried");
            StringAssert.Contains("broken", string.Join("\n", _log));
            Assert.IsTrue(ShowsAd(ads, AfterMatch.MainMenu), "the throw that failed the ad did not start the three minutes");
        }

        [Test]
        public void TheLog_SaysWhyNoAdShowed()
        {
            BetweenMatchAds ads = NewAds(played: 1);
            ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None);
            Play();
            ads.ShowIfDueAsync(AfterMatch.PrivateLobby, CancellationToken.None);
            _ads.IsAvailable = false;
            ads.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None);

            string log = string.Join("\n", _log);
            StringAssert.Contains("first match", log);
            StringAssert.Contains("lobby", log);
            StringAssert.Contains("none is loaded", log);
        }

        [Test]
        public void Constructor_RefusesMissingParts()
        {
            Assert.Throws<ArgumentNullException>(() => new BetweenMatchAds(null, _stats, _noAds, () => _now));
            Assert.Throws<ArgumentNullException>(() => new BetweenMatchAds(_ads, null, _noAds, () => _now));
            Assert.Throws<ArgumentNullException>(() => new BetweenMatchAds(_ads, _stats, null, () => _now));
            Assert.Throws<ArgumentNullException>(() => new BetweenMatchAds(_ads, _stats, _noAds, null));
            Assert.DoesNotThrow(() => new BetweenMatchAds(_ads, _stats, _noAds, () => _now, log: null).ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None));
        }

        /// <summary>The result of an exit that must be over by now (never waits for one that isn't).</summary>
        private static bool Over(Task<bool> leaving)
        {
            Assert.IsTrue(leaving.IsCompleted, "the player went on");
            return leaving.Result;
        }

        /// <summary>Leaves for <paramref name="next"/>, closes the ad if one came up, and says whether one did.</summary>
        private bool ShowsAd(BetweenMatchAds ads, AfterMatch next)
        {
            int shows = _ads.Shows;
            Task<bool> leaving = ads.ShowIfDueAsync(next, CancellationToken.None);
            if (_ads.Shows == shows)
            {
                return Over(leaving);
            }

            Assert.IsFalse(leaving.IsCompleted, "the player waits for the ad to go away");
            _ads.Close();
            Assert.IsTrue(Over(leaving));
            return true;
        }

        private BetweenMatchAds NewAds(int played)
        {
            for (int i = 0; i < played; i++)
            {
                Play();
            }

            return new BetweenMatchAds(_ads, _stats, _noAds, () => _now, _log.Add);
        }

        /// <summary>The player finishes one more match, which the statistics record as the session does.</summary>
        private void Play()
        {
            _matches++;
            _stats.RecordMatch("match-" + _matches, 2, 120, abandoned: false, roundsPlayed: 5);
        }

        private sealed class ScriptedAds : IInterstitialAds
        {
            private TaskCompletionSource<bool> _show;
            private bool _available = true;

            public bool IsAvailable
            {
                get => ThrowOnAvailable ? throw new InvalidOperationException("a broken network") : _available;
                set => _available = value;
            }

            public int Shows { get; private set; }

            public bool ThrowOnAvailable { get; set; }

            public bool ThrowOnShow { get; set; }

            public Task<bool> ShowAsync(CancellationToken ct)
            {
                if (ct.IsCancellationRequested)
                {
                    return Task.FromCanceled<bool>(ct);
                }

                Shows++;
                if (ThrowOnShow)
                {
                    throw new InvalidOperationException("a broken ad");
                }

                var show = new TaskCompletionSource<bool>();
                _show = show;
                ct.Register(() => show.TrySetCanceled());
                return show.Task;
            }

            public void Close() => _show.SetResult(true);

            public void Fail() => _show.SetResult(false);
        }

        private sealed class ScriptedNoAds : INoAds
        {
            private bool _owned;

            public event Action Changed
            {
                add { }
                remove { }
            }

            public bool Owned
            {
                get => ThrowOnOwned ? throw new InvalidOperationException("a broken store") : _owned;
                set => _owned = value;
            }

            public bool ThrowOnOwned { get; set; }

            public bool CanBuy => false;

            public string Price => null;

            public Task<NoAdsPurchase> BuyAsync(CancellationToken ct) => Task.FromResult(NoAdsPurchase.Unavailable);
        }
    }
}
