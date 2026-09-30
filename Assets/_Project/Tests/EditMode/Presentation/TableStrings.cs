using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Callbreak.Application.Localization;
using Callbreak.Application.Ports;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The shipped string tables (Assets/_Project/Localization) in English, so the table's copy is
    /// tested against the text the game will actually show.
    /// </summary>
    public sealed class TableStrings : ILocalization
    {
        private readonly StringTables _tables = new StringTables();

        public TableStrings()
        {
            string folder = FindLocalizationFolder();
            foreach (string table in new[] { "UI", "Rules", "Emotes" })
            {
                _tables.AddTable(table, File.ReadAllText(Path.Combine(folder, table + ".csv")));
            }
        }

        public event Action LanguageChanged
        {
            add { }
            remove { }
        }

        public string Language => StringTables.DefaultLanguage;

        public IReadOnlyList<string> Languages => _tables.Languages;

        public bool Has(string key) => _tables.TryGet(StringTables.DefaultLanguage, key, out _);

        public string Get(string key)
        {
            return _tables.TryGet(StringTables.DefaultLanguage, key, out string value) ? value : key;
        }

        public string Format(string key, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(key), args);
        }

        public void SetLanguage(string language)
        {
        }

        /// <summary>The Editor runs tests from the project root; the dotnet harness from its bin folder, so walk up.</summary>
        private static string FindLocalizationFolder()
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppDomain.CurrentDomain.BaseDirectory })
            {
                for (DirectoryInfo dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                {
                    string candidate = Path.Combine(dir.FullName, "Assets", "_Project", "Localization");
                    if (Directory.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            throw new DirectoryNotFoundException("Assets/_Project/Localization not found above " + Directory.GetCurrentDirectory());
        }
    }
}
