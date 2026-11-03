// The Play In-app Review plugin exists on Android and in the Editor only; elsewhere this
// assembly is empty and the game registers StandInStoreReview (ProjectLifetimeScope).
#if UNITY_ANDROID || UNITY_EDITOR
using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Reviews;
using Google.Play.Common;
using Google.Play.Review;
using UnityEngine;

namespace Callbreak.Infrastructure.Reviews
{
    /// <summary>
    /// Google Play's in-app review behind <see cref="IStoreReview"/>, through the Play In-app Review
    /// plugin (com.google.play.review 1.8.4): <see cref="ReviewManager.RequestReviewFlow"/> for the
    /// card, then <see cref="ReviewManager.LaunchReviewFlow"/> to show it (ADR 080). Play answers only
    /// an app installed from Google Play (internal app sharing and test tracks count), and its quota
    /// decides whether the card shows; either way the launch completes, without saying whether the
    /// player rated. The plugin calls back on the main thread.
    /// Nothing here throws into the game: a plugin error is logged and the request completes. A
    /// request for the card that gets no answer within <see cref="RequestTimeout"/> completes it too.
    /// </summary>
    public sealed class PlayStoreReview : IStoreReview
    {
        /// <summary>How long the request for the card waits for Google Play.</summary>
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

        private const string LogPrefix = "[Review] ";

        private ReviewManager _manager;

        public async Task RequestAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            PlayReviewInfo card = await RequestCardAsync(ct);
            if (card == null)
            {
                return;
            }

            var gone = new TaskCompletionSource<bool>();
            try
            {
                PlayAsyncOperation<VoidResult, ReviewErrorCode> launch = Manager().LaunchReviewFlow(card);
                launch.Completed += done =>
                {
                    // Also when Play chose not to show the card: it never says.
                    if (done.Error != ReviewErrorCode.NoError)
                    {
                        Debug.LogWarning(LogPrefix + "Google Play's review card could not show: " + done.Error + ".");
                    }

                    gone.TrySetResult(true);
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogPrefix + "Google Play's review card could not show: " + e.Message);
                gone.TrySetResult(false);
            }

            using (ct.Register(() => gone.TrySetCanceled()))
            {
                await gone.Task;
            }
        }

        /// <summary>Play's review card to show, or null when Play gave none.</summary>
        private async Task<PlayReviewInfo> RequestCardAsync(CancellationToken ct)
        {
            var answered = new TaskCompletionSource<PlayReviewInfo>();
            try
            {
                PlayAsyncOperation<PlayReviewInfo, ReviewErrorCode> request = Manager().RequestReviewFlow();
                request.Completed += done =>
                {
                    if (done.Error != ReviewErrorCode.NoError)
                    {
                        // E.g. PlayStoreNotFound, or not installed from Google Play.
                        Debug.LogWarning(LogPrefix + "No review card from Google Play: " + done.Error + ".");
                        answered.TrySetResult(null);
                        return;
                    }

                    answered.TrySetResult(done.GetResult());
                };
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogPrefix + "Google Play could not be asked: " + e.Message);
                answered.TrySetResult(null);
            }

            using (ct.Register(() => answered.TrySetCanceled()))
            {
                Task first = await Task.WhenAny(answered.Task, Task.Delay(RequestTimeout, ct));
                if (first != answered.Task && !ct.IsCancellationRequested)
                {
                    Debug.LogWarning(LogPrefix + "No answer from Google Play in " + RequestTimeout.TotalSeconds.ToString("0") + " s.");
                    answered.TrySetResult(null);
                }

                return await answered.Task;
            }
        }

        /// <summary>The plugin's manager, made on first use.</summary>
        private ReviewManager Manager() => _manager ?? (_manager = new ReviewManager());
    }
}
#endif
