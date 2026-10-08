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
    /// so a larger balance or a translation shrinks instead of running out of its place.
    /// </summary>
    public static class DailyRewardsCustomizer
    {
        private const int DayCount = 7;

        private const string CheckBadge = "Assets/_Project/Art/UI/Controls/UI_Check_Badge_Gold.png";

        /// <summary>The check's size and its inset from the tile's top and right edges, mockup pixels.</summary>
        private const float BadgeSize = 36f;
        private const float BadgeInset = 10f;

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);

            Sprite badge = UiBuildKit.LoadSpriteAt(CheckBadge, 0f);
            for (int i = 0; i < DayCount; i++)
            {
                string tile = $"Tile{i}";
                ctx.Nodes[$"ClaimedBadge{i}"] = AddBadge(ctx, tile, badge);
                float width = ctx.Node(tile).rect[2];
                ScreenExtras.LimitWidth(ctx, $"DayLabel{i}", width - 24f);
                ScreenExtras.LimitWidth(ctx, $"Amount{i}", width - 24f);
            }

            // The balance grows to the right, up to the pill's end; the button's label stays inside its
            // quiet middle; the heading and the footer stay between their ornaments.
            ScreenExtras.LimitWidth(ctx, "Balance", 77f);
            ScreenExtras.LimitWidth(ctx, "ClaimLabel", ctx.Node("ClaimButton").rect[2] * 0.62f);
            ScreenExtras.LimitWidth(ctx, "DayHeading", ctx.Node("DayHeading").rect[2] + 12f);
            ScreenExtras.LimitWidth(ctx, "Footer", ctx.Node("Footer").rect[2] + 12f);
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
