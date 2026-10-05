using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The privacy options where no ad network asks for consent: the Editor, the tests and desktop
    /// builds, whose ads are a stand-in too (ProjectLifetimeScope). Never required, so Settings
    /// shows no entry point; a show has no form to open and completes at once.
    /// </summary>
    public sealed class StandInAdPrivacy : IAdPrivacy
    {
        public bool IsRequired => false;

        /// <summary>Never raised: nothing here ever changes.</summary>
        public event Action Changed
        {
            add { }
            remove { }
        }

        public Task ShowAsync(CancellationToken ct) => ct.IsCancellationRequested ? Task.FromCanceled(ct) : Task.CompletedTask;
    }
}
