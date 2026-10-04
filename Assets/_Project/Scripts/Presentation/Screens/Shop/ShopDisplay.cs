namespace Callbreak.Presentation.Screens
{
    /// <summary>What a shop tile says under its name (mockups 15, 20 and 21).</summary>
    public enum ShopTileStatus
    {
        /// <summary>The item the player's own table shows: "Equipped" with a filled radio.</summary>
        Equipped,

        /// <summary>Owned and chosen on this screen, not equipped yet: "Selected" with a filled radio (mockup 20).</summary>
        Selected,

        /// <summary>Owned, neither equipped nor chosen: "Owned" with an empty radio.</summary>
        Owned,

        /// <summary>Not owned: the coin and the item's price (mockup 21).</summary>
        Price,
    }

    /// <summary>What the primary button under the tiles does for the chosen item.</summary>
    public enum ShopAction
    {
        /// <summary>"Use Selected Style / Background / Table": equips an owned item.</summary>
        Use,

        /// <summary>"Buy for (coin) N": buys the item, after a confirmation.</summary>
        Buy,
    }

    /// <summary>
    /// The shop's display rules (designer's spec shop-drawer-and-tables-prompts.md): an unowned
    /// item shows its price; an owned one is Equipped, Selected or Owned, and only the first two
    /// fill their radio. The chosen tile also carries the crimson outline, whatever its status.
    /// Engine-free, so it is tested outside Unity.
    /// </summary>
    public static class ShopDisplay
    {
        public static ShopTileStatus Status(bool owned, bool equipped, bool selected)
        {
            if (!owned)
            {
                return ShopTileStatus.Price;
            }

            if (equipped)
            {
                return ShopTileStatus.Equipped;
            }

            return selected ? ShopTileStatus.Selected : ShopTileStatus.Owned;
        }

        /// <summary>The radio is filled for the equipped item and for an owned item that is chosen.</summary>
        public static bool RadioFilled(ShopTileStatus status) => status == ShopTileStatus.Equipped || status == ShopTileStatus.Selected;

        /// <summary>The words of a status, or null for a price (it shows the coin and a number instead).</summary>
        public static string StatusKey(ShopTileStatus status)
        {
            switch (status)
            {
                case ShopTileStatus.Equipped:
                    return "ui.shop.status.equipped";
                case ShopTileStatus.Selected:
                    return "ui.shop.status.selected";
                case ShopTileStatus.Owned:
                    return "ui.shop.status.owned";
                default:
                    return null;
            }
        }

        /// <summary>An owned item is put to use; anything else is bought.</summary>
        public static ShopAction ActionFor(bool owned) => owned ? ShopAction.Use : ShopAction.Buy;

        /// <summary>A price as the tiles and the Buy button show it: the plain number of coins.</summary>
        public static string Price(int coins) => coins.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
