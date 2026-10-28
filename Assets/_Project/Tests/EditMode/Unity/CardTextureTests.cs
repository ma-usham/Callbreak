using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The decks' card textures (TableCardBuilder): a card thrown on the table lies flat, drawn at 64%
    /// of its height, and a phone's quality level (Mobile) filters anisotropically only the textures
    /// that ask for it. Without it the GPU picks the face's mip level from that squash and the trick
    /// cards blur, so every face and back asks for it.
    /// </summary>
    public sealed class CardTextureTests
    {
        private const string CardsFolder = "Assets/_Project/Art/Cards";
        private const int MinAnisoLevel = 8;

        [Test]
        public void EveryCardFaceAndBack_IsAnisotropicallyFiltered()
        {
            List<string> paths = AssetDatabase.FindAssets("t:Texture2D", new[] { CardsFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.EndsWith(".png"))
                .ToList();
            Assert.GreaterOrEqual(paths.Count, 3 * 53, "three decks of 52 faces and a back");

            var flat = new List<string>();
            foreach (string path in paths)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.anisoLevel < MinAnisoLevel || !importer.mipmapEnabled)
                {
                    flat.Add($"{path}: aniso {importer.anisoLevel}, mip maps {importer.mipmapEnabled}");
                }
            }

            Assert.That(flat, Is.Empty, "cards that would blur lying on the table");
        }
    }
}
