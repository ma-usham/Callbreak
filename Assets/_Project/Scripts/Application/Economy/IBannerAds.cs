using System;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The banner ad on the table (port; the owner, 14 Oct 2026): a small ad left of the Scores button
    /// during a match, which No Ads (<see cref="INoAds"/>) removes. The table says where the banner may
    /// go (<see cref="ShowIn"/>) and when it must not be seen (<see cref="Hide"/>); the banner is a
    /// view of the ad network's own, drawn over the game, so it is hidden whenever anything covers the
    /// bare table. On phones it is Google AdMob (<see cref="BannerAds"/> over the AdMob adapter); in
    /// the Editor, in the tests and on desktop <see cref="StandInBannerAds"/>, which never has one.
    /// Every call comes on the main thread.
    /// </summary>
    public interface IBannerAds
    {
        /// <summary>A banner is on the screen now.</summary>
        bool IsShowing { get; }

        /// <summary>
        /// The banner may show in <paramref name="slot"/>: now when one is loaded and fits there,
        /// otherwise as soon as one is. Asked again with the same slot, nothing changes; with another,
        /// the banner moves.
        /// </summary>
        void ShowIn(BannerSlot slot);

        /// <summary>The banner must not be seen: it goes off the screen and stays loaded.</summary>
        void Hide();
    }

    /// <summary>A place on the screen for the banner: a rectangle in screen pixels, from the screen's top-left corner.</summary>
    public readonly struct BannerSlot : IEquatable<BannerSlot>
    {
        public BannerSlot(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>The slot's left edge, in pixels from the screen's left edge.</summary>
        public int X { get; }

        /// <summary>The slot's top edge, in pixels from the screen's top edge.</summary>
        public int Y { get; }

        public int Width { get; }

        public int Height { get; }

        public bool Equals(BannerSlot other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object obj) => obj is BannerSlot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Width;
                return (hash * 397) ^ Height;
            }
        }

        public override string ToString() => $"{Width}x{Height} at ({X}, {Y})";
    }
}
