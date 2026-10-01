using System;
using Callbreak.Application.Ports;
using UnityEngine;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// <see cref="IShareService"/> (GDD §11 screen 04 "Invite Friends"). On Android it opens the
    /// system chooser for an ACTION_SEND text intent. Everywhere else, and if Android refuses, it
    /// returns false and the caller offers the text another way. The JNI code compiles on every
    /// platform and only runs on an Android player.
    /// </summary>
    public sealed class ShareService : IShareService
    {
        private readonly bool _android;

        public ShareService()
        {
            _android = UnityEngine.Application.platform == RuntimePlatform.Android;
        }

        public bool ShareText(string title, string text)
        {
            if (!_android)
            {
                return false;
            }

            try
            {
                OpenAndroidChooser(title ?? string.Empty, text ?? string.Empty);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ShareService] The share sheet failed: {e.Message}");
                return false;
            }
        }

        private static void OpenAndroidChooser(string title, string text)
        {
            using (var intentClass = new AndroidJavaClass("android.content.Intent"))
            using (var intent = new AndroidJavaObject("android.content.Intent"))
            {
                // The builder methods return the intent; dispose each returned reference.
                using (intent.Call<AndroidJavaObject>("setAction", intentClass.GetStatic<string>("ACTION_SEND")))
                using (intent.Call<AndroidJavaObject>("setType", "text/plain"))
                using (intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_SUBJECT"), title))
                using (intent.Call<AndroidJavaObject>("putExtra", intentClass.GetStatic<string>("EXTRA_TEXT"), text))
                using (var chooser = intentClass.CallStatic<AndroidJavaObject>("createChooser", intent, title))
                {
                    // Unity owns and caches the activity object: not disposed here.
                    UnityEngine.Android.AndroidApplication.currentActivity.Call("startActivity", chooser);
                }
            }
        }
    }
}
