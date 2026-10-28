using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The game's TextMeshPro font atlases are 1024 × 1024 at most (ADR 048, 054): the Latin set at
    /// 90 pt fills about 600 rows of one, and at 2048 three quarters of each atlas were empty and
    /// cost 4 MiB of memory per font. The repack copied every glyph's cell unchanged, so text looks
    /// the same. Read as the assets' texture sub-assets (the test assembly has no TextMeshPro reference).
    /// </summary>
    public sealed class FontAtlasTests
    {
        private const string FontsFolder = "Assets/_Project/Fonts";
        private const int MaxAtlasSide = 1024;

        [Test]
        public void EveryFontAtlas_IsAtMost1024()
        {
            List<string> fonts = AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .ToList();
            Assert.GreaterOrEqual(fonts.Count, 6, "Inter in four weights and Source Serif 4 in two");

            var big = new List<string>();
            foreach (string path in fonts)
            {
                Texture2D[] atlases = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Texture2D>().ToArray();
                Assert.IsNotEmpty(atlases, path + " has its atlas");
                foreach (Texture2D atlas in atlases)
                {
                    if (atlas.width > MaxAtlasSide || atlas.height > MaxAtlasSide)
                    {
                        big.Add($"{path}: {atlas.name} {atlas.width} x {atlas.height}");
                    }
                }
            }

            Assert.That(big, Is.Empty, "font atlases bigger than they need to be");
        }
    }
}
