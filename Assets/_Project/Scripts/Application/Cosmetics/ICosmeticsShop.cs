using System;
using System.Collections.Generic;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>
    /// What the shop sells (ADR 064): card decks and themes. A theme's two parts are kinds of their own,
    /// for the pictures: the background behind the menus and the cloth the game is played on (the
    /// table's kind kept its name). The parts come with their theme: they are never sold or equipped alone.
    /// </summary>
    public enum CosmeticKind
    {
        Deck,
        Background,
        Table,
        Theme,
    }

    /// <summary>What buying an item did (<see cref="ICosmeticsShop.Buy"/>).</summary>
    public enum PurchaseResult
    {
        Bought,
        AlreadyOwned,
        NotEnoughCoins,
        UnknownItem,
    }

    /// <summary>One item of the shop's catalogue. Ids are stable strings, unique within their kind.</summary>
    public sealed class CosmeticItem
    {
        public CosmeticItem(CosmeticKind kind, string id, string nameKey, int price, string backgroundId = null, string tableId = null)
        {
            Kind = kind;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            NameKey = nameKey ?? throw new ArgumentNullException(nameof(nameKey));
            Price = price;
            BackgroundId = backgroundId;
            TableId = tableId;
        }

        public CosmeticKind Kind { get; }

        /// <summary>For example "indigo-dhaka"; a deck's id is its deck theme id.</summary>
        public string Id { get; }

        /// <summary>The localization key of its name, for example "ui.shop.table.indigo_dhaka".</summary>
        public string NameKey { get; }

        /// <summary>In coins; 0 for an item every player owns from the start. A theme's parts carry their theme's.</summary>
        public int Price { get; }

        /// <summary>A theme's background, shown behind the menus; null for the other kinds.</summary>
        public string BackgroundId { get; }

        /// <summary>A theme's cloth, the game's table; null for the other kinds.</summary>
        public string TableId { get; }
    }

    /// <summary>
    /// The shop's side of the player's cosmetics (the owner, 4 Oct 2026; designer's spec
    /// shop-drawer-and-tables-prompts.md; ADR 064): every kind has a catalogue in shop order, items the
    /// player owns, and one equipped item that the player's own table shows from the next match on,
    /// offline and online alike (each player sees their own choices, like decks, ADR 031). Buying
    /// spends coins from the wallet (<see cref="Economy.IWallet"/>); it never equips by itself. The
    /// equipped deck is the same as <see cref="ICosmeticsService.SelectedDeckId"/>. Decks and themes
    /// are bought and equipped; a background or a cloth is owned and equipped with its theme, so the
    /// equipped background and cloth are always the equipped theme's.
    /// </summary>
    public interface ICosmeticsShop
    {
        /// <summary>Ownership or equipment changed.</summary>
        event Action Changed;

        /// <summary>The items of a kind, in the order the shop shows them.</summary>
        IReadOnlyList<CosmeticItem> Catalog(CosmeticKind kind);

        bool IsOwned(CosmeticKind kind, string id);

        /// <summary>The id of the equipped item of a kind.</summary>
        string Equipped(CosmeticKind kind);

        /// <summary>Equips an owned deck or theme; throws for an unknown or unowned one, and for a theme's part.</summary>
        void Equip(CosmeticKind kind, string id);

        /// <summary>
        /// Buys a deck or a theme with coins: on <see cref="PurchaseResult.Bought"/> it is owned, not
        /// equipped. A theme's part alone is not for sale (<see cref="PurchaseResult.UnknownItem"/>).
        /// </summary>
        PurchaseResult Buy(CosmeticKind kind, string id);
    }
}
