using System;
using System.Collections.Generic;

namespace Callbreak.Application.Offers
{
    /// <summary>
    /// The <c>festival_offers</c> value of Firebase Remote Config as written in the console (ADR 067;
    /// Docs/Setup/FestivalOffers.md): plain serializable data, read as it comes and checked by
    /// <see cref="FestivalOfferReader"/>.
    /// </summary>
    [Serializable]
    public sealed class FestivalOffersConfig
    {
        public List<FestivalOfferEntry> offers = new List<FestivalOfferEntry>();
    }

    /// <summary>One offer as written: every value is checked before the game believes it.</summary>
    [Serializable]
    public sealed class FestivalOfferEntry
    {
        /// <summary>For example "tihar-2026".</summary>
        public string id;

        /// <summary>A theme id, for example "tihar-lights".</summary>
        public string theme;

        /// <summary>"free", "watch-ad" or "discount".</summary>
        public string kind;

        /// <summary>In coins, for a discount only.</summary>
        public int price;

        /// <summary>ISO 8601 with its time zone, for example "2026-10-31T00:00:00+05:45".</summary>
        public string start;

        /// <summary>The first moment the offer is gone, written as <see cref="start"/> is.</summary>
        public string end;

        /// <summary>A localization key for the offer's words; empty for the game's own.</summary>
        public string title;
    }
}
