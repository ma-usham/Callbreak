using System.Linq;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 09: refits the title by its capital height and baseline, makes the table regular
    /// (one size per kind of cell, columns and rows on common lines), gives the texts a presenter
    /// changes a maximum width, and keeps the footer's arrows beside its text.
    /// </summary>
    public static class RoundResultsCustomizer
    {
        private const int Rows = 4;
        private static readonly string[] ValueColumns = { "RowBid", "RowWon", "RowDelta", "RowTotal" };

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);

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
        }

        private static string[] Column(string prefix) => Enumerable.Range(0, Rows).Select(i => $"{prefix}{i}").ToArray();
    }
}
