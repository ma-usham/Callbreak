using Callbreak.Presentation.Screens;
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
