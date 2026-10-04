using System;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// Where the shop's one-row carousel rests and how it moves (designer's spec
    /// shop-drawer-and-tables-prompts.md: exactly three items in view, swipe/drag or the chevrons
    /// to browse, the track shows the position, arrows off at the ends, browsing hidden when
    /// everything fits). Positions are offsets in tiles: offset 0 shows tiles 0 to visible - 1.
    /// At rest the row always sits on a whole tile. A chevron moves a page (the visible count); a
    /// drag follows the finger, resisting past either end, and settles on the nearest tile, or on
    /// the next one in a quick flick's direction. Engine-free, so it is tested outside Unity;
    /// <c>ShopCarousel</c> plays it.
    /// </summary>
    public static class CarouselScroll
    {
        /// <summary>Past either end the row follows the finger at this fraction of its travel.</summary>
        public const float Resistance = 0.35f;

        /// <summary>A release at this speed (tiles per second) or more is a flick.</summary>
        public const float FlickSpeed = 1.5f;

        /// <summary>A flick carries on for as far as it would travel in this time.</summary>
        public const float MomentumSeconds = 0.25f;

        private const float Epsilon = 0.001f;

        /// <summary>The furthest the row goes: the last tile at the right of the view.</summary>
        public static int MaxOffset(int count, int visible) => Math.Max(0, count - visible);

        /// <summary>More items than the view holds: the chevrons, the track and the hint show.</summary>
        public static bool Browsable(int count, int visible) => count > visible;

        public static float Clamp(float offset, int count, int visible) => Math.Max(0f, Math.Min(MaxOffset(count, visible), offset));

        public static bool CanGoBack(float offset, int count, int visible) => Browsable(count, visible) && offset > Epsilon;

        public static bool CanGoForward(float offset, int count, int visible) => Browsable(count, visible) && offset < MaxOffset(count, visible) - Epsilon;

        /// <summary>
        /// A chevron: a page (<paramref name="visible"/> tiles) back (<paramref name="direction"/> -1)
        /// or forward (+1) from the tile the row rests on, no further than the ends.
        /// </summary>
        public static int Page(float offset, int direction, int count, int visible)
        {
            int from = (int)Math.Round(Clamp(offset, count, visible));
            return (int)Clamp(from + Math.Sign(direction) * Math.Max(1, visible), count, visible);
        }

        /// <summary>
        /// The row during a drag that started at <paramref name="startOffset"/> and has moved the
        /// row by <paramref name="tilesMoved"/> (positive: on to later tiles). Past either end it
        /// follows at <see cref="Resistance"/>.
        /// </summary>
        public static float Drag(float startOffset, float tilesMoved, int count, int visible)
        {
            float raw = startOffset + tilesMoved;
            float max = MaxOffset(count, visible);
            if (raw < 0f)
            {
                return raw * Resistance;
            }

            if (raw > max)
            {
                return max + (raw - max) * Resistance;
            }

            return raw;
        }

        /// <summary>
        /// Where the row settles when the finger lets go at <paramref name="offset"/>, moving at
        /// <paramref name="velocity"/> tiles per second (positive: on to later tiles): the nearest
        /// tile, or for a flick at least the next tile that way, further for a faster one.
        /// </summary>
        public static int Settle(float offset, float velocity, int count, int visible)
        {
            float target = (float)Math.Round(offset);
            if (Math.Abs(velocity) >= FlickSpeed)
            {
                float projected = (float)Math.Round(offset + velocity * MomentumSeconds);
                target = velocity > 0f
                    ? Math.Max((float)Math.Floor(offset + Epsilon) + 1f, projected)
                    : Math.Min((float)Math.Ceiling(offset - Epsilon) - 1f, projected);
            }

            return (int)Clamp(target, count, visible);
        }

        /// <summary>Where the track's thumb is, from 0 (the start) to 1 (the end); 0 when everything fits.</summary>
        public static float ThumbPosition(float offset, int count, int visible)
        {
            int max = MaxOffset(count, visible);
            return max > 0 ? Clamp(offset, count, visible) / max : 0f;
        }
    }
}
