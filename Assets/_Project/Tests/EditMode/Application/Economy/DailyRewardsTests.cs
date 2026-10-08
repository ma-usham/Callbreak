using System;
using System.Collections.Generic;
using Callbreak.Application.Economy;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The weekly login rewards (mockup 26; the owner, 8 Oct 2026): seven days paying 10, 15, 20,
    /// 25, 30, 40 and 60 coins, one claim per calendar day on the device's clock; a missed day starts
    /// the week again at Day 1, and after Day 7 a new week starts. The page opens by itself at most
    /// once a day, only while a reward is ready. A clock set back hands out nothing extra.
    /// </summary>
    public sealed class DailyRewardsTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 8, 9, 30, 0);

        private InMemorySaveStore _store;
        private Wallet _wallet;
        private DateTime _now;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _wallet = new Wallet(_store);
            _now = Start;
        }

        [Test]
        public void ANewPlayer_StartsAtDay1_WithItsRewardReady()
        {
            DailyRewards rewards = NewRewards();

            CollectionAssert.AreEqual(new[] { 10, 15, 20, 25, 30, 40, 60 }, rewards.Rewards);
            Assert.AreEqual(200, Sum(rewards.Rewards), "the owner: 200 coins a week");
            Assert.AreEqual(0, rewards.Day);
            Assert.IsTrue(rewards.CanClaim);
            Assert.Throws<NotSupportedException>(() => ((IList<int>)rewards.Rewards)[0] = 1000, "the amounts can't be changed");
        }

        [Test]
        public void Claiming_CreditsTodaysCoins_OnceADay()
        {
            DailyRewards rewards = NewRewards();
            int changed = 0;
            rewards.Changed += () => changed++;

            Assert.AreEqual(10, rewards.Claim());
            Assert.AreEqual(510, _wallet.Coins);
            Assert.IsFalse(rewards.CanClaim);
            Assert.AreEqual(0, rewards.Day, "today stays Day 1, now claimed");

            _now = Start.AddHours(10);
            Assert.AreEqual(0, rewards.Claim(), "a second claim the same day pays nothing");
            Assert.AreEqual(510, _wallet.Coins);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void TheNextDay_OffersTheNextReward()
        {
            DailyRewards rewards = NewRewards();
            rewards.Claim();

            _now = Start.AddDays(1);

            Assert.AreEqual(1, rewards.Day);
            Assert.IsTrue(rewards.CanClaim);
            Assert.AreEqual(15, rewards.Claim());
            Assert.AreEqual(525, _wallet.Coins);
        }

        [Test]
        public void TheDayTurns_AtLocalMidnight()
        {
            _now = new DateTime(2026, 10, 8, 23, 59, 0);
            DailyRewards rewards = NewRewards();
            rewards.Claim();

            _now = new DateTime(2026, 10, 8, 23, 59, 59);
            Assert.IsFalse(rewards.CanClaim, "still the same day");

            _now = new DateTime(2026, 10, 9, 0, 0, 0);
            Assert.IsTrue(rewards.CanClaim, "a new calendar day, a minute after the last claim");
            Assert.AreEqual(1, rewards.Day);
        }

        [Test]
        public void AWholeWeek_PaysTheSevenRewards_ThenANewWeekStartsAtDay1()
        {
            DailyRewards rewards = NewRewards();
            var paid = new List<int>();
            for (int day = 0; day < 7; day++)
            {
                _now = Start.AddDays(day);
                Assert.AreEqual(day, rewards.Day);
                paid.Add(rewards.Claim());
            }

            CollectionAssert.AreEqual(new[] { 10, 15, 20, 25, 30, 40, 60 }, paid);
            Assert.AreEqual(700, _wallet.Coins);

            _now = Start.AddDays(7);
            Assert.AreEqual(0, rewards.Day, "after Day 7, Day 1 again");
            Assert.AreEqual(10, rewards.Claim());
        }

        [Test]
        public void AMissedDay_StartsTheWeekAgainAtDay1()
        {
            DailyRewards rewards = NewRewards();
            for (int day = 0; day < 3; day++)
            {
                _now = Start.AddDays(day);
                rewards.Claim();
            }

            _now = Start.AddDays(4);

            Assert.AreEqual(0, rewards.Day, "Day 4 was missed: the streak starts over");
            Assert.IsTrue(rewards.CanClaim);
            Assert.AreEqual(10, rewards.Claim());
        }

        [Test]
        public void AMissedDay_BeforeTheBonus_LosesTheBonus()
        {
            DailyRewards rewards = NewRewards();
            for (int day = 0; day < 6; day++)
            {
                _now = Start.AddDays(day);
                rewards.Claim();
            }

            _now = Start.AddDays(7);

            Assert.AreEqual(0, rewards.Day);
            Assert.AreEqual(10, rewards.Claim(), "Day 7's 60 needs seven days in a row");
        }

        [Test]
        public void AClaimMissedOnTheDay_IsNotOfferedLater()
        {
            DailyRewards rewards = NewRewards();
            rewards.Claim();

            // Day 2 comes and goes without a claim.
            _now = Start.AddDays(1);
            Assert.IsTrue(rewards.CanClaim);

            _now = Start.AddDays(2);
            Assert.AreEqual(0, rewards.Day, "the day not claimed counts as missed");
        }

        [Test]
        public void TheProgress_SurvivesARestart()
        {
            NewRewards().Claim();
            _now = Start.AddDays(1);
            NewRewards().Claim();

            DailyRewards later = NewRewards();
            Assert.AreEqual(1, later.Day);
            Assert.IsFalse(later.CanClaim);

            _now = Start.AddDays(2);
            DailyRewards nextDay = NewRewards();
            Assert.AreEqual(2, nextDay.Day);
            Assert.AreEqual(20, nextDay.Claim());
        }

        [Test]
        public void TheClaim_IsSavedBeforeTheCoinsAreAdded()
        {
            DailyRewards rewards = NewRewards();
            int savedDay = -1;
            _wallet.Changed += () => savedDay = _store.Peek<DailyRewardsData>(DailyRewards.SaveKey).lastClaimDay;

            rewards.Claim();

            Assert.AreEqual(DailyRewards.DayNumber(Start), savedDay, "an app killed between the two saves can't pay the same day twice");
        }

        [Test]
        public void TheClockSetBack_HandsOutNothing_UntilTheNextRealDay()
        {
            DailyRewards rewards = NewRewards();
            for (int day = 0; day < 3; day++)
            {
                _now = Start.AddDays(day);
                rewards.Claim();
            }

            _now = Start.AddDays(-5);
            Assert.IsFalse(rewards.CanClaim, "a clock behind the last claim pays nothing");
            Assert.AreEqual(2, rewards.Day, "and keeps the week where it was");
            Assert.AreEqual(0, rewards.Claim());

            _now = Start.AddDays(2).AddHours(3);
            Assert.IsFalse(rewards.CanClaim, "back on the day already claimed");

            _now = Start.AddDays(3);
            Assert.AreEqual(3, rewards.Day);
            Assert.AreEqual(25, rewards.Claim());
        }

        [Test]
        public void ThePage_OpensByItself_OnceADay_WhileARewardIsReady()
        {
            DailyRewards rewards = NewRewards();

            Assert.IsTrue(rewards.TakeAutoOpen(), "the first menu of the day with a reward ready");
            Assert.IsFalse(rewards.TakeAutoOpen(), "not again that day, even unclaimed");
            Assert.IsFalse(NewRewards().TakeAutoOpen(), "nor after a restart the same day");
            Assert.IsTrue(rewards.CanClaim, "the reward still waits for the button");

            _now = Start.AddDays(1);
            Assert.IsTrue(rewards.TakeAutoOpen(), "a new day, a new reward");
        }

        [Test]
        public void ThePage_DoesNotOpenByItself_OnceTodayIsClaimed()
        {
            DailyRewards rewards = NewRewards();
            rewards.Claim();

            Assert.IsFalse(rewards.TakeAutoOpen());
        }

        [TestCase(-3, 0)]
        [TestCase(9, 6)]
        public void AnOutOfRangeDayOnDisk_IsRepaired(int stored, int expected)
        {
            _store.Put(DailyRewards.SaveKey, new DailyRewardsData { lastClaimDay = DailyRewards.DayNumber(Start), lastClaimIndex = stored });

            DailyRewards rewards = NewRewards();

            Assert.AreEqual(expected, rewards.Day);
            Assert.IsFalse(rewards.CanClaim);
        }

        [Test]
        public void FileFromANewerVersion_IsNeverOverwritten_AndPaysNothing()
        {
            _store.Put(DailyRewards.SaveKey, new DailyRewardsData { schemaVersion = DailyRewardsData.CurrentVersion + 1, lastClaimDay = DailyRewards.DayNumber(Start.AddDays(-1)) });
            DailyRewards rewards = NewRewards();

            Assert.IsFalse(rewards.CanClaim, "progress this build can't save would let a day be claimed again");
            Assert.AreEqual(0, rewards.Claim());
            Assert.IsFalse(rewards.TakeAutoOpen());
            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, _store.SavesOf(DailyRewards.SaveKey));
        }

        private DailyRewards NewRewards() => new DailyRewards(_wallet, _store, () => _now);

        private static int Sum(IReadOnlyList<int> values)
        {
            int sum = 0;
            foreach (int value in values)
            {
                sum += value;
            }

            return sum;
        }
    }
}
