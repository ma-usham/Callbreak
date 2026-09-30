using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using VContainer.Unity;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Player names can be in any script (GDD §11 screen 14), but the game's own fonts are baked
    /// with Latin only. At startup the device's own fonts are put behind them as fallbacks, so a
    /// name in Devanagari or with an accent shows as letters instead of empty boxes. TextMeshPro
    /// does not shape complex scripts (the GDD §15 risk): conjuncts are not formed.
    /// </summary>
    public sealed class SystemFontFallbacks : IInitializable, IDisposable
    {
        private const string Style = "Regular";

        private readonly List<TMP_FontAsset> _added = new List<TMP_FontAsset>();

        /// <summary>The system fonts in use, for diagnostics and tests.</summary>
        public IReadOnlyList<TMP_FontAsset> Added => _added;

        public void Initialize()
        {
            if (TMP_Settings.fallbackFontAssets == null)
            {
                TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
            }

            var installed = new HashSet<string>(Font.GetOSInstalledFontNames(), StringComparer.OrdinalIgnoreCase);
            foreach (string[] choices in FamiliesFor(UnityEngine.Application.platform))
            {
                // The first family of each group that the device has.
                foreach (string family in choices)
                {
                    if (installed.Contains(family) && Add(family))
                    {
                        break;
                    }
                }
            }
        }

        public void Dispose()
        {
            List<TMP_FontAsset> fallbacks = TMP_Settings.fallbackFontAssets;
            foreach (TMP_FontAsset font in _added)
            {
                if (fallbacks != null)
                {
                    fallbacks.Remove(font);
                }

                if (font != null)
                {
                    DestroyWithAtlases(font);
                }
            }

            _added.Clear();
        }

        /// <summary>
        /// Two groups per platform: a wide Latin family (accents, Cyrillic, Greek) and a
        /// Devanagari one, each with the names the platform's versions have used.
        /// </summary>
        private static string[][] FamiliesFor(RuntimePlatform platform)
        {
            switch (platform)
            {
                case RuntimePlatform.Android:
                    return new[]
                    {
                        new[] { "Roboto", "Noto Sans" },
                        new[] { "Noto Sans Devanagari", "Noto Sans Devanagari UI", "Lohit Devanagari" },
                    };
                case RuntimePlatform.IPhonePlayer:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return new[]
                    {
                        new[] { "Helvetica Neue", "Helvetica", "Arial" },
                        new[] { "Kohinoor Devanagari", "Devanagari Sangam MN", "Devanagari MT" },
                    };
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                    return new[]
                    {
                        new[] { "Segoe UI", "Arial" },
                        new[] { "Nirmala UI", "Mangal" },
                    };
                default:
                    return new[]
                    {
                        new[] { "Noto Sans", "DejaVu Sans", "Liberation Sans" },
                        new[] { "Noto Sans Devanagari", "Lohit Devanagari" },
                    };
            }
        }

        private bool Add(string family)
        {
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(family, Style);
            if (font == null)
            {
                return false;
            }

            font.name = family + " (system)";
            _added.Add(font);
            TMP_Settings.fallbackFontAssets.Add(font);
            return true;
        }

        private static void DestroyWithAtlases(TMP_FontAsset font)
        {
            if (font.atlasTextures != null)
            {
                foreach (Texture2D atlas in font.atlasTextures)
                {
                    if (atlas != null)
                    {
                        UnityEngine.Object.Destroy(atlas);
                    }
                }
            }

            if (font.material != null)
            {
                UnityEngine.Object.Destroy(font.material);
            }

            UnityEngine.Object.Destroy(font);
        }
    }
}
