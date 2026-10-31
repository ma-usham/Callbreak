using System;
using System.Collections.Generic;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>
    /// cosmetics.json: what the player has equipped and has bought. Plain serializable data read and
    /// written by <see cref="CosmeticsService"/>. Version 1 held only the selected deck; version 2
    /// (the shop, 4 Oct 2026) adds the background and table and the purchases; version 3 (the themes,
    /// ADR 064) adds the equipped theme and the themes bought, and keeps the background and table
    /// fields as the equipped theme's parts.
    /// </summary>
    [Serializable]
    public sealed class CosmeticsData
    {
        public const int CurrentVersion = 3;

        public int schemaVersion = CurrentVersion;

        /// <summary>The equipped deck (its name is from version 1, when the deck was the only choice).</summary>
        public string selectedDeckId = CosmeticCatalog.Decks.Default;

        /// <summary>The equipped theme (version 3).</summary>
        public string equippedThemeId = CosmeticCatalog.Themes.Default;

        /// <summary>The equipped theme's background (the equipped background itself in version 2).</summary>
        public string equippedBackgroundId = CosmeticCatalog.Backgrounds.Default;

        /// <summary>The equipped theme's cloth (the equipped table or cloth itself in version 2).</summary>
        public string equippedTableId = CosmeticCatalog.Tables.Default;

        /// <summary>Items bought with coins, by id. What every player owns from the start isn't listed.</summary>
        public List<string> boughtDeckIds = new List<string>();

        /// <summary>Themes bought (version 3).</summary>
        public List<string> boughtThemeIds = new List<string>();

        /// <summary>Backgrounds bought on their own, until 31 Oct 2026: each one owns its theme (ADR 064).</summary>
        public List<string> boughtBackgroundIds = new List<string>();

        /// <summary>Tables and cloths bought on their own, until 31 Oct 2026: each one owns its theme (ADR 064).</summary>
        public List<string> boughtTableIds = new List<string>();
    }
}
