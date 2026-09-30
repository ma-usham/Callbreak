using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Callbreak.Application.Config;
using Callbreak.Application.Localization;
using Callbreak.Editor.Screens;
using Callbreak.Infrastructure.Localization;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Editor
{
    /// <summary>
    /// Keeps the string tables in step with the screens (GDD §15). Every screen layout
    /// (ArtSource/Screens/&lt;Name&gt;/layout.json) carries a string-table key and the mockup's English
    /// for each fixed label; <see cref="CollectScreenStrings"/> adds the keys UI.csv doesn't have
    /// yet and reports the ones whose English differs, without changing existing rows or
    /// translations (the table may hold an approved copy change). <see cref="EnsureConfigAsset"/>
    /// creates the LocalizationConfig that points the game at the three tables.
    /// </summary>
    public static class LocalizationTools
    {
        public const string TableFolder = "Assets/_Project/Localization";
        public const string UiTablePath = TableFolder + "/UI.csv";
        public const string RulesTablePath = TableFolder + "/Rules.csv";
        public const string EmotesTablePath = TableFolder + "/Emotes.csv";
        public const string ScreensFolder = "ArtSource/Screens";
        public const string ConfigPath = "Assets/_Project/Config/Localization/LocalizationConfig.asset";

        [MenuItem("Callbreak/Localization/Collect Screen Strings")]
        private static void CollectScreenStringsMenu()
        {
            Debug.Log(CollectScreenStrings());
        }

        [MenuItem("Callbreak/Localization/Create or Update Config Asset")]
        private static void EnsureConfigAssetMenu()
        {
            Debug.Log(EnsureConfigAsset());
        }

        /// <summary>Adds missing screen keys to UI.csv and returns a report of what it found.</summary>
        public static string CollectScreenStrings()
        {
            bool exists = File.Exists(UiTablePath);
            byte[] bytes = exists ? File.ReadAllBytes(UiTablePath) : Array.Empty<byte>();
            string text = new UTF8Encoding(false).GetString(bytes);
            bool bom = !exists || (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
            string newline = !exists || text.Contains("\r\n") ? "\r\n" : "\n";

            List<string[]> rows = Csv.Parse(text);
            if (rows.Count == 0)
            {
                rows.Add(new[] { StringTables.KeyColumn, "en", "ne" });
            }

            string[] header = rows[0];
            int english = Array.FindIndex(header, h => string.Equals(h.Trim(), StringTables.DefaultLanguage, StringComparison.OrdinalIgnoreCase));
            if (english < 1)
            {
                return $"[Localization] {UiTablePath} has no '{StringTables.DefaultLanguage}' column; nothing collected.";
            }

            var rowOfKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int r = 1; r < rows.Count; r++)
            {
                string key = rows[r][0].Trim();
                if (key.Length > 0 && !rowOfKey.ContainsKey(key))
                {
                    rowOfKey[key] = r;
                }
            }

            var added = new List<string>();
            var differs = new List<string>();
            var conflicts = new List<string>();
            var problems = new List<string>();
            var firstSeen = new Dictionary<string, (string text, string screen)>(StringComparer.Ordinal);
            int layouts = 0;

            foreach (string layoutPath in LayoutFiles())
            {
                string screen = Path.GetFileName(Path.GetDirectoryName(layoutPath));
                ScreenLayout layout;
                try
                {
                    layout = JsonUtility.FromJson<ScreenLayout>(File.ReadAllText(layoutPath));
                }
                catch (ArgumentException e)
                {
                    problems.Add($"{screen}: layout.json can't be read ({e.Message})");
                    continue;
                }

                layouts++;
                foreach (LayoutNode node in layout?.nodes ?? Array.Empty<LayoutNode>())
                {
                    string key = node.key?.Trim();
                    if (string.IsNullOrEmpty(key))
                    {
                        continue;
                    }

                    string value = node.text ?? string.Empty;
                    if (value.Trim().Length == 0)
                    {
                        problems.Add($"{screen}: {key} ({node.id}) has no text");
                        continue;
                    }

                    if (firstSeen.TryGetValue(key, out (string text, string screen) first))
                    {
                        if (first.text != value)
                        {
                            conflicts.Add($"{key}: \"{first.text}\" ({first.screen}) vs \"{value}\" ({screen})");
                        }

                        continue;
                    }

                    firstSeen[key] = (value, screen);
                    if (rowOfKey.TryGetValue(key, out int row))
                    {
                        string current = rows[row].Length > english ? rows[row][english] : string.Empty;
                        if (current != value)
                        {
                            differs.Add($"{key}: table \"{current}\", {screen} layout \"{value}\"");
                        }

                        continue;
                    }

                    var newRow = new string[header.Length];
                    for (int c = 0; c < newRow.Length; c++)
                    {
                        newRow[c] = string.Empty;
                    }

                    newRow[0] = key;
                    newRow[english] = value;
                    rows.Add(newRow);
                    rowOfKey[key] = rows.Count - 1;
                    added.Add($"{key} ({screen})");
                }
            }

            if (added.Count > 0)
            {
                string csv = (bom ? "﻿" : string.Empty) + Csv.Write(rows, newline);
                File.WriteAllText(UiTablePath, csv, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(UiTablePath);
            }

            var report = new StringBuilder();
            report.Append($"[Localization] {firstSeen.Count} screen keys in {layouts} layouts; {added.Count} added to {UiTablePath}.");
            AppendList(report, "Added", added);
            AppendList(report, "English differs (table kept; update one side, and re-check translations)", differs);
            AppendList(report, "Same key, different text on two screens", conflicts);
            AppendList(report, "Problems", problems);
            return report.ToString();
        }

        /// <summary>Creates (or re-points) the LocalizationConfig asset at the three tables and reports what loads.</summary>
        public static string EnsureConfigAsset()
        {
            var config = AssetDatabase.LoadAssetAtPath<LocalizationConfig>(ConfigPath);
            bool created = config == null;
            if (created)
            {
                UiBuildKit.EnsureFolder(Path.GetDirectoryName(ConfigPath).Replace('\\', '/'));
                config = ScriptableObject.CreateInstance<LocalizationConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            var so = new SerializedObject(config);
            var missing = new List<string>();
            Assign(so, "_ui", UiTablePath, missing);
            Assign(so, "_rules", RulesTablePath, missing);
            Assign(so, "_emotes", EmotesTablePath, missing);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            StringTables tables = StringTablesFactory.Create(config);
            var report = new StringBuilder();
            report.Append($"[Localization] {(created ? "Created" : "Updated")} {ConfigPath}: {tables.Count} keys, languages [{string.Join(", ", tables.Languages)}].");
            AppendList(report, "Missing tables", missing);
            AppendList(report, "Warnings", tables.Warnings.ToList());
            return report.ToString();
        }

        private static IEnumerable<string> LayoutFiles()
        {
            if (!Directory.Exists(ScreensFolder))
            {
                return Array.Empty<string>();
            }

            return Directory.GetDirectories(ScreensFolder)
                .OrderBy(d => d, StringComparer.Ordinal)
                .Select(d => Path.Combine(d, "layout.json"))
                .Where(File.Exists);
        }

        private static void Assign(SerializedObject so, string field, string path, List<string> missing)
        {
            if (File.Exists(path))
            {
                AssetDatabase.ImportAsset(path);
            }

            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null)
            {
                missing.Add(path);
            }

            so.FindProperty(field).objectReferenceValue = asset;
        }

        private static void AppendList(StringBuilder report, string title, List<string> items)
        {
            if (items.Count == 0)
            {
                return;
            }

            report.Append($"\n{title} ({items.Count}):");
            foreach (string item in items)
            {
                report.Append("\n  ").Append(item);
            }
        }
    }
}
