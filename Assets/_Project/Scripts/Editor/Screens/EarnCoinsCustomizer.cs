using Callbreak.Presentation.Screens;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 22, Earn Coins: the looks mockup 22 doesn't draw. Every reward block rests on the gold
    /// look and gets the crimson "next" look over it (shown on the block the next ad pays: Ad 1 as
    /// built, as on the mockup) and a check badge for a watched ad, hidden as built. The badge is the
    /// kit's crimson check (UI/Controls/UI_Check_Badge.png), kept clear of the block's rounded corner
    /// and its title: a proposal, the owner's to confirm (the designer's prototype puts a crimson check
    /// in the block's corner). The dynamic labels get a maximum width, so a longer balance or
    /// translation shrinks instead of running out of its place. The clock is moved close before the
    /// reset line, the two centred in the panel (<see cref="EarnCoinsView.CentreClockLine"/>).
    /// </summary>
    public static class EarnCoinsCustomizer
    {
        private const int BlockCount = 3;

        // The block built with the crimson look (cut from the mockup's Ad 1) and one with the gold look.
        private const string NextLookSource = "Block0";
        private const string GoldLookSource = "Block1";

        private const string CheckBadge = "Assets/_Project/Art/UI/Controls/UI_Check_Badge.png";

        /// <summary>The check's size and its inset from the block's top and right edges, mockup pixels.</summary>
        private const float BadgeSize = 34f;
        private const float BadgeInset = 14f;

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);

            Sprite gold = Face(ctx, GoldLookSource).sprite;
            Sprite next = Face(ctx, NextLookSource).sprite;
            Sprite badge = UiBuildKit.LoadSpriteAt(CheckBadge, 0f);
            for (int i = 0; i < BlockCount; i++)
            {
                string id = $"Block{i}";
                Image face = Face(ctx, id);
                face.sprite = gold;
                ctx.Nodes[$"NextLook{i}"] = AddNextLook(face, next, i == 0);
                ctx.Nodes[$"WatchedBadge{i}"] = AddBadge(ctx, id, badge);
                ScreenExtras.LimitWidth(ctx, $"Amount{i}", ctx.Node(id).rect[2] - 20f);
            }

            // The balance grows to the right, up to the pill's end; the button's label stays inside
            // the button's quiet middle; the two lines stay inside the panel.
            ScreenExtras.LimitWidth(ctx, "Balance", 77f);
            ScreenExtras.LimitWidth(ctx, "WatchLabel", 360f);
            ScreenExtras.LimitWidth(ctx, "Progress", 560f);
            ScreenExtras.LimitWidth(ctx, "ResetLabel", 560f);

            // The clock close before its line, the two centred in the panel (the owner, 5 Oct 2026),
            // as the view lays them out whenever the line changes.
            EarnCoinsView.CentreClockLine(ctx.Nodes["ResetLabel"].GetComponent<TMPro.TMP_Text>(), ctx.Nodes["ClockIcon"]);

            // An ad's coins fly from its block's pile into the wallet (CoinFlight).
            CoinFlightBuild.Add(ctx);
        }

        private static Image Face(ScreenBuildContext ctx, string id) => ctx.Nodes[id].Find("Face").GetComponent<Image>();

        /// <summary>The crimson look: a copy of the block's face, drawn over it.</summary>
        private static RectTransform AddNextLook(Image face, Sprite sprite, bool shown)
        {
            var source = (RectTransform)face.transform;
            var look = new GameObject("NextLook", typeof(RectTransform));
            var rect = (RectTransform)look.transform;
            rect.SetParent(source.parent, false);
            rect.SetSiblingIndex(source.GetSiblingIndex() + 1);
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition = source.anchoredPosition;
            Image image = UiBuildKit.AddImage(rect, sprite, false);
            image.pixelsPerUnitMultiplier = face.pixelsPerUnitMultiplier;
            look.SetActive(shown);
            return rect;
        }

        /// <summary>The watched check, in the block's top right corner, on top of everything in the block.</summary>
        private static RectTransform AddBadge(ScreenBuildContext ctx, string blockId, Sprite sprite)
        {
            float[] block = ctx.Node(blockId).rect;
            var centre = new Vector2(
                block[0] + block[2] - BadgeInset - (BadgeSize / 2f),
                block[1] + BadgeInset + (BadgeSize / 2f));
            RectTransform rect = UiBuildKit.CreateAt(
                "WatchedBadge", ctx.Nodes[blockId], centre, ctx.CentreOf(blockId), UiBuildKit.FromMock(BadgeSize, BadgeSize));
            UiBuildKit.AddImage(rect, sprite, false);
            rect.gameObject.SetActive(false);
            return rect;
        }
    }
}
