namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 12: the title and the "Highest total" line are refitted by their capital height and
    /// baseline (see <see cref="ScreenExtras"/>), and the view gets the rules panel, which it hides
    /// while the first launch asks the player's name (ADR 022).
    /// </summary>
    public static class HowToPlayCustomizer
    {
        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);
            if (ctx.Nodes.TryGetValue("Panel", out UnityEngine.RectTransform panel))
            {
                ctx.Bound["_panel"] = panel.gameObject;
            }
            else
            {
                ctx.Warnings.Add("Panel: node missing");
            }
        }
    }
}
