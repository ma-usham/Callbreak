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
    /// When an app open ad shows as the player comes back to the game (the owner, 15 Oct 2026): only
    /// after the game was away at least half a minute, never on a start (the game was never away),
    /// never for a player who owns No Ads, never before the player's second match, at most one
    /// full-screen ad every three minutes counting the ads between matches (a clock set back starts
    /// them again), and never waiting for an ad that isn't loaded. Nothing it is given makes it throw.
    /// The bare main menu is the caller's to check (ReturnToGameAdMoment).
    /// </summary>
    public sealed class ReturnToGameAdsTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 15, 18, 0, 0, DateTimeKind.Utc);

        private StatsService _stats;
        private ScriptedAds _ads;
        private ScriptedAds _betweenMatchAds;
        private ScriptedNoAds _noAds;
        private FullScreenAdPacing _pacing;
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
            _betweenMatchAds = new ScriptedAds();
            _noAds = new ScriptedNoAds();
            _pacing = new FullScreenAdPacing();
            _log = new List<string>();
            _now = Start;
            _matches = 0;
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [Test]
        public void BackAfterHalfAMinuteAway_ShowsAnAd_AndCompletesOnceItHasGoneAway()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            ads.Left(_now);
            _now += ReturnToGameAds.MinAway;

            Task<bool> back = ads.ShowIfDueAsync(CancellationToken.None);

            Assert.AreEqual(1, _ads.Shows, "the ad shows as the player comes back");
            Assert.IsFalse(back.IsCompleted, "it completes once the ad has gone away");
            _ads.Close();
            Assert.IsTrue(Over(back), "an ad was shown");
            Assert.AreEqual(TimeSpan.FromSeconds(30), ReturnToGameAds.MinAway);
        }

        [Test]
        public void BackSooner_ShowsNoAd()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            ads.Left(_now);
            _now += ReturnToGameAds.MinAway - TimeSpan.FromSeconds(1);

            Assert.IsFalse(Over(ads.ShowIfDueAsync(CancellationToken.None)), "a quick look at another app");
            Assert.AreEqual(0, _ads.Shows);
        }

        [Test]
        public void AStart_ShowsNoAd_TheGameWasNeverAway()
        {
            ReturnToGameAds ads = NewAds(played: 5);
            _now += TimeSpan.FromMinutes(10);

            Assert.IsFalse(Over(ads.ShowIfDueAsync(CancellationToken.None)), "never on a start");
            Assert.AreEqual(0, _ads.Shows);
        }

        [Test]
        public void EachTimeAway_GivesOneChance_NotTwo()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            _ads.IsAvailable = false;
            ads.Left(_now);
            _now += TimeSpan.FromMinutes(1);
            Assert.IsFalse(Over(ads.ShowIfDueAsync(CancellationToken.None)), "no ad was loaded then");

            _ads.IsAvailable = true;
            _now += FullScreenAdPacing.MinInterval;
            Assert.IsFalse(Over(ads.ShowIfDueAsync(CancellationToken.None)), "the game wasn't away again");
            Assert.AreEqual(0, _ads.Shows);

            ads.Left(_now);
            _now += TimeSpan.FromMinutes(1);
            Assert.IsTrue(ShowsAd(ads), "away again, it may show");
        }

        [Test]
        public void WithNoAds_ShowsNoAd()
        {
            ReturnToGameAds ads = NewAds(played: 5);
            _noAds.Owned = true;
            ads.Left(_now);
            _now += TimeSpan.FromMinutes(5);

            Assert.IsFalse(Over(ads.ShowIfDueAsync(CancellationToken.None)));
            Assert.AreEqual(0, _ads.Shows);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void BeforeThePlayersSecondMatch_ShowsNoAd(int played)
        {
            ReturnToGameAds ads = NewAds(played);
            ads.Left(_now);
            _now += TimeSpan.FromMinutes(5);

            Assert.IsFalse(Over(ads.ShowIfDueAsync(CancellationToken.None)), played + " matches played");
            Assert.AreEqual(0, _ads.Shows);

            Play(2 - played);
            ads.Left(_now);
            _now += TimeSpan.FromMinutes(5);
            Assert.IsTrue(ShowsAd(ads), "after the second match it may show");
        }

        [Test]
        public void WithinThreeMinutesOfTheLastAppOpenAd_ShowsNoAd()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            Assert.IsTrue(AwayAndBack(ads, TimeSpan.FromMinutes(1)));

            Assert.IsFalse(AwayAndBack(ads, FullScreenAdPacing.MinInterval - TimeSpan.FromSeconds(1)), "counted from when the last one came up");
            Assert.AreEqual(1, _ads.Shows);

            Assert.IsTrue(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "three minutes on, it may show again");
            Assert.AreEqual(TimeSpan.FromMinutes(3), FullScreenAdPacing.MinInterval);
        }

        [Test]
        public void AnAdBetweenMatches_CountsToo_AndTheyCountThisOne()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            BetweenMatchAds betweenMatches = NewBetweenMatchAds();

            Task<bool> leaving = betweenMatches.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None);
            _betweenMatchAds.Close();
            Assert.IsTrue(Over(leaving), "an ad between matches came up");

            Assert.IsFalse(AwayAndBack(ads, TimeSpan.FromMinutes(2)), "within three minutes of the ad between matches");
            Assert.IsTrue(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "three minutes after it");

            _now += TimeSpan.FromMinutes(1);
            Assert.IsFalse(Over(betweenMatches.ShowIfDueAsync(AfterMatch.MainMenu, CancellationToken.None)), "and the app open ad counts for the ads between matches");
            Assert.AreEqual(1, _betweenMatchAds.Shows);
        }

        [Test]
        public void NoAdLoaded_GoesOnAtOnce_AndTheThreeMinutesDontStart()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            _ads.IsAvailable = false;

            Assert.IsFalse(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "never waited for");
            Assert.AreEqual(0, _ads.Shows);

            _ads.IsAvailable = true;
            Assert.IsTrue(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "the next return may show one");
        }

        [Test]
        public void AnAdThatCantBeShown_DoesntStartTheThreeMinutes()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            ads.Left(_now);
            _now += TimeSpan.FromMinutes(1);
            Task<bool> back = ads.ShowIfDueAsync(CancellationToken.None);
            _ads.Fail();
            Assert.IsFalse(Over(back));

            Assert.IsTrue(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "none came up, so none counts");
        }

        [Test]
        public void AClockSetBack_WhileAway_ShowsNoAd()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            ads.Left(_now);
            _now -= TimeSpan.FromHours(1);

            Assert.IsFalse(Over(ads.ShowIfDueAsync(CancellationToken.None)), "how long the game was away can't be told");
            Assert.AreEqual(0, _ads.Shows);
        }

        [Test]
        public void AClockSetBack_PastTheLastAd_StartsTheThreeMinutesAgain()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            Assert.IsTrue(AwayAndBack(ads, TimeSpan.FromMinutes(1)));

            _now -= TimeSpan.FromHours(1);
            Assert.IsFalse(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "how long ago the last ad was can't be told");
            Assert.IsFalse(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "the three minutes start again from the set-back clock");
            Assert.IsTrue(AwayAndBack(ads, TimeSpan.FromMinutes(2)), "and then they have passed");
        }

        [Test]
        public void ACancelledWait_Cancels_AndTheAdStillCounts()
        {
            ReturnToGameAds ads = NewAds(played: 2);
            using (var cancel = new CancellationTokenSource())
            {
                ads.Left(_now);
                _now += TimeSpan.FromMinutes(1);
                Task<bool> back = ads.ShowIfDueAsync(cancel.Token);
                cancel.Cancel();
                Assert.IsTrue(back.IsCanceled);
            }

            Assert.IsFalse(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "the ad may still have been up");
        }

        [Test]
        public void NothingItIsGiven_MakesItThrow()
        {
            ReturnToGameAds ads = NewAds(played: 2);

            _ads.ThrowOnAvailable = true;
            Assert.IsFalse(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "a broken network");
            _ads.ThrowOnAvailable = false;

            _noAds.ThrowOnOwned = true;
            Assert.IsFalse(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "a broken store");
            _noAds.ThrowOnOwned = false;

            _ads.ThrowOnShow = true;
            Assert.IsFalse(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "a broken ad");
            _ads.ThrowOnShow = false;
            Assert.IsTrue(AwayAndBack(ads, TimeSpan.FromMinutes(1)), "a broken ad starts no three minutes");

            var broken = new ReturnToGameAds(_ads, _stats, _noAds, _pacing, () => throw new InvalidOperationException("a broken clock"), _log.Add);
            broken.Left(_now);
            _now += TimeSpan.FromMinutes(1);
            Assert.IsFalse(Over(broken.ShowIfDueAsync(CancellationToken.None)), "a broken clock");

            Assert.Throws<ArgumentNullException>(() => new ReturnToGameAds(null, _stats, _noAds, _pacing, () => _now));
            Assert.Throws<ArgumentNullException>(() => new ReturnToGameAds(_ads, null, _noAds, _pacing, () => _now));
            Assert.Throws<ArgumentNullException>(() => new ReturnToGameAds(_ads, _stats, null, _pacing, () => _now));
            Assert.Throws<ArgumentNullException>(() => new ReturnToGameAds(_ads, _stats, _noAds, null, () => _now));
            Assert.Throws<ArgumentNullException>(() => new ReturnToGameAds(_ads, _stats, _noAds, _pacing, null));
            Assert.DoesNotThrow(() => new ReturnToGameAds(_ads, _stats, _noAds, _pacing, () => _now, log: null).ShowIfDueAsync(CancellationToken.None));
        }

        [Test]
        public void TheStandInForeground_NeverMoves()
        {
            var foreground = new StandInAppForeground();
            int moves = 0;
            foreground.WentToBackground += _ => moves++;
            foreground.CameToForeground += () => moves++;

            Assert.AreEqual(0, moves, "the Editor, the tests and desktop builds have no app open ads");
        }

        /// <summary>The game goes away for <paramref name="away"/> and comes back; true when an ad was shown, which the player closes.</summary>
        private bool AwayAndBack(ReturnToGameAds ads, TimeSpan away)
        {
            ads.Left(_now);
            _now += away;
            return ShowsAd(ads);
        }

        private bool ShowsAd(ReturnToGameAds ads)
        {
            int before = _ads.Shows;
            Task<bool> back = ads.ShowIfDueAsync(CancellationToken.None);
            if (_ads.Shows > before && !back.IsCompleted)
            {
                _ads.Close();
            }

            return Over(back);
        }

        private static bool Over(Task<bool> back)
        {
            Assert.IsTrue(back.IsCompleted, "the player isn't kept waiting");
            return back.Result;
        }

        private ReturnToGameAds NewAds(int played)
        {
            Play(played);
            return new ReturnToGameAds(_ads, _stats, _noAds, _pacing, () => _now, _log.Add);
        }

        private BetweenMatchAds NewBetweenMatchAds()
        {
            return new BetweenMatchAds(_betweenMatchAds, _stats, _noAds, () => _now, _log.Add, pacing: _pacing);
        }

        /// <summary>The player finishes <paramref name="count"/> more matches, which the statistics record as the session does.</summary>
        private void Play(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _matches++;
                _stats.RecordMatch("match-" + _matches, 2, 120, abandoned: false, roundsPlayed: 5);
            }
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
