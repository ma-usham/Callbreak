// The Play In-app Updates plugin exists on Android and in the Editor only; elsewhere this
// assembly is empty and the game registers StandInAppUpdates (ProjectLifetimeScope).
#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Updates;
using Google.Play.AppUpdate;
using Google.Play.Common;
using UnityEngine;

namespace Callbreak.Infrastructure.Updates
{
    /// <summary>
    /// Google Play's in-app updates behind <see cref="IAppUpdates"/>, through the Play In-app Updates
    /// plugin (com.google.play.appupdate 1.8.5): <see cref="AppUpdateManager.GetAppUpdateInfo"/> for a
    /// check, and <see cref="AppUpdateManager.StartUpdate"/> with
    /// <see cref="AppUpdateOptions.ImmediateAppUpdateOptions"/> for the full-screen update. Play
    /// answers only an app installed from Google Play (internal app sharing and test tracks count), on
    /// a device with the Play Store; anywhere else the check fails and the player plays on.
    /// <para>
    /// Each of Play's answers starts one update at most, so an update starts from the last check's
    /// answer, which is then spent; with none, Play is asked first. The plugin calls back on the main
    /// thread. It reports an immediate update as gone (cancelled) as soon as the app pauses behind
    /// Play's screen, and when the update installs Play restarts the app; so when the update's task
    /// completes, Play's screen may still be up, or the update still downloading.
    /// <see cref="ForcedUpdate"/> holds the player at "Update required" either way, and brings back an
    /// update still in progress.
    /// </para>
    /// Nothing here throws into the game: a plugin error is logged and becomes a Failed answer or a
    /// completed update task. A check that gets no answer within <see cref="CheckTimeout"/> fails.
    /// </summary>
    public sealed class PlayAppUpdates : IAppUpdates
    {
        /// <summary>How long a check waits for Google Play.</summary>
        public static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(30);

        private const string LogPrefix = "[AppUpdate] ";

        private AppUpdateManager _manager;
        private AppUpdateInfo _info;

        public async Task<AppUpdateCheck> CheckAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var answered = new TaskCompletionSource<AppUpdateCheck>();
            try
            {
                PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> operation = Manager().GetAppUpdateInfo();
                operation.Completed += done => answered.TrySetResult(Read(done));
            }
            catch (Exception e)
            {
                _info = null;
                Debug.LogWarning(LogPrefix + "Google Play could not be asked: " + e.Message);
                answered.TrySetResult(NoAnswer());
            }

            using (ct.Register(() => answered.TrySetCanceled()))
            {
                Task first = await Task.WhenAny(answered.Task, Task.Delay(CheckTimeout, ct));
                if (first != answered.Task && !ct.IsCancellationRequested)
                {
                    Debug.LogWarning(LogPrefix + "No answer from Google Play in " + CheckTimeout.TotalSeconds.ToString("0") + " s.");
                    answered.TrySetResult(NoAnswer());
                }

                return await answered.Task;
            }
        }

        public async Task StartImmediateAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            AppUpdateInfo info = _info;
            _info = null;
            if (info == null)
            {
                // The last answer is spent, or there was none: Play is asked first.
                await CheckAsync(ct);
                info = _info;
                _info = null;
                if (info == null)
                {
                    return;
                }
            }

            var gone = new TaskCompletionSource<bool>();
            try
            {
                AppUpdateOptions immediate = AppUpdateOptions.ImmediateAppUpdateOptions();
                if (info.UpdateAvailability == UpdateAvailability.UpdateNotAvailable || !info.IsUpdateTypeAllowed(immediate))
                {
                    // The plugin would fail such a request and then refuse every later one as "already in progress".
                    Debug.Log(LogPrefix + "Google Play has no update to start: " + info.UpdateAvailability + ".");
                    return;
                }

                AppUpdateRequest request = Manager().StartUpdate(info, immediate);
                request.Completed += done =>
                {
                    // ErrorUserCanceled also when the app merely paused behind Play's screen.
                    Debug.Log(LogPrefix + "Google Play's update request ended: " + done.Status + ", " + done.Error + ".");
                    gone.TrySetResult(true);
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogPrefix + "Google Play's update could not start: " + e.Message);
                gone.TrySetResult(false);
            }

            using (ct.Register(() => gone.TrySetCanceled()))
            {
                await gone.Task;
            }
        }

        /// <summary>Play's answer; its information is kept to start the update from.</summary>
        private AppUpdateCheck Read(PlayAsyncOperation<AppUpdateInfo, AppUpdateErrorCode> done)
        {
            try
            {
                if (!done.IsSuccessful)
                {
                    // E.g. ErrorAppNotOwned (not installed from Google Play) or ErrorPlayStoreNotFound.
                    _info = null;
                    Debug.Log(LogPrefix + "No answer from Google Play: " + done.Error + ".");
                    return NoAnswer();
                }

                AppUpdateInfo info = done.GetResult();
                _info = info;
                AppUpdateAvailability availability = Translate(info.UpdateAvailability);
                if (availability == AppUpdateAvailability.None || availability == AppUpdateAvailability.Failed)
                {
                    Debug.Log(LogPrefix + "Google Play: " + info.UpdateAvailability + ".");
                    return new AppUpdateCheck(availability, false);
                }

                bool immediate = info.IsUpdateTypeAllowed(AppUpdateOptions.ImmediateAppUpdateOptions());
                Debug.Log(LogPrefix + "Google Play: " + info.UpdateAvailability + ", version code " + info.AvailableVersionCode
                    + (immediate ? ", immediate update allowed." : ", immediate update not allowed now."));
                return new AppUpdateCheck(availability, immediate);
            }
            catch (Exception e)
            {
                _info = null;
                Debug.LogWarning(LogPrefix + "Google Play's answer could not be read: " + e.Message);
                return NoAnswer();
            }
        }

        /// <summary>The plugin's manager, made on first use: it adds two objects of its own that outlive scene loads.</summary>
        private AppUpdateManager Manager() => _manager ?? (_manager = new AppUpdateManager());

        private static AppUpdateAvailability Translate(UpdateAvailability availability)
        {
            switch (availability)
            {
                case UpdateAvailability.UpdateNotAvailable:
                    return AppUpdateAvailability.None;

                case UpdateAvailability.UpdateAvailable:
                    return AppUpdateAvailability.Available;

                case UpdateAvailability.DeveloperTriggeredUpdateInProgress:
                    return AppUpdateAvailability.InProgress;

                default:
                    return AppUpdateAvailability.Failed;
            }
        }

        private static AppUpdateCheck NoAnswer() => new AppUpdateCheck(AppUpdateAvailability.Failed, false);
    }
}
#endif
