namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// What a shop tile says under its name (mockups 15, 20 and 21; the owner, 5 Oct 2026: of the
    /// mockups' "Equipped", "Selected" and "Owned", only "Equipped" stays).
    /// </summary>
    public enum ShopTileStatus
    {
        /// <summary>The item the player's own table shows: "Equipped" with a filled radio.</summary>
        Equipped,

        /// <summary>Owned and not equipped: nothing under the name, chosen or not (the crimson outline shows the choice).</summary>
        None,

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
    /// The shop's display rules (designer's spec shop-drawer-and-tables-prompts.md; the owner,
    /// 5 Oct 2026): an unowned item shows its price; an owned one shows "Equipped" with a filled
    /// radio when it is the one in use, and nothing otherwise. Tapping a picture only moves the
    /// crimson outline; "Equipped" moves when the player uses the chosen item. Engine-free, so it is
    /// tested outside Unity.
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

        /// <summary>The radio, shown only with "Equipped", is filled.</summary>
        public static bool RadioFilled(ShopTileStatus status) => status == ShopTileStatus.Equipped;

        /// <summary>The words of a status: "Equipped", or null for none and for a price (a coin and a number).</summary>
        public static string StatusKey(ShopTileStatus status) => status == ShopTileStatus.Equipped ? "ui.shop.status.equipped" : null;

        /// <summary>An owned item is put to use; anything else is bought.</summary>
        public static ShopAction ActionFor(bool owned) => owned ? ShopAction.Use : ShopAction.Buy;

        /// <summary>A price as the tiles and the Buy button show it: the plain number of coins.</summary>
        public static string Price(int coins) => coins.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
