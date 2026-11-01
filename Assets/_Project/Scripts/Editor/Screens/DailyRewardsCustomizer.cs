using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Widgets;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 26, Daily Rewards: the look mockup 26 doesn't draw. A claimed day carries the kit's gold
    /// check badge (UI/Controls/UI_Check_Badge_Gold.png) in its tile's top right corner, on top of
    /// everything in the tile, hidden as built: the designer's own claimed mark (the badge their
    /// "Day 1 claim-state correction" took off the unclaimed Day 1), its size and inset a proposal for
    /// the owner. Today's frame and the light behind its coins come from the layout, built on Day 1 as
    /// the mockup draws them; the view moves them with the day. The dynamic labels get a maximum width,
    /// so a larger balance or a translation shrinks instead of running out of its place. Each tile is
    /// a button with no press look of its own (the owner, 28 Oct 2026: a tap on today's coins collects
    /// them); the view binds them as DayButton0..6. Each tile also has the glow of the day to collect
    /// (the owner, 2 Nov 2026; ADR 069), built hidden, which the view binds as DayHighlight0..6: a warm
    /// gold halo round the tile (Tools/ArtPipeline/NewUI/claim_glow.py) under its gold TodayFrame, the
    /// halo breathing while the day's coins hop twice at each breath's height (ClaimHighlight).
    /// <see cref="AddClaimGlows"/> gives them to the saved prefab without rebuilding it.
    /// </summary>
    public static class DailyRewardsCustomizer
    {
        private const int DayCount = 7;

        private const string CheckBadge = "Assets/_Project/Art/UI/Controls/UI_Check_Badge_Gold.png";

        /// <summary>The check's size and its inset from the tile's top and right edges, mockup pixels.</summary>
        private const float BadgeSize = 36f;
        private const float BadgeInset = 10f;

        private const string ClaimGlow = "Assets/_Project/Art/UI/Decor/Deco_ClaimGlow_Tile.png";

        /// <summary>The halo's 9-slice border (px, at 200 pixels per unit) and how far past the tile's edge it reaches, canvas units.</summary>
        private const float ClaimGlowBorder = 84f;
        private const float ClaimGlowReach = 20f;

        /// <summary>The halo's breath grows it this much (little: the tile is big); the coins hop this high, canvas units, and swell this much.</summary>
        private const float ClaimGlowSwell = 0.02f;
        private const float CoinsHop = 8f;
        private const float CoinsSwell = 0.04f;

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);

            Sprite badge = UiBuildKit.LoadSpriteAt(CheckBadge, 0f);
            Sprite glow = ClaimGlowSprite();
            for (int i = 0; i < DayCount; i++)
            {
                string tile = $"Tile{i}";
                ctx.Nodes[$"ClaimedBadge{i}"] = AddBadge(ctx, tile, badge);
                ctx.Nodes[$"DayHighlight{i}"] = AddClaimGlow(ctx.Nodes[tile], ctx.Nodes[$"TodayFrame{i}"], ctx.Nodes[$"Coins{i}"], glow);
                float width = ctx.Node(tile).rect[2];
                ScreenExtras.LimitWidth(ctx, $"DayLabel{i}", width - 24f);
                ScreenExtras.LimitWidth(ctx, $"Amount{i}", width - 24f);
                ctx.Nodes[$"DayButton{i}"] = AddTileButton(ctx.Nodes[tile]);
            }

            // The balance grows to the right, up to the pill's end; the button's label stays inside its
            // quiet middle; the heading and the footer stay between their ornaments.
            ScreenExtras.LimitWidth(ctx, "Balance", 77f);
            ScreenExtras.LimitWidth(ctx, "ClaimLabel", ctx.Node("ClaimButton").rect[2] * 0.62f);
            ScreenExtras.LimitWidth(ctx, "DayHeading", ctx.Node("DayHeading").rect[2] + 12f);
            ScreenExtras.LimitWidth(ctx, "Footer", ctx.Node("Footer").rect[2] + 12f);

            // A claim's coins fly from today's pile into the wallet (CoinFlight).
            CoinFlightBuild.Add(ctx);
        }

        /// <summary>The whole tile as a button, its own image the target; it looks the same pressed or not.</summary>
        private static RectTransform AddTileButton(RectTransform tile)
        {
            var image = tile.GetComponent<UnityEngine.UI.Image>();
            if (image == null)
            {
                image = tile.gameObject.AddComponent<UnityEngine.UI.Image>();
                image.color = Color.clear;
            }

            image.raycastTarget = true;
            var button = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            button.transition = UnityEngine.UI.Selectable.Transition.None;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            return tile;
        }

        /// <summary>
        /// Gives each day's tile of the saved Daily Rewards prefab its glow (<see cref="AddClaimGlow"/>),
        /// bound to the view; everything else in the prefab stays as it is, where a rebuild from the layout
        /// would give every object a new file id. A prefab that has them is left alone.
        /// </summary>
        public static string AddClaimGlows()
        {
            string path = ScreenLayoutBuilder.PrefabPath("DailyRewards");
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var serialized = new SerializedObject(root.GetComponent<DailyRewardsView>());
                SerializedProperty highlights = serialized.FindProperty("_dayHighlight");
                if (highlights.arraySize > 0)
                {
                    return "the day tiles already have their glow";
                }

                SerializedProperty frames = serialized.FindProperty("_todayFrame");
                SerializedProperty piles = serialized.FindProperty("_coins");
                if (frames.arraySize != DayCount || piles.arraySize != DayCount)
                {
                    return "FAILED: the page has no TodayFrame and coins on each of its seven days";
                }

                Sprite glow = ClaimGlowSprite();
                highlights.arraySize = DayCount;
                for (int i = 0; i < DayCount; i++)
                {
                    var frame = (RectTransform)((GameObject)frames.GetArrayElementAtIndex(i).objectReferenceValue).transform;
                    var coins = (RectTransform)piles.GetArrayElementAtIndex(i).objectReferenceValue;
                    RectTransform added = AddClaimGlow((RectTransform)frame.parent, frame, coins, glow);
                    highlights.GetArrayElementAtIndex(i).objectReferenceValue = added.GetComponent<ClaimHighlight>();
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                return saved ? $"saved {path} with each day's glow" : $"FAILED to save {path}";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Sprite ClaimGlowSprite() => UiBuildKit.LoadSpriteAt(ClaimGlow, 200f, Vector4.one * ClaimGlowBorder);

        /// <summary>
        /// The glow of the day to collect: the halo round the whole tile, just under its gold frame, and
        /// the <see cref="ClaimHighlight"/> that breathes it and makes <paramref name="coins"/> hop, hidden.
        /// </summary>
        private static RectTransform AddClaimGlow(RectTransform tile, RectTransform frame, RectTransform coins, Sprite sprite)
        {
            RectTransform glow = UiBuildKit.CreateAt("ClaimGlow", tile, Vector2.zero, Vector2.zero, tile.sizeDelta);
            glow.SetSiblingIndex(frame.GetSiblingIndex());
            RectTransform halo = UiBuildKit.CreateAt("Halo", glow, Vector2.zero, Vector2.zero, tile.sizeDelta + (Vector2.one * (2f * ClaimGlowReach)));
            UnityEngine.UI.Image image = UiBuildKit.AddImage(halo, sprite, false);

            var highlight = glow.gameObject.AddComponent<ClaimHighlight>();
            var so = new SerializedObject(highlight);
            so.FindProperty("_halo").objectReferenceValue = image;
            so.FindProperty("_glyph").objectReferenceValue = coins;
            so.FindProperty("_swell").floatValue = ClaimGlowSwell;
            so.FindProperty("_nudgeDegrees").floatValue = 0f;
            so.FindProperty("_nudgeLift").floatValue = CoinsHop;
            so.FindProperty("_nudgeSwell").floatValue = CoinsSwell;
            so.ApplyModifiedPropertiesWithoutUndo();
            glow.gameObject.SetActive(false);
            return glow;
        }

        /// <summary>The claimed check, in the tile's top right corner, on top of everything in the tile.</summary>
        private static RectTransform AddBadge(ScreenBuildContext ctx, string tileId, Sprite sprite)
        {
            float[] tile = ctx.Node(tileId).rect;
            var centre = new Vector2(
                tile[0] + tile[2] - BadgeInset - (BadgeSize / 2f),
                tile[1] + BadgeInset + (BadgeSize / 2f));
            RectTransform rect = UiBuildKit.CreateAt(
                "ClaimedBadge", ctx.Nodes[tileId], centre, ctx.CentreOf(tileId), UiBuildKit.FromMock(BadgeSize, BadgeSize));
            UiBuildKit.AddImage(rect, sprite, false);
            rect.gameObject.SetActive(false);
            return rect;
        }
    }
}
