using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using GoogleMobileAds.Common;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace Callbreak.Infrastructure.Ads
{
    /// <summary>
    /// AdMob's privacy options behind <see cref="IAdPrivacy"/>, through Google's User Messaging
    /// Platform (UMP): the entry point is required while
    /// <see cref="ConsentInformation.PrivacyOptionsRequirementStatus"/> says so. That is unknown,
    /// and so not required, until the consent information has been updated:
    /// <see cref="AdMobRewardedAdNetwork"/> updates it when the ads start, at every launch, and calls
    /// <see cref="Refresh"/> after the update and after the consent form. A show opens Google's
    /// privacy options form and refreshes once it has closed, as the player's choice there may
    /// change what UMP says.
    /// <para>
    /// The plugin calls back on other threads, so the form's callback is handed to the main thread
    /// through its <see cref="MobileAdsEventExecutor"/>, like the network's. Nothing here throws into
    /// the game: an SDK error is logged, and a form that can't be shown ends the show.
    /// </para>
    /// </summary>
    public sealed class AdMobAdPrivacy : IAdPrivacy
    {
        private PrivacyOptionsRequirementStatus _status = PrivacyOptionsRequirementStatus.Unknown;
        private Task _form;

        public bool IsRequired => _status == PrivacyOptionsRequirementStatus.Required;

        public event Action Changed;

        /// <summary>
        /// Reads what UMP now says about the entry point, on the main thread once the consent
        /// information was updated or a form closed, and raises <see cref="Changed"/> when that
        /// became known or changed.
        /// </summary>
        public void Refresh()
        {
            PrivacyOptionsRequirementStatus status;
            try
            {
                status = ConsentInformation.PrivacyOptionsRequirementStatus;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return;
            }

            if (status == _status)
            {
                return;
            }

            _status = status;
            Debug.Log(AdMobRewardedAdNetwork.LogPrefix + "Privacy options: " + status + ".");
            try
            {
                Changed?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public async Task ShowAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // One form at a time: a second tap waits for the one that is up.
            if (_form == null || _form.IsCompleted)
            {
                _form = ShowForm();
            }

            Task form = _form;
            var cancelled = new TaskCompletionSource<bool>();
            using (ct.Register(() => cancelled.TrySetResult(true)))
            {
                await Task.WhenAny(form, cancelled.Task);
            }

            ct.ThrowIfCancellationRequested();
        }

        /// <summary>Shows Google's privacy options form; completes once it has closed, or could not be shown.</summary>
        private Task ShowForm()
        {
            var closed = new TaskCompletionSource<bool>();
            try
            {
                MobileAdsEventExecutor.Initialize();
                ConsentForm.ShowPrivacyOptionsForm(formError => AdMobRewardedAdNetwork.OnMainThread(
                    () =>
                    {
                        if (formError != null)
                        {
                            Debug.LogWarning(AdMobRewardedAdNetwork.LogPrefix + "Privacy options form not shown: " + AdMobRewardedAdNetwork.Describe(formError));
                        }

                        Refresh();
                        closed.TrySetResult(true);
                    },
                    () => closed.TrySetResult(false)));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                closed.TrySetResult(false);
            }

            return closed.Task;
        }
    }
}
