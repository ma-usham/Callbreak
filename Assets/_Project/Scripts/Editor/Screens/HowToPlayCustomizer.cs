namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 12: nothing to assemble; the title and the "Highest total" line are refitted by
    /// their capital height and baseline (see <see cref="ScreenExtras"/>).
    /// </summary>
    public static class HowToPlayCustomizer
    {
        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);
        }
    }
}
