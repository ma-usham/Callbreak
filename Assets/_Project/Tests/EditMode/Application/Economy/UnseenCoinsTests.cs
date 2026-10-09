using Callbreak.Application.Economy;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The coins won where the wallet isn't on screen (the owner, 9 Oct 2026): a Stake Match prize,
    /// kept until the main menu shows it flying into its wallet. Only prizes count: a stake given
    /// back is not a win, and a prize paid once is counted once.
    /// </summary>
    public sealed class UnseenCoinsTests
    {
        [Test]
        public void NoneAtFirst_TheyAddUp_AndClearingShowsThemAll()
        {
            var unseen = new UnseenCoins();
            Assert.AreEqual(0, unseen.Amount);

            unseen.Add(100);
            unseen.Add(40);
            Assert.AreEqual(140, unseen.Amount, "two prizes won before the main menu came back");

            unseen.Clear();
            Assert.AreEqual(0, unseen.Amount);
        }

        [TestCase(0)]
        [TestCase(-20)]
        public void NothingWon_AddsNothing(int coins)
        {
            var unseen = new UnseenCoins();

            unseen.Add(coins);

            Assert.AreEqual(0, unseen.Amount);
        }

        [Test]
        public void AStakeMatchPrize_IsUnseen_UntilShown()
        {
            var store = new InMemorySaveStore();
            var unseen = new UnseenCoins();
            var ledger = new StakeLedger(store, new Wallet(store), unseen);
            Assert.IsTrue(ledger.TakeStake("m1", 100));

            Assert.AreEqual(200, ledger.PayPrize("m1", 1));
            Assert.AreEqual(200, unseen.Amount);

            Assert.AreEqual(0, ledger.PayPrize("m1", 1), "the end seen again pays nothing");
            Assert.AreEqual(200, unseen.Amount, "and adds nothing to show");
        }

        [Test]
        public void AStakeGivenBack_IsNoWin()
        {
            var store = new InMemorySaveStore();
            var unseen = new UnseenCoins();
            var ledger = new StakeLedger(store, new Wallet(store), unseen);
            Assert.IsTrue(ledger.TakeStake("m1", 50));

            ledger.CallOff("m1");

            Assert.AreEqual(0, unseen.Amount, "the match was called off: the stake comes back without a show");
        }
    }
}
