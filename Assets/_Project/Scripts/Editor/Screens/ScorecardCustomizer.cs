namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 16: refits the title by its x-height and baseline, and gives the texts a presenter
    /// changes (names, the footer, the button label) a maximum width.
    /// </summary>
    public static class ScorecardCustomizer
    {
        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenLabelRefit.Apply(ctx);

            // Names end before the R1 column (which also holds a minus sign); the footer stays
            // between its two lines; the button label stays between the woven ends and the chevron.
            for (int row = 0; row < 4; row++)
            {
                ScreenLabelRefit.LimitWidth(ctx, $"RowName{row}", 124f);
            }

            ScreenLabelRefit.LimitWidth(ctx, "Footer", 400f);
            ScreenLabelRefit.LimitWidth(ctx, "PrimaryLabel", 370f);
        }
    }
}
