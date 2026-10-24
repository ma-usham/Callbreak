using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// What the game's textures cost in the Android build. The six static font atlases hold the
    /// Latin set at 90 pt in about 600 rows of a 1024 × 1024 atlas: a 2048 one is three quarters
    /// empty and adds 3 MB per font to the build and to memory. A sprite with mip maps whose sides
    /// are not powers of two is stored uncompressed on Android (a 228 × 358 card: 425 KB), so it
    /// is packed into a sprite atlas (Callbreak/Build/Sprite Atlases), whose pages are compressed.
    /// These tests read the project's assets, so they live outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class TextureBudgetTests
    {
        private const string FontFolder = "Assets/_Project/Fonts";
        private const string ArtFolder = "Assets/_Project/Art";
        private const string AtlasFolder = "Assets/_Project/Art/Atlases";
        private const int MaxFontAtlas = 1024;

        [Test]
        public void MipMappedSpritesThatAreNotPowersOfTwoArePackedInAnAtlas()
        {
            SpriteAtlas[] atlases = AssetDatabase.FindAssets("t:SpriteAtlas", new[] { AtlasFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToArray();
            Assert.That(atlases, Is.Not.Empty, AtlasFolder + " has the sprite atlases");

            int checkedSprites = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)
                    || !importer.mipmapEnabled
                    || importer.textureType != TextureImporterType.Sprite)
                {
                    continue;
                }

                importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                // A card back stays on its own texture: in the opponents' fans it is drawn a fifth of its
                // size, where an atlas page's smallest mip maps would soften its rim.
                if ((Mathf.IsPowerOfTwo(width) && Mathf.IsPowerOfTwo(height))
                    || System.IO.Path.GetFileName(path).StartsWith("Back_"))
                {
                    continue;
                }

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(atlases.Any(atlas => atlas.CanBindTo(sprite)), path + " (" + width + " x " + height + ", mip maps) is in a sprite atlas");
                checkedSprites++;
            }

            Assert.That(checkedSprites, Is.GreaterThanOrEqualTo(156 + 5), "the 3 × 52 card faces and the framed avatars");
        }

        [Test]
        public void FontAtlasesAreAtMost1024Square()
        {
            string[] fonts = AssetDatabase.FindAssets("SDF", new[] { FontFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(" SDF.asset"))
                .ToArray();
            Assert.That(fonts, Has.Length.EqualTo(6), "the game's six font assets");
            foreach (string font in fonts)
            {
                Texture2D[] atlases = AssetDatabase.LoadAllAssetsAtPath(font).OfType<Texture2D>().ToArray();
                Assert.That(atlases, Has.Length.EqualTo(1), font + " has one atlas");
                Assert.That(atlases[0].width, Is.LessThanOrEqualTo(MaxFontAtlas), font);
                Assert.That(atlases[0].height, Is.LessThanOrEqualTo(MaxFontAtlas), font);
            }
        }
    }
}
