using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The shared card faces (ADR 054): each is Classic Crimson's whole card less that deck's base,
    /// premultiplied in linear light (compose-decks.ps1). Drawn over the base the way the GPU blends
    /// them, every face must give back the whole card the art pipeline composed (kept in
    /// ArtSource/Exports) to within a level, and the faces must import without Unity's transparency
    /// dilation, which would spoil premultiplied colour.
    /// </summary>
    public sealed class SharedCardFaceTests
    {
        private const string FacesFolder = "Assets/_Project/Art/Cards/Faces";
        private const string ReferenceBase = "Assets/_Project/Art/Cards/ClassicCrimson/Base_ClassicCrimson.png";
        private const string WholeCards = "ArtSource/Exports/Cards/ClassicCrimson";

        [Test]
        public void EverySharedFace_OverClassicCrimsonsBase_IsItsWholeCard()
        {
            Color32[] cardBase = Pixels(ReferenceBase);
            string[] faces = Directory.GetFiles(FacesFolder, "Card_*.png");
            Assert.AreEqual(52, faces.Length, "one face per card");

            var off = new List<string>();
            foreach (string path in faces)
            {
                Color32[] face = Pixels(path);
                Color32[] whole = Pixels(Path.Combine(WholeCards, Path.GetFileName(path)));
                Assert.AreEqual(whole.Length, face.Length, path);
                int worst = 0;
                for (int i = 0; i < whole.Length; i++)
                {
                    if (whole[i].a == 0)
                    {
                        continue;
                    }

                    float alpha = face[i].a / 255f;
                    worst = Math.Max(worst, Math.Abs(Blend(cardBase[i].r, face[i].r, alpha) - whole[i].r));
                    worst = Math.Max(worst, Math.Abs(Blend(cardBase[i].g, face[i].g, alpha) - whole[i].g));
                    worst = Math.Max(worst, Math.Abs(Blend(cardBase[i].b, face[i].b, alpha) - whole[i].b));
                }

                if (worst > 1)
                {
                    off.Add($"{Path.GetFileName(path)}: {worst} levels off");
                }
            }

            Assert.That(off, Is.Empty, "faces that don't give back Classic Crimson's cards");
        }

        [Test]
        public void SharedFaces_ImportAsPremultipliedColour()
        {
            var dilated = new List<string>();
            foreach (string path in Directory.GetFiles(FacesFolder, "Card_*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                Assert.IsNotNull(importer, path);
                if (importer.alphaIsTransparency || !importer.sRGBTexture || !importer.mipmapEnabled)
                {
                    dilated.Add($"{path}: alpha is transparency {importer.alphaIsTransparency}, sRGB {importer.sRGBTexture}, mip maps {importer.mipmapEnabled}");
                }
            }

            Assert.That(dilated, Is.Empty);
        }

        // The base under a premultiplied face, in linear light, back to an 8-bit sRGB value.
        private static int Blend(byte under, byte face, float alpha)
        {
            double linear = Linear(under) * (1 - alpha) + Linear(face);
            return (int)Math.Round(Srgb(linear));
        }

        private static double Linear(byte v)
        {
            double c = v / 255.0;
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static double Srgb(double l)
        {
            l = Math.Max(0, Math.Min(1, l));
            return 255.0 * (l <= 0.0031308 ? l * 12.92 : 1.055 * Math.Pow(l, 1 / 2.4) - 0.055);
        }

        // The file's own pixels (not the imported texture), so nothing the importer does can hide a fault.
        private static Color32[] Pixels(string path)
        {
            Assert.IsTrue(File.Exists(path), path);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(texture.LoadImage(File.ReadAllBytes(path)), path);
                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
