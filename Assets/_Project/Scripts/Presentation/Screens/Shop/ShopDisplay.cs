using System.Globalization;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// What a shop tile says under its name (mockups 15, 20 and 21; the owner, 5 Oct 2026: of the
    /// mockups' "Equipped", "Selected" and "Owned", only "Equipped" stays on the decks; a theme's tile
    /// says "Owned" too, as Figma's "V1 Rebuild · Shop" draws it, ADR 064).
    /// </summary>
    public enum ShopTileStatus
    {
        /// <summary>The item the player's own table shows: "Equipped" with a filled radio.</summary>
        Equipped,

        /// <summary>A deck owned and not equipped: nothing under the name, chosen or not (the crimson outline shows the choice).</summary>
        None,

        /// <summary>Not owned: the coin and the item's price (mockup 21).</summary>
        Price,

        /// <summary>A theme owned and not equipped: "Owned" with an empty radio.</summary>
        Owned,
    }

    /// <summary>What the primary button under the decks does for the chosen deck.</summary>
    public enum ShopAction
    {
        /// <summary>"Use Selected Style": equips an owned deck.</summary>
        Use,

        /// <summary>"Buy for (coin) N": buys the deck, after a confirmation.</summary>
        Buy,
    }

    /// <summary>What the theme preview's button does for its theme (ADR 064).</summary>
    public enum ThemeAction
    {
        /// <summary>"Buy for (coin) N": buys the theme, after a confirmation.</summary>
        Buy,

        /// <summary>"Equip theme": equips an owned theme, its background and its cloth.</summary>
        Equip,

        /// <summary>"Equipped", off: the theme the player has on.</summary>
        Equipped,
    }

    /// <summary>
    /// The shop's display rules (designer's spec shop-drawer-and-tables-prompts.md; the owner,
    /// 5 Oct 2026; ADR 064): an unowned item shows its price; an owned deck shows "Equipped" with a
    /// filled radio when it is the one in use, and nothing otherwise; an owned theme shows "Equipped"
    /// or "Owned". Tapping a deck only moves the crimson outline; "Equipped" moves when the player uses
    /// the chosen deck or equips a theme in its preview. Engine-free, so it is tested outside Unity.
    /// </summary>
    public static class ShopDisplay
    {
        public static ShopTileStatus Status(bool owned, bool equipped)
        {
            if (!owned)
            {
                return ShopTileStatus.Price;
            }

            return equipped ? ShopTileStatus.Equipped : ShopTileStatus.None;
        }

        /// <summary>A theme's tile: Equipped, Owned, or its price.</summary>
        public static ShopTileStatus ThemeStatus(bool owned, bool equipped)
        {
            if (!owned)
            {
                return ShopTileStatus.Price;
            }

            return equipped ? ShopTileStatus.Equipped : ShopTileStatus.Owned;
        }

        /// <summary>The radio, shown with "Equipped" and "Owned", is filled for "Equipped".</summary>
        public static bool RadioFilled(ShopTileStatus status) => status == ShopTileStatus.Equipped;

        /// <summary>The words of a status: "Equipped" or "Owned", or null for none and for a price (a coin and a number).</summary>
        public static string StatusKey(ShopTileStatus status)
        {
            switch (status)
            {
                case ShopTileStatus.Equipped:
                    return "ui.shop.status.equipped";
                case ShopTileStatus.Owned:
                    return "ui.shop.status.owned";
                default:
                    return null;
            }
        }

        /// <summary>An owned deck is put to use; anything else is bought.</summary>
        public static ShopAction ActionFor(bool owned) => owned ? ShopAction.Use : ShopAction.Buy;

        /// <summary>A theme not owned is bought; an owned one is equipped, unless it is on already.</summary>
        public static ThemeAction ThemeActionFor(bool owned, bool equipped)
        {
            if (!owned)
            {
                return ThemeAction.Buy;
            }

            return equipped ? ThemeAction.Equipped : ThemeAction.Equip;
        }

        /// <summary>A price as the tiles and the Buy buttons show it: the number of coins, grouped as the wallet groups them ("1,000").</summary>
        public static string Price(int coins) => coins.ToString("N0", CultureInfo.InvariantCulture);
    }
}
