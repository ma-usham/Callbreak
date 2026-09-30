using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Callbreak.Application.Localization
{
    /// <summary>
    /// The game's strings (GDD §15): several CSV tables (UI, Rules, Emotes) merged into one lookup
    /// by key and language. Each table starts with a header row <c>key,en,ne</c>; every other
    /// column is a language code. Empty cells mean "not translated". Problems that don't stop the
    /// table loading (a duplicate key, a row with extra cells) are collected in <see cref="Warnings"/>.
    /// </summary>
    public sealed class StringTables
    {
        public const string KeyColumn = "key";

        /// <summary>The language every key must have, and the fallback for the others.</summary>
        public const string DefaultLanguage = "en";

        private readonly Dictionary<string, Dictionary<string, string>> _values = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _tableOfKey = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<string> _columnOrder = new List<string>();
        private readonly List<string> _languages = new List<string>();
        private readonly List<string> _warnings = new List<string>();

        public StringTables()
        {
            Languages = new ReadOnlyCollection<string>(_languages);
            Warnings = new ReadOnlyCollection<string>(_warnings);
        }

        /// <summary>
        /// Languages with at least one non-empty value, English first when present, then in the
        /// order their columns first appeared.
        /// </summary>
        public IReadOnlyList<string> Languages { get; }

        public IReadOnlyList<string> Warnings { get; }

        public IEnumerable<string> Keys => _tableOfKey.Keys;

        public int Count => _tableOfKey.Count;

        /// <summary>Builds tables from (name, CSV text) pairs, in order; a later duplicate key wins.</summary>
        public static StringTables FromCsv(params (string name, string csv)[] tables)
        {
            var result = new StringTables();
            foreach ((string name, string csv) in tables)
            {
                result.AddTable(name, csv);
            }

            return result;
        }

        /// <summary>
        /// Parses one table and merges it in. Throws <see cref="FormatException"/>, leaving these
        /// tables unchanged, when the text isn't a table: no header, a header that doesn't start
        /// with <c>key</c>, or an unclosed quote.
        /// </summary>
        public void AddTable(string tableName, string csvText)
        {
            string name = string.IsNullOrEmpty(tableName) ? "table" : tableName;
            List<string[]> rows = Csv.Parse(csvText);
            if (rows.Count == 0)
            {
                throw new FormatException($"{name}: the table is empty (it needs a header row: key,en,ne).");
            }

            string[] header = rows[0];
            if (!string.Equals(header[0].Trim(), KeyColumn, StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException($"{name}: the first column of the header must be '{KeyColumn}', not '{header[0]}'.");
            }

            var columns = new string[header.Length];
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int c = 1; c < header.Length; c++)
            {
                string language = header[c].Trim().ToLowerInvariant();
                if (language.Length == 0)
                {
                    continue;
                }

                if (!seen.Add(language))
                {
                    _warnings.Add($"{name}: language column '{language}' appears twice; the second is ignored.");
                    continue;
                }

                columns[c] = language;
                if (!_columnOrder.Contains(language))
                {
                    _columnOrder.Add(language);
                }
            }

            for (int r = 1; r < rows.Count; r++)
            {
                string[] row = rows[r];
                string key = row[0].Trim();
                if (key.Length == 0)
                {
                    if (row.Length > 1 && Array.Exists(row, cell => cell.Trim().Length > 0))
                    {
                        _warnings.Add($"{name}: row {r + 1} has values but no key; skipped.");
                    }

                    continue;
                }

                if (row.Length > header.Length)
                {
                    _warnings.Add($"{name}: '{key}' has more cells than the header; the extra cells are ignored.");
                }

                if (_tableOfKey.TryGetValue(key, out string firstTable))
                {
                    _warnings.Add($"{name}: duplicate key '{key}' (already in {firstTable}); this row wins.");
                    foreach (Dictionary<string, string> perLanguage in _values.Values)
                    {
                        perLanguage.Remove(key);
                    }
                }

                _tableOfKey[key] = name;
                for (int c = 1; c < header.Length && c < row.Length; c++)
                {
                    string language = columns[c];
                    if (language == null || row[c].Trim().Length == 0)
                    {
                        continue;
                    }

                    if (!_values.TryGetValue(language, out Dictionary<string, string> perLanguage))
                    {
                        perLanguage = new Dictionary<string, string>(StringComparer.Ordinal);
                        _values[language] = perLanguage;
                    }

                    perLanguage[key] = row[c];
                }
            }

            RefreshLanguages();
        }

        public bool Contains(string key) => key != null && _tableOfKey.ContainsKey(key);

        /// <summary>The non-empty value of <paramref name="key"/> in exactly <paramref name="language"/>; no fallback.</summary>
        public bool TryGet(string language, string key, out string value)
        {
            value = null;
            return language != null && key != null
                && _values.TryGetValue(language, out Dictionary<string, string> perLanguage)
                && perLanguage.TryGetValue(key, out value);
        }

        /// <summary>Which table a key came from ("UI", "Rules", ...), or null.</summary>
        public string TableOf(string key) => key != null && _tableOfKey.TryGetValue(key, out string table) ? table : null;

        private void RefreshLanguages()
        {
            _languages.Clear();
            if (HasValues(DefaultLanguage))
            {
                _languages.Add(DefaultLanguage);
            }

            foreach (string language in _columnOrder)
            {
                if (language != DefaultLanguage && HasValues(language))
                {
                    _languages.Add(language);
                }
            }
        }

        private bool HasValues(string language) => _values.TryGetValue(language, out Dictionary<string, string> perLanguage) && perLanguage.Count > 0;
    }
}
