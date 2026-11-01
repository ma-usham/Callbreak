using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Callbreak.Application.Offers;
using Firebase;
using Firebase.RemoteConfig;
using UnityEngine;

namespace Callbreak.Infrastructure.Services
{
    /// <summary>
    /// <see cref="IFestivalOfferSource"/> on Firebase Remote Config (ADR 067), on Android like the
    /// analytics. The value <c>festival_offers</c> defaults to empty: no offer. At start it reads the
    /// value fetched and activated in an earlier session (Remote Config keeps it on the device), then
    /// fetches, at most once an hour, and activates; a fetch that fails, with no network or no answer,
    /// keeps the value it had. Remote Config's dependency check only looks, never fixes, so it can't
    /// race the analytics' own check-and-fix. Nothing about the config, keys or SDK errors is logged.
    /// </summary>
    public sealed class FirebaseFestivalOfferSource : IFestivalOfferSource, IDisposable
    {
        public const string Key = "festival_offers";

        /// <summary>How long a fetched value is used before the next start fetches again.</summary>
        private const ulong MinimumFetchIntervalMs = 60UL * 60UL * 1000UL;

        private const ulong FetchTimeoutMs = 30UL * 1000UL;

        private FirebaseRemoteConfig _remote;
        private string _json = string.Empty;
        private bool _starting;
        private bool _disposed;

        public event Action Changed;

        public FestivalOffersConfig Config { get; private set; }

        /// <summary>Starts in the background; the offers appear when Remote Config answers.</summary>
        public void Initialize()
        {
            if (_starting || _disposed)
            {
                return;
            }

            _starting = true;
            _ = StartAsync();
        }

        public void Dispose()
        {
            _disposed = true;
            _remote = null;
        }

        private async Task StartAsync()
        {
            try
            {
                // Awaited on the main thread: Unity's synchronization context brings each step back to it.
                DependencyStatus status = await FirebaseApp.CheckDependenciesAsync();
                if (_disposed)
                {
                    return;
                }

                if (status != DependencyStatus.Available)
                {
                    Debug.LogWarning("Festival offers: Firebase is unavailable; there is no offer.");
                    return;
                }

                _remote = FirebaseRemoteConfig.DefaultInstance;
                await _remote.SetDefaultsAsync(new Dictionary<string, object> { { Key, string.Empty } });
                await _remote.SetConfigSettingsAsync(new ConfigSettings
                {
                    MinimumFetchIntervalInMilliseconds = MinimumFetchIntervalMs,
                    FetchTimeoutInMilliseconds = FetchTimeoutMs,
                });
                await _remote.EnsureInitializedAsync();
                Read();

                await _remote.FetchAndActivateAsync();
                Read();
            }
            catch (Exception)
            {
                if (!_disposed)
                {
                    Debug.LogWarning("Festival offers: Remote Config didn't answer; the offers fetched before stay.");
                }
            }
        }

        /// <summary>Reads the active value and says so when it changed.</summary>
        private void Read()
        {
            if (_disposed || _remote == null)
            {
                return;
            }

            string json = _remote.GetValue(Key).StringValue ?? string.Empty;
            if (json == _json)
            {
                return;
            }

            _json = json;
            Config = FestivalOffersJson.Parse(json);
            Changed?.Invoke();
        }
    }
}
