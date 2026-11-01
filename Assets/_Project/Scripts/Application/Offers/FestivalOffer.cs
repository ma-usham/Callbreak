using System;

namespace Callbreak.Application.Offers
{
    /// <summary>How a festival offer's theme is had (ADR 067).</summary>
    public enum FestivalOfferKind
    {
        /// <summary>A gift: one tap and the theme is the player's.</summary>
        Free,

        /// <summary>A gift for one rewarded ad watched to the end (the owner's choice for Tihar 2026).</summary>
        WatchAd,

        /// <summary>The theme for fewer coins than the shop's price.</summary>
        Discount,
    }

    /// <summary>
    /// One festival offer as the backend sets it (ADR 067): a theme the shop already has, given away or
    /// sold for less while the festival lasts. Its id names the festival and the year ("tihar-2026"),
    /// so the same theme can be offered again next year; each offer is claimed at most once per player.
    /// It is live from <see cref="Start"/> up to, not including, <see cref="End"/>: instants with their
    /// time zone, so "15 Nov in Nepal" means the same moment on every phone.
    /// </summary>
    public sealed class FestivalOffer
    {
        public FestivalOffer(string id, string themeId, FestivalOfferKind kind, int price, DateTimeOffset start, DateTimeOffset end, string titleKey)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            ThemeId = themeId ?? throw new ArgumentNullException(nameof(themeId));
            Kind = kind;
            Price = kind == FestivalOfferKind.Discount ? price : 0;
            Start = start;
            End = end;
            TitleKey = titleKey ?? throw new ArgumentNullException(nameof(titleKey));
        }

        /// <summary>For example "tihar-2026": lowercase letters, digits and hyphens.</summary>
        public string Id { get; }

        /// <summary>The theme it gives, a <see cref="Cosmetics.CosmeticCatalog.Themes"/> id.</summary>
        public string ThemeId { get; }

        public FestivalOfferKind Kind { get; }

        /// <summary>In coins: what a <see cref="FestivalOfferKind.Discount"/> costs; 0 for a gift.</summary>
        public int Price { get; }

        public DateTimeOffset Start { get; }

        /// <summary>The first moment the offer is gone.</summary>
        public DateTimeOffset End { get; }

        /// <summary>The localization key of the offer's words in the theme's preview.</summary>
        public string TitleKey { get; }

        public bool IsActiveAt(DateTimeOffset now) => now >= Start && now < End;
    }
}
