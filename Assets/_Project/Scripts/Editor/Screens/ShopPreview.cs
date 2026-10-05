using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Screens;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// The shop in a state of each category, for the screen captures (the tiles are filled at run
    /// time by the presenter): Cards as mockup 15 draws it (Classic Crimson equipped and chosen),
    /// Backgrounds with its three (the Himalayan Vista equipped, Pokhara Lakeside chosen) and
    /// Tables as 21 (Himalayan Ivory equipped, Royal Crimson chosen and for sale: "Buy for 350").
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

            Fill(view, Cards,
                new[] { "Classic Crimson", "Himalayan Ivory", "Kathmandu Gold" },
                new[] { Back("ClassicCrimson"), Back("HimalayanIvory"), Back("KathmanduGold") },
                new[] { ShopTileStatus.Equipped, ShopTileStatus.None, ShopTileStatus.None },
                null,
                chosen: 0);

            string[] backgrounds = { CosmeticCatalog.Backgrounds.HimalayanVista, CosmeticCatalog.Backgrounds.PokharaLakeside, CosmeticCatalog.Backgrounds.MonasteryGarden };
            Fill(view, Backgrounds,
                new[] { "Himalayan Vista", "Pokhara Lakeside", "Monastery Garden" },
                System.Array.ConvertAll(backgrounds, id => view.Picture(CosmeticKind.Background, id)),
                new[] { ShopTileStatus.Equipped, ShopTileStatus.None, ShopTileStatus.None },
                null,
                chosen: 1);

            string[] tables = { CosmeticCatalog.Tables.HimalayanIvory, CosmeticCatalog.Tables.RoyalCrimson, CosmeticCatalog.Tables.HeritageWalnut };
            Fill(view, Tables,
                new[] { "Himalayan Ivory", "Royal Crimson", "Heritage Walnut" },
                System.Array.ConvertAll(tables, id => view.Picture(CosmeticKind.Table, id)),
                new[] { ShopTileStatus.Equipped, ShopTileStatus.Price, ShopTileStatus.Price },
                new[] { null, "350", "250" },
                chosen: 1);

            view.ShowCategory(category);
            if (category == Tables)
            {
                view.SetPrimaryBuy("350", true);
            }
            else
            {
                view.SetPrimaryUse(true);
            }
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
