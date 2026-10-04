using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The Earn Coins rewards (mockup 22; the designer's rewarded-coins-prompts.md and its prototype):
    /// three ads paying 20, 30 and 50 coins, each credited the moment it completes; the set resets
    /// five hours after its first completed ad on the wall clock, so time away counts and a restart
    /// loses nothing. A failed, cancelled or unavailable ad, a second tap and a clock set back hand
    /// out nothing extra.
    /// </summary>
    public sealed class AdRewardsTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStore _store;
        private Wallet _wallet;
        private ScriptedAds _ads;
        private DateTime _now;
        private SynchronizationContext _context;

        [SetUp]
        public void SetUp()
        {
            // An ad the test finishes resumes the rewards on the test's own thread, at once, in the
            // Editor as in the dotnet harness.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _store = new InMemorySaveStore();
            _wallet = new Wallet(_store);
            _ads = new ScriptedAds();
            _now = Start;
        }

        [TearDown]
        public void TearDown()
        {
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [Test]
        public void ANewPlayer_HasAFreshSet_Of20_30And50()
        {
            AdRewards rewards = NewRewards();

            CollectionAssert.AreEqual(new[] { 20, 30, 50 }, rewards.Rewards);
            Assert.AreEqual(0, rewards.Watched);
            Assert.AreEqual(20, rewards.NextReward);
            Assert.IsNull(rewards.ResetsAtUtc, "no deadline before the set's first ad");
            Assert.Throws<NotSupportedException>(() => ((IList<int>)rewards.Rewards)[0] = 1000, "the payouts can't be changed");
        }

        [Test]
        public void EachCompletedAd_CreditsItsOwnReward_InOrder()
        {
            AdRewards rewards = NewRewards();
            int changed = 0;
            rewards.Changed += () => changed++;

            Assert.AreEqual(20, WatchNext(rewards));
            Assert.AreEqual(520, _wallet.Coins);
            Assert.AreEqual(1, rewards.Watched);
            Assert.AreEqual(30, rewards.NextReward);

            Assert.AreEqual(30, WatchNext(rewards));
            Assert.AreEqual(550, _wallet.Coins);
            Assert.AreEqual(50, rewards.NextReward);

            Assert.AreEqual(50, WatchNext(rewards));
            Assert.AreEqual(600, _wallet.Coins);
            Assert.AreEqual(3, rewards.Watched);
            Assert.AreEqual(3, changed);
            Assert.AreEqual(3, _ads.Shown);
        }

        [Test]
        public void TheReward_IsCreditedTheMomentItsAdCompletes()
        {
            AdRewards rewards = NewRewards();
            _ads.Hold = true;

            Task<int> watch = rewards.WatchNextAsync(CancellationToken.None);

            Assert.IsFalse(watch.IsCompleted, "the ad is playing");
            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, rewards.Watched);

            _ads.Finish(true);

            Assert.IsTrue(watch.IsCompleted);
            Assert.AreEqual(20, watch.Result);
            Assert.AreEqual(520, _wallet.Coins);
            Assert.AreEqual(1, rewards.Watched);
        }

        [Test]
        public void TheProgress_IsSavedBeforeTheCoinsAreAdded()
        {
            AdRewards rewards = NewRewards();
            int savedWatched = -1;
            _wallet.Changed += () => savedWatched = _store.Peek<AdRewardsData>(AdRewards.SaveKey).watched;

            WatchNext(rewards);

            Assert.AreEqual(1, savedWatched, "an app killed between the two saves can't pay the same ad twice");
        }

        [Test]
        public void AWalletListenerThatThrows_CannotUndoAReward()
        {
            AdRewards rewards = NewRewards();
            _wallet.Changed += () => throw new InvalidOperationException("a broken view");

            Task<int> watch = rewards.WatchNextAsync(CancellationToken.None);

            Assert.IsTrue(watch.IsFaulted, "the listener's exception reaches the caller");
            Assert.IsInstanceOf<InvalidOperationException>(watch.Exception.InnerException);
            Assert.AreEqual(520, new Wallet(_store).Coins, "the coins were added and saved");
            Assert.AreEqual(1, NewRewards().Watched, "and so was the progress, so the ad isn't paid again");
        }

        [Test]
        public void AfterThreeAds_NoAdIsShown_UntilTheReset()
        {
            AdRewards rewards = NewRewards();
            WatchThree(rewards);

            Assert.AreEqual(0, rewards.NextReward);
            Assert.AreEqual(0, WatchNext(rewards));
            Assert.AreEqual(3, _ads.Shown, "no fourth ad");
            Assert.AreEqual(600, _wallet.Coins);
            Assert.AreEqual(3, rewards.Watched);
        }

        [Test]
        public void TheSetResets_FiveHoursAfterItsFirstCompletedAd_KeepingTheCoins()
        {
            AdRewards rewards = NewRewards();
            WatchNext(rewards);
            _now = Start.AddHours(2);
            WatchNext(rewards);
            _now = Start.AddHours(4);
            WatchNext(rewards);

            Assert.AreEqual(Start.AddHours(5), rewards.ResetsAtUtc, "five hours after the first ad, not the last");
            Assert.AreEqual(DateTimeKind.Utc, rewards.ResetsAtUtc.Value.Kind);

            _now = Start.AddHours(5).AddSeconds(-1);
            Assert.AreEqual(3, rewards.Watched);
            Assert.AreEqual(0, rewards.NextReward);

            _now = Start.AddHours(5);
            Assert.AreEqual(0, rewards.Watched);
            Assert.AreEqual(20, rewards.NextReward);
            Assert.IsNull(rewards.ResetsAtUtc);
            Assert.AreEqual(600, _wallet.Coins, "the coins earned are kept");
        }

        [Test]
        public void ANewSet_StartsItsFiveHours_WithItsOwnFirstAd()
        {
            AdRewards rewards = NewRewards();
            WatchThree(rewards);
            _now = Start.AddHours(7);

            Assert.AreEqual(20, WatchNext(rewards));

            Assert.AreEqual(1, rewards.Watched);
            Assert.AreEqual(Start.AddHours(12), rewards.ResetsAtUtc);
            Assert.AreEqual(620, _wallet.Coins);
        }

        [Test]
        public void AResetCanComeMidSet_AndStartsTheSetAgain()
        {
            AdRewards rewards = NewRewards();
            WatchNext(rewards);
            _now = Start.AddHours(6);

            Assert.AreEqual(0, rewards.Watched, "an unfinished set resets too");
            Assert.AreEqual(20, WatchNext(rewards));
            Assert.AreEqual(Start.AddHours(11), rewards.ResetsAtUtc);
        }

        [Test]
        public void TimeAway_Counts_AcrossARestart()
        {
            WatchThree(NewRewards());
            _now = Start.AddHours(6);

            AdRewards later = NewRewards();

            Assert.AreEqual(0, later.Watched);
            Assert.AreEqual(20, later.NextReward);
            Assert.AreEqual(20, WatchNext(later));
        }

        [Test]
        public void ARestartMidSet_KeepsTheProgressAndTheDeadline()
        {
            WatchNext(NewRewards());
            _now = Start.AddHours(2);

            var wallet = new Wallet(_store);
            var rewards = new AdRewards(_ads, wallet, _store, () => _now);

            Assert.AreEqual(1, rewards.Watched);
            Assert.AreEqual(30, rewards.NextReward);
            Assert.AreEqual(Start.AddHours(5), rewards.ResetsAtUtc);
            Assert.AreEqual(30, WatchNext(rewards));
            Assert.AreEqual(550, wallet.Coins);
        }

        [Test]
        public void AFailedAd_CreditsNothing_AndDoesNotAdvance()
        {
            AdRewards rewards = NewRewards();
            int changed = 0;
            rewards.Changed += () => changed++;
            _ads.Next = false;

            Assert.AreEqual(0, WatchNext(rewards));

            Assert.AreEqual(1, _ads.Shown);
            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, rewards.Watched);
            Assert.AreEqual(20, rewards.NextReward);
            Assert.IsNull(rewards.ResetsAtUtc, "a failed ad doesn't start the five hours");
            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);

            _ads.Next = true;
            Assert.AreEqual(20, WatchNext(rewards), "the same reward is offered again");
        }

        [Test]
        public void AnUnavailableAd_IsNotShown_AndCreditsNothing()
        {
            AdRewards rewards = NewRewards();
            _ads.IsAvailable = false;

            Assert.AreEqual(0, WatchNext(rewards));

            Assert.AreEqual(0, _ads.Shown);
            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, rewards.Watched);
        }

        [Test]
        public void ACancelledAd_CreditsNothing_AndTheNextOneStillWorks()
        {
            AdRewards rewards = NewRewards();
            _ads.Hold = true;
            using (var cancel = new CancellationTokenSource())
            {
                Task<int> watch = rewards.WatchNextAsync(cancel.Token);
                cancel.Cancel();

                Assert.IsTrue(watch.IsCanceled);
            }

            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, rewards.Watched);

            _ads.Hold = false;
            Assert.AreEqual(20, WatchNext(rewards));
        }

        [Test]
        public void AWatchAlreadyCancelled_ShowsNoAd()
        {
            AdRewards rewards = NewRewards();
            using (var cancel = new CancellationTokenSource())
            {
                cancel.Cancel();

                Assert.IsTrue(rewards.WatchNextAsync(cancel.Token).IsCanceled);
            }

            Assert.AreEqual(0, _ads.Shown);
            Assert.AreEqual(500, _wallet.Coins);
        }

        [Test]
        public void ASecondWatch_WhileOneIsRunning_DoesNothing()
        {
            AdRewards rewards = NewRewards();
            _ads.Hold = true;
            Task<int> first = rewards.WatchNextAsync(CancellationToken.None);

            Task<int> second = rewards.WatchNextAsync(CancellationToken.None);

            Assert.IsTrue(second.IsCompleted, "the double tap is answered at once");
            Assert.AreEqual(0, second.Result);
            Assert.AreEqual(1, _ads.Shown, "one ad only");

            _ads.Finish(true);

            Assert.AreEqual(20, first.Result);
            Assert.AreEqual(520, _wallet.Coins, "credited once");
            Assert.AreEqual(1, rewards.Watched);

            _ads.Hold = false;
            Assert.AreEqual(30, WatchNext(rewards), "once the ad is over the next one can start");
        }

        [Test]
        public void TheClockSetBack_NeverResetsTheSet()
        {
            AdRewards rewards = NewRewards();
            WatchThree(rewards);

            _now = Start.AddDays(-1);

            Assert.AreEqual(3, rewards.Watched);
            Assert.AreEqual(0, rewards.NextReward);
            Assert.AreEqual(Start.AddHours(5), rewards.ResetsAtUtc, "the deadline doesn't move");
            Assert.AreEqual(0, WatchNext(rewards));
            Assert.AreEqual(0, NewRewards().NextReward, "nor after a restart");
            Assert.AreEqual(600, _wallet.Coins);

            _now = Start.AddHours(5);
            Assert.AreEqual(20, rewards.NextReward, "the set resets when the clock reaches its deadline");
        }

        [Test]
        public void TheClockSetBackAndForwardAgain_HandsOutNoExtraSet()
        {
            AdRewards rewards = NewRewards();
            WatchThree(rewards);

            _now = Start.AddHours(-6);
            Assert.AreEqual(0, WatchNext(rewards));
            _now = Start.AddHours(1);

            Assert.AreEqual(0, rewards.NextReward);
            Assert.AreEqual(0, WatchNext(rewards));
            Assert.AreEqual(600, _wallet.Coins);
        }

        [Test]
        public void TheClockSetBack_MidSet_ContinuesTheSameSet()
        {
            AdRewards rewards = NewRewards();
            WatchNext(rewards);
            _now = Start.AddDays(-2);

            Assert.AreEqual(30, WatchNext(rewards));

            Assert.AreEqual(2, rewards.Watched);
            Assert.AreEqual(Start.AddHours(5), rewards.ResetsAtUtc);
        }

        [Test]
        public void AnAdThatOutlastsItsSet_PaysWhatItWasStartedFor()
        {
            AdRewards rewards = NewRewards();
            WatchNext(rewards);
            WatchNext(rewards);
            _now = Start.AddHours(5).AddSeconds(-10);
            _ads.Hold = true;
            Task<int> watch = rewards.WatchNextAsync(CancellationToken.None);

            _now = Start.AddHours(5).AddSeconds(20);
            _ads.Finish(true);

            Assert.AreEqual(50, watch.Result, "the button said +50");
            Assert.AreEqual(600, _wallet.Coins);
            Assert.AreEqual(0, rewards.Watched, "and the set has reset since");
            Assert.AreEqual(20, rewards.NextReward);
        }

        [Test]
        public void AClockWithoutAKind_IsReadAsUtc()
        {
            var rewards = new AdRewards(_ads, _wallet, _store, () => new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Unspecified));

            WatchNext(rewards);

            Assert.AreEqual(Start.AddHours(5), rewards.ResetsAtUtc);
            Assert.AreEqual(DateTimeKind.Utc, rewards.ResetsAtUtc.Value.Kind);
        }

        [Test]
        public void ProgressWithoutADeadline_OnDisk_WaitsFiveHoursFromNow_AndKeepsThatDeadline()
        {
            _store.Put(AdRewards.SaveKey, new AdRewardsData { watched = 2, resetsAtUnixSeconds = 0 });

            AdRewards rewards = NewRewards();

            Assert.AreEqual(2, rewards.Watched, "no new set from a damaged file");
            Assert.AreEqual(Start.AddHours(5), rewards.ResetsAtUtc);

            _now = Start.AddHours(1);
            Assert.AreEqual(Start.AddHours(5), NewRewards().ResetsAtUtc, "the repaired deadline was saved, so a restart doesn't push it back");
        }

        [TestCase(7, 3)]
        [TestCase(-2, 0)]
        public void AnOutOfRangeCountOnDisk_IsRepaired(int stored, int expected)
        {
            _store.Put(AdRewards.SaveKey, new AdRewardsData { watched = stored, resetsAtUnixSeconds = UnixSeconds(Start.AddHours(1)) });

            AdRewards rewards = NewRewards();

            Assert.AreEqual(expected, rewards.Watched);
            Assert.AreEqual(expected == 0 ? (DateTime?)null : Start.AddHours(1), rewards.ResetsAtUtc);
        }

        [Test]
        public void FileFromANewerVersion_IsNeverOverwritten_AndNoAdIsShown()
        {
            _store.Put(AdRewards.SaveKey, new AdRewardsData { schemaVersion = AdRewardsData.CurrentVersion + 1, watched = 1, resetsAtUnixSeconds = UnixSeconds(Start.AddHours(5)) });
            AdRewards rewards = NewRewards();

            Assert.AreEqual(0, WatchNext(rewards), "progress this build can't save would let a set be watched again");

            Assert.AreEqual(0, _ads.Shown);
            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, _store.SaveCount);
            Assert.AreEqual(1, _store.Peek<AdRewardsData>(AdRewards.SaveKey).watched);
        }

        private AdRewards NewRewards() => new AdRewards(_ads, _wallet, _store, () => _now);

        /// <summary>Watches the next ad, completing at once unless the ads are held; the coins it credited.</summary>
        private static int WatchNext(IAdRewards rewards)
        {
            Task<int> watch = rewards.WatchNextAsync(CancellationToken.None);
            Assert.IsTrue(watch.IsCompleted, "an ad that completes at once is answered at once");
            return watch.Result;
        }

        private static void WatchThree(IAdRewards rewards)
        {
            Assert.AreEqual(20, WatchNext(rewards));
            Assert.AreEqual(30, WatchNext(rewards));
            Assert.AreEqual(50, WatchNext(rewards));
        }

        private static long UnixSeconds(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

        /// <summary>
        /// Ads the test drives: each completes at once with <see cref="Next"/>, or, while
        /// <see cref="Hold"/> is set, plays until <see cref="Finish"/> or its cancellation.
        /// </summary>
        private sealed class ScriptedAds : IRewardedAds
        {
            private TaskCompletionSource<bool> _playing;

            public bool IsAvailable { get; set; } = true;

            public bool Next { get; set; } = true;

            public bool Hold { get; set; }

            public int Shown { get; private set; }

            public Task<bool> ShowAsync(CancellationToken ct)
            {
                Shown++;
                if (!Hold)
                {
                    return Task.FromResult(Next);
                }

                var playing = new TaskCompletionSource<bool>();
                if (ct.CanBeCanceled)
                {
                    ct.Register(() => playing.TrySetCanceled());
                }

                _playing = playing;
                return playing.Task;
            }

            public void Finish(bool watched) => _playing.SetResult(watched);
        }
    }
}
