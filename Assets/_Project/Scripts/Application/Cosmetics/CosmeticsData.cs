using System;
using System.Collections.Generic;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>
    /// cosmetics.json: what the player has equipped and has bought. Plain serializable data read and
    /// written by <see cref="CosmeticsService"/>. Version 1 held only the selected deck; version 2
    /// (the shop, 4 Oct 2026) adds the background and table and the purchases.
    /// </summary>
    [Serializable]
    public sealed class CosmeticsData
    {
        public const int CurrentVersion = 2;

        public int schemaVersion = CurrentVersion;

        /// <summary>The equipped deck (its name is from version 1, when the deck was the only choice).</summary>
        public string selectedDeckId = CosmeticCatalog.Decks.Default;

        public string equippedBackgroundId = CosmeticCatalog.Backgrounds.Default;

        public string equippedTableId = CosmeticCatalog.Tables.Default;

        /// <summary>Items bought with coins, by id. What every player owns from the start isn't listed.</summary>
        public List<string> boughtDeckIds = new List<string>();

        public List<string> boughtBackgroundIds = new List<string>();

        public List<string> boughtTableIds = new List<string>();
    }
}
