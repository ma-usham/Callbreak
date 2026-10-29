using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>
    /// What the shop sells, in shop order (the owner, 4 and 5 Oct 2026; mockups 15, 20 and 21 and the
    /// designer's shop-drawer-and-tables-prompts.md): three items of each kind, as the shop shows
    /// exactly three per category. Ids are stable strings: save files keep them and the art is found
    /// by them. A deck's id is its deck theme id (card-designs/full-decks/manifest.json). In every
    /// kind the first item is owned from the start (price 0) and the other two cost coins: 500 for
    /// the second and 750 for the third (the owner, 6 Oct 2026). The defaults are the game's look
    /// before the shop: Classic Crimson cards, the Himalayan Vista scenery and the Himalayan Ivory
    /// table; now the Green Dhaka cloth (ADR 058) and Durbar Square (the owner, 30 Oct 2026). Names are localization keys: the
    /// decks keep theirs, the rest are <c>ui.shop.&lt;kind&gt;.&lt;id with underscores&gt;</c>. An id
    /// that has left the shop (Kathmandu Courtyard, Village Terrace and Vintage Home, 5 Oct 2026) is
    /// unknown from then on, and a save file that still names it falls back to the default. The
    /// tables became cloths, each in its table's place (<see cref="Tables.Renamed"/>), and Durbar
    /// Square took the Himalayan Vista's (<see cref="Backgrounds.Renamed"/>).
    /// </summary>
    public static class CosmeticCatalog
    {
        public static class Decks
        {
            public const string ClassicCrimson = "classic-crimson";
            public const string HimalayanIvory = "himalayan-ivory";
            public const string KathmanduGold = "kathmandu-gold";
            public const string Default = ClassicCrimson;

            /// <summary>In coins (the owner, 6 Oct 2026).</summary>
            public const int HimalayanIvoryPrice = 500;

            /// <summary>In coins (the owner, 6 Oct 2026).</summary>
            public const int KathmanduGoldPrice = 750;
        }

        public static class Backgrounds
        {
            public const string DurbarSquare = "durbar-square";
            public const string PokharaLakeside = "pokhara-lakeside";
            public const string MonasteryGarden = "monastery-garden";
            public const string Default = DurbarSquare;

            /// <summary>In coins (the owner, 6 Oct 2026).</summary>
            public const int PokharaLakesidePrice = 500;

            /// <summary>In coins (the owner, 6 Oct 2026).</summary>
            public const int MonasteryGardenPrice = 750;

            /// <summary>
            /// The backgrounds the shop gave until 30 Oct 2026, each with the one in its place: the free
            /// Himalayan Vista became Durbar Square (the owner: "use durbar square as mainmenu bg image"),
            /// so a save file that names it owns and equips Durbar Square instead.
            /// </summary>
            public static readonly IReadOnlyDictionary<string, string> Renamed = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["himalayan-vista"] = DurbarSquare,
            });
        }

        /// <summary>
        /// What the game is played on. The game has no table any more: it is played on a cloth that
        /// fills the screen, and the shop sells cloths where it sold tables (the owner, 29 Oct 2026,
        /// ADR 058). The kind keeps its name, <see cref="CosmeticKind.Table"/>, and its save fields.
        /// </summary>
        public static class Tables
        {
            public const string GreenDhaka = "green-dhaka";
            public const string IndigoDhaka = "indigo-dhaka";
            public const string DuskHimalaya = "dusk-himalaya";
            public const string Default = GreenDhaka;

            /// <summary>In coins (the owner, 29 Oct 2026: the price of the table it replaced).</summary>
            public const int IndigoDhakaPrice = 500;

            /// <summary>In coins (the owner, 29 Oct 2026: the price of the table it replaced).</summary>
            public const int DuskHimalayaPrice = 750;

            /// <summary>
            /// The tables the shop sold until 29 Oct 2026, each with the cloth in its place: a save file
            /// that names a table owns and equips its cloth instead (the owner, ADR 058).
            /// </summary>
            public static readonly IReadOnlyDictionary<string, string> Renamed = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["himalayan-ivory"] = GreenDhaka,
                ["royal-crimson"] = IndigoDhaka,
                ["heritage-walnut"] = DuskHimalaya,
            });
        }

        private static readonly ReadOnlyCollection<CosmeticItem> DeckItems = Items(
            new CosmeticItem(CosmeticKind.Deck, Decks.ClassicCrimson, "ui.deck.classic_crimson", 0),
            new CosmeticItem(CosmeticKind.Deck, Decks.HimalayanIvory, "ui.deck.himalayan_ivory", Decks.HimalayanIvoryPrice),
            new CosmeticItem(CosmeticKind.Deck, Decks.KathmanduGold, "ui.deck.kathmandu_gold", Decks.KathmanduGoldPrice));

        private static readonly ReadOnlyCollection<CosmeticItem> BackgroundItems = Items(
            new CosmeticItem(CosmeticKind.Background, Backgrounds.DurbarSquare, "ui.shop.background.durbar_square", 0),
            new CosmeticItem(CosmeticKind.Background, Backgrounds.PokharaLakeside, "ui.shop.background.pokhara_lakeside", Backgrounds.PokharaLakesidePrice),
            new CosmeticItem(CosmeticKind.Background, Backgrounds.MonasteryGarden, "ui.shop.background.monastery_garden", Backgrounds.MonasteryGardenPrice));

        private static readonly ReadOnlyCollection<CosmeticItem> TableItems = Items(
            new CosmeticItem(CosmeticKind.Table, Tables.GreenDhaka, "ui.shop.table.green_dhaka", 0),
            new CosmeticItem(CosmeticKind.Table, Tables.IndigoDhaka, "ui.shop.table.indigo_dhaka", Tables.IndigoDhakaPrice),
            new CosmeticItem(CosmeticKind.Table, Tables.DuskHimalaya, "ui.shop.table.dusk_himalaya", Tables.DuskHimalayaPrice));

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
