using System;
using System.Linq;
using Callbreak.Application.Localization;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>Parsing and merging the UI, Rules and Emotes tables.</summary>
    public sealed class StringTablesTests
    {
        private const string Ui = "key,en,ne\r\nui.title,Play with Friends,साथीहरूसँग खेल्नुहोस्\r\nui.cta,Create Room,\r\n";
        private const string Emotes = "key,en,ne\r\nemote.namaste,Namaste!,\r\n";

        [Test]
        public void TryGet_ReturnsTheValueForExactlyThatLanguage()
        {
            StringTables tables = StringTables.FromCsv(("UI", Ui));

            Assert.IsTrue(tables.TryGet("en", "ui.title", out string english));
            Assert.AreEqual("Play with Friends", english);
            Assert.IsTrue(tables.TryGet("ne", "ui.title", out string nepali));
            Assert.AreEqual("साथीहरूसँग खेल्नुहोस्", nepali);
        }

        [Test]
        public void EmptyCell_IsNotATranslation()
        {
            StringTables tables = StringTables.FromCsv(("UI", Ui));

            Assert.IsFalse(tables.TryGet("ne", "ui.cta", out _));
            Assert.IsTrue(tables.Contains("ui.cta"));
        }

        [Test]
        public void MergesSeveralTables_AndRemembersWhereEachKeyCameFrom()
        {
            StringTables tables = StringTables.FromCsv(("UI", Ui), ("Emotes", Emotes));

            Assert.AreEqual(3, tables.Count);
            Assert.AreEqual("UI", tables.TableOf("ui.cta"));
            Assert.AreEqual("Emotes", tables.TableOf("emote.namaste"));
            Assert.IsNull(tables.TableOf("missing"));
            Assert.AreEqual(0, tables.Warnings.Count);
        }

        [Test]
        public void Languages_ListsOnlyColumnsWithAValue_EnglishFirst()
        {
            StringTables onlyEnglish = StringTables.FromCsv(("Emotes", Emotes));
            StringTables both = StringTables.FromCsv(("Emotes", "key,ne,en\r\nemote.a,नमस्ते,Namaste!\r\n"));

            CollectionAssert.AreEqual(new[] { "en" }, onlyEnglish.Languages);
            CollectionAssert.AreEqual(new[] { "en", "ne" }, both.Languages);
        }

        [Test]
        public void DuplicateKey_LaterRowWins_WithAWarning()
        {
            StringTables tables = StringTables.FromCsv(("UI", Ui), ("Rules", "key,en,ne\r\nui.title,Other,\r\n"));

            Assert.IsTrue(tables.TryGet("en", "ui.title", out string value));
            Assert.AreEqual("Other", value);
            Assert.IsFalse(tables.TryGet("ne", "ui.title", out _), "the older row's translation goes with it");
            Assert.AreEqual(1, tables.Warnings.Count);
            StringAssert.Contains("ui.title", tables.Warnings[0]);
        }

        [Test]
        public void HeaderIsCaseInsensitive_KeysAreTrimmed_WhitespaceValuesAreEmpty()
        {
            StringTables tables = StringTables.FromCsv(("UI", "Key,EN,NE\r\n  ui.a  ,Hello,   \r\n"));

            Assert.IsTrue(tables.TryGet("en", "ui.a", out string value));
            Assert.AreEqual("Hello", value);
            Assert.IsFalse(tables.TryGet("ne", "ui.a", out _));
        }

        [Test]
        public void ShortRows_AreFine_ExtraCellsAreIgnoredWithAWarning()
        {
            StringTables tables = StringTables.FromCsv(("UI", "key,en,ne\r\nui.a,Hello\r\nui.b,Hi,,surplus\r\n"));

            Assert.IsTrue(tables.TryGet("en", "ui.a", out _));
            Assert.IsTrue(tables.TryGet("en", "ui.b", out _));
            Assert.AreEqual(1, tables.Warnings.Count);
        }

        [Test]
        public void BlankLinesAreSkipped_ARowWithValuesButNoKeyIsReported()
        {
            StringTables tables = StringTables.FromCsv(("UI", "key,en\r\n\r\nui.a,Hello\r\n,orphan\r\n"));

            Assert.AreEqual(1, tables.Count);
            Assert.AreEqual(1, tables.Warnings.Count);
        }

        [Test]
        public void MultiLineValue_KeepsItsLineBreak()
        {
            StringTables tables = StringTables.FromCsv(("Rules", "key,en\r\nrules.a,\"Line one\r\nLine two\"\r\n"));

            Assert.IsTrue(tables.TryGet("en", "rules.a", out string value));
            Assert.AreEqual("Line one\nLine two", value);
        }

        [TestCase("")]
        [TestCase("en,key\r\nui.a,b\r\n")]
        [TestCase("key,en\r\nui.a,\"unclosed\r\n")]
        public void NotATable_Throws_AndChangesNothing(string csv)
        {
            StringTables tables = StringTables.FromCsv(("UI", Ui));

            Assert.Throws<FormatException>(() => tables.AddTable("Broken", csv));

            Assert.AreEqual(2, tables.Count);
            Assert.AreEqual(0, tables.Warnings.Count);
            CollectionAssert.AreEqual(new[] { "en", "ne" }, tables.Languages.ToArray());
        }
    }
}
