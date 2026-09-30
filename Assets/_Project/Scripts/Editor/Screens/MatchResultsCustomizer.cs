using System.Linq;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 10: refits the serif headlines by their capital height and baseline, makes the
    /// standings regular (one size per kind of cell, columns and rows on common lines), gives the
    /// texts a presenter changes a maximum width, and keeps the footer's rules beside its text.
    /// </summary>
    public static class MatchResultsCustomizer
    {
        private const int Rows = 4;
        private static readonly string[] Columns = { "RowRank", "RowName", "RowTotal" };

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

            // "Shared Victory!" is nearly twice as long as "You Win!": the headline may use the
            // width between the scenery on both sides. Names end before the totals column; the
            // footer stays inside the panel with its rules.
            ScreenExtras.LimitWidth(ctx, "Headline", 900f);
            foreach (string name in Column("RowName"))
            {
                ScreenExtras.LimitWidth(ctx, name, 340f);
            }

            ScreenExtras.LimitWidth(ctx, "Footer", 420f);
            ctx.Bound["_footerFlanks"] = ScreenExtras.Flank(ctx, "Footer", "FooterLeft", "FooterRight");
        }

        private static string[] Column(string prefix) => Enumerable.Range(0, Rows).Select(i => $"{prefix}{i}").ToArray();
    }
}
