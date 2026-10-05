using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The ads between matches where there is no ad network: the Editor, the tests and desktop builds
    /// (ProjectLifetimeScope). It never has an ad, so the player goes on from the match results at
    /// once and nothing is shown; <see cref="BetweenMatchAds"/> still decides when one would be due,
    /// as on phones.
    /// </summary>
    public sealed class StandInInterstitialAds : IInterstitialAds
    {
        public bool IsAvailable => false;

        public Task<bool> ShowAsync(CancellationToken ct) => ct.IsCancellationRequested ? Task.FromCanceled<bool>(ct) : Task.FromResult(false);
    }
}
