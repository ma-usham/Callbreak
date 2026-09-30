using System.Globalization;

namespace Callbreak.Core.Scoring
{
    /// <summary>
    /// Display text for scores held in tenths (GDD §4 "Display one decimal place with a true minus sign";
    /// §11 screen 09 "negatives use a true minus (U+2212)"). Culture-invariant: always a dot and U+2212,
    /// whatever the device language, so every screen shows the same digits the server scored.
    /// </summary>
    public static class ScoreTenths
    {
        /// <summary>The true minus sign, U+2212.</summary>
        public const string MinusSign = "−";

        /// <summary>One decimal place, e.g. 41 → "4.1", −30 → "−3.0", 0 → "0.0".</summary>
        /// <param name="tenths">A score in tenths.</param>
        /// <returns>The display text.</returns>
        public static string Format(int tenths)
        {
            // Widen before negating so int.MinValue has a magnitude.
            long value = tenths;
            bool negative = value < 0;
            long magnitude = negative ? -value : value;
            string text = (magnitude / 10).ToString(CultureInfo.InvariantCulture)
                + "."
                + (magnitude % 10).ToString(CultureInfo.InvariantCulture);
            return negative ? MinusSign + text : text;
        }

        /// <summary>
        /// Signed variant for round deltas, e.g. 41 → "+4.1", −30 → "−3.0". Zero has no sign ("0.0"); a valid
        /// round delta is never zero because every call is at least 1.
        /// </summary>
        /// <param name="tenths">A score change in tenths.</param>
        /// <returns>The display text.</returns>
        public static string FormatSigned(int tenths)
        {
            return tenths > 0 ? "+" + Format(tenths) : Format(tenths);
        }
    }
}
