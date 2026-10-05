using System;
using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The shop page's own state (designer's spec shop-drawer-and-tables-prompts.md): which
    /// category is showing and, for each category, the item the player has chosen. Choosing only
    /// previews an item; buying and equipping go through <see cref="ICosmeticsShop"/>. Every
    /// category keeps its choice while the player switches between them. A shop that opens afresh
    /// shows Cards, with each category's choice on its equipped item.
    /// Engine-free, so it is tested outside Unity.
    /// </summary>
    public sealed class ShopState
    {
        private readonly ICosmeticsShop _shop;
        private readonly IWallet _wallet;
        private readonly Dictionary<CosmeticKind, string> _chosen = new Dictionary<CosmeticKind, string>();

        public ShopState(ICosmeticsShop shop, IWallet wallet)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Reset();
        }

        /// <summary>The kinds in drawer order: Cards, Backgrounds, Tables.</summary>
        public static IReadOnlyList<CosmeticKind> Kinds { get; } = new[] { CosmeticKind.Deck, CosmeticKind.Background, CosmeticKind.Table };

        public CosmeticKind Category { get; private set; }

        /// <summary>Opening the shop: Cards, and every category's choice back on its equipped item.</summary>
        public void Reset()
        {
            Category = CosmeticKind.Deck;
            foreach (CosmeticKind kind in Kinds)
            {
                _chosen[kind] = _shop.Equipped(kind);
            }
        }

        /// <summary>Shows another category; its choice is kept from before.</summary>
        public void Show(CosmeticKind kind)
        {
            Category = kind;
        }

        /// <summary>The player taps an item; false when there is no such item.</summary>
        public bool Choose(CosmeticKind kind, int index)
        {
            IReadOnlyList<CosmeticItem> items = _shop.Catalog(kind);
            if (index < 0 || index >= items.Count)
            {
                return false;
            }

            _chosen[kind] = items[index].Id;
            return true;
        }

        /// <summary>
        /// The chosen item's place in its catalogue: the chosen one, or the equipped one if the
        /// chosen one has gone, or the first; -1 for an empty catalogue.
        /// </summary>
        public int ChosenIndex(CosmeticKind kind)
        {
            IReadOnlyList<CosmeticItem> items = _shop.Catalog(kind);
            int index = IndexOf(items, _chosen.TryGetValue(kind, out string id) ? id : null);
            if (index < 0)
            {
                index = IndexOf(items, _shop.Equipped(kind));
            }

            return index >= 0 ? index : items.Count > 0 ? 0 : -1;
        }

        /// <summary>The chosen item, or null for an empty catalogue.</summary>
        public CosmeticItem Chosen(CosmeticKind kind)
        {
            int index = ChosenIndex(kind);
            return index >= 0 ? _shop.Catalog(kind)[index] : null;
        }

        public ShopTileStatus StatusOf(CosmeticKind kind, int index)
        {
            CosmeticItem item = _shop.Catalog(kind)[index];
            return ShopDisplay.Status(_shop.IsOwned(kind, item.Id), _shop.Equipped(kind) == item.Id);
        }

        /// <summary>What the primary button does for the chosen item of a category.</summary>
        public ShopAction ActionFor(CosmeticKind kind)
        {
            CosmeticItem item = Chosen(kind);
            return ShopDisplay.ActionFor(item == null || _shop.IsOwned(kind, item.Id));
        }

        /// <summary>Use is possible for a chosen item that is owned and not equipped yet.</summary>
        public bool CanUse(CosmeticKind kind)
        {
            CosmeticItem item = Chosen(kind);
            return item != null && _shop.IsOwned(kind, item.Id) && _shop.Equipped(kind) != item.Id;
        }

        /// <summary>The wallet holds enough coins for the chosen item.</summary>
        public bool CanAfford(CosmeticKind kind)
        {
            CosmeticItem item = Chosen(kind);
            return item != null && _wallet.Coins >= item.Price;
        }

        private static int IndexOf(IReadOnlyList<CosmeticItem> items, string id)
        {
            if (id == null)
            {
                return -1;
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
