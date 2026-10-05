using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// No Ads where there is no store (the Editor, the tests and desktop builds): for sale at once at
    /// the owner's price; a purchase is granted at once, raising Changed, and then reads as owned; the
    /// grant lives in memory only, so a new stand-in starts without it; a purchase with a cancelled
    /// token is cancelled, as the port says, without throwing at the caller.
    /// </summary>
    public sealed class StandInNoAdsTests
    {
        [Test]
        public void IsForSale_AtOnce_AtTheOwnersPrice()
        {
            INoAds noAds = new StandInNoAds();

            Assert.IsTrue(noAds.CanBuy);
            Assert.IsFalse(noAds.Owned);
            Assert.AreEqual("$0.99", noAds.Price);
        }

        [Test]
        public void Buy_GrantsAtOnce_AndRaisesChanged()
        {
            INoAds noAds = new StandInNoAds();
            int changed = 0;
            noAds.Changed += () => changed++;

            Task<NoAdsPurchase> buy = noAds.BuyAsync(CancellationToken.None);

            Assert.AreEqual(TaskStatus.RanToCompletion, buy.Status);
            Assert.AreEqual(NoAdsPurchase.Bought, buy.Result);
            Assert.IsTrue(noAds.Owned);
            Assert.IsFalse(noAds.CanBuy);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void ASecondBuy_IsAlreadyOwned_AndChangesNothing()
        {
            INoAds noAds = new StandInNoAds();
            noAds.BuyAsync(CancellationToken.None);
            int changed = 0;
            noAds.Changed += () => changed++;

            Assert.AreEqual(NoAdsPurchase.AlreadyOwned, noAds.BuyAsync(CancellationToken.None).Result);
            Assert.AreEqual(0, changed);
        }

        [Test]
        public void TheGrant_LivesInMemoryOnly()
        {
            new StandInNoAds().BuyAsync(CancellationToken.None);

            Assert.IsFalse(new StandInNoAds().Owned, "nothing is saved: every run starts with the ads");
        }

        [Test]
        public void Buy_WithACancelledToken_IsCancelled_WithoutThrowingAtTheCaller_AndGrantsNothing()
        {
            INoAds noAds = new StandInNoAds();
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Task<NoAdsPurchase> buy = null;

                Assert.DoesNotThrow(() => buy = noAds.BuyAsync(cancelled.Token));
                Assert.IsTrue(buy.IsCanceled);
                Assert.IsFalse(noAds.Owned);
            }
        }
    }
}
