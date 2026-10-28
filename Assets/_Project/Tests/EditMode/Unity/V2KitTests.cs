using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Callbreak.Application.Cosmetics;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The V2 UI kit (Figma "V2 · Design System", Tools/ArtPipeline/V2): every panel, button,
    /// control, icon, decor and portrait sprite is imported as ArtSource/Kit/V2.json says (pixels
    /// per unit, 9-slice borders, mip maps only for the portraits), no older kit manifest still
    /// claims one of them, every layout names
    /// art that exists, and the built screens draw the V2 shadows at full strength (their colour and
    /// opacity are in the sprites) and the pills sliced only across at the scale of their height, so
    /// their round ends stay round. The prefabs are the built ones, so these live outside the folders
    /// the dotnet harness compiles.
    /// </summary>
    public sealed class V2KitTests
    {
        private const string ManifestPath = "ArtSource/Kit/V2.json";
        private const string KitFolder = "ArtSource/Kit";
        private const string ArtRoot = "Assets/_Project/Art/";

        // A layout background that is the player's equipped one (ScreenLayout.EquippedBackground), not a file.
        private const string EquippedBackground = "equipped";
        private static readonly string[] PrefabFolders = { "Assets/_Project/Prefabs/UI/Screens", "Assets/_Project/Prefabs/UI/Overlays" };

        [Test]
        public void EveryKitSprite_IsImportedAsTheManifestSays()
        {
            Manifest manifest = LoadManifest();
            Assert.That(manifest.sprites, Is.Not.Empty);
            foreach (Entry entry in manifest.sprites)
            {
                string path = ArtRoot + entry.path;
                Assert.IsTrue(File.Exists(path), $"{path} is missing");
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.IsNotNull(importer, $"{path} has no texture importer");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.AreEqual(entry.ppu, importer.spritePixelsPerUnit, 0.001f, $"{path}: pixels per unit");
                Assert.AreEqual(Border(entry), importer.spriteBorder, $"{path}: 9-slice border (left, bottom, right, top)");
                Assert.AreEqual(entry.mipmaps, importer.mipmapEnabled, $"{path}: only the portraits keep mip maps (ADR 041)");
                Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode, path);
                Assert.IsTrue(importer.alphaIsTransparency, path);
            }
        }

        [Test]
        public void NoOlderKitManifest_ClaimsAV2Sprite()
        {
            var kit = new HashSet<string>(LoadManifest().sprites.Select(s => s.path));
            foreach (string file in Directory.GetFiles(KitFolder, "*.json"))
            {
                if (Path.GetFileName(file) == "V2.json")
                {
                    continue;
                }

                Manifest other = JsonUtility.FromJson<Manifest>(File.ReadAllText(file));
                foreach (Entry entry in other.sprites ?? Array.Empty<Entry>())
                {
                    Assert.IsFalse(kit.Contains(entry.path), $"{Path.GetFileName(file)} still sets {entry.path}, which V2.json owns");
                }
            }
        }

        [Test]
        public void EveryLayout_NamesArtThatExists()
        {
            var missing = new List<string>();
            foreach (string layout in Directory.GetFiles("ArtSource/Screens", "layout.json", SearchOption.AllDirectories))
            {
                Layout parsed = JsonUtility.FromJson<Layout>(File.ReadAllText(layout));
                var paths = new List<string> { parsed.background == EquippedBackground ? null : parsed.background };
                // What the layout builder loads; "use" only records which kit piece a sprite is.
                foreach (LayoutNode node in parsed.nodes ?? Array.Empty<LayoutNode>())
                {
                    paths.Add(node.sprite);
                    paths.Add(node.shadowSprite);
                    paths.Add(string.IsNullOrEmpty(node.shadowUse) ? null : ArtRoot + node.shadowUse);
                }

                missing.AddRange(paths.Where(p => !string.IsNullOrEmpty(p) && !File.Exists(p)).Select(p => $"{layout}: {p}"));
            }

            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void EveryBackgroundAndTableTheShopSells_HasItsV2Picture()
        {
            // "pokhara-lakeside" → "PokharaLakeside", as TableBackdropBuilder names the files.
            string Name(string id) => string.Concat(id.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));
            foreach (CosmeticItem item in CosmeticCatalog.Of(CosmeticKind.Background))
            {
                Assert.IsTrue(File.Exists($"{ArtRoot}Backgrounds/BG_{Name(item.Id)}.png"), item.Id);
            }

            foreach (CosmeticItem item in CosmeticCatalog.Of(CosmeticKind.Table))
            {
                Assert.IsTrue(File.Exists($"{ArtRoot}Tables/Table_{Name(item.Id)}.png"), item.Id);
            }
        }

        [Test]
        public void TheBuiltScreens_DrawTheV2ShadowsAtFullStrength()
        {
            var shadows = new HashSet<string>(LoadManifest().sprites.Where(s => s.shadow != null && s.shadow.face != null && s.shadow.face.Length == 2).Select(s => ArtRoot + s.path));
            var faint = new List<string>();
            int seen = 0;
            foreach (Image image in BuiltImages())
            {
                if (image.sprite == null || !shadows.Contains(AssetDatabase.GetAssetPath(image.sprite)))
                {
                    continue;
                }

                seen++;
                if (image.color.a < 0.999f)
                {
                    faint.Add($"{PathOf(image.transform)} at {image.color.a:0.##}");
                }
            }

            Assert.Greater(seen, 50, "the screens draw the kit's shadows");
            Assert.That(faint, Is.Empty);
        }

        [Test]
        public void TheBuiltScreens_ScalePillsToTheirHeight()
        {
            var pills = new HashSet<string>(LoadManifest().sprites
                .Where(s => s.nine != null && s.nine.Length == 4 && s.nine[0] > 0f && s.nine[1] == 0f && s.nine[3] == 0f)
                .Select(s => ArtRoot + s.path));
            var stretched = new List<string>();
            int seen = 0;
            foreach (Image image in BuiltImages())
            {
                if (image.sprite == null || image.type != Image.Type.Sliced || !pills.Contains(AssetDatabase.GetAssetPath(image.sprite)))
                {
                    continue;
                }

                float height = ((RectTransform)image.transform).rect.height;
                if (height <= 0f)
                {
                    // Sized at runtime (a pill that hugs its words): its builder sets the scale from the height it is laid out at.
                    continue;
                }

                seen++;
                float native = image.sprite.rect.height / image.sprite.pixelsPerUnit * 100f;
                float wanted = native / height;
                if (Mathf.Abs(image.pixelsPerUnitMultiplier - wanted) > wanted * 0.02f)
                {
                    stretched.Add($"{PathOf(image.transform)}: {image.pixelsPerUnitMultiplier:0.###}, wants {wanted:0.###}");
                }
            }

            Assert.Greater(seen, 20, "the screens draw the kit's pills");
            Assert.That(stretched, Is.Empty);
        }

        private static IEnumerable<Image> BuiltImages()
        {
            foreach (string folder in PrefabFolders)
            {
                foreach (string file in Directory.GetFiles(folder, "*.prefab"))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\', '/'));
                    foreach (Image image in prefab.GetComponentsInChildren<Image>(true))
                    {
                        yield return image;
                    }
                }
            }
        }

        private static Manifest LoadManifest() => JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));

        /// <summary>The manifest's left, top, right, bottom as Unity's left, bottom, right, top.</summary>
        private static Vector4 Border(Entry entry)
        {
            return entry.nine != null && entry.nine.Length == 4 ? new Vector4(entry.nine[0], entry.nine[3], entry.nine[2], entry.nine[1]) : Vector4.zero;
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                path = p.name + "/" + path;
            }

            return path;
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
            public bool mipmaps;
            public Shadow shadow;
        }

        [Serializable]
        private sealed class Shadow
        {
            public float spread;
            public float drop;
            public string slice;
            public float[] face;
        }

        [Serializable]
        private sealed class Layout
        {
            public string background;
            public LayoutNode[] nodes;
        }

        [Serializable]
        private sealed class LayoutNode
        {
            public string sprite;
            public string shadowSprite;
            public string shadowUse;
        }
    }
}
