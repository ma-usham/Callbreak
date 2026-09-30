namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 10: refits the serif headlines by their capital height and baseline, and gives the
    /// texts a presenter changes (the headline, names, the footer) a maximum width.
    /// </summary>
    public static class MatchResultsCustomizer
    {
        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenLabelRefit.Apply(ctx);

            // "Shared Victory!" is nearly twice as long as "You Win!": the headline may use the
            // width between the scenery on both sides. Names end before the totals column; the
            // footer stays between its two lines.
            ScreenLabelRefit.LimitWidth(ctx, "Headline", 900f);
            for (int row = 0; row < 4; row++)
            {
                ScreenLabelRefit.LimitWidth(ctx, $"RowName{row}", 340f);
            }

            ScreenLabelRefit.LimitWidth(ctx, "Footer", 224f);
        }
    }
}
