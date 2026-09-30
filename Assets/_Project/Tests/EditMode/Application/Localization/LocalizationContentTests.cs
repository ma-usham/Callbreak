using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Callbreak.Application.Localization;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// The shipped tables (Assets/_Project/Localization): they parse, keys are unique and live in
    /// the right table, every key has English, placeholders are well formed and translations keep
    /// the same placeholders, and the strings code depends on exist.
    /// </summary>
    public sealed class LocalizationContentTests
    {
        private static readonly string[] TableNames = { "UI", "Rules", "Emotes" };

        [Test]
        public void EveryTable_HasTheHeader_KeyEnNe()
        {
            foreach (string table in TableNames)
            {
                string[] header = Csv.Parse(Read(table))[0];

                CollectionAssert.AreEqual(new[] { "key", "en", "ne" }, header, table);
            }
        }

        [Test]
        public void TablesMerge_WithoutDuplicateKeysOrMalformedRows()
        {
            StringTables tables = LoadAll();

            CollectionAssert.IsEmpty(tables.Warnings);
            Assert.Greater(tables.Count, 0);
        }

        [Test]
        public void KeysLiveInTheirTable()
        {
            StringTables tables = LoadAll();
            var prefix = new Dictionary<string, string> { { "UI", "ui." }, { "Rules", "rules." }, { "Emotes", "emote." } };

            foreach (string key in tables.Keys)
            {
                string table = tables.TableOf(key);
                StringAssert.StartsWith(prefix[table], key, $"{key} is in {table}");
                Assert.IsTrue(Regex.IsMatch(key, "^[a-z0-9_]+(\\.[a-z0-9_]+)+$"), $"key '{key}': lower case, dotted");
            }
        }

        [Test]
        public void EveryKey_HasEnglish()
        {
            StringTables tables = LoadAll();

            foreach (string key in tables.Keys)
            {
                Assert.IsTrue(tables.TryGet("en", key, out _), $"{key} has no English");
            }
        }

        [Test]
        public void Placeholders_FormatAndTranslationsKeepThem()
        {
            StringTables tables = LoadAll();
            object[] args = Enumerable.Range(0, 10).Select(i => (object)("arg" + i)).ToArray();

            foreach (string key in tables.Keys)
            {
                tables.TryGet("en", key, out string english);
                Assert.DoesNotThrow(() => string.Format(CultureInfo.InvariantCulture, english, args), $"{key}: '{english}'");
                foreach (string language in tables.Languages.Where(l => l != "en"))
                {
                    if (tables.TryGet(language, key, out string translated))
                    {
                        CollectionAssert.AreEquivalent(Placeholders(english), Placeholders(translated), $"{key} ({language})");
                    }
                }
            }
        }

        [Test]
        public void TheSixEmotes_InTheirDisplayOrder()
        {
            string[] keys = Csv.Parse(Read("Emotes")).Skip(1).Select(r => r[0]).Where(k => k.Length > 0).ToArray();

            CollectionAssert.AreEqual(
                new[] { "emote.namaste", "emote.well_played", "emote.nice_call", "emote.oops", "emote.thinking", "emote.good_game" },
                keys);
        }

        [TestCase("ui.toast.redeal")]
        [TestCase("ui.toast.welcome_back")]
        [TestCase("ui.toast.connect_failed")]
        [TestCase("ui.toast.no_internet")]
        [TestCase("ui.error.room_not_found")]
        [TestCase("ui.error.room_full")]
        [TestCase("ui.error.match_started")]
        [TestCase("ui.error.update_required")]
        [TestCase("ui.prompt.lead_any")]
        [TestCase("ui.prompt.beat")]
        [TestCase("ui.prompt.follow")]
        [TestCase("ui.prompt.trump")]
        [TestCase("ui.prompt.any_card")]
        [TestCase("ui.prompt.waiting_for")]
        [TestCase("ui.prompt.wins_trick")]
        [TestCase("ui.seat.waiting")]
        [TestCase("ui.seat.calling")]
        [TestCase("ui.seat.bid")]
        [TestCase("ui.seat.bid_won")]
        [TestCase("ui.seat.away")]
        [TestCase("ui.seat.reconnecting")]
        [TestCase("ui.seat.bot_playing")]
        [TestCase("ui.dialog.leave_match.title")]
        [TestCase("ui.dialog.leave_match.body")]
        [TestCase("ui.headline.win")]
        [TestCase("ui.headline.shared")]
        [TestCase("ui.headline.second")]
        [TestCase("ui.headline.third")]
        [TestCase("ui.headline.fourth")]
        public void StringsTheGameUses_Exist(string key)
        {
            Assert.IsTrue(LoadAll().TryGet("en", key, out _), key);
        }

        private static StringTables LoadAll()
        {
            var tables = new StringTables();
            foreach (string table in TableNames)
            {
                tables.AddTable(table, Read(table));
            }

            return tables;
        }

        private static string[] Placeholders(string text)
        {
            // Fully qualified: inside Callbreak.Tests.EditMode.*, "Match" is the rules engine's test namespace.
            return Regex.Matches(text, "\\{\\d+[^}]*\\}").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Value).Distinct().ToArray();
        }

        private static string Read(string table)
        {
            return File.ReadAllText(Path.Combine(FindLocalizationFolder(), table + ".csv"));
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
