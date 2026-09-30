using System;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The numbers behind the local hand's arc (GDD §11 table rules, §12 "Cards on screen"). All
    /// lengths are canvas units at the 1920 × 1080 reference. The defaults are the GDD's; the table
    /// prefab carries its own copy with the values measured on mockup 08.
    /// </summary>
    [Serializable]
    public sealed class HandLayoutSettings
    {
        /// <summary>Width of a hand card (GDD: 180).</summary>
        public float CardWidth = 180f;

        /// <summary>A full hand takes at least this share of the safe-area width (GDD: 70%).</summary>
        public float MinWidthFraction = 0.70f;

        /// <summary>The hand may widen to this share to keep every strip readable (GDD: 86%).</summary>
        public float MaxWidthFraction = 0.86f;

        /// <summary>
        /// The strip every card should keep visible beside its neighbour: 7 mm on the device (GDD §12),
        /// which the view converts to canvas units. The hand widens up to
        /// <see cref="MaxWidthFraction"/> to keep it, and never goes below <see cref="HandLayout.HardMinStrip"/>.
        /// </summary>
        public float MinStrip = HandLayout.HardMinStrip;

        /// <summary>Spacing stops widening here as cards are played; the hand then gets narrower.</summary>
        public float MaxStrip = 144f;

        /// <summary>How far a legal card rises along its own axis (GDD: 28).</summary>
        public float Lift = 28f;

        /// <summary>Extra rise of the card under the finger while sliding along the hand.</summary>
        public float HoverLift = 18f;

        /// <summary>Radius of the circle the card centres sit on; larger is flatter.</summary>
        public float ArcRadius = 3740f;

        public HandLayoutSettings Clone() => (HandLayoutSettings)MemberwiseClone();
    }

    /// <summary>Where one hand card goes, relative to the centre of the middle card of an unlifted hand.</summary>
    public readonly struct CardPose : IEquatable<CardPose>
    {
        public CardPose(float x, float y, float rotation)
        {
            X = x;
            Y = y;
            Rotation = rotation;
        }

        /// <summary>Card centre, right of the hand's centre.</summary>
        public float X { get; }

        /// <summary>Card centre, above the middle card's centre (y up); the ends of the arc are below it.</summary>
        public float Y { get; }

        /// <summary>Degrees anticlockwise: cards left of centre lean left (positive), right of centre lean right.</summary>
        public float Rotation { get; }

        public bool Equals(CardPose other) => X == other.X && Y == other.Y && Rotation == other.Rotation;

        public override bool Equals(object obj) => obj is CardPose other && Equals(other);

        public override int GetHashCode() => (X, Y, Rotation).GetHashCode();

        public override string ToString() => FormattableString.Invariant($"({X:0.##}, {Y:0.##}) {Rotation:0.##}°");
    }

    /// <summary>
    /// The local hand as a pure function (GDD §23 "HandLayout is a pure function (n cards →
    /// positions and rotations), unit-tested, and never uses LayoutGroups"): n cards on an arc,
    /// spaced so that a full hand takes 70–86% of the safe-area width, every card keeps a visible
    /// strip, spacing widens as cards are played, and legal cards rise.
    /// </summary>
    public static class HandLayout
    {
        /// <summary>No card ever shows less than this (GDD §11: "at least 7 mm (≈ 44 pt)").</summary>
        public const float HardMinStrip = 44f;

        /// <summary>
        /// Distance between neighbouring card centres along the arc: what stays visible of every
        /// card but the last.
        /// </summary>
        public static float Strip(int count, float safeWidth, HandLayoutSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (count <= 1)
            {
                return 0f;
            }

            float gaps = count - 1;
            float preferred = (settings.MinWidthFraction * safeWidth - settings.CardWidth) / gaps;
            float widest = (settings.MaxWidthFraction * safeWidth - settings.CardWidth) / gaps;

            // Fewer cards spread out, up to MaxStrip; after that the hand gets narrower instead.
            float strip = Math.Min(preferred, settings.MaxStrip);

            // Too tight to read comfortably: widen, but not beyond 86% of the safe area.
            if (strip < settings.MinStrip)
            {
                strip = Math.Min(settings.MinStrip, Math.Max(widest, strip));
            }

            // The 44 px strip is the hard rule: a hand that cannot keep it inside 86% overflows.
            return Math.Max(strip, Math.Min(HardMinStrip, settings.MaxStrip));
        }

        /// <summary>Distance from the left edge of the first card to the right edge of the last, before rotation.</summary>
        public static float Width(int count, float safeWidth, HandLayoutSettings settings)
        {
            if (count <= 0)
            {
                return 0f;
            }

            return Strip(count, safeWidth, settings) * (count - 1) + settings.CardWidth;
        }

        /// <summary>
        /// Fills <paramref name="poses"/> (at least <paramref name="count"/> long) with the card
        /// poses, left to right. <paramref name="lifted"/> marks the cards that rise (legal cards);
        /// <paramref name="hovered"/> is the index under the finger, or -1. Neither may be shorter
        /// than the hand; null means nothing is lifted.
        /// </summary>
        public static void Compute(int count, float safeWidth, HandLayoutSettings settings, bool[] lifted, int hovered, CardPose[] poses)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (poses == null)
            {
                throw new ArgumentNullException(nameof(poses));
            }

            if (count < 0 || poses.Length < count)
            {
                throw new ArgumentException("The pose buffer is shorter than the hand.", nameof(poses));
            }

            if (lifted != null && lifted.Length < count)
            {
                throw new ArgumentException("The lift flags are shorter than the hand.", nameof(lifted));
            }

            float strip = Strip(count, safeWidth, settings);
            float radius = Math.Max(settings.ArcRadius, 1f);
            float middle = (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                // Arc length from the middle of the hand, turned into an angle on the circle.
                double angle = (i - middle) * strip / radius;
                double sin = Math.Sin(angle), cos = Math.Cos(angle);
                double x = radius * sin;
                double y = -radius * (1.0 - cos);

                float rise = 0f;
                if (lifted != null && lifted[i])
                {
                    rise += settings.Lift;
                }

                if (i == hovered)
                {
                    rise += settings.HoverLift;
                }

                // A card rises along its own axis, which leans with the arc.
                x += rise * sin;
                y += rise * cos;
                poses[i] = new CardPose((float)x, (float)y, (float)(-angle * (180.0 / Math.PI)));
            }
        }
    }
}
