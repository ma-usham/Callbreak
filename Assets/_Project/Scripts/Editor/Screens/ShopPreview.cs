using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Presentation.Screens;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// The shop in a state of each tab, and the theme preview, for the screen captures (the tiles are
    /// filled at run time by the presenter): Themes with Durbar Square equipped and every other theme
    /// at its price, as Figma's "V1 Rebuild · Shop" A shows it (1,200 coins, so the 1,000 themes are
    /// within reach); Cards with Classic Crimson equipped, as mockup 15; and Tihar Lights' preview,
    /// not owned ("Buy for 1,000").
    /// </summary>
    public static class ShopPreview
    {
        public const int Themes = ShopView.Themes;
        public const int Cards = ShopView.Cards;
        public const int Preview = 2;

        private const int Coins = 1200;

        public static void Prepare(GameObject instance) => PrepareCategory(instance, Themes);

        /// <summary>Captures each tab and the preview at 16:9, 21:9 and 4:3 into Temp/Captures (Shop_Themes_..., Shop_Cards_..., Shop_Preview_...).</summary>
        public static string CaptureAll()
        {
            string prefab = ScreenLayoutBuilder.PrefabPath("Shop");
            string[] names = { "Themes", "Cards", "Preview" };
            foreach (int category in new[] { Themes, Cards, Preview })
            {
                int c = category;
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2520, 1080), new Vector2Int(1920, 1440) })
                {
                    ScreenCaptureTool.Capture(prefab, size.x, size.y, $"{ScreenCaptureTool.OutputFolder}/Shop_{names[c]}_{size.x}x{size.y}.png", go => PrepareCategory(go, c));
                }
            }

            return "captured Shop_{Themes,Cards,Preview}_{1920x1080,2520x1080,1920x1440}.png";
        }

        public static void PrepareCategory(GameObject instance, int category)
        {
            var view = instance.GetComponent<ShopView>();
            if (view == null)
            {
                return;
            }

            var strings = new Dictionary<string, string>
            {
                ["durbar-square"] = "Durbar Square", ["himalayan-sunrise"] = "Himalayan Sunrise", ["laligurans-forest"] = "Laligurans Forest",
                ["golden-terraces"] = "Golden Terraces", ["tihar-lights"] = "Tihar Lights", ["prayer-flag-pass"] = "Prayer Flag Pass",
                ["mithila-garden"] = "Mithila Garden", ["phewa-moonlight"] = "Phewa Moonlight", ["pokhara-lakeside"] = "Pokhara Lakeside",
                ["monastery-garden"] = "Monastery Garden",
            };

            view.SetCoins(ShopDisplay.Price(Coins));
            view.SetNoAdsVisible(true);
            IReadOnlyList<CosmeticItem> themes = CosmeticCatalog.Of(CosmeticKind.Theme);
            for (int i = 0; i < themes.Count; i++)
            {
                CosmeticItem theme = themes[i];
                ShopTileStatus status = i == 0 ? ShopTileStatus.Equipped : ShopTileStatus.Price;
                view.SetTheme(i, strings[theme.Id], view.ThemePicture(theme.Id), i >= 1 && i <= 7, status, status == ShopTileStatus.Equipped ? "Equipped" : null, ShopDisplay.Price(theme.Price), theme.Price <= Coins);
            }

            string[] decks = { "Classic Crimson", "Himalayan Ivory", "Kathmandu Gold" };
            string[] backs = { "ClassicCrimson", "HimalayanIvory", "KathmanduGold" };
            view.SetDeckCount(decks.Length);
            for (int i = 0; i < decks.Length; i++)
            {
                ShopTileStatus status = i == 0 ? ShopTileStatus.Equipped : ShopTileStatus.Price;
                int price = CosmeticCatalog.Of(CosmeticKind.Deck)[i].Price;
                view.SetDeck(i, decks[i], Back(backs[i]), status, status == ShopTileStatus.Equipped ? "Equipped" : null, ShopDisplay.Price(price), i == 0);
            }

            view.ShowCategory(category == Cards ? Cards : Themes);
            view.SetPrimaryUse(false);
            if (category == Preview)
            {
                CosmeticItem tihar = CosmeticCatalog.Find(CosmeticKind.Theme, CosmeticCatalog.Themes.TiharLights);
                Sprite background = view.BackgroundPicture(tihar.BackgroundId, out bool night);
                Sprite cloth = view.ClothPicture(tihar.TableId, out Sprite frame);
                view.ShowPreview(
                    "Tihar Lights",
                    "Tihar night at home: a glowing carved window, marigold garlands, diyo on every step, a rangoli and fireworks. The cloth is plum, with marigold swags.",
                    background, night, cloth, frame, ThemeAction.Buy, ShopDisplay.Price(tihar.Price), true);
            }
        }

        private static Sprite Back(string deck) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Project/Art/Cards/{deck}/Back_{deck}.png");
    }
}
