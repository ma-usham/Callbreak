using System.Collections.Generic;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// One background behind every menu screen (the owner, 31 Oct 2026: "cant we like use the one bg for
    /// all so that it wont be noticed"; ADR 058's addendum). The main menu's
    /// <see cref="EquippedBackground"/> is the only picture the menu screens have: when the Menu scene
    /// starts it is lifted out of the main menu to stand beside the screens, behind all of them. The
    /// router then fades and slides only the screens over it, and the picture stays as it is, showing
    /// the equipped theme.
    /// </summary>
    public static class MenuBackdrop
    {
        /// <summary>Lifts the picture out of the screen that has one and returns it; null when none has.</summary>
        public static EquippedBackground StandBehind(IReadOnlyList<UIScreen> screens)
        {
            foreach (UIScreen screen in screens)
            {
                if (screen == null)
                {
                    continue;
                }

                EquippedBackground picture = screen.GetComponentInChildren<EquippedBackground>(true);
                if (picture != null)
                {
                    // A screen fills the screens' canvas, so the picture keeps its place and size there.
                    picture.transform.SetParent(screen.transform.parent, false);
                    picture.transform.SetAsFirstSibling();
                    return picture;
                }
            }

            return null;
        }
    }
}
