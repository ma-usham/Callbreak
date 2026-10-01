using System;
using Callbreak.Application.Ports;
using UnityEngine;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// <see cref="IOnScreenKeyboard"/>. iOS reports the keyboard through
    /// <c>TouchScreenKeyboard.area</c>; on Android that is always empty, so the height is read from
    /// the window over JNI: the keyboard's inset (Android 11 and later), or else how much of the
    /// window the keyboard leaves hidden below its visible frame. Either is converted from device
    /// pixels to Unity's. A device that says nothing gets the keyboard of a phone held sideways
    /// (60% of the screen) while one is up. Read at most ten times a second; never throws.
    /// </summary>
    public sealed class OnScreenKeyboard : IOnScreenKeyboard, IDisposable
    {
        private const float AssumedShare = 0.6f;
        private const float ReadEverySeconds = 0.1f;

        private readonly RuntimePlatform _platform;
        private AndroidJavaObject _decorView;
        private AndroidJavaObject _frame;
        private int _sdk;
        private int _imeType;
        private bool _androidReady;
        private bool _androidBroken;
        private float _readAt = float.NegativeInfinity;
        private float _height;

        public OnScreenKeyboard()
        {
            _platform = UnityEngine.Application.platform;
        }

        public float Height
        {
            get
            {
                try
                {
                    if (!TouchScreenKeyboard.isSupported || !TouchScreenKeyboard.visible)
                    {
                        _readAt = float.NegativeInfinity;
                        return 0f;
                    }

                    float now = Time.unscaledTime;
                    if (now - _readAt >= ReadEverySeconds)
                    {
                        _readAt = now;
                        _height = Read();
                    }

                    return _height;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[OnScreenKeyboard] {e.Message}");
                    return 0f;
                }
            }
        }

        public void Dispose()
        {
            _decorView?.Dispose();
            _frame?.Dispose();
            _decorView = null;
            _frame = null;
            _androidReady = false;
        }

        private float Read()
        {
            float reported = TouchScreenKeyboard.area.height;
            if (reported > 0f)
            {
                return reported;
            }

            if (_platform == RuntimePlatform.Android && !_androidBroken)
            {
                try
                {
                    float covered = AndroidCovered();
                    if (covered > 0f)
                    {
                        return covered;
                    }
                }
                catch (Exception e)
                {
                    _androidBroken = true;
                    Debug.LogWarning($"[OnScreenKeyboard] The keyboard's height cannot be read; assuming a phone's: {e.Message}");
                }
            }

            return Screen.height * AssumedShare;
        }

        /// <summary>The keyboard's height in Unity's screen pixels, from the window it covers.</summary>
        private float AndroidCovered()
        {
            if (!_androidReady)
            {
                _androidReady = true;
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _sdk = version.GetStatic<int>("SDK_INT");
                }

                // Unity owns and caches the activity object: not disposed here.
                AndroidJavaObject activity = UnityEngine.Android.AndroidApplication.currentActivity;
                using (AndroidJavaObject window = activity.Call<AndroidJavaObject>("getWindow"))
                {
                    _decorView = window.Call<AndroidJavaObject>("getDecorView");
                }

                if (_sdk >= 30)
                {
                    using (var types = new AndroidJavaClass("android.view.WindowInsets$Type"))
                    {
                        _imeType = types.CallStatic<int>("ime");
                    }
                }

                _frame = new AndroidJavaObject("android.graphics.Rect");
            }

            int windowHeight = _decorView.Call<int>("getHeight");
            if (windowHeight <= 0)
            {
                return 0f;
            }

            int covered;
            if (_sdk >= 30)
            {
                using (AndroidJavaObject insets = _decorView.Call<AndroidJavaObject>("getRootWindowInsets"))
                using (AndroidJavaObject ime = insets?.Call<AndroidJavaObject>("getInsets", _imeType))
                {
                    covered = ime != null ? ime.Get<int>("bottom") : 0;
                }
            }
            else
            {
                _decorView.Call("getWindowVisibleDisplayFrame", _frame);
                covered = windowHeight - _frame.Get<int>("bottom");
            }

            return Mathf.Max(0, covered) * (Screen.height / (float)windowHeight);
        }
    }
}
