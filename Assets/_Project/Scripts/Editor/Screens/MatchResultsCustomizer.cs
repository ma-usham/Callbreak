using System.Linq;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 10: refits the serif headlines by their capital height and baseline, makes the
    /// standings regular (one size per kind of cell, columns and rows on common lines), gives the
    /// texts a presenter changes a maximum width, adds each row's Stake Match prize (hidden), keeps
    /// the footer's rules beside its text, gives the panel its rise and the trophy its entrance, and
    /// the "View Scorecard" link the GDD's 88 px touch target.
    /// </summary>
    public static class MatchResultsCustomizer
    {
        private const int Rows = 4;

        // The Stake Match prize (the owner, 5 Oct 2026), which mockup 10 does not draw: the widest
        // prize there is (1st place at a 100-coin stake) and the shared kit's gold coin.
        private const string PrizeSample = "+200 coins";
        private const string CoinSprite = "UI/Icons/Icon_Coin.png";

        // The headline's box on the mockup, and how far a long headline may shrink before it ends
        // in an ellipsis ("2nd Place · Well played!" needs about a third of the design size).
        private const float HeadlineWidth = 426f;
        private const float HeadlineMinScale = 0.3f;

        private static readonly string[] Columns = { "RowRank", "RowName", "RowTotal" };

        // In the order they light up: left, top, right.
        private static readonly string[] Sparkles = { "TrophySparkle", "TrophySparkleTop", "TrophySparkleRight" };

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);

            foreach (string column in Columns)
            {
                ScreenExtras.UnifyStyle(ctx, Column(column));
                ScreenExtras.AlignColumn(ctx, Column(column));
            }

            for (int row = 0; row < Rows; row++)
            {
                int r = row;
                ScreenExtras.AlignRow(ctx, Columns.Select(c => $"{c}{r}"));
            }

            // "Shared Victory!" is nearly twice as long as "You Win!" and the other places' lines
            // are longer still: the headline keeps the width the mockup gives it and a longer one
            // shrinks to it, staying centred on the line of the capitals (on its baseline the
            // smaller lettering's descenders would still touch the subtitle). Names end before the
            // totals column; the footer stays inside the panel with its rules.
            ScreenExtras.CentreOnCapitals(ctx, "Headline");
            ScreenExtras.LimitWidth(ctx, "Headline", HeadlineWidth, HeadlineMinScale);
            foreach (string name in Column("RowName"))
            {
                ScreenExtras.LimitWidth(ctx, name, 340f);
            }

            BuildStakePrizes(ctx);

            ScreenExtras.LimitWidth(ctx, "Footer", 420f);
            ctx.Bound["_footerFlanks"] = ScreenExtras.Flank(ctx, "Footer", "FooterLeft", "FooterRight");

            // GDD §14 "Results: panel rises" and "Victory: trophy drops in with a restrained sparkle".
            ctx.Bound["_panelRise"] = ScreenExtras.Rise(ctx, "Panel");
            ctx.Bound["_trophyEntrance"] = BuildTrophyEntrance(ctx);

            // GDD: touch targets of at least 88 px, 16 px apart. The "View Scorecard" link's area is
            // 66.6 px high and starts 5.9 px under the two big buttons: it now starts 16 px under
            // them and reaches down to 88 px (past the panel's edge, where nothing else is).
            ScreenExtras.GrowHit(ctx, "ScorecardButton", -10.2f, 31.7f);
        }

        /// <summary>
        /// Gives every row the Stake Match prize, hidden until the presenter shows it on the row of
        /// the player it paid: a group <c>RowPrize{i}</c> with the gold coin (<c>RowPrizeCoin{i}</c>)
        /// and "+200 coins" (<c>RowPrizeLabel{i}</c>), between the name and the points. Nothing the
        /// mockup draws moves. The line takes the row's own measures:
        /// <list type="bullet">
        /// <item>the text is the row's name label (font, size, width, colour) on the name's baseline;</item>
        /// <item>the coin is as wide as the row's crown and centred on the capitals' middle;</item>
        /// <item>the coin is as far from its text as the crown is from the name.</item>
        /// </list>
        /// The pair sits where the widest prize is centred between the player's name and points on
        /// the mockup ("You" and "21.3"), at the same place on every row; a shorter prize keeps the
        /// coin there. A longer translation shrinks, then ends in an ellipsis, before the points.
        /// </summary>
        private static void BuildStakePrizes(ScreenBuildContext ctx)
        {
            Sprite coin = UiBuildKit.LoadSprite(CoinSprite, 0f);
            if (coin == null || !TryRowCell(ctx, "RowName0", out TextMeshProUGUI name0, out Rect nameInk0) ||
                !TryRowCell(ctx, "RowTotal0", out _, out Rect totalInk0) || !ctx.Nodes.TryGetValue("RowCrown0", out RectTransform crown0))
            {
                ctx.Warnings.Add("RowPrize: needs the coin sprite and row 0's crown, name and total; no Stake Match prize");
                return;
            }

            // In a row's own space (canvas units); the rows are alike and lie one above the other.
            float coinSize = crown0.rect.width;
            float gap = nameInk0.xMin - (crown0.localPosition.x + crown0.rect.xMax);
            float textWidth = PrizeInkWidth(name0);
            float left = (nameInk0.xMax + totalInk0.xMin - (coinSize + gap + textWidth)) / 2f;
            float textLeft = left + coinSize + gap;

            for (int row = 0; row < Rows; row++)
            {
                if (!ctx.Nodes.TryGetValue($"Row{row}", out RectTransform rowRect) || !ctx.Nodes.TryGetValue($"RowName{row}", out RectTransform name))
                {
                    ctx.Warnings.Add($"RowPrize{row}: row {row} has no name label; no prize there");
                    continue;
                }

                var group = new GameObject($"RowPrize{row}", typeof(RectTransform));
                var groupRect = (RectTransform)group.transform;
                groupRect.SetParent(rowRect, false);
                groupRect.SetSiblingIndex(name.GetSiblingIndex() + 1);
                groupRect.anchorMin = groupRect.anchorMax = groupRect.pivot = new Vector2(0.5f, 0.5f);
                groupRect.sizeDelta = rowRect.rect.size;
                groupRect.localPosition = Vector3.zero;

                TextMeshProUGUI label = PrizeLabel(name, groupRect, row, textLeft);
                ctx.Nodes[$"RowPrizeLabel{row}"] = label.rectTransform;
                float labelLeft = label.rectTransform.localPosition.x + label.rectTransform.rect.xMin;
                float maxWidth = totalInk0.xMin - gap - labelLeft;
                ScreenExtras.LimitWidth(ctx, $"RowPrizeLabel{row}", maxWidth / UiBuildKit.ScaleX);

                // The coin's middle on the middle of the capitals, as the crown sits by the name.
                label.text = "H";
                UiBuildKit.InkBounds(label, out Rect capitals, out _);
                label.text = PrizeSample;
                var coinRect = new GameObject($"RowPrizeCoin{row}", typeof(RectTransform)).GetComponent<RectTransform>();
                coinRect.SetParent(groupRect, false);
                coinRect.SetAsFirstSibling();
                coinRect.anchorMin = coinRect.anchorMax = coinRect.pivot = new Vector2(0.5f, 0.5f);
                coinRect.sizeDelta = new Vector2(coinSize, coinSize);
                coinRect.localPosition = new Vector3(left + coinSize / 2f, label.rectTransform.localPosition.y + capitals.center.y, 0f);
                UiBuildKit.AddImage(coinRect, coin, false).preserveAspect = true;
                ctx.Nodes[$"RowPrizeCoin{row}"] = coinRect;

                ctx.Nodes[$"RowPrize{row}"] = groupRect;
                group.SetActive(false);
            }
        }

        /// <summary>A label of the row's name style for the prize, its ink starting at <paramref name="textLeft"/> (row space), on the name's baseline.</summary>
        private static TextMeshProUGUI PrizeLabel(RectTransform name, RectTransform group, int row, float textLeft)
        {
            GameObject copy = Object.Instantiate(name.gameObject, group, false);
            copy.name = $"RowPrizeLabel{row}";
            if (copy.TryGetComponent(out LocalizedText localized))
            {
                Object.DestroyImmediate(localized);
            }

            var label = copy.GetComponent<TextMeshProUGUI>();
            label.text = PrizeSample;   // design-time sample; the presenter sets the prize paid
            if (UiBuildKit.InkBounds(label, out Rect ink, out _))
            {
                label.rectTransform.localPosition += new Vector3(textLeft - (label.rectTransform.localPosition.x + ink.xMin), 0f, 0f);
            }

            return label;
        }

        /// <summary>How wide the widest prize is drawn in the style of <paramref name="name"/>.</summary>
        private static float PrizeInkWidth(TextMeshProUGUI name)
        {
            string text = name.text;
            name.text = PrizeSample;
            float width = UiBuildKit.InkBounds(name, out Rect ink, out _) ? ink.width : 0f;
            name.text = text;
            name.ForceMeshUpdate(true, true);
            return width;
        }

        /// <summary>A label of a row and its ink in that row's space.</summary>
        private static bool TryRowCell(ScreenBuildContext ctx, string id, out TextMeshProUGUI label, out Rect ink)
        {
            ink = default;
            label = null;
            if (!ctx.Nodes.TryGetValue(id, out RectTransform rect) || !rect.TryGetComponent(out label) || !UiBuildKit.InkBounds(label, out Rect local, out _))
            {
                return false;
            }

            Vector3 at = rect.localPosition;
            ink = new Rect(local.x + at.x, local.y + at.y, local.width, local.height);
            return true;
        }

        /// <summary>The entrance sits on the Trophy group, so hiding the trophy also ends it.</summary>
        private static TrophyEntrance BuildTrophyEntrance(ScreenBuildContext ctx)
        {
            Graphic trophy = Face(ctx, "TrophyArt");
            Graphic[] sparkles = Sparkles.Select(id => Face(ctx, id)).Where(g => g != null).ToArray();
            if (!ctx.Nodes.TryGetValue("Trophy", out RectTransform group) || trophy == null || sparkles.Length == 0)
            {
                ctx.Warnings.Add("Trophy: the entrance needs the Trophy group, TrophyArt and the sparkles");
                return null;
            }

            var entrance = group.gameObject.AddComponent<TrophyEntrance>();
            var so = new SerializedObject(entrance);
            so.FindProperty("_trophy").objectReferenceValue = trophy;
            SerializedProperty list = so.FindProperty("_sparkles");
            list.arraySize = sparkles.Length;
            for (int i = 0; i < sparkles.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = sparkles[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return entrance;
        }

        private static Graphic Face(ScreenBuildContext ctx, string id)
        {
            return ctx.Nodes.TryGetValue(id, out RectTransform rect) ? rect.Find("Face")?.GetComponent<Graphic>() : null;
        }

        private static string[] Column(string prefix) => Enumerable.Range(0, Rows).Select(i => $"{prefix}{i}").ToArray();
    }
}
