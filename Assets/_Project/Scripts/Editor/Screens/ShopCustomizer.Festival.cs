using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// The shop's parts for festival offers (the owner, 31 Oct 2026; ADR 067): on each theme tile a
    /// Festival tag where the NEW tag sits, in marigold, and "Free" where the price goes; in the theme
    /// preview a claim button where Buy sits, the kit's crimson button with a mark and its words
    /// ("(play) Watch ad • Free"). All are built hidden; the tile and the preview show them while an
    /// offer gives the theme. <see cref="AddFestivalOffers"/> adds them to the saved prefab.
    /// </summary>
    public static partial class ShopCustomizer
    {
        private static readonly Color FestivalMarigold = UiBuildKit.Hex("#C25E0B");
        private static readonly Color OfferInk = UiBuildKit.Hex("#810911");

        private const string WatchAdIconPath = "UI/Icons/Icon_Play.png";

        // The festival tile's glow (ADR 076): the Daily Rewards tile's halo art and 9-slice border (px, at
        // 200 pixels per unit), reaching this far past the tile, canvas units; its breath grows it a
        // little, and the tile's "Free" swells this much at its height.
        private const string OfferGlowArt = "Assets/_Project/Art/UI/Decor/Deco_ClaimGlow_Tile.png";
        private const float OfferGlowBorder = 84f;
        private const float OfferGlowReach = 16f;
        private const float OfferGlowSwell = 0.02f;
        private const float OfferWordsSwell = 0.12f;
        private const string FreeIconPath = "UI/Icons/Icon_Spade_Gold.png";
        private const string DiscountIconPath = "UI/Icons/Icon_Coin.png";

        /// <summary>
        /// Gives the saved shop prefab's theme tiles their Festival tag and "Free", and its preview its
        /// claim button, each bound to its view; everything else in the prefab stays as it is. Parts
        /// already there are left alone.
        /// </summary>
        public static string AddFestivalOffers()
        {
            string path = ScreenLayoutBuilder.PrefabPath("Shop");
            GameObject root = PrefabUtility.LoadPrefabContents(path);

            // TextMeshPro only lays text out under a Canvas, and the claim button's row is measured,
            // so the parts are built inside a temporary one, as ScreenLayoutBuilder does.
            var canvas = new GameObject("LayoutBuildCanvas", typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvas, root.scene);
            root.transform.SetParent(canvas.transform, false);
            try
            {
                int tiles = 0;
                foreach (ThemeTile tile in root.GetComponentsInChildren<ThemeTile>(true))
                {
                    var so = new SerializedObject(tile);
                    if (so.FindProperty("_festivalTag").objectReferenceValue != null)
                    {
                        continue;
                    }

                    var tileRoot = (RectTransform)tile.transform;
                    var newTag = so.FindProperty("_newTag").objectReferenceValue as GameObject;
                    so.FindProperty("_festivalTag").objectReferenceValue = AddFestivalTag(tileRoot, newTag != null ? newTag.transform.GetSiblingIndex() + 1 : -1);
                    so.FindProperty("_offerText").objectReferenceValue = AddOfferText(tileRoot);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    tiles++;
                }

                ThemePreview preview = root.GetComponentInChildren<ThemePreview>(true);
                if (preview == null)
                {
                    return $"FAILED: {path} has no theme preview";
                }

                var previewSo = new SerializedObject(preview);
                bool claim = previewSo.FindProperty("_claimButton").objectReferenceValue == null;
                if (claim)
                {
                    // The preview is saved hidden, and TextMeshPro lays out no words while hidden: it
                    // shows while the claim button's row is measured.
                    bool shown = preview.gameObject.activeSelf;
                    preview.gameObject.SetActive(true);
                    var buy = (Button)previewSo.FindProperty("_buyButton").objectReferenceValue;
                    var buyRect = (RectTransform)buy.transform;
                    AddClaimButton(previewSo, (RectTransform)buyRect.parent, buyRect.anchoredPosition, buyRect.GetSiblingIndex() + 1);
                    previewSo.ApplyModifiedPropertiesWithoutUndo();
                    preview.gameObject.SetActive(shown);
                }

                if (tiles == 0 && !claim)
                {
                    return "the shop already has its festival parts";
                }

                root.transform.SetParent(null, false);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                return saved ? $"saved {path}: festival parts on {tiles} tiles{(claim ? " and the preview's claim button" : string.Empty)}" : $"FAILED to save {path}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                Object.DestroyImmediate(canvas);
            }
        }

        /// <summary>
        /// The Festival Offer panel (the owner, 2 Nov 2026; ADR 074): binds the saved shop prefab's page
        /// (its Content, hidden while the offer opens by itself on the main menu) to its view, and the
        /// preview's subtitle (which says "Tihar Lights · for a few days only" there) and its wallet
        /// (hidden there) to the preview; everything else in the prefab stays as it is.
        /// </summary>
        public static string BindFestivalPanel()
        {
            string path = ScreenLayoutBuilder.PrefabPath("Shop");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                ShopView view = root.GetComponentInChildren<ShopView>(true);
                ThemePreview preview = root.GetComponentInChildren<ThemePreview>(true);
                Transform page = root.transform.Find("Content");
                Transform subtitle = preview != null ? preview.transform.Find("Panel/Subtitle") : null;
                Transform wallet = preview != null ? preview.transform.Find("Panel/Wallet") : null;
                if (view == null || page == null || subtitle == null || wallet == null)
                {
                    return $"FAILED: {path} has no shop view, page, preview subtitle or preview wallet";
                }

                var viewSo = new SerializedObject(view);
                viewSo.FindProperty("_page").objectReferenceValue = page.gameObject;
                viewSo.ApplyModifiedPropertiesWithoutUndo();
                var previewSo = new SerializedObject(preview);
                previewSo.FindProperty("_subtitle").objectReferenceValue = subtitle.GetComponent<TMP_Text>();
                previewSo.FindProperty("_wallet").objectReferenceValue = wallet.gameObject;
                previewSo.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                return saved ? $"saved {path}: the page and the preview's subtitle and wallet bound" : $"FAILED to save {path}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The festival tile's glow (ADR 076), added to the saved shop prefab's theme tiles that have
        /// none, each bound to its tile; everything else in the prefab stays as it is.
        /// </summary>
        public static string AddOfferGlows()
        {
            string path = ScreenLayoutBuilder.PrefabPath("Shop");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int added = 0;
                foreach (ThemeTile tile in root.GetComponentsInChildren<ThemeTile>(true))
                {
                    var so = new SerializedObject(tile);
                    if (so.FindProperty("_offerGlow").objectReferenceValue != null)
                    {
                        continue;
                    }

                    var words = so.FindProperty("_offerText").objectReferenceValue as TMP_Text;
                    so.FindProperty("_offerGlow").objectReferenceValue = AddOfferGlow((RectTransform)tile.transform, words);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    added++;
                }

                if (added == 0)
                {
                    return "the theme tiles already glow";
                }

                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                return saved ? $"saved {path}: a glow on {added} theme tiles" : $"FAILED to save {path}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// The glow round a theme tile while it is a festival offer's (ADR 076): the warm gold halo of a
        /// reward ready to collect (ADR 069's art), behind the whole tile and reaching a little past its
        /// edge, breathing while the tile's "Free" swells at each breath's height; hidden.
        /// </summary>
        private static ClaimHighlight AddOfferGlow(RectTransform tile, TMP_Text offerText)
        {
            RectTransform glow = UiBuildKit.CreateAt("OfferGlow", tile, Vector2.zero, Vector2.zero, tile.rect.size);
            glow.SetAsFirstSibling();
            RectTransform halo = UiBuildKit.CreateAt("Halo", glow, Vector2.zero, Vector2.zero, tile.rect.size + (Vector2.one * (2f * OfferGlowReach)));
            Image image = UiBuildKit.AddImage(halo, UiBuildKit.LoadSpriteAt(OfferGlowArt, 200f, Vector4.one * OfferGlowBorder), false);
            image.raycastTarget = false;

            var highlight = glow.gameObject.AddComponent<ClaimHighlight>();
            var so = new SerializedObject(highlight);
            so.FindProperty("_halo").objectReferenceValue = image;
            so.FindProperty("_glyph").objectReferenceValue = offerText != null ? offerText.rectTransform : null;
            so.FindProperty("_swell").floatValue = OfferGlowSwell;
            so.FindProperty("_nudgeDegrees").floatValue = 0f;
            so.FindProperty("_nudgeLift").floatValue = 0f;
            so.FindProperty("_nudgeSwell").floatValue = OfferWordsSwell;
            so.ApplyModifiedPropertiesWithoutUndo();
            glow.gameObject.SetActive(false);
            return highlight;
        }

        /// <summary>The Festival tag: the NEW tag's pill and words, wider, in marigold, hidden.</summary>
        private static GameObject AddFestivalTag(RectTransform tile, int siblingIndex)
        {
            const float width = 150f;
            RectTransform tag = Pinned("FestivalTag", tile, new Vector2(0f, 1f), new Vector2(30f + (width / 2f), -30f - 18f), new Vector2(width, 36f));
            if (siblingIndex >= 0)
            {
                tag.SetSiblingIndex(siblingIndex);
            }

            Image face = UiBuildKit.AddImage(tag, Load($"{UiBuildKit.ArtRoot}/UI/Panels/UI_Panel_Card_Light.png"), false);
            face.type = Image.Type.Sliced;
            face.pixelsPerUnitMultiplier = 1.6f;
            face.color = FestivalMarigold;
            TextMeshProUGUI words = NewLabel("Words", tag, "FESTIVAL", "SansBold", 22f, UiBuildKit.Hex("#FFF6E8"), TextAlignmentOptions.Center, "ui.shop.themes.festival");
            words.characterSpacing = 10f;
            Fill(words.rectTransform);
            tag.gameObject.SetActive(false);
            return tag.gameObject;
        }

        /// <summary>"Free" where the price goes, in the title's crimson, hidden.</summary>
        private static TextMeshProUGUI AddOfferText(RectTransform tile)
        {
            TextMeshProUGUI offer = NewLabel("Offer", tile, "Free", "SansBold", 36f, OfferInk, TextAlignmentOptions.Center);
            Pinned(offer.rectTransform, tile, new Vector2(0.5f, 1f), new Vector2(0f, -495f), new Vector2(TileWidth, 56f));
            offer.gameObject.SetActive(false);
            return offer;
        }

        /// <summary>
        /// The claim button where Buy sits: the crimson button with the offer's mark and its words in a
        /// centred row, hidden, bound to the preview with the three marks.
        /// </summary>
        private static void AddClaimButton(SerializedObject preview, RectTransform panel, Vector2 at, int siblingIndex)
        {
            Button claim = CrimsonButton("ClaimButton", panel, at, out CanvasGroup content, out RectTransform words);
            claim.transform.SetSiblingIndex(siblingIndex);
            RectTransform icon = Pinned("Icon", words, new Vector2(0.5f, 0.5f), new Vector2(-150f, 0f), new Vector2(44f, 50f));
            Image iconImage = UiBuildKit.AddImage(icon, Load($"{UiBuildKit.ArtRoot}/{WatchAdIconPath}"), false);
            iconImage.preserveAspect = true;
            TextMeshProUGUI label = NewLabel("Words", words, "Watch ad • Free", "SerifSemiBold", 44f, ButtonWords, TextAlignmentOptions.MidlineLeft);
            SizeLeft(label.rectTransform, new Vector2(-110f, 0f), new Vector2(360f, 60f));
            ShopRow row = Row(words, icon, label.rectTransform);
            row.Refresh();
            claim.gameObject.SetActive(false);

            preview.FindProperty("_claimButton").objectReferenceValue = claim;
            preview.FindProperty("_claimContent").objectReferenceValue = content;
            preview.FindProperty("_claimRow").objectReferenceValue = row;
            preview.FindProperty("_claimIcon").objectReferenceValue = iconImage;
            preview.FindProperty("_claimLabel").objectReferenceValue = label;
            preview.FindProperty("_watchAdIcon").objectReferenceValue = Load($"{UiBuildKit.ArtRoot}/{WatchAdIconPath}");
            preview.FindProperty("_freeIcon").objectReferenceValue = Load($"{UiBuildKit.ArtRoot}/{FreeIconPath}");
            preview.FindProperty("_discountIcon").objectReferenceValue = Load($"{UiBuildKit.ArtRoot}/{DiscountIconPath}");
        }
    }
}
