using System;
using System.Collections.Generic;

namespace Callbreak.Presentation.Table
{
    /// <summary>The top seat's horizontal room beside a banner, in canvas units.</summary>
    public static class TopSeatBannerLayout
    {
        public readonly struct Bounds
        {
            public Bounds(float left, float bottom, float right, float top)
            {
                Left = left;
                Bottom = bottom;
                Right = right;
                Top = top;
            }

            public float Left { get; }
            public float Bottom { get; }
            public float Right { get; }
            public float Top { get; }
        }

        public readonly struct Placement
        {
            public Placement(float leftShift, bool fits)
            {
                LeftShift = leftShift;
                Fits = fits;
            }

            public float LeftShift { get; }
            public bool Fits { get; }
        }

        /// <summary>Finds the smallest left shift that clears the banner without moving a piece into a left-side obstacle.</summary>
        public static Placement Place(Bounds banner, IReadOnlyList<Bounds> pieces, IReadOnlyList<Bounds> leftObstacles, float bannerGap, float obstacleGap = 0f)
        {
            if (pieces == null)
            {
                throw new ArgumentNullException(nameof(pieces));
            }

            if (leftObstacles == null)
            {
                throw new ArgumentNullException(nameof(leftObstacles));
            }

            float needed = 0f;
            float available = float.MaxValue;
            for (int i = 0; i < pieces.Count; i++)
            {
                Bounds piece = pieces[i];
                for (int j = 0; j < leftObstacles.Count; j++)
                {
                    Bounds obstacle = leftObstacles[j];
                    if (piece.Bottom < obstacle.Top && obstacle.Bottom < piece.Top)
                    {
                        available = Math.Min(available, piece.Left - obstacle.Right - obstacleGap);
                    }
                }

                if (piece.Bottom < banner.Top && banner.Bottom < piece.Top && piece.Left < banner.Right)
                {
                    needed = Math.Max(needed, piece.Right + bannerGap - banner.Left);
                }
            }

            if (needed <= 0f)
            {
                return new Placement(0f, true);
            }

            float shift = Math.Max(0f, Math.Min(needed, available));
            return new Placement(shift, needed <= available);
        }
    }
}
