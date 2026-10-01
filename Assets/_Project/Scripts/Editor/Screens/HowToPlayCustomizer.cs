using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Editor.Screens
{
    /// <summary>
    /// Screen 12: the title and the "Highest total" line are refitted by their capital height and
    /// baseline (see <see cref="ScreenExtras"/>). For the first launch (ADR 022) the view gets the
    /// rules panel, which it hides while the player's name is asked, and a dim between the logo and
    /// the panel, the name question's own, for the rules shown that first time.
    /// </summary>
    public static class HowToPlayCustomizer
    {
        // The dialog's dim (DialogBuilder), measured on mockup 13; the name question uses it too.
        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.817f);

        // Far enough past the design area to cover a 21:9 or 4:3 screen as well.
        private const float DimOutset = 2400f;

        public static void Customize(ScreenBuildContext ctx)
        {
            ScreenExtras.Apply(ctx);
            if (!ctx.Nodes.TryGetValue("Panel", out RectTransform panel))
            {
                ctx.Warnings.Add("Panel: node missing");
                return;
            }

            ctx.Bound["_panel"] = panel.gameObject;

            // Over the background and the logo, under the rules.
            var dimObject = new GameObject("Dim", typeof(RectTransform));
            var dim = (RectTransform)dimObject.transform;
            dim.SetParent(panel.parent, false);
            dim.SetSiblingIndex(panel.GetSiblingIndex());
            dim.anchorMin = Vector2.zero;
            dim.anchorMax = Vector2.one;
            dim.offsetMin = new Vector2(-DimOutset, -DimOutset);
            dim.offsetMax = new Vector2(DimOutset, DimOutset);
            Image image = dimObject.AddComponent<Image>();
            image.color = Dim;
            image.raycastTarget = false;
            dimObject.SetActive(false);
            ctx.Bound["_dim"] = dimObject;
        }
    }
}
