using System;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;
using UnityEngine;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// <see cref="IHaptics"/> (GDD §14): Light (your turn, a trick won), Warning (double light:
    /// illegal tap, timer at 5 s) and Success (call met). Nothing plays while the Vibration setting
    /// is off. Android goes through Vibrator/VibrationEffect over JNI: the predefined click, double
    /// click and a two-step waveform on API 29+, amplitude waveforms on 26-28, plain patterns
    /// before. iOS only has <c>Handheld.Vibrate()</c>, a long buzz, so it plays Warning and Success
    /// and skips Light. Other platforms do nothing. Never throws; after a JNI failure it falls back to
    /// <c>Handheld.Vibrate()</c> for the stronger patterns. (That call also makes Unity add the
    /// VIBRATE permission to the Android manifest.)
    /// </summary>
    public sealed class HapticsService : IHaptics, IDisposable
    {
        private const int EffectClick = 0;
        private const int EffectDoubleClick = 1;

        private readonly ISettingsService _settings;
        private readonly RuntimePlatform _platform;
        private AndroidJavaObject _vibrator;
        private int _sdk;
        private bool _androidReady;
        private bool _androidBroken;
        private bool _warned;

        public HapticsService(ISettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _platform = UnityEngine.Application.platform;
        }

        public void Play(HapticPattern pattern)
        {
            try
            {
                if (!_settings.Vibration)
                {
                    return;
                }

                if (_platform == RuntimePlatform.Android)
                {
                    PlayAndroid(pattern);
                }
                else if (_platform == RuntimePlatform.IPhonePlayer && pattern != HapticPattern.Light)
                {
                    Handheld.Vibrate();
                }
            }
            catch (Exception e)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogWarning($"[HapticsService] Vibration failed: {e.Message}");
                }
            }
        }

        public void Dispose()
        {
            _vibrator?.Dispose();
            _vibrator = null;
            _androidReady = false;
        }

        private void PlayAndroid(HapticPattern pattern)
        {
            if (!_androidBroken)
            {
                try
                {
                    if (EnsureVibrator())
                    {
                        VibrateAndroid(pattern);
                    }

                    return;
                }
                catch (Exception e)
                {
                    _androidBroken = true;
                    Debug.LogWarning($"[HapticsService] Android vibrator unavailable, using the default buzz: {e.Message}");
                }
            }

            if (pattern != HapticPattern.Light)
            {
                Handheld.Vibrate();
            }
        }

        private bool EnsureVibrator()
        {
            if (_androidReady)
            {
                return _vibrator != null;
            }

            _androidReady = true;
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                _sdk = version.GetStatic<int>("SDK_INT");
            }

            // Unity owns and caches the context object: not disposed here.
            _vibrator = UnityEngine.Android.AndroidApplication.currentContext.Call<AndroidJavaObject>("getSystemService", "vibrator");

            if (_vibrator != null && !_vibrator.Call<bool>("hasVibrator"))
            {
                _vibrator.Dispose();
                _vibrator = null;
            }

            return _vibrator != null;
        }

        private void VibrateAndroid(HapticPattern pattern)
        {
            // Timings alternate off/on in milliseconds; amplitudes are 0-255.
            long[] timings;
            int[] amplitudes;
            switch (pattern)
            {
                case HapticPattern.Light:
                    timings = new long[] { 0, 20 };
                    amplitudes = new[] { 0, 90 };
                    break;
                case HapticPattern.Warning:
                    timings = new long[] { 0, 25, 70, 25 };
                    amplitudes = new[] { 0, 140, 0, 140 };
                    break;
                default:
                    timings = new long[] { 0, 30, 60, 60 };
                    amplitudes = new[] { 0, 110, 0, 255 };
                    break;
            }

            if (_sdk >= 26)
            {
                using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                using (AndroidJavaObject effect = _sdk >= 29 && pattern != HapticPattern.Success
                           ? effectClass.CallStatic<AndroidJavaObject>("createPredefined", pattern == HapticPattern.Light ? EffectClick : EffectDoubleClick)
                           : effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1))
                {
                    _vibrator.Call("vibrate", effect);
                }

                return;
            }

            _vibrator.Call("vibrate", timings, -1);
        }
    }
}
