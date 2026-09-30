using System;
using Callbreak.Application.Config;
using Callbreak.Application.Localization;
using UnityEngine;

namespace Callbreak.Infrastructure.Localization
{
    /// <summary>
    /// Builds the game's <see cref="StringTables"/> from the CSV text assets in a
    /// <see cref="LocalizationConfig"/> (UI, Rules, Emotes). A missing or broken table is logged and
    /// skipped, so the rest still load; keys without a string show as themselves.
    /// </summary>
    public static class StringTablesFactory
    {
        public static StringTables Create(LocalizationConfig config)
        {
            var tables = new StringTables();
            if (config == null)
            {
                Debug.LogError("[Localization] No LocalizationConfig: every text shows its key.");
                return tables;
            }

            Add(tables, "UI", config.Ui);
            Add(tables, "Rules", config.Rules);
            Add(tables, "Emotes", config.Emotes);
            foreach (string warning in tables.Warnings)
            {
                Debug.LogWarning($"[Localization] {warning}");
            }

            return tables;
        }

        private static void Add(StringTables tables, string name, TextAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError($"[Localization] The {name} table is not assigned in the LocalizationConfig.");
                return;
            }

            try
            {
                tables.AddTable(name, asset.text);
            }
            catch (FormatException e)
            {
                Debug.LogError($"[Localization] {e.Message}");
            }
        }
    }
}
