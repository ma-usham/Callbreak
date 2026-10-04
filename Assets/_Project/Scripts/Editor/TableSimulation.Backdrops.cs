using System;
using System.Collections.Generic;
using System.IO;
using Callbreak.Application.Cosmetics;
using Callbreak.Editor.Screens;
using Callbreak.Presentation.Table;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Callbreak.Editor
{
    public static partial class TableSimulation
    {
        /// <summary>The shop as the table sees it: what the player has equipped, set by the run.</summary>
        private sealed class BackdropShop : ICosmeticsShop
        {
            public string Background = CosmeticCatalog.Backgrounds.Default;
            public string Table = CosmeticCatalog.Tables.Default;

            public event Action Changed
            {
                add { }
                remove { }
            }

            public IReadOnlyList<CosmeticItem> Catalog(CosmeticKind kind) => CosmeticCatalog.Of(kind);

            public bool IsOwned(CosmeticKind kind, string id) => true;

            public string Equipped(CosmeticKind kind)
            {
                switch (kind)
                {
                    case CosmeticKind.Background:
                        return Background;
                    case CosmeticKind.Table:
                        return Table;
                    default:
                        return CosmeticCatalog.DefaultOf(kind);
                }
            }

            public void Equip(CosmeticKind kind, string id)
            {
                if (kind == CosmeticKind.Background)
                {
                    Background = id;
                }
                else if (kind == CosmeticKind.Table)
                {
                    Table = id;
                }
            }

            public PurchaseResult Buy(CosmeticKind kind, string id) => PurchaseResult.AlreadyOwned;
        }

        private sealed partial class Runner
        {
            private const string SkinLogTag = "[TableSkin]";

            // Where a stand-in library leaves a background and a table without art, and cuts a layer.
            private const string NoArtBackground = CosmeticCatalog.Backgrounds.MonasteryGarden;
            private const string NoArtTable = CosmeticCatalog.Tables.HeritageWalnut;
            private static readonly Rect StandInFrontArea = new Rect(0f, 905f, 2230f, 350f);
            private static readonly Rect StandInTableArea = new Rect(40f, 280f, 2150f, 975f);

            // The owner (5 Oct 2026): the game keeps three backgrounds, and these left it. A player may
            // still have one saved (the shop falls back to the default) or equipped.
            private static readonly string[] RemovedBackgrounds = { "kathmandu-courtyard", "village-terrace", "vintage-home" };

            private readonly BackdropShop _shop = new BackdropShop();
            private readonly List<string> _skinWarnings = new List<string>();

            /// <summary>
            /// The backdrop (the owner, 4 Oct 2026): every background the shop sells on every table it
            /// sells shows, as the player equips them, in three layers (the scenery, the table, the
            /// scenery's foreground) behind everything, in BG_Table's frame and place. First with a
            /// library of stand-in pictures, so each combination is told apart whatever art is in yet
            /// (one background and one table without art, a front and a table cut down to their
            /// visible part); then with the game's own library, whose defaults must look as BG_Table did.
            /// That library holds only what the shop sells (all of it loads with the table), and a
            /// background the game no longer has (the owner, 5 Oct 2026), still equipped, shows the
            /// default and says so once.
            /// </summary>
            private void StageBackdrops()
            {
                _stage = "backdrops";
                TableBackdropLibrary real = _table.Backdrops;
                Check(real != null, "the table holds the library of backgrounds and tables");
                if (real == null)
                {
                    return;
                }

                VerifyBackdropPlace();
                foreach (CosmeticKind kind in new[] { CosmeticKind.Background, CosmeticKind.Table })
                {
                    foreach (CosmeticItem item in CosmeticCatalog.Of(kind))
                    {
                        bool listed = kind == CosmeticKind.Background ? real.Background(item.Id) != null : real.Table(item.Id) != null;
                        Check(listed, "every background and table the shop sells is in the table's library", kind + " " + item.Id);
                    }
                }

                // All of the library's pictures load with the table: nothing the shop does not sell may be among them.
                for (int i = 0; i < real.BackgroundCount; i++)
                {
                    string id = real.BackgroundAt(i)?.Id;
                    Check(CosmeticCatalog.Find(CosmeticKind.Background, id) != null, "the table's library holds only what the shop sells", "background " + (id ?? "(none)"));
                }

                for (int i = 0; i < real.TableCount; i++)
                {
                    string id = real.TableAt(i)?.Id;
                    Check(CosmeticCatalog.Find(CosmeticKind.Table, id) != null, "the table's library holds only what the shop sells", "table " + (id ?? "(none)"));
                }

                foreach (string removed in RemovedBackgrounds)
                {
                    Check(real.Background(removed) == null, "the backgrounds the owner removed (5 Oct 2026) are not in the table's library", removed);
                }

                UnityEngine.Application.logMessageReceived += OnSkinLog;
                var made = new List<UnityEngine.Object>();
                try
                {
                    TableBackdropLibrary standIn = StandInLibrary(real, made);
                    SetBackdropLibrary(standIn);
                    foreach (CosmeticItem background in CosmeticCatalog.Of(CosmeticKind.Background))
                    {
                        foreach (CosmeticItem table in CosmeticCatalog.Of(CosmeticKind.Table))
                        {
                            ShowEquipped(background.Id, table.Id, standIn, true, null);
                        }
                    }

                    ShowEquipped("rooftop-cafe", "glass-top", standIn, true, null);
                    ShowEquipped(null, null, standIn, true, null);
                }
                finally
                {
                    SetBackdropLibrary(real);
                    foreach (UnityEngine.Object o in made)
                    {
                        UnityEngine.Object.DestroyImmediate(o);
                    }
                }

                int combinations = 0;
                IReadOnlyList<CosmeticItem> tables = CosmeticCatalog.Of(CosmeticKind.Table);
                try
                {
                    foreach (CosmeticItem background in CosmeticCatalog.Of(CosmeticKind.Background))
                    {
                        foreach (CosmeticItem table in tables)
                        {
                            string name = $"Backdrop_{TableBackdropBuilder.ArtName(background.Id)}_{TableBackdropBuilder.ArtName(table.Id)}";
                            ShowEquipped(background.Id, table.Id, real, false, name);
                            combinations++;
                        }
                    }

                    // A background the game no longer has, still equipped: the default (the Himalayan
                    // Vista) on the equipped table, said once.
                    for (int i = 0; i < RemovedBackgrounds.Length; i++)
                    {
                        string removed = RemovedBackgrounds[i], table = tables[i % tables.Count].Id;
                        TableBackdrop shown = real.Backdrop(removed, table);
                        Check(
                            shown.BackgroundId == CosmeticCatalog.Backgrounds.Default,
                            "a removed background still equipped shows the default one (the Himalayan Vista)",
                            $"{removed} shows {shown.BackgroundId}");
                        ShowEquipped(removed, table, real, false, null);
                    }
                }
                finally
                {
                    UnityEngine.Application.logMessageReceived -= OnSkinLog;
                }

                int withArt = 0;
                for (int i = 0; i < real.BackgroundCount; i++)
                {
                    withArt += real.BackgroundAt(i).Back.IsEmpty ? 0 : 1;
                }

                int tablesWithArt = 0;
                for (int i = 0; i < real.TableCount; i++)
                {
                    tablesWithArt += real.TableAt(i).Table.IsEmpty ? 0 : 1;
                }

                Note($"backdrop art in the library: {withArt} of {real.BackgroundCount} backgrounds, {tablesWithArt} of {real.TableCount} tables; {combinations} combinations shown with the game's art, and {RemovedBackgrounds.Length} removed backgrounds");
                VerifyDefaultLooksAsBefore(real);
            }

            /// <summary>
            /// GDD/owner: the backdrop lies behind everything else on the table, as BG_Table did, in
            /// BG_Table's frame (the layout's background: the mockup plus margins for 21:9 and 4:3).
            /// </summary>
            private void VerifyBackdropPlace()
            {
                Image back = _table.BackdropBack, table = _table.BackdropTable, front = _table.BackdropFront;
                Check(back != null && table != null && front != null, "the table has the three backdrop layers");
                if (back == null || table == null || front == null)
                {
                    return;
                }

                var frame = back.rectTransform.parent as RectTransform;
                Check(frame != null && table.transform.parent == frame && front.transform.parent == frame, "the backdrop's three layers share one frame");
                Check(
                    back.transform.GetSiblingIndex() < table.transform.GetSiblingIndex() && table.transform.GetSiblingIndex() < front.transform.GetSiblingIndex(),
                    "the backdrop's layers lie scenery, table, foreground",
                    $"{back.transform.GetSiblingIndex()} {table.transform.GetSiblingIndex()} {front.transform.GetSiblingIndex()}");
                Check(frame != null && frame.parent == _table.transform && frame.GetSiblingIndex() == 0, "the backdrop is behind everything else on the table");
                Check(!back.raycastTarget && !table.raycastTarget && !front.raycastTarget, "the backdrop takes no touch");
                if (frame == null)
                {
                    return;
                }

                ScreenLayout layout = ScreenLayout.Load("Table");
                float[] r = layout.backgroundRect;
                Vector2 size = UiBuildKit.FromMock(r[2], r[3]);
                var centre = new Vector2((r[0] + r[2] / 2f - layout.mockW / 2f) * UiBuildKit.ScaleX, -(r[1] + r[3] / 2f - layout.mockH / 2f) * UiBuildKit.ScaleY);
                bool sized = Mathf.Abs(frame.rect.width - size.x) < 0.01f && Mathf.Abs(frame.rect.height - size.y) < 0.01f;
                bool placed = Vector2.Distance(frame.anchoredPosition, centre) < 0.01f && frame.anchorMin == new Vector2(0.5f, 0.5f) && frame.anchorMax == new Vector2(0.5f, 0.5f);
                Check(sized && placed, "the backdrop's frame is BG_Table's, at its size and place", $"{frame.rect.size} at {frame.anchoredPosition}, BG_Table {size} at {centre}");
                Check(size.x >= 2520f - 0.01f && size.y >= 1440f - 0.01f, "the backdrop covers 21:9 and 4:3 screens too", size.ToString());
            }

            /// <summary>Opens a table with these equipped, checks what its backdrop shows, and closes it again.</summary>
            private void ShowEquipped(string backgroundId, string tableId, TableBackdropLibrary library, bool standIn, string snapshot)
            {
                _shop.Background = backgroundId;
                _shop.Table = tableId;
                _skinWarnings.Clear();
                StartMatch(false);
                try
                {
                    string what = $"{backgroundId ?? "(none)"} on {tableId ?? "(none)"}";
                    TableBackdrop expected = Expected(library, backgroundId, tableId, standIn, out int problems);
                    string kind = standIn ? "stand-in" : "game";
                    ShowsLayer(_table.BackdropBack, expected.Back, expected.Frame, $"the equipped background's scenery shows at the back ({kind} art)", what);
                    ShowsLayer(_table.BackdropTable, expected.Table, expected.Frame, $"the equipped table shows on it ({kind} art)", what);
                    ShowsLayer(_table.BackdropFront, expected.Front, expected.Frame, $"the equipped background's foreground shows in front of the table ({kind} art)", what);
                    Check(problems == _skinWarnings.Count, "a background or table without art shows the default and says so once", $"{what} ({kind} art): {_skinWarnings.Count} warnings, expected {problems}");

                    if (snapshot != null)
                    {
                        Snapshot(snapshot);
                    }
                }
                finally
                {
                    StopMatch();
                }
            }

            /// <summary>
            /// What the table must show, and how many of the two it must say it cannot show. The
            /// stand-in library is known here, so its answer is worked out independently of the
            /// library's own lookup; the game's library is asked.
            /// </summary>
            private static TableBackdrop Expected(TableBackdropLibrary library, string backgroundId, string tableId, bool standIn, out int problems)
            {
                problems = 0;
                if (!standIn)
                {
                    TableBackdrop shown = library.Backdrop(backgroundId, tableId);
                    problems = (shown.BackgroundId != backgroundId ? 1 : 0) + (shown.TableId != tableId ? 1 : 0);
                    return shown;
                }

                bool backgroundKnown = false, tableKnown = false;
                foreach (CosmeticItem item in CosmeticCatalog.Of(CosmeticKind.Background))
                {
                    backgroundKnown |= item.Id == backgroundId;
                }

                foreach (CosmeticItem item in CosmeticCatalog.Of(CosmeticKind.Table))
                {
                    tableKnown |= item.Id == tableId;
                }

                string background = backgroundKnown && backgroundId != NoArtBackground ? backgroundId : CosmeticCatalog.Backgrounds.Default;
                string table = tableKnown && tableId != NoArtTable ? tableId : CosmeticCatalog.Tables.Default;
                problems = (background != backgroundId ? 1 : 0) + (table != tableId ? 1 : 0);
                TableBackdropLibrary.BackgroundArt b = library.Background(background);
                TableBackdropLibrary.TableArt t = library.Table(table);
                return new TableBackdrop(background, table, b.Back, t.Table, b.Front, library.Frame);
            }

            private void ShowsLayer(Image image, BackdropLayer layer, Vector2 frame, string check, string what)
            {
                if (image == null)
                {
                    Fail(check, "no layer image");
                    return;
                }

                bool on = layer.Sprite != null;
                bool right = image.enabled == on && (!on || image.sprite == layer.Sprite);
                Check(right, check, $"{what}: shows {(image.enabled && image.sprite != null ? image.sprite.name : "nothing")}, expected {(on ? layer.Sprite.name : "nothing")}");
                if (!on)
                {
                    return;
                }

                // Where in the frame the picture lies, read back from its anchors.
                RectTransform rect = image.rectTransform;
                var area = Rect.MinMaxRect(rect.anchorMin.x * frame.x, (1f - rect.anchorMax.y) * frame.y, rect.anchorMax.x * frame.x, (1f - rect.anchorMin.y) * frame.y);
                Rect wanted = layer.Area.width > 0f ? layer.Area : new Rect(0f, 0f, frame.x, frame.y);
                bool placed = Mathf.Abs(area.xMin - wanted.xMin) < 0.01f && Mathf.Abs(area.yMin - wanted.yMin) < 0.01f
                    && Mathf.Abs(area.width - wanted.width) < 0.01f && Mathf.Abs(area.height - wanted.height) < 0.01f
                    && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero;
                Check(placed, "each backdrop layer lies on its part of the frame", $"{what}: {image.name} at {area}, expected {wanted}");
            }

            /// <summary>
            /// A library of plain pictures, one per layer: every background and table the shop sells but
            /// one of each. The default background has a foreground (as the Vista's red flowers are), so a
            /// background that falls back brings one along; the other backgrounds alternate with and without.
            /// </summary>
            private static TableBackdropLibrary StandInLibrary(TableBackdropLibrary real, List<UnityEngine.Object> made)
            {
                Sprite Picture(string name, int index)
                {
                    var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { name = name };
                    var colour = Color.HSVToRGB(index * 0.083f % 1f, 0.6f, 0.9f);
                    var pixels = new Color[64];
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        pixels[i] = colour;
                    }

                    texture.SetPixels(pixels);
                    texture.Apply(false);
                    Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = name;
                    made.Add(texture);
                    made.Add(sprite);
                    return sprite;
                }

                var backgrounds = new List<TableBackdropLibrary.BackgroundArt>();
                int n = 0, others = 0;
                foreach (CosmeticItem item in CosmeticCatalog.Of(CosmeticKind.Background))
                {
                    bool art = item.Id != NoArtBackground;
                    bool front = art && (item.Id == CosmeticCatalog.Backgrounds.Default || others++ % 2 == 1);
                    backgrounds.Add(new TableBackdropLibrary.BackgroundArt(
                        item.Id,
                        art ? new BackdropLayer(Picture(item.Id + " back", n++), Rect.zero) : default,
                        front ? new BackdropLayer(Picture(item.Id + " front", n++), StandInFrontArea) : default));
                }

                var tables = new List<TableBackdropLibrary.TableArt>();
                foreach (CosmeticItem item in CosmeticCatalog.Of(CosmeticKind.Table))
                {
                    bool art = item.Id != NoArtTable;
                    Rect area = tables.Count == 1 ? StandInTableArea : Rect.zero;
                    tables.Add(new TableBackdropLibrary.TableArt(item.Id, art ? new BackdropLayer(Picture(item.Id + " table", n++), area) : default));
                }

                TableBackdropLibrary library = ScriptableObject.CreateInstance<TableBackdropLibrary>();
                made.Add(library);
                library.Set(real.Frame, backgrounds.ToArray(), tables.ToArray(), CosmeticCatalog.Backgrounds.Default, CosmeticCatalog.Tables.Default);
                return library;
            }

            private void SetBackdropLibrary(TableBackdropLibrary library)
            {
                typeof(TableView).GetField("_backdrops", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .SetValue(_table, library);
            }

            private void OnSkinLog(string condition, string stackTrace, LogType type)
            {
                if (type == LogType.Warning && condition.StartsWith(SkinLogTag, StringComparison.Ordinal))
                {
                    _skinWarnings.Add(condition);
                }
            }

            /// <summary>
            /// The owner: the defaults (Himalayan Vista on the Himalayan Ivory table) look as the table
            /// did before the layers. The backdrop alone (everything else put away) is drawn twice in the
            /// same frame, once as the three default layers and once as BG_Table, and the two pictures
            /// are compared pixel by pixel.
            /// </summary>
            private void VerifyDefaultLooksAsBefore(TableBackdropLibrary real)
            {
                var plate = AssetDatabase.LoadAssetAtPath<Sprite>(TableBackdropBuilder.PlatePath);
                if (plate == null)
                {
                    Note("no BG_Table to compare the default backdrop with");
                    return;
                }

                var hidden = new List<GameObject>();
                Transform frame = _table.BackdropBack.transform.parent;
                foreach (Transform part in _canvas)
                {
                    if (part != _table.transform && part.gameObject.activeSelf)
                    {
                        hidden.Add(part.gameObject);
                    }
                }

                foreach (Transform part in _table.transform)
                {
                    if (part != frame && part.gameObject.activeSelf)
                    {
                        hidden.Add(part.gameObject);
                    }
                }

                Color32[] layered, before;
                try
                {
                    foreach (GameObject part in hidden)
                    {
                        part.SetActive(false);
                    }

                    TableBackdrop defaults = real.Default;
                    _table.SetBackdrop(defaults);
                    layered = RenderBackdrop("BackdropDefault");
                    _table.SetBackdrop(new TableBackdrop(defaults.BackgroundId, defaults.TableId, new BackdropLayer(plate, Rect.zero), default, default, defaults.Frame));
                    before = RenderBackdrop("BackdropPlate");
                }
                finally
                {
                    _table.SetBackdrop(real.Default);
                    foreach (GameObject part in hidden)
                    {
                        part.SetActive(true);
                    }
                }

                long sum = 0;
                int worst = 0, off8 = 0, off24 = 0;
                for (int i = 0; i < layered.Length; i++)
                {
                    int d = Math.Max(Math.Abs(layered[i].r - before[i].r), Math.Max(Math.Abs(layered[i].g - before[i].g), Math.Abs(layered[i].b - before[i].b)));
                    sum += Math.Abs(layered[i].r - before[i].r) + Math.Abs(layered[i].g - before[i].g) + Math.Abs(layered[i].b - before[i].b);
                    worst = Math.Max(worst, d);
                    off8 += d > 8 ? 1 : 0;
                    off24 += d > 24 ? 1 : 0;
                }

                float mean = sum / (3f * layered.Length);
                float share24 = off24 * 100f / layered.Length;
                Note($"default backdrop against BG_Table at 1920 x 1080: mean {mean:0.000} levels, worst {worst}, {off8 * 100f / layered.Length:0.000}% of pixels off by more than 8, {share24:0.000}% by more than 24");
                Check(mean <= 1f, "the default backdrop looks as BG_Table did (mean difference at most 1 level)", $"mean {mean:0.000}");
                Check(share24 <= 0.5f, "the default backdrop looks as BG_Table did (at most 0.5% of pixels off by more than 24 levels)", $"{share24:0.000}%");
            }

            /// <summary>The camera's picture of the canvas now, at 1920 × 1080 (also written to Temp/Captures with captures on).</summary>
            private Color32[] RenderBackdrop(string name)
            {
                const int width = 1920, height = 1080;
                if (_target == null)
                {
                    _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    _camera.targetTexture = _target;
                }

                Canvas.ForceUpdateCanvases();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _target };
                if (RenderPipeline.SupportsRenderRequest(_camera, request))
                {
                    RenderPipeline.SubmitRenderRequest(_camera, request);
                }
                else
                {
                    _camera.Render();
                }

                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = _target;
                var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                try
                {
                    pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    pixels.Apply();
                    if (_captures)
                    {
                        Directory.CreateDirectory(ScreenCaptureTool.OutputFolder);
                        File.WriteAllBytes($"{ScreenCaptureTool.OutputFolder}/Sim_{name}.png", pixels.EncodeToPNG());
                    }

                    return pixels.GetPixels32();
                }
                finally
                {
                    RenderTexture.active = previous;
                    UnityEngine.Object.DestroyImmediate(pixels);
                }
            }
        }
    }
}
