using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Updates
{
    /// <summary>
    /// The store where there is no Google Play to ask: the Editor, the tests and desktop builds
    /// (ProjectLifetimeScope). It answers at once, so the launch never waits for it, and never has a
    /// newer version, so the game never asks for an update there. A test can give it one
    /// (<see cref="Availability"/>) to see "Update required": its update then goes at once without
    /// updating, as when the player backs out of Google Play's screen.
    /// </summary>
    public sealed class StandInAppUpdates : IAppUpdates
    {
        /// <summary>What every check answers: no newer version unless a test says otherwise.</summary>
        public AppUpdateAvailability Availability { get; set; } = AppUpdateAvailability.None;

        /// <summary>Whether the store would install a newer version now; true unless a test says otherwise.</summary>
        public bool ImmediateAllowed { get; set; } = true;

        /// <summary>How many times the store was asked.</summary>
        public int Checks { get; private set; }

        /// <summary>How many updates were started.</summary>
        public int Starts { get; private set; }

        public Task<AppUpdateCheck> CheckAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled<AppUpdateCheck>(ct);
            }

            Checks++;
            return Task.FromResult(new AppUpdateCheck(Availability, ImmediateAllowed));
        }

        public Task StartImmediateAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
            {
                return Task.FromCanceled(ct);
            }

            Starts++;
            return Task.CompletedTask;
        }
    }
}
