using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The ads between matches where there is no ad network (the Editor, the tests and desktop
    /// builds): there is never an ad, so a show is over at once and shows nothing; a show with a
    /// cancelled token is cancelled, as the port says, without throwing at the caller.
    /// </summary>
    public sealed class StandInInterstitialAdsTests
    {
        [Test]
        public void NeverHasAnAd()
        {
            IInterstitialAds ads = new StandInInterstitialAds();

            Assert.IsFalse(ads.IsAvailable);
        }

        [Test]
        public void Show_IsOverAtOnce_AndShowsNothing()
        {
            Task<bool> shown = new StandInInterstitialAds().ShowAsync(CancellationToken.None);

            Assert.AreEqual(TaskStatus.RanToCompletion, shown.Status);
            Assert.IsFalse(shown.Result);
        }

        [Test]
        public void Show_WithACancelledToken_IsCancelled_WithoutThrowingAtTheCaller()
        {
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Task<bool> shown = null;

                Assert.DoesNotThrow(() => shown = new StandInInterstitialAds().ShowAsync(cancelled.Token));
                Assert.IsTrue(shown.IsCanceled);
            }
        }
    }
}
