using System;
using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Editor.Screens;
using Callbreak.Presentation.Table;
using UnityEngine;
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

            // Where a stand-in library leaves a table without art.
            private const string NoArtTable = CosmeticCatalog.Tables.DuskHimalaya;

            // The owner (5 Oct 2026): the game keeps three backgrounds, and these left it. A player may
            // still have one saved (the shop falls back to the default) or equipped.
            private static readonly string[] RemovedBackgrounds = { "kathmandu-courtyard", "village-terrace", "vintage-home" };

            private readonly BackdropShop _shop = new BackdropShop();
            private readonly List<string> _skinWarnings = new List<string>();

            /// <summary>
            /// The backdrop (the owner, 4 Oct 2026; V2, 28 Oct 2026): every table the shop sells fills
            /// the screen behind everything, in the backdrop frame, as the player equips it; the
            /// equipped background shows behind the menus, not here. First with a library of stand-in
            /// pictures, so each table is told apart whatever art is in yet (one table without art);
            /// then with the game's own library. That library holds only what the shop sells (all of it
            /// loads with the table), and a table without art, or unknown, shows the default and says
            /// so once.
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
                    TableBackdropLibrary standIn = StandInLibrary(made);
                    SetBackdropLibrary(standIn);
                    foreach (CosmeticItem table in CosmeticCatalog.Of(CosmeticKind.Table))
                    {
                        ShowEquipped(table.Id, standIn, true, null);
                    }

                    ShowEquipped("glass-top", standIn, true, null);
                    ShowEquipped(null, standIn, true, null);
                }
                finally
                {
                    SetBackdropLibrary(real);
                    foreach (UnityEngine.Object o in made)
                    {
                        UnityEngine.Object.DestroyImmediate(o);
                    }
                }

                int shown = 0;
                try
                {
                    foreach (CosmeticItem table in CosmeticCatalog.Of(CosmeticKind.Table))
                    {
                        ShowEquipped(table.Id, real, false, $"Backdrop_{TableBackdropBuilder.ArtName(table.Id)}");
                        shown++;
                    }

                    // The equipped background is not the table's: whatever it is, the table shows the equipped table.
                    _shop.Background = RemovedBackgrounds[0];
                    ShowEquipped(CosmeticCatalog.Tables.IndigoDhaka, real, false, null);
                }
                finally
                {
                    _shop.Background = CosmeticCatalog.Backgrounds.Default;
                    UnityEngine.Application.logMessageReceived -= OnSkinLog;
                }

                int backgroundsWithArt = 0, tablesWithArt = 0;
                for (int i = 0; i < real.BackgroundCount; i++)
                {
                    backgroundsWithArt += real.BackgroundAt(i).Sprite != null ? 1 : 0;
                }

                for (int i = 0; i < real.TableCount; i++)
                {
                    tablesWithArt += real.TableAt(i).Sprite != null ? 1 : 0;
                }

                Note($"backdrop art in the library: {backgroundsWithArt} of {real.BackgroundCount} backgrounds, {tablesWithArt} of {real.TableCount} tables; {shown} tables shown with the game's art");
            }

            /// <summary>
            /// GDD/owner: the table lies behind everything else, over the backdrop frame (the layout's
            /// background: the screen plus margins for 21:9 and 4:3), and takes no touch.
            /// </summary>
            private void VerifyBackdropPlace()
            {
                Image table = _table.BackdropTable;
                Check(table != null, "the table has its backdrop picture");
                if (table == null)
                {
                    return;
                }

                var frame = table.rectTransform.parent as RectTransform;
                Check(frame != null && frame.parent == _table.transform && frame.GetSiblingIndex() == 0, "the backdrop is behind everything else on the table");
                Check(!table.raycastTarget, "the backdrop takes no touch");
                RectTransform rect = table.rectTransform;
                Check(
                    rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero,
                    "the table fills the backdrop frame",
                    $"anchors {rect.anchorMin} to {rect.anchorMax}, offsets {rect.offsetMin} {rect.offsetMax}");
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
                Check(sized && placed, "the backdrop's frame is the layout background's, at its size and place", $"{frame.rect.size} at {frame.anchoredPosition}, layout {size} at {centre}");
                Check(size.x >= 2520f - 0.01f && size.y >= 1440f - 0.01f, "the backdrop covers 21:9 and 4:3 screens too", size.ToString());
            }

            /// <summary>Opens a table with this table equipped, checks what its backdrop shows, and closes it again.</summary>
            private void ShowEquipped(string tableId, TableBackdropLibrary library, bool standIn, string snapshot)
            {
                _shop.Table = tableId;
                _skinWarnings.Clear();
                StartMatch(false);
                try
                {
                    string what = tableId ?? "(none)";
                    string kind = standIn ? "stand-in" : "game";
                    Sprite expected = Expected(library, tableId, standIn, out int problems);
                    Image image = _table.BackdropTable;
                    bool right = image != null && image.enabled && image.sprite == expected;
                    string actual = image != null && image.enabled && image.sprite != null ? image.sprite.name : "nothing";
                    Check(right, $"the equipped table fills the screen ({kind} art)", $"{what}: shows {actual}, expected {(expected != null ? expected.name : "nothing")}");
                    Check(problems == _skinWarnings.Count, "a table without art shows the default and says so once", $"{what} ({kind} art): {_skinWarnings.Count} warnings, expected {problems}");

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
            /// What the table must show, and whether it must say it cannot show the one asked for. The
            /// stand-in library is known here, so its answer is worked out independently of the
            /// library's own lookup; the game's library is asked.
            /// </summary>
            private static Sprite Expected(TableBackdropLibrary library, string tableId, bool standIn, out int problems)
            {
                if (!standIn)
                {
                    Sprite shown = library.TablePicture(tableId, out string shownId);
                    problems = shownId != tableId ? 1 : 0;
                    return shown;
                }

                bool known = false;
                foreach (CosmeticItem item in CosmeticCatalog.Of(CosmeticKind.Table))
                {
                    known |= item.Id == tableId;
                }

                string table = known && tableId != NoArtTable ? tableId : CosmeticCatalog.Tables.Default;
                problems = table != tableId ? 1 : 0;
                return library.Table(table).Sprite;
            }

            /// <summary>A library of plain pictures: every background and table the shop sells, but one table without art.</summary>
            private static TableBackdropLibrary StandInLibrary(List<UnityEngine.Object> made)
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

                int n = 0;
                var backgrounds = new List<TableBackdropLibrary.Picture>();
                foreach (CosmeticItem item in CosmeticCatalog.Of(CosmeticKind.Background))
                {
                    backgrounds.Add(new TableBackdropLibrary.Picture(item.Id, Picture(item.Id + " scene", n++)));
                }

                var tables = new List<TableBackdropLibrary.Picture>();
                foreach (CosmeticItem item in CosmeticCatalog.Of(CosmeticKind.Table))
                {
                    tables.Add(new TableBackdropLibrary.Picture(item.Id, item.Id != NoArtTable ? Picture(item.Id + " table", n++) : null));
                }

                TableBackdropLibrary library = ScriptableObject.CreateInstance<TableBackdropLibrary>();
                made.Add(library);
                library.Set(backgrounds.ToArray(), tables.ToArray(), CosmeticCatalog.Backgrounds.Default, CosmeticCatalog.Tables.Default);
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
        }
    }
}
