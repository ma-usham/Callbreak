using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>
    /// What the shop sells, in shop order (the owner, 4 and 5 Oct 2026; mockup 15; ADR 064): the card
    /// decks, and the themes. A theme is a background shown behind the menus together with the cloth
    /// the game is played on, bought and equipped as one (the owner, 31 Oct 2026: "implement what are
    /// remained in the game", the shop themes of Figma "V1 Rebuild · Themes" and "V1 Rebuild · Shop").
    /// The backgrounds and the cloths are listed too, as the parts of the themes, in the themes' order:
    /// the pictures are found by their ids, but they are never sold or equipped on their own. Ids are
    /// stable strings: save files keep them and the art is found by them (a deck's in Art/Cards, a new
    /// theme's background and cloth carry the theme's own id). The first deck and the first theme
    /// (Durbar Square on the Green Dhaka cloth) are owned from the start (price 0); the rest cost
    /// coins: their <see cref="Decks"/> and <see cref="Themes"/> prices. Names are localization keys:
    /// a deck's is <c>ui.deck.&lt;id with underscores&gt;</c>, a theme's
    /// <c>ui.shop.theme.&lt;id with underscores&gt;</c>, and its parts carry their theme's. An id that has left the shop (Kathmandu Courtyard, Village Terrace and Vintage Home,
    /// 5 Oct 2026) is unknown from then on, and a save file that still names it falls back to the
    /// default. The tables became cloths, each in its table's place (<see cref="Tables.Renamed"/>),
    /// Durbar Square took the Himalayan Vista's (<see cref="Backgrounds.Renamed"/>), and the Figma decks
    /// the old decks' (<see cref="Decks.Renamed"/>).
    /// </summary>
    public static class CosmeticCatalog
    {
        /// <summary>
        /// The decks of Figma's "Card Decks" page the game ships (the owner, 2 Nov 2026; ADR 068), in
        /// shop order, each in an old deck's place: Classic, everyone's, in Classic Crimson's; Mithila
        /// Ink in Himalayan Ivory's and Kathmandu Royal in Kathmandu Gold's, at their prices (the owner,
        /// 6 Oct 2026), kept here for the owner to change. Figma's fourth deck, Vintage (342:569), waits
        /// to be downloaded only by those who buy it: its art alone would add about 16 MB to every
        /// phone's download.
        /// </summary>
        public static class Decks
        {
            public const string Classic = "classic";
            public const string MithilaInk = "mithila-ink";
            public const string KathmanduRoyal = "kathmandu-royal";
            public const string Default = Classic;

            /// <summary>In coins: a second goal after a starter theme (the owner, 3 Nov 2026; ADR 077).</summary>
            public const int MithilaInkPrice = 600;

            /// <summary>In coins: the dearer deck (the owner, 3 Nov 2026; ADR 077).</summary>
            public const int KathmanduRoyalPrice = 1000;

            /// <summary>
            /// The decks the shop gave and sold until 2 Nov 2026, each with the deck in its place: a
            /// save file that names one owns and equips that deck instead (ADR 068).
            /// </summary>
            public static readonly IReadOnlyDictionary<string, string> Renamed = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["classic-crimson"] = Classic,
                ["himalayan-ivory"] = MithilaInk,
                ["kathmandu-gold"] = KathmanduRoyal,
            });
        }

        /// <summary>
        /// The themes (ADR 064), in shop order: the free Durbar Square first, the seven new themes,
        /// then the two that were sold as a background until 31 Oct 2026. Their prices come in three
        /// tiers (the owner, 3 Nov 2026: "with the default money the user can at least purchase one
        /// theme, then he has to earn money to purchase other themes"; ADR 077): starter themes the
        /// 500 starting coins buy one of, with coins left for a stake match; themes a week of play
        /// buys; and premium themes, which festival offers can give or discount.
        /// </summary>
        public static class Themes
        {
            public const string DurbarSquare = "durbar-square";
            public const string HimalayanSunrise = "himalayan-sunrise";
            public const string LaliguransForest = "laligurans-forest";
            public const string GoldenTerraces = "golden-terraces";
            public const string TiharLights = "tihar-lights";
            public const string PrayerFlagPass = "prayer-flag-pass";
            public const string MithilaGarden = "mithila-garden";
            public const string PhewaMoonlight = "phewa-moonlight";
            public const string PokharaLakeside = "pokhara-lakeside";
            public const string MonasteryGarden = "monastery-garden";
            public const string Default = DurbarSquare;

            /// <summary>In coins: a starter theme (ADR 077).</summary>
            public const int HimalayanSunrisePrice = 400;

            /// <summary>In coins: a starter theme (ADR 077).</summary>
            public const int LaliguransForestPrice = 400;

            /// <summary>In coins: a week of play (ADR 077).</summary>
            public const int GoldenTerracesPrice = 900;

            /// <summary>In coins: premium (ADR 077).</summary>
            public const int TiharLightsPrice = 1500;

            /// <summary>In coins: a week of play (ADR 077).</summary>
            public const int PrayerFlagPassPrice = 900;

            /// <summary>In coins: premium (ADR 077).</summary>
            public const int MithilaGardenPrice = 1500;

            /// <summary>In coins: premium (ADR 077).</summary>
            public const int PhewaMoonlightPrice = 1500;

            /// <summary>In coins: a starter theme (ADR 077).</summary>
            public const int PokharaLakesidePrice = 400;

            /// <summary>In coins: a week of play (ADR 077).</summary>
            public const int MonasteryGardenPrice = 900;
        }

        /// <summary>The themes' backgrounds, shown behind the menus.</summary>
        public static class Backgrounds
        {
            public const string DurbarSquare = "durbar-square";
            public const string PokharaLakeside = "pokhara-lakeside";
            public const string MonasteryGarden = "monastery-garden";
            public const string Default = DurbarSquare;

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
        /// What the game is played on: the themes' cloths. The game has no table any more: it is played
        /// on a cloth that fills the screen (the owner, 29 Oct 2026, ADR 058). The kind keeps its name,
        /// <see cref="CosmeticKind.Table"/>, and its save fields.
        /// </summary>
        public static class Tables
        {
            public const string GreenDhaka = "green-dhaka";
            public const string IndigoDhaka = "indigo-dhaka";
            public const string DuskHimalaya = "dusk-himalaya";
            public const string Default = GreenDhaka;

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
            new CosmeticItem(CosmeticKind.Deck, Decks.Classic, "ui.deck.classic", 0),
            new CosmeticItem(CosmeticKind.Deck, Decks.MithilaInk, "ui.deck.mithila_ink", Decks.MithilaInkPrice),
            new CosmeticItem(CosmeticKind.Deck, Decks.KathmanduRoyal, "ui.deck.kathmandu_royal", Decks.KathmanduRoyalPrice));

        private static readonly ReadOnlyCollection<CosmeticItem> ThemeItems = Items(
            Theme(Themes.DurbarSquare, 0, Backgrounds.DurbarSquare, Tables.GreenDhaka),
            Theme(Themes.HimalayanSunrise, Themes.HimalayanSunrisePrice),
            Theme(Themes.LaliguransForest, Themes.LaliguransForestPrice),
            Theme(Themes.GoldenTerraces, Themes.GoldenTerracesPrice),
            Theme(Themes.TiharLights, Themes.TiharLightsPrice),
            Theme(Themes.PrayerFlagPass, Themes.PrayerFlagPassPrice),
            Theme(Themes.MithilaGarden, Themes.MithilaGardenPrice),
            Theme(Themes.PhewaMoonlight, Themes.PhewaMoonlightPrice),
            Theme(Themes.PokharaLakeside, Themes.PokharaLakesidePrice, Backgrounds.PokharaLakeside, Tables.IndigoDhaka),
            Theme(Themes.MonasteryGarden, Themes.MonasteryGardenPrice, Backgrounds.MonasteryGarden, Tables.DuskHimalaya));

        private static readonly ReadOnlyCollection<CosmeticItem> BackgroundItems = Parts(CosmeticKind.Background);

        private static readonly ReadOnlyCollection<CosmeticItem> TableItems = Parts(CosmeticKind.Table);

        /// <summary>The items of a kind, in shop order (a part, in its theme's).</summary>
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
                case CosmeticKind.Theme:
                    return ThemeItems;
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

        /// <summary>The theme a background or a cloth belongs to; null for any other id or kind.</summary>
        public static CosmeticItem ThemeOf(CosmeticKind part, string id)
        {
            if (id == null || (part != CosmeticKind.Background && part != CosmeticKind.Table))
            {
                return null;
            }

            foreach (CosmeticItem theme in ThemeItems)
            {
                if ((part == CosmeticKind.Background ? theme.BackgroundId : theme.TableId) == id)
                {
                    return theme;
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
                case CosmeticKind.Theme:
                    return Themes.Default;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cosmetic kind.");
            }
        }

        /// <summary>A theme: its own background and cloth carry its id unless they are named.</summary>
        private static CosmeticItem Theme(string id, int price, string backgroundId = null, string tableId = null) =>
            new CosmeticItem(CosmeticKind.Theme, id, NameKey(id), price, backgroundId ?? id, tableId ?? id);

        /// <summary>The themes' backgrounds or cloths, in the themes' order, each named and priced as its theme.</summary>
        private static ReadOnlyCollection<CosmeticItem> Parts(CosmeticKind kind)
        {
            var parts = new CosmeticItem[ThemeItems.Count];
            for (int i = 0; i < parts.Length; i++)
            {
                CosmeticItem theme = ThemeItems[i];
                parts[i] = new CosmeticItem(kind, kind == CosmeticKind.Background ? theme.BackgroundId : theme.TableId, theme.NameKey, theme.Price);
            }

            return Items(parts);
        }

        private static string NameKey(string themeId) => "ui.shop.theme." + themeId.Replace('-', '_');

        private static ReadOnlyCollection<CosmeticItem> Items(params CosmeticItem[] items) => new ReadOnlyCollection<CosmeticItem>(items);
    }
}
