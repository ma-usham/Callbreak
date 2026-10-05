using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Updates;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The store where there is no Google Play to ask (the Editor, the tests, desktop builds): it
    /// answers at once and never has a newer version, so the launch never waits for it. A test can make
    /// it have one, to see "Update required"; its update then goes at once without updating.
    /// </summary>
    public sealed class StandInAppUpdatesTests
    {
        [Test]
        public void NeverHasANewerVersion_AndAnswersAtOnce()
        {
            var store = new StandInAppUpdates();

            Task<AppUpdateCheck> check = store.CheckAsync(CancellationToken.None);

            Assert.IsTrue(check.IsCompleted, "at once: the launch never waits for it");
            Assert.AreEqual(AppUpdateAvailability.None, check.Result.Availability);
            Assert.AreEqual(1, store.Checks);
            Assert.AreEqual(0, store.Starts);
        }

        [Test]
        public void ATest_CanGiveItANewerVersion_WhoseUpdateGoesWithoutUpdating()
        {
            var store = new StandInAppUpdates { Availability = AppUpdateAvailability.Available };

            AppUpdateCheck answer = store.CheckAsync(CancellationToken.None).Result;
            Task update = store.StartImmediateAsync(CancellationToken.None);

            Assert.AreEqual(AppUpdateAvailability.Available, answer.Availability);
            Assert.IsTrue(answer.ImmediateAllowed);
            Assert.IsTrue(update.IsCompleted, "as if the player backed out of Play's screen at once");
            Assert.AreEqual(1, store.Starts);
        }

        [Test]
        public void ACancelledToken_IsCancelled()
        {
            var store = new StandInAppUpdates();
            var cancelled = new CancellationToken(canceled: true);

            Assert.IsTrue(store.CheckAsync(cancelled).IsCanceled);
            Assert.IsTrue(store.StartImmediateAsync(cancelled).IsCanceled);
            Assert.AreEqual(0, store.Checks);
            Assert.AreEqual(0, store.Starts);
        }
    }
}
