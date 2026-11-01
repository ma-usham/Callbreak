using System.Collections.Generic;
using System.IO;
using Callbreak.Application.Cosmetics;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The decks of Figma's "Card Decks" page (ADR 068): every deck the shop sells has its own 52 faces
    /// and its back in the card library, each a whole card in the shape of Figma's 512 × 712 card at
    /// the old 386 px height (cut_decks.py), so no deck has a base and no two decks share a face. The
    /// cards are opaque but for their rounded corners and their edge's soft line, and import as ADR 048 keeps card art:
    /// uncompressed, with mip maps, filtered for lying flat, their see-through corners dilated.
    /// </summary>
    public sealed class DeckCardArtTests
    {
        private const string Library = "Assets/_Project/Config/Cards/CardSprites.asset";
        private const int Width = 278;
        private const int Height = 386;

        // Opaque enough that no cloth shows through (the Vintage pack's paper is 253-255).
        private const byte Opaque = 250;
        private const int EdgeIn = 3;

        [Test]
        public void EveryDeckInTheShop_HasItsOwn52FacesAndABack_AndNoBase()
        {
            var library = AssetDatabase.LoadAssetAtPath<CardSpriteProvider>(Library);
            Assert.IsNotNull(library, Library);
            IReadOnlyList<CosmeticItem> decks = CosmeticCatalog.Of(CosmeticKind.Deck);
            Assert.AreEqual(decks.Count, library.DeckCount, "one deck in the library for each deck in the shop");
            Assert.AreEqual(CosmeticCatalog.Decks.Default, library.DefaultDeckId);

            var seen = new HashSet<Sprite>();
            foreach (CosmeticItem deck in decks)
            {
                Assert.IsTrue(library.HasDeck(deck.Id), deck.Id);
                Assert.IsNotNull(library.Back(deck.Id), deck.Id + "'s back");
                Assert.IsNull(library.Base(deck.Id), deck.Id + "'s faces are whole cards");
                Assert.IsTrue(seen.Add(library.Back(deck.Id)), deck.Id + "'s back is its own");
                for (int id = 0; id < Card.Count; id++)
                {
                    Card card = Card.FromId(id);
                    Sprite face = library.Face(deck.Id, card);
                    Assert.IsNotNull(face, $"{deck.Id} {card}");
                    Assert.IsTrue(seen.Add(face), $"{deck.Id} {card} is its own face");
                }
            }
        }

        [Test]
        public void EveryCard_IsWhole_InFigmasShape_SeeThroughOnlyAtItsCorners()
        {
            var library = AssetDatabase.LoadAssetAtPath<CardSpriteProvider>(Library);
            var off = new List<string>();
            foreach (Sprite sprite in Sprites(library))
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                Color32[] pixels = Pixels(path, out int w, out int h);
                if (w != Width || h != Height)
                {
                    off.Add($"{path}: {w} x {h}");
                    continue;
                }

                // The corners are rounded away; the card's middle and its sides, a few pixels in from the
                // edge's soft line (Vintage's cards are a pixel inside theirs), are paper or ink.
                byte corner = pixels[0].a;
                byte middle = pixels[(h / 2 * w) + (w / 2)].a;
                byte edge = pixels[(h / 2 * w) + EdgeIn].a;
                if (corner != 0 || middle < Opaque || edge < Opaque)
                {
                    off.Add($"{path}: corner alpha {corner}, middle {middle}, edge {edge}");
                }
            }

            Assert.That(off, Is.Empty);
        }

        [Test]
        public void EveryCard_ImportsUncompressed_WithMipMaps_ForLyingFlat()
        {
            var library = AssetDatabase.LoadAssetAtPath<CardSpriteProvider>(Library);
            var off = new List<string>();
            foreach (Sprite sprite in Sprites(library))
            {
                string path = AssetDatabase.GetAssetPath(sprite);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);

                // As imported for the build target: card art stays RGBA32 (ADR 048's compression was
                // reverted for quality).
                TextureFormat format = sprite.texture.format;
                if (!importer.mipmapEnabled || !importer.alphaIsTransparency || !importer.sRGBTexture || format != TextureFormat.RGBA32
                    || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel < 8)
                {
                    off.Add($"{path}: mip maps {importer.mipmapEnabled}, alpha is transparency {importer.alphaIsTransparency}, "
                        + $"sRGB {importer.sRGBTexture}, {format}, {importer.filterMode}, aniso {importer.anisoLevel}");
                }
            }

            Assert.That(off, Is.Empty);
        }

        private static IEnumerable<Sprite> Sprites(CardSpriteProvider library)
        {
            Assert.IsNotNull(library, Library);
            for (int d = 0; d < library.DeckCount; d++)
            {
                CardSpriteProvider.DeckSprites deck = library.DeckAt(d);
                yield return deck.Back;
                for (int id = 0; id < Card.Count; id++)
                {
                    yield return deck.Face(id);
                }
            }
        }

        // The file's own pixels (not the imported texture), so nothing the importer does can hide a fault.
        private static Color32[] Pixels(string path, out int width, out int height)
        {
            Assert.IsTrue(File.Exists(path), path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)), path);
                width = texture.width;
                height = texture.height;
                return texture.GetPixels32();
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
