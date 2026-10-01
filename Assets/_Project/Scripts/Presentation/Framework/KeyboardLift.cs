using System;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// How far a panel is lifted so its text field stays above the on-screen keyboard: on a phone
    /// held sideways the keyboard covers half the screen or more, and a centred field is under it.
    /// Lengths are canvas units from the bottom of the screen, measured with the panel at rest.
    /// </summary>
    public static class KeyboardLift
    {
        /// <summary>The space kept between the field and the keyboard, and between the field and the top of the screen.</summary>
        public const float Gap = 24f;

        /// <summary>
        /// The lift that puts the bottom of the field <see cref="Gap"/> above the keyboard's top
        /// edge, but never so far that the field's top leaves the screen. 0 while the keyboard is
        /// down, or when it already clears the field.
        /// </summary>
        public static float For(float fieldBottom, float fieldTop, float keyboardTop, float screenTop)
        {
            if (keyboardTop <= 0f)
            {
                return 0f;
            }

            float needed = keyboardTop + Gap - fieldBottom;
            if (needed <= 0f)
            {
                return 0f;
            }

            float room = screenTop - Gap - fieldTop;
            return Math.Max(0f, Math.Min(needed, room));
        }
    }
}
