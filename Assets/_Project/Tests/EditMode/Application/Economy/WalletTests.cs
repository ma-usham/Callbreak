using System;
using Callbreak.Application.Economy;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The player's gold coins (the owner, 4 Oct 2026): 500 to start, positive amounts only, never
    /// below 0, kept in the wallet's own save file.
    /// </summary>
    public sealed class WalletTests
    {
        private InMemorySaveStore _store;
        private Wallet _wallet;

        [SetUp]
        public void SetUp()
        {
            _store = new InMemorySaveStore();
            _wallet = new Wallet(_store);
        }

        [Test]
        public void ANewPlayer_StartsWithTheStartingCoins()
        {
            Assert.AreEqual(500, WalletRules.StartingCoins);
            Assert.AreEqual(WalletRules.StartingCoins, _wallet.Coins);
            Assert.AreEqual(0, _store.SaveCount, "nothing to save until the coins change");
        }

        [Test]
        public void Add_CreditsSavesAndRaisesChanged_AndSurvivesARestart()
        {
            int changed = 0;
            _wallet.Changed += () => changed++;

            _wallet.Add(20);

            Assert.AreEqual(520, _wallet.Coins);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(1, _store.SavesOf(Wallet.SaveKey));
            Assert.AreEqual(520, new Wallet(_store).Coins);
        }

        [Test]
        public void TrySpend_TakesTheCoins_WhenThereAreEnough()
        {
            int changed = 0;
            _wallet.Changed += () => changed++;

            Assert.IsTrue(_wallet.TrySpend(350));

            Assert.AreEqual(150, _wallet.Coins);
            Assert.AreEqual(1, changed);
            Assert.AreEqual(150, new Wallet(_store).Coins);
        }

        [Test]
        public void TrySpend_MoreThanTheBalance_TakesNothing()
        {
            int changed = 0;
            _wallet.Changed += () => changed++;

            Assert.IsFalse(_wallet.TrySpend(501));

            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, changed);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void TrySpend_TheWholeBalance_LeavesZero_AndNeverLess()
        {
            Assert.IsTrue(_wallet.TrySpend(500));
            Assert.AreEqual(0, _wallet.Coins);

            Assert.IsFalse(_wallet.TrySpend(1));
            Assert.AreEqual(0, _wallet.Coins);
            Assert.AreEqual(0, new Wallet(_store).Coins, "an empty wallet stays empty after a restart: no new starting coins");
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void Amounts_ArePositive(int amount)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.Add(amount));
            Assert.Throws<ArgumentOutOfRangeException>(() => _wallet.TrySpend(amount));

            Assert.AreEqual(500, _wallet.Coins);
            Assert.AreEqual(0, _store.SaveCount);
        }

        [Test]
        public void Add_StopsAtTheLargestBalance()
        {
            _store.Put(Wallet.SaveKey, new WalletData { coins = int.MaxValue - 5 });
            var wallet = new Wallet(_store);

            wallet.Add(20);

            Assert.AreEqual(int.MaxValue, wallet.Coins);
        }

        [Test]
        public void ANegativeBalanceOnDisk_IsRepairedToZero()
        {
            _store.Put(Wallet.SaveKey, new WalletData { coins = -40 });

            Assert.AreEqual(0, new Wallet(_store).Coins);
        }

        [Test]
        public void FileFromANewerVersion_IsIgnored_AndNeverOverwritten()
        {
            _store.Put(Wallet.SaveKey, new WalletData { schemaVersion = WalletData.CurrentVersion + 1, coins = 9999 });
            var wallet = new Wallet(_store);

            Assert.IsTrue(wallet.IsReadOnly);
            Assert.AreEqual(WalletRules.StartingCoins, wallet.Coins);

            wallet.Add(20);

            Assert.AreEqual(520, wallet.Coins, "the session goes on in memory");
            Assert.AreEqual(0, _store.SaveCount);
            Assert.AreEqual(9999, _store.Peek<WalletData>(Wallet.SaveKey).coins);
        }
    }
}
