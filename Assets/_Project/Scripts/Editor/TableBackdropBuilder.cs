using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Table;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Callbreak.Editor
{
    /// <summary>
    /// The art behind the table (the owner, 4 Oct 2026; mockups 20 and 21). Imports the backdrop
    /// layers the art agents finish (ArtSource/Working/Shop{Tables,BackgroundsA,BackgroundsB}/final)
    /// into Art/Backgrounds (BG_&lt;Name&gt;_Back, BG_&lt;Name&gt;_Front) and Art/Tables (Table_&lt;Name&gt;),
    /// every one drawn in BG_Table's 2230 × 1255 frame, with BG_Table's import settings. Then it fills
    /// the TableBackdrops library the Table prefab holds.
    /// <para>
    /// Linear light. The art agents composite and check their layers in sRGB values, as image tools
    /// do, but this project renders in Linear colour space, so the GPU blends in linear light, and a
    /// semi-transparent pixel (the table's anti-aliased edge, the soft edges of the Vista's flowers)
    /// would come out lighter or darker than they made it. So the pixels they solved are solved again,
    /// the same way but in linear light, so that Unity's blend gives the colour their sRGB composite
    /// gives: the default background's back under the default table's edge (the agents tuned exactly
    /// those pixels so that back and table reproduce BG_Table), and every front over its back with the
    /// default table on it. Table layers stay as made: their edges are clean for every background.
    /// </para>
    /// <para>
    /// A table or front layer is then cut down to its visible part (the rest of the frame is
    /// transparent, and would only cost memory and fill rate), starting on the frame's grid of
    /// compression blocks. Each imported layer remembers in its importer's user data where in the frame
    /// it was cut from and what it was made from.
    /// </para>
    /// Until the default table's own layer is in, BG_Table (scenery, table and flowers in one picture)
    /// stands in for the Himalayan Vista's back with no table or front layer, so the default table looks
    /// as it always did, and the other backgrounds wait: their scenery has no table in it.
    /// Run: Tools/Unity/ueval.sh 'return Callbreak.Editor.TableBackdropBuilder.Build();'
    /// </summary>
    public static class TableBackdropBuilder
    {
        public const string LibraryPath = "Assets/_Project/Config/Table/TableBackdrops.asset";
        public const string BackgroundsFolder = UiBuildKit.ArtRoot + "/Backgrounds";
        public const string TablesFolder = UiBuildKit.ArtRoot + "/Tables";

        /// <summary>The table's one picture before the layers: the Himalayan Vista's back until its own is in.</summary>
        public const string PlatePath = BackgroundsFolder + "/BG_Table.png";

        public const string DefaultBackground = CosmeticCatalog.Backgrounds.Default;
        public const string DefaultTable = CosmeticCatalog.Tables.Default;
        public const int FrameWidth = 2230;
        public const int FrameHeight = 1255;

        /// <summary>The shop's backgrounds, in its order.</summary>
        public static readonly string[] BackgroundIds = Ids(CosmeticKind.Background);

        /// <summary>The shop's tables, in its order.</summary>
        public static readonly string[] TableIds = Ids(CosmeticKind.Table);

        // BG_Table's pixels per unit (the Table layout's backgroundPpu): a frame pixel is a mockup pixel.
        // The same number to the last digit, so that the layout's build and this one never re-import it.
        private const float Ppu = 87.0833f;

        // Transparent pixels kept around a cut layer, so that its edges filter as they did in the whole frame.
        private const int CutMargin = 2;

        // A layer is cut only when that saves at least a tenth of the frame.
        private const float CutWhenBelow = 0.9f;

        // A cut starts on the frame's grid of compression blocks (ASTC 6 x 6 on Android, 4 x 4 or 8 x 8
        // elsewhere; textures are stored bottom row first). Its blocks then hold the same pixels as the
        // whole frame's, so where a layer has BG_Table's own pixels it compresses exactly as BG_Table did.
        private const int BlockGrid = 24;

        // The cutting and solving rules a layer was imported under; a new rule imports it again.
        private const string Rules = "cut24.linear2";

        private const string SourceTag = "source=";
        private const string AreaTag = "frame-area=";

        private static readonly string[] Finals =
        {
            "ArtSource/Working/ShopTables/final", "ArtSource/Working/ShopBackgroundsA/final", "ArtSource/Working/ShopBackgroundsB/final",
        };

        private static readonly float[] LinearOf = LinearTable();

        private enum LayerKind
        {
            Back,
            Table,
            Front,
        }

        /// <summary>"kathmandu-courtyard" → "KathmanduCourtyard": the name the id's art files carry.</summary>
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

        public static string BackPath(string id) => $"{BackgroundsFolder}/BG_{ArtName(id)}_Back.png";

        public static string FrontPath(string id) => $"{BackgroundsFolder}/BG_{ArtName(id)}_Front.png";

        public static string TablePath(string id) => $"{TablesFolder}/Table_{ArtName(id)}.png";

        [MenuItem("Callbreak/Table/Build Table Backdrops")]
        private static void BuildFromMenu() => Debug.Log(Build());

        /// <summary>Imports the layers that are new or changed in the finals, then fills the library. Returns a report.</summary>
        public static string Build()
        {
            var report = new StringBuilder();
            Import(report);
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

        /// <summary>Imports every layer that is new or changed in the art agents' finals (or in what it is solved against).</summary>
        public static void Import(StringBuilder report)
        {
            foreach (string id in BackgroundIds)
            {
                ImportLayer(BackPath(id), LayerKind.Back, id, report);
                ImportLayer(FrontPath(id), LayerKind.Front, id, report);
            }

            foreach (string id in TableIds)
            {
                ImportLayer(TablePath(id), LayerKind.Table, id, report);
            }
        }

        /// <summary>Fills the library from the art in the project (see the class summary for the stand-in) and saves it.</summary>
        public static TableBackdropLibrary Fill(StringBuilder report)
        {
            bool tableLayers = File.Exists(TablePath(DefaultTable));
            var backgrounds = new List<TableBackdropLibrary.BackgroundArt>(BackgroundIds.Length);
            foreach (string id in BackgroundIds)
            {
                BackdropLayer back = default, front = default;
                if (id == DefaultBackground)
                {
                    // BG_Table has the ivory table and the flowers in it: never under a front layer.
                    bool ownBack = tableLayers && File.Exists(BackPath(id));
                    back = Layer(ownBack ? BackPath(id) : PlatePath, report);
                    front = ownBack ? Layer(FrontPath(id), report) : default;
                    if (!ownBack)
                    {
                        report.AppendLine($"{id}: BG_Table stands in for its back (no table or front layer yet)");
                    }
                }
                else if (tableLayers)
                {
                    back = Layer(BackPath(id), report);
                    front = Layer(FrontPath(id), report);
                }
                else if (File.Exists(BackPath(id)))
                {
                    report.AppendLine($"{id}: waits for {TablePath(DefaultTable)} (its scenery has no table in it)");
                }

                backgrounds.Add(new TableBackdropLibrary.BackgroundArt(id, back, front));
                report.AppendLine($"background {id}: back {Describe(back)}, front {Describe(front)}");
            }

            var tables = new List<TableBackdropLibrary.TableArt>(TableIds.Length);
            foreach (string id in TableIds)
            {
                BackdropLayer table = Layer(TablePath(id), report);
                tables.Add(new TableBackdropLibrary.TableArt(id, table));
                report.AppendLine($"table {id}: {Describe(table)}");
            }

            var library = AssetDatabase.LoadAssetAtPath<TableBackdropLibrary>(LibraryPath);
            if (library == null)
            {
                UiBuildKit.EnsureFolder(Path.GetDirectoryName(LibraryPath).Replace('\\', '/'));
                library = ScriptableObject.CreateInstance<TableBackdropLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.Set(new Vector2(FrameWidth, FrameHeight), backgrounds.ToArray(), tables.ToArray(), DefaultBackground, DefaultTable);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            report.AppendLine($"library {LibraryPath}: {backgrounds.Count} backgrounds, {tables.Count} tables");
            return library;
        }

        /// <summary>
        /// The table at mockup 08's moment (TablePreview) with every background on every table, or with
        /// only the default table under each background and the default background under each table, at
        /// 1920 × 1080: Temp/Captures/Backdrop_&lt;Background&gt;_&lt;Table&gt;.png. Returns the files written.
        /// </summary>
        public static string CaptureCombinations(bool all = true)
        {
            TableBackdropLibrary library = EnsureLibrary();
            string prefab = Screens.ScreenLayoutBuilder.PrefabPath("Table");
            var written = new StringBuilder();
            foreach (string background in BackgroundIds)
            {
                foreach (string table in TableIds)
                {
                    if (!all && background != DefaultBackground && table != DefaultTable)
                    {
                        continue;
                    }

                    string path = $"{ScreenCaptureTool.OutputFolder}/Backdrop_{ArtName(background)}_{ArtName(table)}.png";
                    ScreenCaptureTool.Capture(prefab, 1920, 1080, path, instance => instance.GetComponent<TableView>().SetBackdrop(library.Backdrop(background, table)));
                    written.AppendLine(path);
                }
            }

            return written.ToString();
        }

        /// <summary>What the library's pictures take in memory on the Editor's build target (all of them load with the table).</summary>
        public static void Memory(StringBuilder report)
        {
            var library = AssetDatabase.LoadAssetAtPath<TableBackdropLibrary>(LibraryPath);
            if (library == null)
            {
                return;
            }

            var seen = new HashSet<Texture2D>();
            long total = 0;
            void Count(BackdropLayer layer)
            {
                Texture2D texture = layer.Sprite != null ? layer.Sprite.texture : null;
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
                Count(library.BackgroundAt(i).Back);
                Count(library.BackgroundAt(i).Front);
            }

            for (int i = 0; i < library.TableCount; i++)
            {
                Count(library.TableAt(i).Table);
            }

            report.AppendLine($"memory on {EditorUserBuildSettings.activeBuildTarget}: {seen.Count} textures, {total / 1048576f:0.00} MB, all loaded with the Table scene");
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

        /// <summary>A layer of the frame from a picture in the project, with BG_Table's import settings; empty when there is none.</summary>
        private static BackdropLayer Layer(string path, StringBuilder report)
        {
            if (!File.Exists(path))
            {
                return default;
            }

            Sprite sprite = UiBuildKit.LoadSpriteAt(path, Ppu);
            if (sprite == null)
            {
                return default;
            }

            Rect area = AreaOf(path);
            Vector2 size = new Vector2(sprite.texture.width, sprite.texture.height);
            Vector2 expected = area.width > 0f ? area.size : new Vector2(FrameWidth, FrameHeight);
            if (size != expected)
            {
                report.AppendLine($"left out {path}: {size.x} x {size.y}, where its frame area is {expected.x} x {expected.y}");
                return default;
            }

            return new BackdropLayer(sprite, area);
        }

        private static void ImportLayer(string target, LayerKind kind, string id, StringBuilder report)
        {
            string source = FindFinal(Path.GetFileName(target));
            if (source == null)
            {
                return;
            }

            // What the picture is made from: the final itself, the rules, and the finals it is solved
            // against (a change in any of them imports it again).
            bool linear = PlayerSettings.colorSpace == ColorSpace.Linear;
            string[] under = linear ? UnderFinals(kind, id) : Array.Empty<string>();
            bool solve = under.Length > 0 && Array.TrueForAll(under, f => f != null);
            var key = new StringBuilder(Sha1(File.ReadAllBytes(source)));
            key.Append('+').Append(Rules).Append(linear ? string.Empty : ".gamma");
            foreach (string f in under)
            {
                key.Append('+').Append(f != null ? Sha1(File.ReadAllBytes(f)) : "missing");
            }

            if (File.Exists(target) && Tag(target, SourceTag) == key.ToString())
            {
                return;
            }

            Color32[] pixels = LoadFrame(source, report);
            if (pixels == null)
            {
                return;
            }

            string how = string.Empty;
            if (solve)
            {
                int solved = kind == LayerKind.Back ? SolveBack(pixels, LoadFrame(under[0], null), out int limits) : SolveFront(pixels, id, under, out limits);
                how = $"; {solved} pixels solved for linear-light blending ("
                    + (kind == LayerKind.Back ? $"{limits} colour channels out of reach)" : $"{limits} of them more opaque)");
            }
            else if (under.Length > 0)
            {
                how = "; NOT yet solved for linear-light blending: waits for " + string.Join(", ", MissingUnder(kind, id, under));
            }

            RectInt area = new RectInt(0, 0, FrameWidth, FrameHeight);
            byte[] png;
            if (kind == LayerKind.Back)
            {
                png = solve ? Encode(pixels, area, false) : File.ReadAllBytes(source);
            }
            else
            {
                area = VisiblePart(pixels);
                if (area.width <= 0 || area.height <= 0)
                {
                    report.AppendLine($"skipped {source}: nothing in it is visible");
                    return;
                }

                if ((float)area.width * area.height >= CutWhenBelow * FrameWidth * FrameHeight)
                {
                    area = new RectInt(0, 0, FrameWidth, FrameHeight);
                }

                png = Encode(pixels, area, true);
            }

            UiBuildKit.EnsureFolder(Path.GetDirectoryName(target).Replace('\\', '/'));
            File.WriteAllBytes(target, png);
            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(target);
            bool whole = area.width == FrameWidth && area.height == FrameHeight;
            importer.userData = SourceTag + key + (whole ? string.Empty : $";{AreaTag}{area.x},{area.y},{area.width},{area.height}");
            importer.SaveAndReimport();
            report.AppendLine($"imported {source} -> {target}" + (whole ? string.Empty : $" (cut to {area.width} x {area.height} at {area.x},{area.y} of the frame)") + how);
        }

        /// <summary>
        /// The finals a layer is solved against: the default background's back, the default table; a
        /// front, its own background's back and the default table. Other backs and the tables are not
        /// solved. Null where a final is not there yet.
        /// </summary>
        private static string[] UnderFinals(LayerKind kind, string id)
        {
            string table = FindFinal(Path.GetFileName(TablePath(DefaultTable)));
            switch (kind)
            {
                case LayerKind.Back:
                    return id == DefaultBackground ? new[] { table } : Array.Empty<string>();
                case LayerKind.Front:
                    return new[] { FindFinal(Path.GetFileName(BackPath(id))), table };
                default:
                    return Array.Empty<string>();
            }
        }

        private static IEnumerable<string> MissingUnder(LayerKind kind, string id, string[] under)
        {
            string[] names = kind == LayerKind.Back
                ? new[] { Path.GetFileName(TablePath(DefaultTable)) }
                : new[] { Path.GetFileName(BackPath(id)), Path.GetFileName(TablePath(DefaultTable)) };
            for (int i = 0; i < under.Length; i++)
            {
                if (under[i] == null)
                {
                    yield return names[i];
                }
            }
        }

        /// <summary>
        /// The default background's back where the default table's edge is semi-transparent over it:
        /// each such (opaque) pixel gets the colour that, under the table's edge blended in linear light,
        /// gives what the art's sRGB composite of the two gives. A channel no colour can reach is clamped.
        /// </summary>
        private static int SolveBack(Color32[] back, Color32[] table, out int clamped)
        {
            int solved = 0;
            clamped = 0;
            for (int i = 0; i < back.Length; i++)
            {
                Color32 t = table[i];
                if (t.a == 0 || t.a == 255)
                {
                    continue;
                }

                Color32 b = back[i];
                float a = t.a / 255f;
                back[i] = new Color32(
                    UnderChannel(t.r, b.r, a, ref clamped),
                    UnderChannel(t.g, b.g, a, ref clamped),
                    UnderChannel(t.b, b.b, a, ref clamped),
                    b.a);
                solved++;
            }

            return solved;
        }

        private static byte UnderChannel(byte top, byte under, float alpha, ref int clamped)
        {
            float target = ToLinear(alpha * Srgb(top) + (1f - alpha) * Srgb(under));
            float linear = (target - alpha * LinearOf[top]) / (1f - alpha);
            if (linear < 0f || linear > 1f)
            {
                clamped++;
            }

            return SrgbByte(linear);
        }

        /// <summary>
        /// A front's semi-transparent pixels, solved over its back with the default table on it: the
        /// colour aimed at is the art's sRGB composite of their back, table and front; what lies under it
        /// in the game is the linear-light blend of the table over the back as imported (the default
        /// background's back solved under the table's edge, see <see cref="SolveBack"/>).
        /// </summary>
        private static int SolveFront(Color32[] front, string backgroundId, string[] under, out int clamped)
        {
            Color32[] back = LoadFrame(under[0], null);
            Color32[] table = LoadFrame(under[1], null);
            var imported = (Color32[])back.Clone();
            clamped = 0;
            if (backgroundId == DefaultBackground)
            {
                SolveBack(imported, table, out _);
            }

            int solved = 0;
            for (int i = 0; i < front.Length; i++)
            {
                Color32 p = front[i];
                if (p.a == 0 || p.a == 255)
                {
                    continue;
                }

                Color32 b = back[i], t = table[i], g = imported[i];
                float ta = t.a / 255f;
                Color32 s = front[i] = Solve(
                    p,
                    ta * Srgb(t.r) + (1f - ta) * Srgb(b.r),
                    ta * Srgb(t.g) + (1f - ta) * Srgb(b.g),
                    ta * Srgb(t.b) + (1f - ta) * Srgb(b.b),
                    ta * LinearOf[t.r] + (1f - ta) * LinearOf[g.r],
                    ta * LinearOf[t.g] + (1f - ta) * LinearOf[g.g],
                    ta * LinearOf[t.b] + (1f - ta) * LinearOf[g.b]);
                clamped += s.a > p.a ? 1 : 0;
                solved++;
            }

            return solved;
        }

        /// <summary>
        /// The colour and opacity that, blended in linear light over an under-layer of linear colour
        /// (ur, ug, ub), give what <paramref name="p"/> gives blended in sRGB values over the under-layer's
        /// sRGB colour (sr, sg, sb): <paramref name="p"/>'s own opacity where a colour can reach it, else
        /// the smallest opacity at which one can.
        /// </summary>
        private static Color32 Solve(Color32 p, float sr, float sg, float sb, float ur, float ug, float ub)
        {
            float a = p.a / 255f;
            float tr = ToLinear(a * Srgb(p.r) + (1f - a) * sr);
            float tg = ToLinear(a * Srgb(p.g) + (1f - a) * sg);
            float tb = ToLinear(a * Srgb(p.b) + (1f - a) * sb);
            float need = a;
            need = Needed(need, tr, ur);
            need = Needed(need, tg, ug);
            need = Needed(need, tb, ub);
            byte a8 = (byte)Mathf.Clamp(Mathf.CeilToInt(need * 255f - 1e-3f), 1, 255);
            float q = a8 / 255f;
            return new Color32(Channel(tr, ur, q), Channel(tg, ug, q), Channel(tb, ub, q), a8);
        }

        /// <summary>The least opacity at which a colour between black and white blends <paramref name="u"/> into <paramref name="t"/>.</summary>
        private static float Needed(float need, float t, float u)
        {
            if (u > 0f && t < (1f - need) * u)
            {
                need = Mathf.Max(need, 1f - t / u);
            }

            if (u < 1f && t - (1f - need) * u > need)
            {
                need = Mathf.Max(need, (t - u) / (1f - u));
            }

            return Mathf.Min(need, 1f);
        }

        private static byte Channel(float target, float under, float alpha) => SrgbByte((target - (1f - alpha) * under) / alpha);

        /// <summary>A linear-light value (clamped to 0-1) as an 8-bit sRGB one.</summary>
        private static byte SrgbByte(float linear)
        {
            linear = Mathf.Clamp01(linear);
            float srgb = linear <= 0.0031308f ? linear * 12.92f : 1.055f * Mathf.Pow(linear, 1f / 2.4f) - 0.055f;
            return (byte)Mathf.Clamp(Mathf.RoundToInt(srgb * 255f), 0, 255);
        }

        private static float Srgb(byte value) => value / 255f;

        private static float ToLinear(float srgb) => srgb <= 0.04045f ? srgb / 12.92f : Mathf.Pow((srgb + 0.055f) / 1.055f, 2.4f);

        private static float[] LinearTable()
        {
            var table = new float[256];
            for (int i = 0; i < table.Length; i++)
            {
                table[i] = ToLinear(i / 255f);
            }

            return table;
        }

        /// <summary>A final's pixels (bottom row first); null, with a line in the report, when it is not a frame-sized picture.</summary>
        private static Color32[] LoadFrame(string path, StringBuilder report)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                {
                    report?.AppendLine($"skipped {path}: not an image");
                    return null;
                }

                if (texture.width != FrameWidth || texture.height != FrameHeight)
                {
                    report?.AppendLine($"skipped {path}: {texture.width} x {texture.height}, not the frame's {FrameWidth} x {FrameHeight}");
                    return null;
                }

                return texture.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>The newest file of that name among the art agents' finals; null when none has it yet.</summary>
        private static string FindFinal(string fileName)
        {
            string found = null;
            DateTime newest = DateTime.MinValue;
            foreach (string folder in Finals)
            {
                string path = Path.Combine(folder, fileName).Replace('\\', '/');
                if (File.Exists(path) && File.GetLastWriteTimeUtc(path) > newest)
                {
                    found = path;
                    newest = File.GetLastWriteTimeUtc(path);
                }
            }

            return found;
        }

        /// <summary>
        /// The part of the frame with any visible pixel, plus a transparent margin, starting on the block
        /// grid, in frame pixels from the top-left corner (the pixels run bottom row first).
        /// </summary>
        private static RectInt VisiblePart(Color32[] pixels)
        {
            int minX = FrameWidth, minY = FrameHeight, maxX = -1, maxY = -1;
            for (int y = 0; y < FrameHeight; y++)
            {
                int row = y * FrameWidth;
                for (int x = 0; x < FrameWidth; x++)
                {
                    if (pixels[row + x].a == 0)
                    {
                        continue;
                    }

                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
            }

            if (maxX < 0)
            {
                return new RectInt(0, 0, 0, 0);
            }

            minX = Math.Max(0, minX - CutMargin) / BlockGrid * BlockGrid;
            minY = Math.Max(0, minY - CutMargin) / BlockGrid * BlockGrid;
            maxX = Math.Min(FrameWidth - 1, maxX + CutMargin);
            maxY = Math.Min(FrameHeight - 1, maxY + CutMargin);
            return new RectInt(minX, FrameHeight - 1 - maxY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>A PNG of the frame's pixels inside <paramref name="area"/> (frame pixels from the top-left), with or without alpha.</summary>
        private static byte[] Encode(Color32[] pixels, RectInt area, bool alpha)
        {
            int fromY = FrameHeight - area.y - area.height;
            var part = new Color32[area.width * area.height];
            for (int y = 0; y < area.height; y++)
            {
                Array.Copy(pixels, (fromY + y) * FrameWidth + area.x, part, y * area.width, area.width);
            }

            var texture = new Texture2D(area.width, area.height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            try
            {
                texture.SetPixels32(part);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        /// <summary>Where in the frame a cut layer lies (frame pixels from the top-left); empty for a whole-frame one.</summary>
        private static Rect AreaOf(string path)
        {
            string value = Tag(path, AreaTag);
            if (string.IsNullOrEmpty(value))
            {
                return Rect.zero;
            }

            string[] parts = value.Split(',');
            if (parts.Length != 4)
            {
                return Rect.zero;
            }

            float Part(int i) => float.Parse(parts[i], CultureInfo.InvariantCulture);
            return new Rect(Part(0), Part(1), Part(2), Part(3));
        }

        private static string Tag(string path, string tag)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null || string.IsNullOrEmpty(importer.userData))
            {
                return null;
            }

            foreach (string part in importer.userData.Split(';'))
            {
                if (part.StartsWith(tag, StringComparison.Ordinal))
                {
                    return part.Substring(tag.Length);
                }
            }

            return null;
        }

        private static string Describe(BackdropLayer layer)
        {
            if (layer.IsEmpty)
            {
                return "none";
            }

            string where = layer.Area.width > 0f ? $" at {layer.Area.x},{layer.Area.y} {layer.Area.width} x {layer.Area.height}" : string.Empty;
            return AssetDatabase.GetAssetPath(layer.Sprite) + where;
        }

        private static string Sha1(byte[] bytes)
        {
            using (var sha = SHA1.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
