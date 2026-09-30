using System.Linq;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 09: refits the title by its capital height and baseline, makes the table regular
    /// (tabular figures, one size per kind of cell, columns and rows on common lines), gives the
    /// texts a presenter changes a maximum width, keeps the footer's arrows beside its text, gives
    /// the panel its rise and the two buttons under the table the GDD's 88 px touch target.
    /// </summary>
    public static class RoundResultsCustomizer
    {
        private const int Rows = 4;
        private static readonly string[] ValueColumns = { "RowBid", "RowWon", "RowDelta", "RowTotal" };

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);

            ScreenExtras.Tabular(ctx, ValueColumns.SelectMany(Column));
            ScreenExtras.UnifyStyle(ctx, Column("RowName"));
            ScreenExtras.UnifyStyle(ctx, ValueColumns.SelectMany(Column));
            ScreenExtras.AlignColumn(ctx, Column("RowName"));
            foreach (string column in ValueColumns)
            {
                ScreenExtras.AlignColumn(ctx, Column(column));
            }

            for (int row = 0; row < Rows; row++)
            {
                int r = row;
                ScreenExtras.AlignRow(ctx, new[] { "RowName" }.Concat(ValueColumns).Select(c => $"{c}{r}"));
            }

            // Names end before the Bid column; the button label stays between the woven ends and
            // left of the chevron; the footer stays inside the panel with its arrows.
            foreach (string name in Column("RowName"))
            {
                ScreenExtras.LimitWidth(ctx, name, 205f);
            }

            ScreenExtras.LimitWidth(ctx, "NextLabel", 370f);
            ScreenExtras.LimitWidth(ctx, "Footer", 420f);
            ctx.Bound["_footerFlanks"] = ScreenExtras.Flank(ctx, "Footer", "FooterLeft", "FooterRight");

            // GDD §14 "Results: panel rises": the panel carries everything between the headers.
            ctx.Bound["_panelRise"] = ScreenExtras.Rise(ctx, "Panel");

            // GDD: touch targets of at least 88 px. Next Round is drawn 85.0 px high and View
            // Scorecard 64.3, 15.1 px apart. Each grows away from the other (Next upward over the
            // rule line, Scorecard downward over the footer), so the two touch areas keep the
            // distance the art has.
            ScreenExtras.GrowHit(ctx, "NextButton", 3.1f, 0f);
            ScreenExtras.GrowHit(ctx, "ScorecardButton", 0f, 23.8f);
        }

        private static string[] Column(string prefix) => Enumerable.Range(0, Rows).Select(i => $"{prefix}{i}").ToArray();
    }
}
