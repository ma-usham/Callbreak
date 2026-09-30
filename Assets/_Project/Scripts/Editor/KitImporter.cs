using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Import settings for the generated UI kit (Designer Handbook §11): every designer lists the
    /// sprites it exported in its own manifest, <c>ArtSource/Kit/&lt;Family&gt;.json</c>, with the
    /// pixels per unit and the 9-slice borders, and this applies them. A manifest looks like
    /// <c>{ "sprites": [ { "path": "UI/Panels/UI_Panel_Large.png", "ppu": 200, "nine": [L, T, R, B] } ] }</c>:
    /// the path is under <c>Assets/_Project/Art</c>, <c>ppu</c> 200 shows an @2x export (mockup pixels
    /// x 2.3) at its mockup size on the 1080p canvas, and <c>nine</c> is in sprite pixels in the
    /// handbook's order (left, top, right, bottom); leave it out for a sprite that never stretches.
    /// </summary>
    public static class KitImporter
    {
        public const string ManifestFolder = "ArtSource/Kit";

        [MenuItem("Callbreak/Build/Kit Import Settings")]
        private static void ApplyFromMenu() => Debug.Log(Apply());

        public static string Apply()
        {
            if (!Directory.Exists(ManifestFolder))
            {
                return $"no {ManifestFolder} folder";
            }

            AssetDatabase.Refresh();
            int applied = 0;
            var problems = new List<string>();
            foreach (string file in Directory.GetFiles(ManifestFolder, "*.json"))
            {
                Manifest manifest;
                try
                {
                    manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(file));
                }
                catch (ArgumentException e)
                {
                    problems.Add($"{Path.GetFileName(file)}: {e.Message}");
                    continue;
                }

                if (manifest == null || manifest.sprites == null)
                {
                    problems.Add($"{Path.GetFileName(file)}: no \"sprites\" list");
                    continue;
                }

                foreach (Entry entry in manifest.sprites)
                {
                    string asset = $"{UiBuildKit.ArtRoot}/{entry.path}";
                    if (!File.Exists(asset))
                    {
                        problems.Add($"{entry.path}: file missing");
                        continue;
                    }

                    // The handbook writes borders left, top, right, bottom; Unity wants left, bottom, right, top.
                    Vector4 border = entry.nine != null && entry.nine.Length == 4
                        ? new Vector4(entry.nine[0], entry.nine[3], entry.nine[2], entry.nine[1])
                        : Vector4.zero;
                    UiBuildKit.LoadSpriteAt(asset, entry.ppu > 0f ? entry.ppu : CommonKit.PpuMenu, border);
                    applied++;
                }
            }

            AssetDatabase.SaveAssets();
            string result = $"kit import settings applied to {applied} sprites";
            return problems.Count == 0 ? result : result + "; problems: " + string.Join("; ", problems);
        }

        [Serializable]
        private sealed class Manifest
        {
            public Entry[] sprites;
        }

        [Serializable]
        private sealed class Entry
        {
            public string path;
            public float ppu;
            public float[] nine;
        }
    }
}
