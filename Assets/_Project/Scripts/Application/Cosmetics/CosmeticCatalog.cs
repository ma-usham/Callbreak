using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>
    /// What the shop sells, in shop order (the owner, 4 Oct 2026; mockups 15, 20 and 21 and the
    /// designer's shop-drawer-and-tables-prompts.md). Ids are stable strings: save files keep them
    /// and the art is found by them. A deck's id is its deck theme id
    /// (card-designs/full-decks/manifest.json). Every deck and background and the Himalayan Ivory
    /// table are owned from the start (price 0); the other tables cost coins. The defaults are the
    /// game's look before the shop: Classic Crimson cards, the Himalayan Vista scenery and the
    /// Himalayan Ivory table. Names are localization keys: the decks keep theirs, the rest are
    /// <c>ui.shop.&lt;kind&gt;.&lt;id with underscores&gt;</c>.
    /// </summary>
    public static class CosmeticCatalog
    {
        public static class Decks
        {
            public const string ClassicCrimson = "classic-crimson";
            public const string HimalayanIvory = "himalayan-ivory";
            public const string KathmanduGold = "kathmandu-gold";
            public const string Default = ClassicCrimson;
        }

        public static class Backgrounds
        {
            public const string KathmanduCourtyard = "kathmandu-courtyard";
            public const string VillageTerrace = "village-terrace";
            public const string VintageHome = "vintage-home";
            public const string HimalayanVista = "himalayan-vista";
            public const string PokharaLakeside = "pokhara-lakeside";
            public const string MonasteryGarden = "monastery-garden";
            public const string Default = HimalayanVista;
        }

        public static class Tables
        {
            public const string HimalayanIvory = "himalayan-ivory";
            public const string RoyalCrimson = "royal-crimson";
            public const string HeritageWalnut = "heritage-walnut";
            public const string Default = HimalayanIvory;

            /// <summary>In coins (the owner, 4 Oct 2026; placeholders to tune).</summary>
            public const int RoyalCrimsonPrice = 350;

            /// <summary>In coins (the owner, 4 Oct 2026; placeholders to tune).</summary>
            public const int HeritageWalnutPrice = 250;
        }

        private static readonly ReadOnlyCollection<CosmeticItem> DeckItems = Items(
            new CosmeticItem(CosmeticKind.Deck, Decks.ClassicCrimson, "ui.deck.classic_crimson", 0),
            new CosmeticItem(CosmeticKind.Deck, Decks.HimalayanIvory, "ui.deck.himalayan_ivory", 0),
            new CosmeticItem(CosmeticKind.Deck, Decks.KathmanduGold, "ui.deck.kathmandu_gold", 0));

        private static readonly ReadOnlyCollection<CosmeticItem> BackgroundItems = Items(
            new CosmeticItem(CosmeticKind.Background, Backgrounds.KathmanduCourtyard, "ui.shop.background.kathmandu_courtyard", 0),
            new CosmeticItem(CosmeticKind.Background, Backgrounds.VillageTerrace, "ui.shop.background.village_terrace", 0),
            new CosmeticItem(CosmeticKind.Background, Backgrounds.VintageHome, "ui.shop.background.vintage_home", 0),
            new CosmeticItem(CosmeticKind.Background, Backgrounds.HimalayanVista, "ui.shop.background.himalayan_vista", 0),
            new CosmeticItem(CosmeticKind.Background, Backgrounds.PokharaLakeside, "ui.shop.background.pokhara_lakeside", 0),
            new CosmeticItem(CosmeticKind.Background, Backgrounds.MonasteryGarden, "ui.shop.background.monastery_garden", 0));

        private static readonly ReadOnlyCollection<CosmeticItem> TableItems = Items(
            new CosmeticItem(CosmeticKind.Table, Tables.HimalayanIvory, "ui.shop.table.himalayan_ivory", 0),
            new CosmeticItem(CosmeticKind.Table, Tables.RoyalCrimson, "ui.shop.table.royal_crimson", Tables.RoyalCrimsonPrice),
            new CosmeticItem(CosmeticKind.Table, Tables.HeritageWalnut, "ui.shop.table.heritage_walnut", Tables.HeritageWalnutPrice));

        /// <summary>The items of a kind, in shop order.</summary>
        public static IReadOnlyList<CosmeticItem> Of(CosmeticKind kind)
        {
            switch (kind)
            {
                case CosmeticKind.Deck:
                    return DeckItems;
                case CosmeticKind.Background:
                    return BackgroundItems;
                case CosmeticKind.Table:
                    return TableItems;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cosmetic kind.");
            }
        }

        /// <summary>The item of that kind with that id (ids are case-sensitive), or null when there is none.</summary>
        public static CosmeticItem Find(CosmeticKind kind, string id)
        {
            if (id == null || !Enum.IsDefined(typeof(CosmeticKind), kind))
            {
                return null;
            }

            foreach (CosmeticItem item in Of(kind))
            {
                if (item.Id == id)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>What a new player has equipped.</summary>
        public static string DefaultOf(CosmeticKind kind)
        {
            switch (kind)
            {
                case CosmeticKind.Deck:
                    return Decks.Default;
                case CosmeticKind.Background:
                    return Backgrounds.Default;
                case CosmeticKind.Table:
                    return Tables.Default;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cosmetic kind.");
            }
        }

        private static ReadOnlyCollection<CosmeticItem> Items(params CosmeticItem[] items) => new ReadOnlyCollection<CosmeticItem>(items);
    }
}
