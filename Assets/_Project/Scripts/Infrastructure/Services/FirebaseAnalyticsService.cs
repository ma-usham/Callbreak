using System;
using System.Collections.Generic;
using Callbreak.Application.Ports;
using Firebase;
using Firebase.Analytics;
using Firebase.Extensions;
using UnityEngine;
using AnalyticsSdk = Firebase.Analytics.FirebaseAnalytics;

namespace Callbreak.Infrastructure.Services
{
    /// <summary>
    /// Firebase Analytics on Android, behind the existing product analytics port. The scope starts
    /// dependency checking without blocking boot. Events before readiness are dropped, not replayed.
    /// The Android manifest disables collection and advertising consent before native auto-start.
    /// </summary>
    public sealed class FirebaseAnalyticsService : IAnalytics, IDisposable
    {
        private FirebaseApp _app;
        private bool _starting;
        private bool _ready;
        private bool _enabled;
        private bool _disposed;

        public void Initialize()
        {
            if (_starting || _disposed) { return; }
            _starting = true;
            try
            {
                FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
                {
                    if (_disposed) { return; }
                    if (task.IsCanceled || task.IsFaulted || task.Result != DependencyStatus.Available)
                    {
                        // Observe faults without logging configuration, keys or SDK exception text.
                        if (task.IsFaulted) { _ = task.Exception; }
                        Debug.LogWarning("Product analytics: Firebase dependencies are unavailable; events are blocked.");
                        return;
                    }

                    try
                    {
                        _app = FirebaseApp.DefaultInstance;
                        _ready = true;
                        ApplyCollection();
                    }
                    catch (Exception)
                    {
                        _ready = false;
                        _enabled = false;
                        Debug.LogWarning("Product analytics: Firebase did not initialize; events are blocked.");
                    }
                });
            }
            catch (Exception)
            {
                Debug.LogWarning("Product analytics: Firebase dependency checking did not start; events are blocked.");
            }
        }

        public void SetCollectionEnabled(bool enabled)
        {
            if (_disposed) { return; }
            _enabled = enabled;
            if (_ready) { ApplyCollection(); }
        }

        public void Track(string eventName, params (string key, object value)[] parameters)
        {
            if (!_ready || !_enabled || _disposed) { return; }
            var converted = new Parameter[parameters?.Length ?? 0];
            for (int i = 0; i < converted.Length; i++)
            {
                string key = parameters[i].key;
                switch (parameters[i].value)
                {
                    case string text: converted[i] = new Parameter(key, text); break;
                    case bool flag: converted[i] = new Parameter(key, flag ? 1L : 0L); break;
                    case int integer: converted[i] = new Parameter(key, (long)integer); break;
                    case long number: converted[i] = new Parameter(key, number); break;
                    default: return;
                }
            }

            AnalyticsSdk.LogEvent(eventName, converted);
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            try { SetCollectionEnabled(false); }
            catch (Exception) { /* Disposal must not interrupt the game's shutdown. */ }
            _disposed = true;
            _ready = false;
            _app?.Dispose();
            _app = null;
        }

        private void ApplyCollection()
        {
            // Disable before updating consent, so a failed consent update never enables collection.
            AnalyticsSdk.SetAnalyticsCollectionEnabled(false);
            AnalyticsSdk.SetConsent(new Dictionary<ConsentType, ConsentStatus>
            {
                { ConsentType.AnalyticsStorage, _enabled ? ConsentStatus.Granted : ConsentStatus.Denied },
                { ConsentType.AdStorage, ConsentStatus.Denied },
                { ConsentType.AdUserData, ConsentStatus.Denied },
                { ConsentType.AdPersonalization, ConsentStatus.Denied },
            });
            if (_enabled) { AnalyticsSdk.SetAnalyticsCollectionEnabled(true); }
        }
    }
}
