using System;
using System.Reflection;
using Callbreak.Infrastructure.Ads;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Writes the AdMob app id (<see cref="AdMobIds.AndroidAppId"/>) into the Google Mobile Ads
    /// plugin's settings, <see cref="SettingsPath"/>, where the plugin keeps it and from where its own
    /// build step writes it into the Android manifest. The id lives in code, so every machine builds
    /// with the same one: before each Android and iOS build (ahead of the plugin's steps) a missing
    /// asset is created and a different id put right, and Callbreak/Ads/Apply AdMob Settings does the
    /// same by hand. The plugin's settings class is internal, so the asset is edited as serialized data.
    /// </summary>
    public sealed class AdMobSetup : IPreprocessBuildWithReport
    {
        /// <summary>Where the plugin keeps its settings (GoogleMobileAdsSettings.LoadInstance).</summary>
        public const string SettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

        /// <summary>Ahead of the plugin's AndroidBuildPreProcessor (-1) and ManifestProcessor (0).</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildTarget target = report.summary.platform;
            if (target == BuildTarget.Android || target == BuildTarget.iOS)
            {
                Debug.Log("[Callbreak] " + Apply());
            }
        }

        [MenuItem("Callbreak/Ads/Apply AdMob Settings")]
        public static void ApplyMenu() => Debug.Log("[Callbreak] " + Apply());

        /// <summary>Puts the app id into the plugin's settings; returns what it did.</summary>
        public static string Apply()
        {
            ScriptableObject settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SettingsPath) ?? CreateSettings();
            if (settings == null)
            {
                return "AdMob settings not written: the Google Mobile Ads plugin's settings could not be created (is com.google.ads.mobile installed?)";
            }

            var serialized = new SerializedObject(settings);
            SerializedProperty appId = serialized.FindProperty("adMobAndroidAppId");
            if (appId == null)
            {
                return "AdMob settings not written: " + SettingsPath + " has no adMobAndroidAppId (did the plugin change?)";
            }

            if (appId.stringValue == AdMobIds.AndroidAppId)
            {
                return "AdMob settings up to date (Android app id " + AdMobIds.AndroidAppId + ")";
            }

            appId.stringValue = AdMobIds.AndroidAppId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(settings);
            return "AdMob Android app id " + AdMobIds.AndroidAppId + " written to " + SettingsPath;
        }

        /// <summary>The plugin's own LoadInstance creates the asset where the plugin looks for it; null without the plugin.</summary>
        private static ScriptableObject CreateSettings()
        {
            Type type = Type.GetType("GoogleMobileAds.Editor.GoogleMobileAdsSettings, GoogleMobileAds.Editor");
            MethodInfo load = type?.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return load?.Invoke(null, null) as ScriptableObject;
        }
    }
}
