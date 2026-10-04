#if UNITY_ANDROID
using System.IO;
using Callbreak.Infrastructure.Ads;
using UnityEditor.Android;
using UnityEditor.Build;

namespace Callbreak.Editor
{
    /// <summary>
    /// Checks the Gradle project Unity generated for an Android build, after the plugins' own Gradle
    /// steps: AdMob's Android libraries (the Google Mobile Ads SDK and Google's consent SDK, which the
    /// External Dependency Manager writes into Assets/Plugins/Android/mainTemplate.gradle) and the
    /// AdMob app id in the plugin's library manifest. A build without them would show no ads, or
    /// crash, on a phone, so it stops here and says what to do.
    /// </summary>
    public sealed class AndroidAdMobGradle : IPostGenerateGradleAndroidProject
    {
        /// <summary>The Maven libraries unityLibrary must depend on (any version).</summary>
        public static readonly string[] Libraries =
        {
            "com.google.android.gms:play-services-ads:",
            "com.google.android.ump:user-messaging-platform:",
        };

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // path is the unityLibrary module; the Gradle project's root holds it.
            string problem = Check(Path.GetFullPath(Path.Combine(path, "..")));
            if (problem != null)
            {
                throw new BuildFailedException("[Callbreak] AdMob is not set up in this Android build: " + problem);
            }
        }

        /// <summary>What is missing from the Gradle project at <paramref name="root"/>; null when nothing is.</summary>
        public static string Check(string root)
        {
            string script = Path.Combine(root, "unityLibrary", "build.gradle");
            string text = File.Exists(script) ? File.ReadAllText(script) : string.Empty;
            foreach (string library in Libraries)
            {
                if (!text.Contains(library))
                {
                    return "unityLibrary/build.gradle has no " + library + " Keep Custom Main Gradle Template on and run "
                        + "Assets > External Dependency Manager > Android Resolver > Force Resolve (Docs/Setup/AdMob.md).";
                }
            }

            string manifest = Path.Combine(root, "unityLibrary", "GoogleMobileAdsPlugin.androidlib", "AndroidManifest.xml");
            if (!File.Exists(manifest) || !File.ReadAllText(manifest).Contains(AdMobIds.AndroidAppId))
            {
                return "the AdMob app id is not in GoogleMobileAdsPlugin.androidlib/AndroidManifest.xml (Callbreak/Ads/Apply AdMob Settings).";
            }

            return null;
        }
    }
}
#endif
