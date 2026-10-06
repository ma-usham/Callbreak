using System.Linq;
using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Screens;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// The shop in a state of each category, for the screen captures (the tiles are filled at run
    /// time by the presenter). In each the first item is equipped and the other two are for sale
    /// at the catalogue's prices (500 and 750 since 6 Oct 2026): Cards with Classic Crimson chosen,
    /// as mockup 15; Backgrounds with Pokhara Lakeside chosen ("Buy for 500"); Tables as 21, with
    /// Royal Crimson chosen ("Buy for 500").
    /// </summary>
    public static class ShopPreview
    {
        public const int Cards = 0;
        public const int Backgrounds = 1;
        public const int Tables = 2;

        public static void Prepare(GameObject instance) => PrepareCategory(instance, Cards);

        /// <summary>Captures every category at 16:9, 21:9 and 4:3 into Temp/Captures (Shop_Cards_..., Shop_Backgrounds_..., Shop_Tables_...).</summary>
        public static string CaptureAll()
        {
            string prefab = ScreenLayoutBuilder.PrefabPath("Shop");
            string[] names = { "Cards", "Backgrounds", "Tables" };
            foreach (int category in new[] { Cards, Backgrounds, Tables })
            {
                int c = category;
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2520, 1080), new Vector2Int(1920, 1440) })
                {
                    ScreenCaptureTool.Capture(prefab, size.x, size.y, $"{ScreenCaptureTool.OutputFolder}/Shop_{names[c]}_{size.x}x{size.y}.png", go => PrepareCategory(go, c));
                }
            }

            return "captured Shop_{Cards,Backgrounds,Tables}_{1920x1080,2520x1080,1920x1440}.png";
        }

        public static void PrepareCategory(GameObject instance, int category)
        {
            var view = instance.GetComponent<ShopView>();
            if (view == null)
            {
                return;
            }

            ShopTileStatus[] firstOwned = { ShopTileStatus.Equipped, ShopTileStatus.Price, ShopTileStatus.Price };

            Fill(view, Cards,
                new[] { "Classic Crimson", "Himalayan Ivory", "Kathmandu Gold" },
                new[] { Back("ClassicCrimson"), Back("HimalayanIvory"), Back("KathmanduGold") },
                firstOwned,
                Prices(CosmeticKind.Deck),
                chosen: 0);

            string[] backgrounds = { CosmeticCatalog.Backgrounds.HimalayanVista, CosmeticCatalog.Backgrounds.PokharaLakeside, CosmeticCatalog.Backgrounds.MonasteryGarden };
            Fill(view, Backgrounds,
                new[] { "Himalayan Vista", "Pokhara Lakeside", "Monastery Garden" },
                System.Array.ConvertAll(backgrounds, id => view.Picture(CosmeticKind.Background, id)),
                firstOwned,
                Prices(CosmeticKind.Background),
                chosen: 1);

            string[] tables = { CosmeticCatalog.Tables.HimalayanIvory, CosmeticCatalog.Tables.RoyalCrimson, CosmeticCatalog.Tables.HeritageWalnut };
            Fill(view, Tables,
                new[] { "Himalayan Ivory", "Royal Crimson", "Heritage Walnut" },
                System.Array.ConvertAll(tables, id => view.Picture(CosmeticKind.Table, id)),
                firstOwned,
                Prices(CosmeticKind.Table),
                chosen: 1);

            view.ShowCategory(category);
            if (category == Cards)
            {
                view.SetPrimaryUse(true);
            }
            else
            {
                view.SetPrimaryBuy(Prices(category == Tables ? CosmeticKind.Table : CosmeticKind.Background)[1], true);
            }
        }

        /// <summary>Each item's price as its tile shows it, from the catalogue; null for the free first one.</summary>
        private static string[] Prices(CosmeticKind kind)
        {
            return CosmeticCatalog.Of(kind).Select(item => item.Price > 0 ? ShopDisplay.Price(item.Price) : null).ToArray();
        }

        private static void Fill(ShopView view, int category, string[] names, Sprite[] pictures, ShopTileStatus[] statuses, string[] prices, int chosen)
        {
            view.SetTileCount(category, names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                ShopTileStatus status = statuses[i];
                view.SetTile(category, i, names[i], pictures[i], status, StatusText(status), prices != null ? prices[i] : null, i == chosen);
            }
        }

        private static string StatusText(ShopTileStatus status) => status == ShopTileStatus.Equipped ? "Equipped" : null;

        private static Sprite Back(string deck) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Cards/{deck}/Back_{deck}.png");
    }
}
