using System.Collections.Generic;
using System.IO;
using System.Text;
using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    /// <summary>
    /// The backgrounds and cloths the shop sells (mockups 20 and 21; cloths since ADR 058): every one
    /// a single picture of the backdrop frame, 2230 × 1255 frame pixels (the 1920 × 1080 screen plus
    /// margins for 21:9 and 4:3; a frame pixel is a mockup pixel). Tools/ArtPipeline/NewUI/build.py
    /// draws them from the Figma file's vector scenes and cloths, with that margin:
    /// Art/Backgrounds/BG_&lt;Name&gt;.png, shown behind the menus, and Art/Tables/Table_&lt;Name&gt;.png,
    /// filling the screen at the table, with a dhaka cloth's border (Art/Tables/Frame_Dhaka.png)
    /// tiled round the screen. This fills the TableBackdrops library that the Table prefab and the
    /// menu screens hold with them.
    /// Run: Tools/Unity/ueval.sh 'return Callbreak.Editor.TableBackdropBuilder.Build();'
    /// </summary>
    public static class TableBackdropBuilder
    {
        public const string LibraryPath = "Assets/_Project/Config/Table/TableBackdrops.asset";
        public const string BackgroundsFolder = UiBuildKit.ArtRoot + "/Backgrounds";
        public const string TablesFolder = UiBuildKit.ArtRoot + "/Tables";

        public const string DefaultBackground = CosmeticCatalog.Backgrounds.Default;
        public const string DefaultTable = CosmeticCatalog.Tables.Default;
        public const int FrameWidth = 2230;
        public const int FrameHeight = 1255;

        /// <summary>
        /// The shop's backgrounds, in its order: only these are listed. A background the shop no
        /// longer sells (the owner dropped three on 5 Oct 2026) has no picture in the game.
        /// </summary>
        public static readonly string[] BackgroundIds = Ids(CosmeticKind.Background);

        /// <summary>The shop's tables, in its order.</summary>
        public static readonly string[] TableIds = Ids(CosmeticKind.Table);

        // The pictures' pixels per unit: a frame pixel is a mockup pixel (the layouts' backgroundPpu).
        private const float Ppu = 87.0833f;

        // The dhaka cloths' border: drawn at twice the canvas size, its 168-unit corners fixed and the
        // rest of its edges tiled round the screen (cloths.py).
        public const string DhakaFramePath = TablesFolder + "/Frame_Dhaka.png";
        private const float FramePpu = 200f;
        private const float FrameBorder = 336f;

        /// <summary>The dhaka cloths share one border; every theme's own cloth has its own (ADR 064), Frame_&lt;Name&gt;.png.</summary>
        private static readonly Dictionary<string, string> SharedFramePaths = new Dictionary<string, string>
        {
            [CosmeticCatalog.Tables.GreenDhaka] = DhakaFramePath,
            [CosmeticCatalog.Tables.IndigoDhaka] = DhakaFramePath,
        };

        /// <summary>The night scenes (ADR 064): a menu draws its words light on them.</summary>
        public static readonly HashSet<string> NightScenes = new HashSet<string>
        {
            CosmeticCatalog.Themes.TiharLights,
            CosmeticCatalog.Themes.PhewaMoonlight,
        };

        /// <summary>A cloth's border: the dhaka one, or its own when its art is in the project; null for none (Dusk Himalaya).</summary>
        public static string FramePath(string tableId)
        {
            if (SharedFramePaths.TryGetValue(tableId, out string shared))
            {
                return shared;
            }

            string own = $"{TablesFolder}/Frame_{ArtName(tableId)}.png";
            return File.Exists(own) ? own : null;
        }

        /// <summary>"durbar-square" → "DurbarSquare": the name the id's art files carry.</summary>
        public static string ArtName(string id)
        {
            var name = new StringBuilder(id.Length);
            foreach (string word in id.Split('-'))
            {
                if (word.Length > 0)
                {
                    name.Append(char.ToUpperInvariant(word[0])).Append(word, 1, word.Length - 1);
                }
            }

            return name.ToString();
        }

        public static string BackgroundPath(string id) => $"{BackgroundsFolder}/BG_{ArtName(id)}.png";

        public static string TablePath(string id) => $"{TablesFolder}/Table_{ArtName(id)}.png";

        [MenuItem("Callbreak/Table/Build Table Backdrops")]
        private static void BuildFromMenu() => Debug.Log(Build());

        /// <summary>Fills the library from the pictures in the project. Returns a report.</summary>
        public static string Build()
        {
            var report = new StringBuilder();
            Fill(report);
            Memory(report);
            return report.ToString();
        }

        /// <summary>The library asset, built from the art in the project on first use.</summary>
        public static TableBackdropLibrary EnsureLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<TableBackdropLibrary>(LibraryPath);
            return library != null && library.BackgroundCount == BackgroundIds.Length && library.TableCount == TableIds.Length
                ? library
                : Fill(new StringBuilder());
        }

        /// <summary>Fills the library from the pictures in the project and saves it.</summary>
        public static TableBackdropLibrary Fill(StringBuilder report)
        {
            var backgrounds = new List<TableBackdropLibrary.Picture>(BackgroundIds.Length);
            foreach (string id in BackgroundIds)
            {
                backgrounds.Add(new TableBackdropLibrary.Picture(id, Picture(BackgroundPath(id), report), null, NightScenes.Contains(id)));
                report.AppendLine($"background {id}: {Describe(backgrounds[backgrounds.Count - 1])}");
            }

            var tables = new List<TableBackdropLibrary.Picture>(TableIds.Length);
            foreach (string id in TableIds)
            {
                string framePath = FramePath(id);
                Sprite frame = framePath != null
                    ? UiBuildKit.LoadSpriteAt(framePath, FramePpu, new Vector4(FrameBorder, FrameBorder, FrameBorder, FrameBorder))
                    : null;
                tables.Add(new TableBackdropLibrary.Picture(id, Picture(TablePath(id), report), frame));
                report.AppendLine($"table {id}: {Describe(tables[tables.Count - 1])}{(frame != null ? " + " + framePath : "")}");
            }

            var library = AssetDatabase.LoadAssetAtPath<TableBackdropLibrary>(LibraryPath);
            if (library == null)
            {
                UiBuildKit.EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
                library = ScriptableObject.CreateInstance<TableBackdropLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.Set(backgrounds.ToArray(), tables.ToArray(), DefaultBackground, DefaultTable);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            report.AppendLine($"library {LibraryPath}: {backgrounds.Count} backgrounds, {tables.Count} tables");
            return library;
        }

        /// <summary>
        /// The table at mockup 08's moment (TablePreview) on every table, and the main menu over every
        /// background, at 1920 × 1080: Temp/Captures/Backdrop_&lt;Table&gt;.png and
        /// Menu_&lt;Background&gt;.png. Returns the files written.
        /// </summary>
        public static string CaptureAll()
        {
            TableBackdropLibrary library = EnsureLibrary();
            var written = new StringBuilder();
            foreach (string table in TableIds)
            {
                string path = $"{ScreenCaptureTool.OutputFolder}/Backdrop_{ArtName(table)}.png";
                ScreenCaptureTool.Capture(
                    Screens.ScreenLayoutBuilder.PrefabPath("Table"), 1920, 1080, path,
                    instance => instance.GetComponent<TableView>().SetTable(library.TablePicture(table, out _), library.TableFrame(table)));
                written.AppendLine(path);
            }

            foreach (string background in BackgroundIds)
            {
                string path = $"{ScreenCaptureTool.OutputFolder}/Menu_{ArtName(background)}.png";
                ScreenCaptureTool.Capture(
                    MainMenuBuilder.PrefabPath, 1920, 1080, path,
                    instance => instance.GetComponentInChildren<EquippedBackground>().GetComponent<Image>().sprite = library.BackgroundPicture(background, out _));
                written.AppendLine(path);
            }

            return written.ToString();
        }

        /// <summary>What the library's pictures take in memory on the Editor's build target (all of them load with the menus and with the table).</summary>
        public static void Memory(StringBuilder report)
        {
            var library = AssetDatabase.LoadAssetAtPath<TableBackdropLibrary>(LibraryPath);
            if (library == null)
            {
                return;
            }

            var seen = new HashSet<Texture2D>();
            long total = 0;
            void Count(TableBackdropLibrary.Picture picture)
            {
                Texture2D texture = picture?.Sprite != null ? picture.Sprite.texture : null;
                if (texture == null || !seen.Add(texture))
                {
                    return;
                }

                long bytes = GraphicsFormatUtility.ComputeMipmapSize(texture.width, texture.height, texture.format);
                total += bytes;
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                string android = importer != null ? importer.GetAutomaticFormat("Android").ToString() : "?";
                report.AppendLine($"  {texture.name}: {texture.width} x {texture.height} {texture.format} (Android automatic {android}) {bytes / 1048576f:0.00} MB");
            }

            for (int i = 0; i < library.BackgroundCount; i++)
            {
                Count(library.BackgroundAt(i));
            }

            for (int i = 0; i < library.TableCount; i++)
            {
                Count(library.TableAt(i));
            }

            report.AppendLine($"memory on {EditorUserBuildSettings.activeBuildTarget}: {seen.Count} textures, {total / 1048576f:0.00} MB, all loaded with the Menu and Table scenes");
        }

        private static string[] Ids(CosmeticKind kind)
        {
            IReadOnlyList<CosmeticItem> items = CosmeticCatalog.Of(kind);
            var ids = new string[items.Count];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = items[i].Id;
            }

            return ids;
        }

        /// <summary>A picture of the whole frame, as a sprite; null when there is none or it is not the frame's size.</summary>
        private static Sprite Picture(string path, StringBuilder report)
        {
            if (!File.Exists(path))
            {
                report.AppendLine($"no picture {path}");
                return null;
            }

            Sprite sprite = UiBuildKit.LoadSpriteAt(path, Ppu);
            if (sprite == null)
            {
                return null;
            }

            if (sprite.texture.width != FrameWidth || sprite.texture.height != FrameHeight)
            {
                report.AppendLine($"left out {path}: {sprite.texture.width} x {sprite.texture.height}, where the frame is {FrameWidth} x {FrameHeight}");
                return null;
            }

            return sprite;
        }

        private static string Describe(TableBackdropLibrary.Picture picture) =>
            picture.Sprite != null ? AssetDatabase.GetAssetPath(picture.Sprite) : "none";
    }
}
