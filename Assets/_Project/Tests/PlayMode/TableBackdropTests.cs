using System;
using System.Collections;
using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Flow;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The backgrounds and tables the shop sells (the owner, 4 Oct 2026; mockups 20 and 21), as V2
    /// draws them (the owner, 28 Oct 2026): in the real game the equipped background shows behind the
    /// menus, and changes as soon as another is equipped; the equipped table fills the screen behind
    /// everything at the table, and a change in the shop shows at the next match. A table the build
    /// has no art for shows the default and the log says so once. A background the game has no art
    /// for, or no longer has (the owner, 5 Oct 2026), equipped or saved, shows the Himalayan Vista.
    /// What must show for an id comes from the game's own library, so these hold whatever art is in.
    /// Saves go to a temporary folder.
    /// </summary>
    public sealed class TableBackdropTests
    {
        private const string SkinLogTag = "[TableSkin]";

        private readonly List<string> _warnings = new List<string>();
        private GameFixture _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            UnityEngine.Application.logMessageReceived += OnLog;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.Application.logMessageReceived -= OnLog;
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator TheEquippedBackgroundShowsBehindTheMenus_AndTheEquippedTableFillsTheTable()
        {
            // The two backgrounds were bought before (500 and 750 coins since 6 Oct 2026).
            yield return _game.Boot(seed: saves =>
            {
                var bought = new CosmeticsData();
                bought.boughtBackgroundIds.Add(CosmeticCatalog.Backgrounds.PokharaLakeside);
                bought.boughtBackgroundIds.Add(CosmeticCatalog.Backgrounds.MonasteryGarden);
                saves.Save(CosmeticsService.SaveKey, bought);
            });
            ICosmeticsShop shop = _game.Services.Resolve<ICosmeticsShop>();
            AssertMenusShow(CosmeticCatalog.Backgrounds.HimalayanVista, "the default background before anything is equipped");

            shop.Equip(CosmeticKind.Background, CosmeticCatalog.Backgrounds.PokharaLakeside);
            AssertMenusShow(CosmeticCatalog.Backgrounds.PokharaLakeside, "a background shows behind the menus as soon as it is equipped");
            Assert.AreEqual(PurchaseResult.Bought, shop.Buy(CosmeticKind.Table, CosmeticCatalog.Tables.RoyalCrimson), "the starting coins buy the Royal Crimson table");
            shop.Equip(CosmeticKind.Table, CosmeticCatalog.Tables.RoyalCrimson);

            _warnings.Clear();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            AssertMenusShow(CosmeticCatalog.Backgrounds.PokharaLakeside, "the next screen shows it too");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return UntilTableOpens("the first match's table to open");
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();
            AssertShows(table, CosmeticCatalog.Tables.RoyalCrimson);
            AssertBehindEverything(table);

            // Back to the menu (the match is kept), a new choice in the shop, and a new match.
            GameFixture.PressIn(table, "HomeButton");
            GameFixture.Press(_game.TableRouter, "LeaveButton");
            yield return null;
            _game.PressDialog(confirm: true);
            yield return _game.UntilMenu();
            AssertMenusShow(CosmeticCatalog.Backgrounds.PokharaLakeside, "the menus after the table");
            shop.Equip(CosmeticKind.Background, CosmeticCatalog.Backgrounds.MonasteryGarden);
            shop.Equip(CosmeticKind.Table, CosmeticCatalog.Tables.HimalayanIvory);
            AssertMenusShow(CosmeticCatalog.Backgrounds.MonasteryGarden, "a new background shows at once");

            _warnings.Clear();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            yield return GameFixture.Until(() => _game.Dialogs.IsOpen, "\"Resume your match?\"");
            _game.PressDialog(confirm: false);
            yield return GameFixture.Until(() => _game.MenuRouter.Current == ScreenId.OfflineSetup && !_game.Dialogs.IsOpen, "a new game's setup");
            GameFixture.Press(_game.MenuRouter, "StartButton");

            // The left match is still saved: starting a new one asks before it replaces it.
            yield return GameFixture.Until(() => _game.Dialogs.IsOpen, "\"Replace your saved match?\"");
            _game.PressDialog(confirm: true);
            yield return UntilTableOpens("the next match's table to open");
            table = UnityEngine.Object.FindFirstObjectByType<TableView>();
            AssertShows(table, CosmeticCatalog.Tables.HimalayanIvory);
        }

        /// <summary>
        /// The owner (5 Oct 2026): the game keeps three backgrounds. A player who still has one of the
        /// three that left equipped (Kathmandu Courtyard here) sees the Himalayan Vista behind the menus,
        /// and the table they equipped at the table; the table does not name the background.
        /// </summary>
        [UnityTest]
        public IEnumerator ARemovedBackground_ShowsTheHimalayanVistaBehindTheMenus()
        {
            const string removed = "kathmandu-courtyard";
            string crimson = CosmeticCatalog.Tables.RoyalCrimson;
            var shop = new EquippedOnly(removed, crimson);
            yield return _game.Boot(extra: builder => builder.RegisterInstance<ICosmeticsShop>(shop));
            Assert.AreSame(shop, _game.Services.Resolve<ICosmeticsShop>());
            AssertMenusShow(CosmeticCatalog.Backgrounds.HimalayanVista, "the default for a removed background");

            _warnings.Clear();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return UntilTableOpens("the table to open");
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();

            Assert.IsNotNull(table.Backdrops, "the table holds the library of backgrounds and tables");
            Assert.IsNull(table.Backdrops.Background(removed), "the library no longer has it");
            AssertShows(table, crimson);
            Assert.AreEqual(0, Count(removed), "the table does not show backgrounds: " + string.Join(" | ", _warnings));
        }

        /// <summary>
        /// The same from a save file written before 5 Oct 2026, through the game's own shop: the menus
        /// show the Himalayan Vista and the match shows the table the shop has equipped.
        /// </summary>
        [UnityTest]
        public IEnumerator ARemovedBackgroundInTheSave_ShowsTheHimalayanVistaBehindTheMenus()
        {
            const string removed = "village-terrace";
            string crimson = CosmeticCatalog.Tables.RoyalCrimson;
            yield return _game.Boot(seed: saves => saves.Save(CosmeticsService.SaveKey, new CosmeticsData
            {
                equippedBackgroundId = removed,
                equippedTableId = crimson,
                boughtTableIds = new List<string> { crimson },
            }));
            AssertMenusShow(CosmeticCatalog.Backgrounds.HimalayanVista, "the default for a removed background");
            string equippedTable = _game.Services.Resolve<ICosmeticsShop>().Equipped(CosmeticKind.Table);

            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return UntilTableOpens("the table to open");
            AssertShows(UnityEngine.Object.FindFirstObjectByType<TableView>(), equippedTable);
        }

        [UnityTest]
        public IEnumerator AnUnknownBackgroundAndTable_ShowTheDefaults_AndTheTableSaysSoOnce()
        {
            var shop = new EquippedOnly("rooftop-cafe", "glass-top");
            yield return _game.Boot(extra: builder => builder.RegisterInstance<ICosmeticsShop>(shop));
            Assert.AreSame(shop, _game.Services.Resolve<ICosmeticsShop>());
            AssertMenusShow(CosmeticCatalog.Backgrounds.HimalayanVista, "the default background");

            _warnings.Clear();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return UntilTableOpens("the table to open");
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();

            Assert.AreSame(table.Backdrops.TablePicture(table.Backdrops.DefaultTableId, out _), table.BackdropTable.sprite, "the default table");
            Assert.IsTrue(table.BackdropTable.enabled);
            Assert.AreEqual(1, Count("glass-top"), "the unknown table is named once: " + string.Join(" | ", _warnings));
            Assert.AreEqual(0, Count("rooftop-cafe"), "the table does not show backgrounds: " + string.Join(" | ", _warnings));
        }

        /// <summary>As <see cref="GameFixture.UntilTable"/>, saying which table it waited for.</summary>
        private IEnumerator UntilTableOpens(string what)
        {
            yield return GameFixture.Until(
                () => _game.Flow.State == AppState.InMatch && _game.Table != null && _game.Table.Container != null && !_game.Curtain.IsCovering,
                what);
        }

        /// <summary>Every menu screen on show has the background's picture behind it (the library's, for this id).</summary>
        private static void AssertMenusShow(string backgroundId, string what)
        {
            EquippedBackground[] shown = UnityEngine.Object.FindObjectsByType<EquippedBackground>(FindObjectsSortMode.None);
            Assert.IsNotEmpty(shown, what + ": a screen with the background is on show");
            foreach (EquippedBackground background in shown)
            {
                Assert.IsNotNull(background.Library, what + ": " + background.name + " has the library");
                Sprite expected = background.Library.BackgroundPicture(backgroundId, out _);
                Assert.IsNotNull(expected, what + ": the game has a picture for " + backgroundId);
                Assert.AreSame(expected, background.Shown, what + " (" + background.transform.parent.name + ")");
            }
        }

        /// <summary>The table shows what its library has for this id; one it has no art for is named once in the log.</summary>
        private void AssertShows(TableView table, string tableId)
        {
            Assert.IsNotNull(table, "the table is open");
            Assert.IsNotNull(table.Backdrops, "the table holds the library of backgrounds and tables");
            Sprite expected = table.Backdrops.TablePicture(tableId, out string shownId);
            Assert.IsNotNull(expected, tableId + " has a picture");
            Assert.IsTrue(table.BackdropTable.enabled, tableId + ": shown");
            Assert.AreSame(expected, table.BackdropTable.sprite, tableId);
            Assert.AreEqual(shownId == tableId ? 0 : 1, Count(tableId), "said once when it has no art: " + string.Join(" | ", _warnings));
        }

        /// <summary>The table fills the backdrop frame, which is the first thing on the table, and takes no touch.</summary>
        private static void AssertBehindEverything(TableView table)
        {
            var picture = table.BackdropTable.rectTransform;
            Transform frame = picture.parent;
            Assert.AreSame(table.transform, frame.parent);
            Assert.AreEqual(0, frame.GetSiblingIndex(), "the backdrop is drawn first, behind everything else");
            Assert.AreEqual(Vector2.zero, picture.anchorMin, "the table fills the frame");
            Assert.AreEqual(Vector2.one, picture.anchorMax, "the table fills the frame");
            Assert.AreEqual(Vector2.zero, picture.offsetMin);
            Assert.AreEqual(Vector2.zero, picture.offsetMax);
            Assert.IsFalse(table.BackdropTable.raycastTarget, "the backdrop takes no touch");
        }

        private int Count(string id)
        {
            int n = 0;
            foreach (string warning in _warnings)
            {
                if (warning.Contains("\"" + id + "\""))
                {
                    n++;
                }
            }

            return n;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Warning && condition.StartsWith(SkinLogTag, StringComparison.Ordinal))
            {
                _warnings.Add(condition);
            }
        }

        /// <summary>A shop whose player has these equipped, whatever they are.</summary>
        private sealed class EquippedOnly : ICosmeticsShop
        {
            private readonly string _background;
            private readonly string _table;

            public EquippedOnly(string background, string table)
            {
                _background = background;
                _table = table;
            }

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
                        return _background;
                    case CosmeticKind.Table:
                        return _table;
                    default:
                        return CosmeticCatalog.DefaultOf(kind);
                }
            }

            public void Equip(CosmeticKind kind, string id)
            {
            }

            public PurchaseResult Buy(CosmeticKind kind, string id) => PurchaseResult.AlreadyOwned;
        }
    }
}
