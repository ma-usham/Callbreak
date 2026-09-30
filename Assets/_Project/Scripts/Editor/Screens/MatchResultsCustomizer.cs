using System.Linq;
using Callbreak.Presentation.Widgets;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 10: refits the serif headlines by their capital height and baseline, makes the
    /// standings regular (one size per kind of cell, columns and rows on common lines), gives the
    /// texts a presenter changes a maximum width, keeps the footer's rules beside its text, gives
    /// the panel its rise and the trophy its entrance, and the "View Scorecard" link the GDD's
    /// 88 px touch target.
    /// </summary>
    public static class MatchResultsCustomizer
    {
        private const int Rows = 4;

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
