using System;
using System.Collections.Generic;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>What the shop sells (mockups 15, 20 and 21): card decks, the scenery behind the table, and the table.</summary>
    public enum CosmeticKind
    {
        Deck,
        Background,
        Table,
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
        public CosmeticItem(CosmeticKind kind, string id, string nameKey, int price)
        {
            Kind = kind;
            Id = id ?? throw new ArgumentNullException(nameof(id));
            NameKey = nameKey ?? throw new ArgumentNullException(nameof(nameKey));
            Price = price;
        }

        public CosmeticKind Kind { get; }

        /// <summary>For example "indigo-dhaka"; a deck's id is its deck theme id.</summary>
        public string Id { get; }

        /// <summary>The localization key of its name, for example "ui.shop.table.indigo_dhaka".</summary>
        public string NameKey { get; }

        /// <summary>In coins; 0 for an item every player owns from the start.</summary>
        public int Price { get; }
    }

    /// <summary>
    /// The shop's side of the player's cosmetics (the owner, 4 Oct 2026; designer's spec
    /// shop-drawer-and-tables-prompts.md): every kind has a catalogue in shop order, items the player
    /// owns, and one equipped item that the player's own table shows from the next match on, offline
    /// and online alike (each player sees their own choices, like decks, ADR 031). Buying spends coins
    /// from the wallet (<see cref="Economy.IWallet"/>); it never equips by itself. The equipped deck is
    /// the same as <see cref="ICosmeticsService.SelectedDeckId"/>.
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

        /// <summary>Equips an owned item; throws for an unknown or unowned one.</summary>
        void Equip(CosmeticKind kind, string id);

        /// <summary>Buys an item with coins: on <see cref="PurchaseResult.Bought"/> it is owned, not equipped.</summary>
        PurchaseResult Buy(CosmeticKind kind, string id);
    }
}
