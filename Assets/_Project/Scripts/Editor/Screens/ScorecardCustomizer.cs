using System.Linq;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 16: refits the title by its x-height and baseline, makes the table regular (tabular
    /// figures, one size per kind of cell, columns and rows on common lines), gives the texts a
    /// presenter changes a maximum width, and keeps the footer's rules beside its text.
    /// </summary>
    public static class ScorecardCustomizer
    {
        private const int Rows = 4;
        private const int Rounds = 5;

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);

            // RowRound labels are numbered row-major: row r, round i is RowRound{r * 5 + i}.
            ScreenExtras.Tabular(ctx, Enumerable.Range(0, Rows * Rounds).Select(i => $"RowRound{i}"));
            ScreenExtras.UnifyStyle(ctx, Enumerable.Range(0, Rows * Rounds).Select(i => $"RowRound{i}"));
            for (int round = 0; round < Rounds; round++)
            {
                int i = round;
                ScreenExtras.AlignColumn(ctx, Enumerable.Range(0, Rows).Select(r => $"RowRound{r * Rounds + i}"));
            }

            foreach (string column in new[] { "RowName", "RowTotal", "RowRank" })
            {
                ScreenExtras.UnifyStyle(ctx, Column(column));
                ScreenExtras.AlignColumn(ctx, Column(column));
            }

            // The five round scores of a row share a baseline; the name, the larger serif total and
            // the rank are centred on the row, as on the mockup.
            for (int row = 0; row < Rows; row++)
            {
                int r = row;
                ScreenExtras.AlignRow(ctx, Enumerable.Range(0, Rounds).Select(i => $"RowRound{r * Rounds + i}"));
            }

            // Names end before the R1 column (which also holds a minus sign); the footer stays
            // inside the panel with its rules; the button label stays between the woven ends and
            // left of the chevron.
            foreach (string name in Column("RowName"))
            {
                ScreenExtras.LimitWidth(ctx, name, 124f);
            }

            ScreenExtras.LimitWidth(ctx, "Footer", 560f);
            ScreenExtras.LimitWidth(ctx, "PrimaryLabel", 370f);
            ctx.Bound["_footerFlanks"] = ScreenExtras.Flank(ctx, "Footer", "FooterLeft", "FooterRight");
        }

        private static string[] Column(string prefix) => Enumerable.Range(0, Rows).Select(i => $"{prefix}{i}").ToArray();
    }
}
