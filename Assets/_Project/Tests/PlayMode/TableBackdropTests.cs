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
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The backdrop (the owner, 4 Oct 2026; mockups 20 and 21): the real Table scene shows, behind
    /// everything, the background and the table this player equipped in the shop, as three layers in
    /// BG_Table's frame (the scenery, the table, the scenery's foreground). A change in the shop shows
    /// at the next match. A background or table the build has no art for shows the default and the
    /// log says so once; so does a background the game no longer has (the owner, 5 Oct 2026), equipped
    /// or saved. What the table must show for an id comes from the table's own library, so these hold
    /// before and after the art lands. Saves go to a temporary folder.
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
        public IEnumerator TheEquippedBackgroundAndTable_ShowBehindEverything_AndAChangeShowsAtTheNextMatch()
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
            shop.Equip(CosmeticKind.Background, CosmeticCatalog.Backgrounds.PokharaLakeside);
            Assert.AreEqual(PurchaseResult.Bought, shop.Buy(CosmeticKind.Table, CosmeticCatalog.Tables.RoyalCrimson), "the starting coins buy the Royal Crimson table");
            shop.Equip(CosmeticKind.Table, CosmeticCatalog.Tables.RoyalCrimson);

            _warnings.Clear();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return UntilTableOpens("the first match's table to open");
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();
            AssertShows(table, CosmeticCatalog.Backgrounds.PokharaLakeside, CosmeticCatalog.Tables.RoyalCrimson);
            AssertBehindEverything(table);

            // Back to the menu (the match is kept), a new choice in the shop, and a new match.
            GameFixture.PressIn(table, "HomeButton");
            GameFixture.Press(_game.TableRouter, "LeaveButton");
            yield return null;
            _game.PressDialog(confirm: true);
            yield return _game.UntilMenu();
            shop.Equip(CosmeticKind.Background, CosmeticCatalog.Backgrounds.MonasteryGarden);
            shop.Equip(CosmeticKind.Table, CosmeticCatalog.Tables.HimalayanIvory);

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
            AssertShows(table, CosmeticCatalog.Backgrounds.MonasteryGarden, CosmeticCatalog.Tables.HimalayanIvory);
        }

        /// <summary>
        /// The owner (5 Oct 2026): the game keeps three backgrounds. A player who still has one of the
        /// three that left equipped (Kathmandu Courtyard here) sees the Himalayan Vista on the table
        /// they equipped, and the log names the old background once.
        /// </summary>
        [UnityTest]
        public IEnumerator ARemovedBackground_ShowsTheHimalayanVista_AndTheLogSaysSoOnce()
        {
            const string removed = "kathmandu-courtyard";
            string crimson = CosmeticCatalog.Tables.RoyalCrimson;
            var shop = new EquippedOnly(removed, crimson);
            yield return _game.Boot(extra: builder => builder.RegisterInstance<ICosmeticsShop>(shop));
            Assert.AreSame(shop, _game.Services.Resolve<ICosmeticsShop>());

            _warnings.Clear();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return UntilTableOpens("the table to open");
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();

            Assert.IsNotNull(table.Backdrops, "the table holds the library of backgrounds and tables");
            Assert.IsNull(table.Backdrops.Background(removed), "the table's library no longer has it");
            TableBackdrop vista = table.Backdrops.Backdrop(CosmeticCatalog.Backgrounds.HimalayanVista, crimson);
            Assert.AreEqual(CosmeticCatalog.Backgrounds.HimalayanVista, vista.BackgroundId, "the Himalayan Vista has its art");
            AssertLayer(vista.Back, table.BackdropBack, "the Himalayan Vista's scenery");
            AssertLayer(vista.Table, table.BackdropTable, "the equipped table");
            AssertLayer(vista.Front, table.BackdropFront, "the Himalayan Vista's foreground");
            Assert.AreEqual(1, Count(removed), "the removed background is named once: " + string.Join(" | ", _warnings));
            Assert.AreEqual(vista.TableId == crimson ? 0 : 1, Count(crimson), "the table is named only when it has no art: " + string.Join(" | ", _warnings));
        }

        /// <summary>
        /// The same from a save file written before 5 Oct 2026, through the game's own shop: the match
        /// shows the Himalayan Vista on the table the shop has equipped, and the old id is never named
        /// more than once (the shop may fall back to the default itself, so the table may never see it).
        /// </summary>
        [UnityTest]
        public IEnumerator ARemovedBackgroundInTheSave_ShowsTheHimalayanVista()
        {
            const string removed = "village-terrace";
            string crimson = CosmeticCatalog.Tables.RoyalCrimson;
            yield return _game.Boot(seed: saves => saves.Save(CosmeticsService.SaveKey, new CosmeticsData
            {
                equippedBackgroundId = removed,
                equippedTableId = crimson,
                boughtTableIds = new List<string> { crimson },
            }));
            string equippedTable = _game.Services.Resolve<ICosmeticsShop>().Equipped(CosmeticKind.Table);

            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return UntilTableOpens("the table to open");
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();

            Assert.IsNotNull(table.Backdrops, "the table holds the library of backgrounds and tables");
            TableBackdrop vista = table.Backdrops.Backdrop(CosmeticCatalog.Backgrounds.HimalayanVista, equippedTable);
            Assert.AreEqual(CosmeticCatalog.Backgrounds.HimalayanVista, vista.BackgroundId, "the Himalayan Vista has its art");
            AssertLayer(vista.Back, table.BackdropBack, "the Himalayan Vista's scenery");
            AssertLayer(vista.Table, table.BackdropTable, "the equipped table");
            AssertLayer(vista.Front, table.BackdropFront, "the Himalayan Vista's foreground");
            Assert.LessOrEqual(Count(removed), 1, "never named more than once: " + string.Join(" | ", _warnings));
        }

        [UnityTest]
        public IEnumerator AnUnknownBackgroundAndTable_ShowTheDefaults_AndTheLogSaysSoOnce()
        {
            var shop = new EquippedOnly("rooftop-cafe", "glass-top");
            yield return _game.Boot(extra: builder => builder.RegisterInstance<ICosmeticsShop>(shop));
            Assert.AreSame(shop, _game.Services.Resolve<ICosmeticsShop>());

            _warnings.Clear();
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return UntilTableOpens("the table to open");
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();

            TableBackdrop defaults = table.Backdrops.Default;
            AssertLayer(defaults.Back, table.BackdropBack, "the default scenery");
            AssertLayer(defaults.Table, table.BackdropTable, "the default table");
            AssertLayer(defaults.Front, table.BackdropFront, "the default foreground");
            Assert.AreEqual(1, Count("rooftop-cafe"), "the unknown background is named once: " + string.Join(" | ", _warnings));
            Assert.AreEqual(1, Count("glass-top"), "the unknown table is named once: " + string.Join(" | ", _warnings));
        }

        /// <summary>As <see cref="GameFixture.UntilTable"/>, saying which table it waited for.</summary>
        private IEnumerator UntilTableOpens(string what)
        {
            yield return GameFixture.Until(
                () => _game.Flow.State == AppState.InMatch && _game.Table != null && _game.Table.Container != null && !_game.Curtain.IsCovering,
                what);
        }

        /// <summary>The table shows what its library has for these ids; one it has no art for is named once in the log.</summary>
        private void AssertShows(TableView table, string backgroundId, string tableId)
        {
            Assert.IsNotNull(table, "the table is open");
            Assert.IsNotNull(table.Backdrops, "the table holds the library of backgrounds and tables");
            TableBackdrop expected = table.Backdrops.Backdrop(backgroundId, tableId);
            AssertLayer(expected.Back, table.BackdropBack, backgroundId + "'s scenery");
            AssertLayer(expected.Table, table.BackdropTable, tableId);
            AssertLayer(expected.Front, table.BackdropFront, backgroundId + "'s foreground");
            Assert.AreEqual(expected.BackgroundId == backgroundId ? 0 : 1, Count(backgroundId), "said once when it has no art: " + string.Join(" | ", _warnings));
            Assert.AreEqual(expected.TableId == tableId ? 0 : 1, Count(tableId), "said once when it has no art: " + string.Join(" | ", _warnings));
        }

        private static void AssertLayer(BackdropLayer layer, Image image, string what)
        {
            Assert.IsNotNull(image, what + ": the layer exists");
            Assert.AreEqual(layer.Sprite != null, image.enabled, what + ": shown when it has a picture");
            if (layer.Sprite != null)
            {
                Assert.AreSame(layer.Sprite, image.sprite, what);
            }
        }

        /// <summary>The three layers share BG_Table's frame, which is the first thing on the table, and take no touch.</summary>
        private static void AssertBehindEverything(TableView table)
        {
            Transform frame = table.BackdropBack.transform.parent;
            Assert.AreSame(frame, table.BackdropTable.transform.parent);
            Assert.AreSame(frame, table.BackdropFront.transform.parent);
            Assert.AreSame(table.transform, frame.parent);
            Assert.AreEqual(0, frame.GetSiblingIndex(), "the backdrop is drawn first, behind everything else");
            Assert.Less(table.BackdropBack.transform.GetSiblingIndex(), table.BackdropTable.transform.GetSiblingIndex(), "the table lies on the scenery");
            Assert.Less(table.BackdropTable.transform.GetSiblingIndex(), table.BackdropFront.transform.GetSiblingIndex(), "the foreground lies on the table");
            Assert.IsFalse(table.BackdropBack.raycastTarget || table.BackdropTable.raycastTarget || table.BackdropFront.raycastTarget, "the backdrop takes no touch");
            Assert.IsTrue(table.BackdropBack.enabled, "there is always scenery");
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
