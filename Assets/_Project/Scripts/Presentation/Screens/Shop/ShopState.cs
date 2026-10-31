using System;
using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The shop page's own state (designer's spec shop-drawer-and-tables-prompts.md; the themes,
    /// ADR 064): which tab shows, Themes or Cards; on Cards, the deck the player has chosen (choosing
    /// only previews a deck; the primary button uses or buys it); on Themes, the theme open in the
    /// preview, if any. Buying and equipping go through <see cref="ICosmeticsShop"/>. Cards keeps its
    /// choice while the player switches tabs; showing another tab closes the preview. A shop that
    /// opens afresh shows Themes, with no preview open and the chosen deck back on the equipped one.
    /// Engine-free, so it is tested outside Unity.
    /// </summary>
    public sealed class ShopState
    {
        /// <summary>No theme is open in the preview.</summary>
        public const int NoPreview = -1;

        private readonly ICosmeticsShop _shop;
        private readonly IWallet _wallet;
        private string _chosenDeck;

        public ShopState(ICosmeticsShop shop, IWallet wallet)
        {
            _shop = shop ?? throw new ArgumentNullException(nameof(shop));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            Reset();
        }

        /// <summary>The tabs in drawer order: Themes, Cards (No Ads, under them, is the store's purchase, not a tab).</summary>
        public static IReadOnlyList<CosmeticKind> Kinds { get; } = new[] { CosmeticKind.Theme, CosmeticKind.Deck };

        public CosmeticKind Category { get; private set; }

        /// <summary>The theme open in the preview, by its place in the catalogue; <see cref="NoPreview"/> for none.</summary>
        public int Previewing { get; private set; }

        /// <summary>Opening the shop: Themes, no preview, and the chosen deck back on the equipped one.</summary>
        public void Reset()
        {
            Category = CosmeticKind.Theme;
            Previewing = NoPreview;
            _chosenDeck = _shop.Equipped(CosmeticKind.Deck);
        }

        /// <summary>Shows a tab; Cards keeps its choice from before, and the preview closes.</summary>
        public void Show(CosmeticKind kind)
        {
            if (kind != CosmeticKind.Theme && kind != CosmeticKind.Deck)
            {
                return;
            }

            Category = kind;
            Previewing = NoPreview;
        }

        // ------------------------------------------------------------------ Cards

        /// <summary>The player taps a deck; false when there is no such deck.</summary>
        public bool Choose(CosmeticKind kind, int index)
        {
            IReadOnlyList<CosmeticItem> decks = _shop.Catalog(CosmeticKind.Deck);
            if (kind != CosmeticKind.Deck || index < 0 || index >= decks.Count)
            {
                return false;
            }

            _chosenDeck = decks[index].Id;
            return true;
        }

        /// <summary>
        /// The chosen deck's place in its catalogue: the chosen one, or the equipped one if the chosen
        /// one has gone, or the first; -1 for an empty catalogue.
        /// </summary>
        public int ChosenIndex(CosmeticKind kind)
        {
            IReadOnlyList<CosmeticItem> decks = _shop.Catalog(CosmeticKind.Deck);
            int index = IndexOf(decks, _chosenDeck);
            if (index < 0)
            {
                index = IndexOf(decks, _shop.Equipped(CosmeticKind.Deck));
            }

            return index >= 0 ? index : decks.Count > 0 ? 0 : -1;
        }

        /// <summary>The chosen deck, or null for an empty catalogue.</summary>
        public CosmeticItem Chosen(CosmeticKind kind)
        {
            int index = ChosenIndex(kind);
            return index >= 0 ? _shop.Catalog(CosmeticKind.Deck)[index] : null;
        }

        /// <summary>What a tile says: a deck says Equipped or nothing (the owner, 5 Oct 2026), a theme Equipped or Owned.</summary>
        public ShopTileStatus StatusOf(CosmeticKind kind, int index)
        {
            CosmeticItem item = _shop.Catalog(kind)[index];
            bool owned = _shop.IsOwned(kind, item.Id);
            bool equipped = _shop.Equipped(kind) == item.Id;
            return kind == CosmeticKind.Theme ? ShopDisplay.ThemeStatus(owned, equipped) : ShopDisplay.Status(owned, equipped);
        }

        /// <summary>What the primary button under the decks does for the chosen deck.</summary>
        public ShopAction ActionFor(CosmeticKind kind)
        {
            CosmeticItem item = Chosen(kind);
            return ShopDisplay.ActionFor(item == null || _shop.IsOwned(CosmeticKind.Deck, item.Id));
        }

        /// <summary>Use is possible for a chosen deck that is owned and not equipped yet.</summary>
        public bool CanUse(CosmeticKind kind)
        {
            CosmeticItem item = Chosen(kind);
            return item != null && _shop.IsOwned(CosmeticKind.Deck, item.Id) && _shop.Equipped(CosmeticKind.Deck) != item.Id;
        }

        /// <summary>The wallet holds enough coins for the chosen deck.</summary>
        public bool CanAfford(CosmeticKind kind)
        {
            CosmeticItem item = Chosen(kind);
            return item != null && _wallet.Coins >= item.Price;
        }

        // ------------------------------------------------------------------ Themes

        /// <summary>The wallet holds enough coins for the theme at that place (its price shows red otherwise).</summary>
        public bool CanAffordTheme(int index)
        {
            IReadOnlyList<CosmeticItem> themes = _shop.Catalog(CosmeticKind.Theme);
            return index >= 0 && index < themes.Count && _wallet.Coins >= themes[index].Price;
        }

        /// <summary>The player taps a theme: it opens in the preview. False when there is no such theme.</summary>
        public bool OpenPreview(int index)
        {
            if (index < 0 || index >= _shop.Catalog(CosmeticKind.Theme).Count)
            {
                return false;
            }

            Category = CosmeticKind.Theme;
            Previewing = index;
            return true;
        }

        public void ClosePreview() => Previewing = NoPreview;

        /// <summary>The preview's arrows: the next or the previous theme, round from the last to the first.</summary>
        public void Step(int delta)
        {
            int count = _shop.Catalog(CosmeticKind.Theme).Count;
            if (Previewing == NoPreview || count == 0)
            {
                return;
            }

            Previewing = ((Previewing + delta) % count + count) % count;
        }

        /// <summary>The theme open in the preview, or null.</summary>
        public CosmeticItem PreviewTheme
        {
            get
            {
                IReadOnlyList<CosmeticItem> themes = _shop.Catalog(CosmeticKind.Theme);
                return Previewing >= 0 && Previewing < themes.Count ? themes[Previewing] : null;
            }
        }

        /// <summary>The preview's button for its theme: Buy, Equip theme, or Equipped (off).</summary>
        public ThemeAction PreviewAction
        {
            get
            {
                CosmeticItem theme = PreviewTheme;
                return theme == null
                    ? ThemeAction.Equipped
                    : ShopDisplay.ThemeActionFor(_shop.IsOwned(CosmeticKind.Theme, theme.Id), _shop.Equipped(CosmeticKind.Theme) == theme.Id);
            }
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
