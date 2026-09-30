#if UNITY_ANDROID
using System;
using System.IO;
using System.Xml;
using Callbreak.Application.Rooms;
using UnityEditor.Android;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Registers the invite link with Android (GDD §11 screens 00 and 04): adds a VIEW intent
    /// filter for <c>callbreak://join/...</c> to the launcher activity of the generated Gradle
    /// project, whichever activity class the Player Settings' application entry point produced.
    /// A failure here is logged and never stops a build; the app then simply does not open links.
    /// </summary>
    public sealed class AndroidInviteLinkManifest : IPostGenerateGradleAndroidProject
    {
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";

        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            try
            {
                string manifest = Path.Combine(path, "src", "main", "AndroidManifest.xml");
                if (!File.Exists(manifest))
                {
                    Debug.LogWarning("[Callbreak] Invite links: no AndroidManifest.xml at " + manifest);
                    return;
                }

                var document = new XmlDocument();
                document.Load(manifest);
                if (AddInviteFilter(document))
                {
                    document.Save(manifest);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Callbreak] Invite links were not registered in the Android manifest: " + e.Message);
            }
        }

        /// <summary>Adds the filter to every launcher activity that lacks it; true when the document changed.</summary>
        public static bool AddInviteFilter(XmlDocument document)
        {
            bool changed = false;
            XmlNodeList activities = document.SelectNodes("/manifest/application/activity");
            if (activities == null)
            {
                return false;
            }

            foreach (XmlNode activity in activities)
            {
                if (!IsLauncher(activity) || HasInviteFilter(activity))
                {
                    continue;
                }

                activity.AppendChild(CreateFilter(document));
                changed = true;
            }

            return changed;
        }

        private static bool IsLauncher(XmlNode activity)
        {
            foreach (XmlNode category in activity.SelectNodes("intent-filter/category"))
            {
                if (AndroidName(category) == "android.intent.category.LAUNCHER")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasInviteFilter(XmlNode activity)
        {
            foreach (XmlNode data in activity.SelectNodes("intent-filter/data"))
            {
                XmlAttribute scheme = data.Attributes?["scheme", AndroidNamespace];
                if (scheme != null && scheme.Value == InviteLink.Scheme)
                {
                    return true;
                }
            }

            return false;
        }

        private static XmlElement CreateFilter(XmlDocument document)
        {
            XmlElement filter = document.CreateElement("intent-filter");
            filter.AppendChild(Named(document, "action", "android.intent.action.VIEW"));
            filter.AppendChild(Named(document, "category", "android.intent.category.DEFAULT"));
            filter.AppendChild(Named(document, "category", "android.intent.category.BROWSABLE"));
            XmlElement data = document.CreateElement("data");
            data.SetAttribute("scheme", AndroidNamespace, InviteLink.Scheme);
            data.SetAttribute("host", AndroidNamespace, InviteLink.Host);
            filter.AppendChild(data);
            return filter;
        }

        private static XmlElement Named(XmlDocument document, string element, string name)
        {
            XmlElement node = document.CreateElement(element);
            node.SetAttribute("name", AndroidNamespace, name);
            return node;
        }

        private static string AndroidName(XmlNode node) => node.Attributes?["name", AndroidNamespace]?.Value;
    }
}
#endif
