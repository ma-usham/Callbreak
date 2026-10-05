using System;
using System.Threading;
using System.Threading.Tasks;

namespace Callbreak.Application.Updates
{
    /// <summary>What the store says about a newer version of the app.</summary>
    public enum AppUpdateAvailability
    {
        /// <summary>No answer: no Play Store, an app not installed from it, offline, or an error.</summary>
        Failed,

        /// <summary>This is the newest version.</summary>
        None,

        /// <summary>A newer version is out.</summary>
        Available,

        /// <summary>An update this app started is still downloading or installing: the player left the store's screen.</summary>
        InProgress,
    }

    /// <summary>One answer of the store.</summary>
    public sealed class AppUpdateCheck
    {
        public AppUpdateCheck(AppUpdateAvailability availability, bool immediateAllowed)
        {
            Availability = availability;
            ImmediateAllowed = immediateAllowed;
        }

        public AppUpdateAvailability Availability { get; }

        /// <summary>The store would install a newer version now, full screen (Google Play may not, e.g. on low storage).</summary>
        public bool ImmediateAllowed { get; }
    }

    /// <summary>
    /// The app store's in-app updates (port), as <see cref="ForcedUpdate"/> drives them: ask whether
    /// a newer version is out, then start the store's full-screen immediate update. On Android it is
    /// Google Play's; in the Editor, in the tests and on desktop <see cref="StandInAppUpdates"/> never
    /// has one. Every task completes on the main thread; nothing throws but for the
    /// <see cref="OperationCanceledException"/> of a cancelled token: a store that can't be asked is a
    /// <see cref="AppUpdateAvailability.Failed"/> answer.
    /// </summary>
    public interface IAppUpdates
    {
        /// <summary>Asks the store whether a newer version is out.</summary>
        Task<AppUpdateCheck> CheckAsync(CancellationToken ct);

        /// <summary>
        /// Starts the store's full-screen immediate update for the newer version the last check found,
        /// or brings back the update in progress. When the update installs, the store restarts the app
        /// and this never completes. It completes once the store's screen has gone without updating
        /// (the player backed out, or the update failed), or when the update could not start.
        /// </summary>
        Task StartImmediateAsync(CancellationToken ct);
    }
}
