namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 09: refits the title by its capital height and baseline, and gives the texts a
    /// presenter changes (names, the button label, the footer) a maximum width.
    /// </summary>
    public static class RoundResultsCustomizer
    {
        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenLabelRefit.Apply(ctx);

            // Names end before the Bid column; the button label stays between the woven ends and
            // left of the chevron; the footer stays between its two arrows.
            for (int row = 0; row < 4; row++)
            {
                ScreenLabelRefit.LimitWidth(ctx, $"RowName{row}", 205f);
            }

            ScreenLabelRefit.LimitWidth(ctx, "NextLabel", 370f);
            ScreenLabelRefit.LimitWidth(ctx, "Footer", 236f);
        }
    }
}
