using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Sprite atlases for the sprites that keep mip maps (menu Callbreak/Build/Sprite Atlases): one per
    /// card deck and one for the framed avatars. On Android Unity stores a texture that has mip maps
    /// and sides that are not powers of two uncompressed (RGBA32), so a 228 × 358 card cost 425 KB in
    /// the build and the three decks 66 MB. Packed into power-of-two atlas pages the same pixels are
    /// compressed (ASTC) and keep their mip maps; the sprites, and every reference to them, stay as
    /// they are. Needs Editor Settings > Sprite Packer Mode = Sprite Atlas V2. Re-running rewrites the
    /// atlases in place (same GUIDs).
    /// </summary>
    public static class SpriteAtlasBuilder
    {
        public const string AtlasFolder = "Assets/_Project/Art/Atlases";
        public const string AvatarAtlasPath = AtlasFolder + "/Avatars_Framed.spriteatlasv2";

        // Room for the mip chain: at 8 px a neighbour stays a texel away down to an eighth of the size.
        private const int Padding = 8;

        // ASTC 4×4 (8 bits a pixel): the cards are the game's most detailed art, and 5×5 or 6×6 visibly
        // soften the court cards' small jewels (ADR 048).
        private const TextureImporterFormat AndroidFormat = TextureImporterFormat.ASTC_4x4;

        // OpenGL ES 3 only promises 2048 × 2048 textures.
        private const int MaxPageSize = 2048;

        [MenuItem("Callbreak/Build/Sprite Atlases")]
        public static void BuildMenu() => Debug.Log(BuildAll());

        /// <summary>Writes the deck and avatar atlases; returns a report.</summary>
        public static string BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(AtlasFolder))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(AtlasFolder).Replace('\\', '/'), Path.GetFileName(AtlasFolder));
            }

            var report = new List<string>();
            // The faces only: a back is drawn a fifth of its size in the opponents' fans, where an
            // atlas page's smallest mip maps blend its edge with the empty padding around it and
            // soften the rims; on its own texture it keeps its edge (and stays uncompressed, 3 × 425 KB).
            foreach (string deckFolder in AssetDatabase.GetSubFolders(TableCardBuilder.CardsFolder))
            {
                string deck = Path.GetFileName(deckFolder);
                var faces = new List<Object>();
                foreach (string guid in AssetDatabase.FindAssets("Card_ t:Sprite", new[] { deckFolder }))
                {
                    faces.Add(AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)));
                }

                report.Add(Build($"{AtlasFolder}/Cards_{deck}.spriteatlasv2", faces.ToArray(), FilterMode.Trilinear, MaxPageSize));
            }

            var avatars = new List<Object>();
            for (int i = 0; ; i++)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiBuildKit.ArtRoot}/UI/Avatars/Avatar_Framed_{i:00}.png");
                if (sprite == null)
                {
                    break;
                }

                avatars.Add(sprite);
            }

            // Five 384 px portraits: on 2048 pages Unity gave them a 2048 × 1024 page, half of it empty.
            report.Add(Build(AvatarAtlasPath, avatars.ToArray(), FilterMode.Bilinear, 1024));
            AssetDatabase.SaveAssets();
            return "[SpriteAtlasBuilder] " + string.Join("; ", report);
        }

        private static string Build(string path, Object[] packables, FilterMode filter, int maxPageSize)
        {
            var atlas = new SpriteAtlasAsset();
            atlas.Add(packables);
            SpriteAtlasAsset.Save(atlas, path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
            importer.includeInBuild = true;
            importer.packingSettings = new SpriteAtlasPackingSettings
            {
                blockOffset = 1,
                padding = Padding,
                enableRotation = false,
                enableTightPacking = false,
                enableAlphaDilation = true,
            };
            importer.textureSettings = new SpriteAtlasTextureSettings
            {
                readable = false,
                generateMipMaps = true,
                sRGB = true,
                filterMode = filter,
                anisoLevel = 1,
            };
            TextureImporterPlatformSettings android = importer.GetPlatformSettings("Android");
            android.overridden = true;
            android.maxTextureSize = maxPageSize;
            android.format = AndroidFormat;
            importer.SetPlatformSettings(android);
            importer.SaveAndReimport();
            return $"{path} ({packables.Length} packed)";
        }
    }
}
