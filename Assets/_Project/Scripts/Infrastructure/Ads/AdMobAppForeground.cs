using System;
using Callbreak.Application.Ports;
using GoogleMobileAds.Api;
using GoogleMobileAds.Common;
using UnityEngine;

namespace Callbreak.Infrastructure.Ads
{
    /// <summary>
    /// <see cref="IAppForeground"/> on phones: Google Mobile Ads' app state
    /// (<see cref="AppStateEventNotifier"/>), which follows the whole app going to the background and
    /// coming back. A full-screen ad of the game's own is part of the app, so closing one is not a
    /// return, as Unity's pause would make it. The plugin may call on another thread, so each move is
    /// handed to the main thread, the move to the background with the time it happened; a listener
    /// that throws is logged and never reaches the plugin.
    /// </summary>
    public sealed class AdMobAppForeground : IAppForeground, IDisposable
    {
        private bool _disposed;

        public AdMobAppForeground()
        {
            try
            {
                AppStateEventNotifier.AppStateChanged += OnAppStateChanged;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public event Action<DateTime> WentToBackground;

        public event Action CameToForeground;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                AppStateEventNotifier.AppStateChanged -= OnAppStateChanged;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void OnAppStateChanged(AppState state)
        {
            // Read here, as it happens: the main thread sleeps while the game is away, so the move to
            // the background reaches it only on the return.
            DateTime atUtc = DateTime.UtcNow;
            AdMobRewardedAdNetwork.OnMainThread(() =>
            {
                if (_disposed)
                {
                    return;
                }

                if (state == AppState.Foreground)
                {
                    CameToForeground?.Invoke();
                }
                else
                {
                    WentToBackground?.Invoke(atUtc);
                }
            });
        }
    }
}
