using System;
using Callbreak.Application.Recording;
using UnityEngine;

namespace Callbreak.Infrastructure.Recording
{
    /// <summary>
    /// Smartlook behind <see cref="ISessionRecorder"/> (ADR 044): the Smartlook Android SDK 1.8.7, the
    /// "unity-lite-support" build that Smartlook's Unity package 1.2 ships (vendored in
    /// Assets/Smartlook/SmartlookAnalytics/Android, with its Kotlin dependency for the External
    /// Dependency Manager), called over JNI on the main thread.
    /// <para>
    /// The calls are the ones Smartlook's Unity docs name (Setup, StartRecording, StopRecording and
    /// SetRenderingMode with no_rendering, their way to hide a sensitive screen in Unity, where parts of
    /// the screen can't be masked), made on the SDK class directly rather than through the package's
    /// C# bridge (SmartlookUnity.Smartlook). That bridge's SetRenderingMode always sends "native" on
    /// Android (it compares an int with a boxed enum), and its StartFullscreenSensitiveMode calls a
    /// method the 1.8.7 SDK no longer has, so neither could hide a screen; the bridge itself is left
    /// out.
    /// </para>
    /// The first start sets the SDK up without recording, puts the rendering mode in place, then
    /// starts, so a hidden screen is hidden from the first frame; later starts only start. Nothing
    /// here throws into the game: an SDK error (no SDK in the build, a bad key) is logged without the
    /// key and becomes a false or nothing. Android only; the game builds it on Android phones only.
    /// </summary>
    public sealed class SmartlookSessionRecorder : ISessionRecorder, IDisposable
    {
        internal const string LogPrefix = "[Smartlook] ";

        private const string SdkClass = "com.smartlook.sdk.smartlook.Smartlook";
        private const string NativeRendering = "native";
        private const string NoRendering = "no_rendering";

        private readonly string _projectKey;
        private AndroidJavaClass _sdk;
        private bool _setUp;

        /// <param name="projectKey">The Smartlook project key (<see cref="SmartlookProject.ProjectKey"/>); with none, nothing ever starts.</param>
        public SmartlookSessionRecorder(string projectKey)
        {
            _projectKey = projectKey ?? string.Empty;
        }

        public bool Start(bool hidden)
        {
            if (_projectKey.Length == 0)
            {
                Debug.LogWarning(LogPrefix + "No project key, so no recording.");
                return false;
            }

            try
            {
                AndroidJavaClass sdk = Sdk();
                if (!_setUp)
                {
                    sdk.CallStatic("setup", _projectKey);
                    _setUp = true;
                }

                sdk.CallStatic("setRenderingMode", hidden ? NoRendering : NativeRendering);
                sdk.CallStatic("startRecording");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogPrefix + "Recording did not start: " + Scrub(e.Message));
                return false;
            }
        }

        public void Stop()
        {
            if (!_setUp)
            {
                return;
            }

            try
            {
                Sdk().CallStatic("stopRecording");
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogPrefix + "Recording did not stop: " + Scrub(e.Message));
            }
        }

        public bool SetHidden(bool hidden)
        {
            if (!_setUp)
            {
                return false;
            }

            try
            {
                Sdk().CallStatic("setRenderingMode", hidden ? NoRendering : NativeRendering);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning(LogPrefix + "The screen could not be " + (hidden ? "hidden" : "shown again") + ": " + Scrub(e.Message));
                return false;
            }
        }

        public void Dispose()
        {
            _sdk?.Dispose();
            _sdk = null;
        }

        private AndroidJavaClass Sdk() => _sdk ?? (_sdk = new AndroidJavaClass(SdkClass));

        /// <summary>An SDK message without the project key, should the SDK have put it in.</summary>
        private string Scrub(string message)
        {
            if (string.IsNullOrEmpty(message) || _projectKey.Length == 0)
            {
                return message;
            }

            return message.Replace(_projectKey, "<project key>");
        }
    }
}
