using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The privacy options where no ad network asks for consent (the Editor, the tests and desktop
    /// builds): never required, so Settings shows no entry point; nothing about them ever changes;
    /// a show has no form to open and completes at once; a show with a cancelled token is
    /// cancelled, as the port says, without throwing at the caller.
    /// </summary>
    public sealed class StandInAdPrivacyTests
    {
        [Test]
        public void IsNeverRequired()
        {
            IAdPrivacy privacy = new StandInAdPrivacy();

            Assert.IsFalse(privacy.IsRequired);
        }

        [Test]
        public void Changed_CanBeListenedTo_AndIsNeverRaised()
        {
            IAdPrivacy privacy = new StandInAdPrivacy();
            int heard = 0;
            Action listener = () => heard++;

            Assert.DoesNotThrow(() => privacy.Changed += listener);
            privacy.ShowAsync(CancellationToken.None);
            Assert.DoesNotThrow(() => privacy.Changed -= listener);

            Assert.AreEqual(0, heard, "nothing changes");
            Assert.IsFalse(privacy.IsRequired, "not even after a show");
        }

        [Test]
        public void Show_CompletesAtOnce()
        {
            Task shown = new StandInAdPrivacy().ShowAsync(CancellationToken.None);

            Assert.AreEqual(TaskStatus.RanToCompletion, shown.Status);
        }

        [Test]
        public void Show_WithACancelledToken_IsCancelled_WithoutThrowingAtTheCaller()
        {
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Task shown = null;

                Assert.DoesNotThrow(() => shown = new StandInAdPrivacy().ShowAsync(cancelled.Token));
                Assert.IsTrue(shown.IsCanceled);
            }
        }
    }
}
