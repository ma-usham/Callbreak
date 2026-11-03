using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Reviews
{
    /// <summary>
    /// The store where there is no Google Play to ask: the Editor, the tests and desktop builds
    /// (ProjectLifetimeScope). It shows no card and completes at once; tests count its requests.
    /// </summary>
    public sealed class StandInStoreReview : IStoreReview
    {
        /// <summary>How many times the store was asked for its review card.</summary>
        public int Requests { get; private set; }

        public Task RequestAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled(ct);
            }

            Requests++;
            return Task.CompletedTask;
        }
    }
}
