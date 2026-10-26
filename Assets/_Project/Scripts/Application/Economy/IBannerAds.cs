using System;

namespace Callbreak.Application.Economy
{
    /// <summary>
    /// The banner ad on the table (port; the owner, 14 and 26 Oct 2026, ADR 051): a standard banner at
    /// the top of the table during a match, which No Ads (<see cref="INoAds"/>) removes. The table asks
    /// whether one may come (<see cref="MayShow"/>) to make room for it, says where the banner may go
    /// (<see cref="ShowIn"/>) and when it must not be seen (<see cref="Hide"/>); the banner is a view of
    /// the ad network's own, drawn over the game, so it is hidden whenever anything covers the bare
    /// table. On phones it is Google AdMob (<see cref="BannerAds"/> over the AdMob adapter); in the
    /// Editor, in the tests and on desktop <see cref="StandInBannerAds"/>, which never has one.
    /// Every call comes on the main thread.
    /// </summary>
    public interface IBannerAds
    {
        /// <summary>A banner is on the screen now.</summary>
        bool IsShowing { get; }

        /// <summary>
        /// A banner may come: there is an ad network and the player doesn't own No Ads. The table
        /// makes room for it when it opens while this is true, before a banner has loaded.
        /// </summary>
        bool MayShow { get; }

        /// <summary>
        /// The banner may show in <paramref name="slot"/>: now when one is loaded and fits there,
        /// otherwise as soon as one is. Asked again with the same slot, nothing changes; with another,
        /// the banner moves.
        /// </summary>
        void ShowIn(BannerSlot slot);

        /// <summary>The banner must not be seen: it goes off the screen and stays loaded.</summary>
        void Hide();
    }

    /// <summary>
    /// A place on the screen for the banner: a rectangle in screen pixels, from the screen's top-left
    /// corner, and the line the banner is centred on as far as the rectangle allows (the screen's
    /// middle on the table).
    /// </summary>
    public readonly struct BannerSlot : IEquatable<BannerSlot>
    {
        /// <summary>A slot whose banner is centred in it.</summary>
        public BannerSlot(int x, int y, int width, int height)
            : this(x, y, width, height, x + (width / 2))
        {
        }

        /// <summary>A slot whose banner is centred on <paramref name="centreX"/>, kept inside the slot.</summary>
        public BannerSlot(int x, int y, int width, int height, int centreX)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
            CentreX = centreX;
        }

        /// <summary>The slot's left edge, in pixels from the screen's left edge.</summary>
        public int X { get; }

        /// <summary>The slot's top edge, in pixels from the screen's top edge.</summary>
        public int Y { get; }

        public int Width { get; }

        public int Height { get; }

        /// <summary>The line the banner is centred on, in pixels from the screen's left edge.</summary>
        public int CentreX { get; }

        public bool Equals(BannerSlot other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height && CentreX == other.CentreX;

        public override bool Equals(object obj) => obj is BannerSlot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X;
                hash = (hash * 397) ^ Y;
                hash = (hash * 397) ^ Width;
                hash = (hash * 397) ^ Height;
                return (hash * 397) ^ CentreX;
            }
        }

        public override string ToString() => $"{Width}x{Height} at ({X}, {Y}), centred on {CentreX}";
    }
}
