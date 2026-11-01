using System;
using System.Collections.Generic;
using System.Globalization;
using Callbreak.Application.Cosmetics;

namespace Callbreak.Application.Offers
{
    /// <summary>
    /// Turns the backend's <see cref="FestivalOffersConfig"/> into offers the game can trust (ADR 067).
    /// An entry with anything wrong is left out on its own, so a typo never gives a theme away by
    /// mistake and never takes the other offers with it:
    /// <list type="bullet">
    /// <item>its id is missing, longer than 64, or not lowercase letters, digits and hyphens, or an
    /// earlier entry has it;</item>
    /// <item>its theme isn't a paid theme of the shop (the free one can't be given);</item>
    /// <item>its kind isn't "free", "watch-ad" or "discount";</item>
    /// <item>a discount's price isn't above 0 and below the theme's own;</item>
    /// <item>its start or end isn't ISO 8601 with a time zone ("+05:45" or "Z"), or it ends before it
    /// starts.</item>
    /// </list>
    /// An entry without a title gets the game's own words, <see cref="DefaultTitleKey"/>.
    /// </summary>
    public static class FestivalOfferReader
    {
        /// <summary>The offer's words when the backend names none.</summary>
        public const string DefaultTitleKey = "ui.offer.default";

        private const int MaxIdLength = 64;

        private static readonly string[] ZonedFormats = { "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mmzzz" };
        private static readonly string[] UtcFormats = { "yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm'Z'" };

        /// <summary>The usable offers, in the order written; none for a null config.</summary>
        public static IReadOnlyList<FestivalOffer> Read(FestivalOffersConfig config)
        {
            var offers = new List<FestivalOffer>();
            if (config?.offers == null)
            {
                return offers;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (FestivalOfferEntry entry in config.offers)
            {
                FestivalOffer offer = ReadEntry(entry);
                if (offer != null && ids.Add(offer.Id))
                {
                    offers.Add(offer);
                }
            }

            return offers;
        }

        private static FestivalOffer ReadEntry(FestivalOfferEntry entry)
        {
            if (entry == null || !IsId(entry.id))
            {
                return null;
            }

            CosmeticItem theme = CosmeticCatalog.Find(CosmeticKind.Theme, entry.theme?.Trim());
            if (theme == null || theme.Price <= 0 || !TryKind(entry.kind, out FestivalOfferKind kind))
            {
                return null;
            }

            if (kind == FestivalOfferKind.Discount && (entry.price <= 0 || entry.price >= theme.Price))
            {
                return null;
            }

            if (!TryInstant(entry.start, out DateTimeOffset start) || !TryInstant(entry.end, out DateTimeOffset end) || end <= start)
            {
                return null;
            }

            string title = string.IsNullOrWhiteSpace(entry.title) ? DefaultTitleKey : entry.title.Trim();
            return new FestivalOffer(entry.id, theme.Id, kind, entry.price, start, end, title);
        }

        private static bool IsId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > MaxIdLength)
            {
                return false;
            }

            foreach (char c in id)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '-')
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryKind(string text, out FestivalOfferKind kind)
        {
            switch (text?.Trim().ToLowerInvariant())
            {
                case "free":
                    kind = FestivalOfferKind.Free;
                    return true;
                case "watch-ad":
                    kind = FestivalOfferKind.WatchAd;
                    return true;
                case "discount":
                    kind = FestivalOfferKind.Discount;
                    return true;
                default:
                    kind = FestivalOfferKind.Free;
                    return false;
            }
        }

        /// <summary>An instant with its time zone: "2026-10-31T00:00:00+05:45" or "2026-10-30T18:15:00Z".</summary>
        private static bool TryInstant(string text, out DateTimeOffset instant)
        {
            instant = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string trimmed = text.Trim();
            return DateTimeOffset.TryParseExact(trimmed, ZonedFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out instant)
                || DateTimeOffset.TryParseExact(trimmed, UtcFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out instant);
        }
    }
}
