using System;
using System.Collections.Generic;
using Callbreak.Application.Economy;
using Callbreak.Core.Scoring;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// Stake Match's coins (the owner, 5 Oct 2026; mockups 23 and 24, <see cref="StakeRules"/>): the
    /// prizes by place, a shared place paid as that place, the stake taken once when a match begins,
    /// the prize paid once at its end, nothing for a match left or never staked, the stake given back
    /// for a match called off, and the stakes file across restarts, kept before any coins move.
    /// </summary>
    public sealed class StakeLedgerTests
    {
        private InMemorySaveStore _store;
        private Wallet _wallet;
        private StakeLedger _ledger;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _wallet = new Wallet(_store);
            _ledger = new StakeLedger(_store, _wallet);
        }

        [Test]
        public void Rules_TwoStakes_AndThePrizesByPlace()
        {
            Assert.AreEqual(0, StakeRules.None, "Quick Match has no stake");
            CollectionAssert.AreEqual(new[] { 50, 100 }, StakeRules.Stakes);
            CollectionAssert.AreEqual(new[] { 100, 40, 30, 20 }, StakeRules.PrizesFor(50));
            CollectionAssert.AreEqual(new[] { 200, 80, 60, 40 }, StakeRules.PrizesFor(100));
            Assert.IsFalse(StakeRules.IsStake(StakeRules.None));
            Assert.IsFalse(StakeRules.IsStake(25));
        }

        [TestCase(50, 1, 100)]
        [TestCase(50, 2, 40)]
        [TestCase(50, 3, 30)]
        [TestCase(50, 4, 20)]
        [TestCase(100, 1, 200)]
        [TestCase(100, 2, 80)]
        [TestCase(100, 3, 60)]
        [TestCase(100, 4, 40)]
        public void PayPrize_PaysThePrizeOfThePlace(int stake, int place, int prize)
        {
            Assert.IsTrue(_ledger.TakeStake("m1", stake));
            Assert.AreEqual(500 - stake, _wallet.Coins);

            Assert.AreEqual(prize, _ledger.PayPrize("m1", place));

            Assert.AreEqual(500 - stake + prize, _wallet.Coins);
            Assert.IsTrue(_ledger.TryGetStake("m1", out StakedMatch match));
            Assert.AreEqual(StakeState.Paid, match.State);
            Assert.AreEqual(stake, match.Stake);
            Assert.AreEqual(place, match.Place);
            Assert.AreEqual(prize, match.Prize);
        }

        [Test]
        public void SharedFirst_AfterEveryTieBreak_BothGetTheFirstPrize_AndTheNextIsThird()
        {
            // Core's Standings: seats 0 and 1 are level on the total, the rounds met and the best
            // round, so they share 1st; the next seat is 3rd (GDD §4 tie rule 4).
            IReadOnlyList<Standing> standings = Standings.Compute(new[] { 210, 210, 150, 90 }, new[] { 4, 4, 3, 2 }, new[] { 60, 60, 50, 40 });

            int[] paid = PayEachOnADeviceOfItsOwn(standings, 50);

            CollectionAssert.AreEqual(new[] { 100, 100, 30, 20 }, paid);
        }

        [Test]
        public void LevelTotals_SplitByATieBreak_ArePaidByThePlaceItGives()
        {
            // Level on the total, but seat 1 met its call in more rounds: 1st and 2nd, not a shared 1st.
            IReadOnlyList<Standing> standings = Standings.Compute(new[] { 210, 210, 150, 90 }, new[] { 3, 4, 3, 2 }, new[] { 60, 60, 50, 40 });

            int[] paid = PayEachOnADeviceOfItsOwn(standings, 100);

            CollectionAssert.AreEqual(new[] { 80, 200, 60, 40 }, paid);
        }

        [Test]
        public void TakeStake_TakesTheStakeOnce_PerMatch()
        {
            int changed = 0;
            _ledger.Changed += () => changed++;

            Assert.IsTrue(_ledger.TakeStake("m1", 50));
            Assert.IsFalse(_ledger.TakeStake("m1", 50), "the same match again takes nothing");
            Assert.IsFalse(_ledger.TakeStake("m1", 100), "whatever the amount");

            Assert.AreEqual(450, _wallet.Coins);
            Assert.AreEqual(1, changed);
            Assert.IsTrue(_ledger.TryGetStake("m1", out StakedMatch match));
            Assert.AreEqual(50, match.Stake);
            Assert.AreEqual(StakeState.Taken, match.State);
            Assert.AreEqual(0, match.Prize);
        }

        [Test]
        public void PayPrize_PaysOnce_ARepeatedEndPaysNothing()
        {
            _ledger.TakeStake("m1", 50);
            int changed = 0;
            _ledger.Changed += () => changed++;

            Assert.AreEqual(40, _ledger.PayPrize("m1", 2));
            Assert.AreEqual(0, _ledger.PayPrize("m1", 2), "the end seen again (a resync)");
            Assert.AreEqual(0, _ledger.PayPrize("m1", 1), "whatever place it says");

            Assert.AreEqual(490, _wallet.Coins);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void AMatchWithNoStakeTaken_TakesNothingAndPaysNothing()
        {
            // Quick Match, or a staked match whose start this device never saw.
            Assert.AreEqual(0, _ledger.PayPrize("quick", 1));
            _ledger.Forfeit("quick");
            _ledger.CallOff("quick");

            Assert.AreEqual(500, _wallet.Coins);
            Assert.IsFalse(_ledger.TryGetStake("quick", out _));
            Assert.AreEqual(0, _store.SaveCount, "nothing to keep");
        }

        [Test]
        public void TakeStake_TheWalletCannotCoverIt_StakesNothing_AndTheEndPaysNothing()
        {
            // Coins spent after the Play Online screen checked them.
            _wallet.TrySpend(460);

            Assert.IsFalse(_ledger.TakeStake("m1", 50));
            Assert.AreEqual(40, _wallet.Coins, "nothing taken");
            Assert.IsFalse(_ledger.TryGetStake("m1", out _), "the match isn't staked");

            Assert.AreEqual(0, _ledger.PayPrize("m1", 1), "so it wins nothing");
            Assert.AreEqual(40, _wallet.Coins);
        }

        [Test]
        public void CanAfford_WhatTheWalletHoldsNow()
        {
            Assert.IsTrue(_ledger.CanAfford(StakeRules.None), "Quick Match is free");
            Assert.IsTrue(_ledger.CanAfford(50));
            Assert.IsTrue(_ledger.CanAfford(100));
            Assert.IsFalse(_ledger.CanAfford(25), "not a stake");
            Assert.IsFalse(_ledger.CanAfford(-50));

            _wallet.TrySpend(420);

            Assert.IsTrue(_ledger.CanAfford(50), "80 coins cover 50");
            Assert.IsFalse(_ledger.CanAfford(100), "but not 100");
            Assert.IsTrue(_ledger.CanAfford(StakeRules.None));
        }

        [Test]
        public void Forfeit_LosesTheStake_AndNothingIsPaidAfterIt()
        {
            _ledger.TakeStake("m1", 50);

            _ledger.Forfeit("m1");

            Assert.AreEqual(0, _ledger.PayPrize("m1", 1), "a match left pays nothing at its end");
            _ledger.CallOff("m1");
            Assert.AreEqual(450, _wallet.Coins, "nor gives the stake back");
            Assert.IsTrue(_ledger.TryGetStake("m1", out StakedMatch match));
            Assert.AreEqual(StakeState.Forfeited, match.State);
        }

        [Test]
        public void CallOff_GivesTheStakeBack_Once()
        {
            _ledger.TakeStake("m1", 100);
            Assert.AreEqual(400, _wallet.Coins);

            _ledger.CallOff("m1");
            _ledger.CallOff("m1");

            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, _ledger.PayPrize("m1", 1), "and it pays no prize");
            Assert.AreEqual(500, _wallet.Coins);
            Assert.IsTrue(_ledger.TryGetStake("m1", out StakedMatch match));
            Assert.AreEqual(StakeState.Refunded, match.State);
        }

        [Test]
        public void ARestartMidMatch_KeepsTheStakeTaken_AndTheEndStillPaysOnce()
        {
            _ledger.TakeStake("m1", 50);

            // The app is killed during the match and comes back to it.
            var wallet = new Wallet(_store);
            var ledger = new StakeLedger(_store, wallet);
            Assert.AreEqual(450, wallet.Coins);
            Assert.IsTrue(ledger.TryGetStake("m1", out StakedMatch match));
            Assert.AreEqual(StakeState.Taken, match.State);
            Assert.IsFalse(ledger.TakeStake("m1", 50), "not taken again");

            Assert.AreEqual(100, ledger.PayPrize("m1", 1), "it sees the end: paid");
            Assert.AreEqual(550, wallet.Coins);

            var again = new Wallet(_store);
            Assert.AreEqual(0, new StakeLedger(_store, again).PayPrize("m1", 1), "and after another restart, not again");
            Assert.AreEqual(550, again.Coins);
        }

        [Test]
        public void TheLedgerIsSaved_BeforeTheCoinsMove()
        {
            var seen = new List<string>();
            _wallet.Changed += () =>
            {
                bool known = new StakeLedger(_store, new Wallet(_store)).TryGetStake("m1", out StakedMatch match);
                seen.Add(known ? match.State.ToString() : "nothing");
            };

            _ledger.TakeStake("m1", 50);
            _ledger.PayPrize("m1", 2);

            CollectionAssert.AreEqual(new[] { "Taken", "Paid" }, seen, "what the file said as the wallet changed");
        }

        [Test]
        public void AWalletListenerThatThrows_CannotTakeAStakeOrPayAPrizeTwice()
        {
            _wallet.Changed += () => throw new InvalidOperationException("a broken view");

            Assert.Throws<InvalidOperationException>(() => _ledger.TakeStake("m1", 50), "the listener's exception reaches the caller");
            Assert.AreEqual(450, new Wallet(_store).Coins);
            Assert.IsFalse(_ledger.TakeStake("m1", 50), "staked already");

            Assert.Throws<InvalidOperationException>(() => _ledger.PayPrize("m1", 1));
            Assert.AreEqual(0, _ledger.PayPrize("m1", 1), "paid already");
            Assert.AreEqual(550, new Wallet(_store).Coins);
        }

        [TestCase(0)]
        [TestCase(25)]
        [TestCase(-50)]
        [TestCase(150)]
        public void TakeStake_AnAmountThatIsNotAStake_Throws(int stake)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _ledger.TakeStake("m1", stake));

            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [TestCase(0)]
        [TestCase(5)]
        public void PayPrize_APlaceOutside1To4_Throws_AndTheMatchStaysUnsettled(int place)
        {
            _ledger.TakeStake("m1", 50);

            Assert.Throws<ArgumentOutOfRangeException>(() => _ledger.PayPrize("m1", place));

            Assert.AreEqual(450, _wallet.Coins);
            Assert.AreEqual(30, _ledger.PayPrize("m1", 3), "a real place still pays");
        }

        [TestCase(null)]
        [TestCase("")]
        public void AMatchIdIsRequired(string matchId)
        {
            Assert.Throws<ArgumentException>(() => _ledger.TakeStake(matchId, 50));
            Assert.Throws<ArgumentException>(() => _ledger.PayPrize(matchId, 1));
            Assert.Throws<ArgumentException>(() => _ledger.Forfeit(matchId));
            Assert.Throws<ArgumentException>(() => _ledger.CallOff(matchId));
            Assert.IsFalse(_ledger.TryGetStake(matchId, out _));
            Assert.AreEqual(500, _wallet.Coins);
        }

        [Test]
        public void TheFileKeepsTheLast50Matches()
        {
            _wallet.Add(10000);
            for (int i = 0; i < StakeLedger.RememberedMatches + 5; i++)
            {
                Assert.IsTrue(_ledger.TakeStake("m" + i, 50));
            }

            var ledger = new StakeLedger(_store, new Wallet(_store));
            Assert.IsFalse(ledger.TryGetStake("m4", out _), "the oldest are forgotten");
            Assert.IsTrue(ledger.TryGetStake("m5", out _));
            Assert.IsTrue(ledger.TryGetStake("m" + (StakeLedger.RememberedMatches + 4), out _));
            Assert.AreEqual(StakeLedger.RememberedMatches, _store.Peek<StakeLedgerData>(StakeLedger.SaveKey).matches.Count);
        }

        [Test]
        public void FileFromANewerVersion_IsIgnored_NeverOverwritten_AndNothingIsStaked()
        {
            _store.Put(StakeLedger.SaveKey, new StakeLedgerData { schemaVersion = StakeLedgerData.CurrentVersion + 1 });
            var ledger = new StakeLedger(_store, _wallet);

            Assert.IsTrue(ledger.IsReadOnly);
            Assert.IsFalse(ledger.TakeStake("m1", 50), "a stake this build couldn't keep isn't taken");
            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, _store.SavesOf(StakeLedger.SaveKey));
            Assert.AreEqual(StakeLedgerData.CurrentVersion + 1, _store.Peek<StakeLedgerData>(StakeLedger.SaveKey).schemaVersion);
        }

        [Test]
        public void AFileWithUnusableMatches_IsRepaired()
        {
            _store.Put(StakeLedger.SaveKey, new StakeLedgerData
            {
                matches = new List<StakeLedgerEntry>
                {
                    new StakeLedgerEntry { matchId = string.Empty, stake = 50 },
                    new StakeLedgerEntry { matchId = "odd", stake = 70 },
                    new StakeLedgerEntry { matchId = "m1", stake = 50, state = StakeState.Taken, place = 2, prize = 999 },
                    new StakeLedgerEntry { matchId = "m1", stake = 100 },
                    new StakeLedgerEntry { matchId = "m2", stake = 100, state = (StakeState)9 },
                    new StakeLedgerEntry { matchId = "m3", stake = 100, state = StakeState.Paid, place = 1, prize = 200 },
                },
            });

            var ledger = new StakeLedger(_store, _wallet);

            Assert.IsFalse(ledger.TryGetStake("odd", out _), "an amount that isn't a stake");
            Assert.IsFalse(ledger.TryGetStake("m2", out _), "a state this build doesn't know");
            Assert.IsTrue(ledger.TryGetStake("m1", out StakedMatch m1));
            Assert.AreEqual(50, m1.Stake, "the first of a repeated id");
            Assert.AreEqual(0, m1.Prize, "an unpaid match has no prize");
            Assert.IsTrue(ledger.TryGetStake("m3", out StakedMatch m3));
            Assert.AreEqual(200, m3.Prize, "a paid match keeps its prize");
            Assert.AreEqual(100, ledger.PayPrize("m1", 1));
            Assert.AreEqual(600, _wallet.Coins);
        }

        /// <summary>Each seat is a person on a device of its own who staked <paramref name="stake"/> on the match; what each was paid, by seat.</summary>
        private static int[] PayEachOnADeviceOfItsOwn(IReadOnlyList<Standing> standings, int stake)
        {
            var paid = new int[standings.Count];
            foreach (Standing standing in standings)
            {
                var store = new InMemorySaveStore();
                var wallet = new Wallet(store);
                var ledger = new StakeLedger(store, wallet);
                Assert.IsTrue(ledger.TakeStake("match", stake));
                paid[(int)standing.Seat] = ledger.PayPrize("match", standing.Rank);
                Assert.AreEqual(500 - stake + paid[(int)standing.Seat], wallet.Coins);
            }

            return paid;
        }
    }
}
