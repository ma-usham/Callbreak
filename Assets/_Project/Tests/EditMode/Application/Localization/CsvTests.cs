using System;
using System.Collections.Generic;
using Callbreak.Application.Localization;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>The CSV dialect the string tables are stored in (spreadsheet exports, git line endings).</summary>
    public sealed class CsvTests
    {
        [Test]
        public void Parse_PlainRows()
        {
            List<string[]> rows = Csv.Parse("key,en,ne\r\nui.a,Hello,\r\n");

            Assert.AreEqual(2, rows.Count);
            CollectionAssert.AreEqual(new[] { "key", "en", "ne" }, rows[0]);
            CollectionAssert.AreEqual(new[] { "ui.a", "Hello", "" }, rows[1]);
        }

        [Test]
        public void Parse_QuotedFields_HoldCommasQuotesAndLineBreaks()
        {
            List<string[]> rows = Csv.Parse("k,\"Bid 3, Won 1\",\"Say \"\"hi\"\"\",\"line one\r\nline two\"\n");

            Assert.AreEqual(1, rows.Count);
            CollectionAssert.AreEqual(new[] { "k", "Bid 3, Won 1", "Say \"hi\"", "line one\nline two" }, rows[0]);
        }

        [Test]
        public void Parse_LineBreaksInsideQuotes_AreAlwaysLf()
        {
            Assert.AreEqual("a\nb\nc", Csv.Parse("\"a\r\nb\rc\"")[0][0]);
        }

        [Test]
        public void Parse_SkipsTheByteOrderMark_AndAcceptsLfOrCrLf()
        {
            List<string[]> crlf = Csv.Parse("﻿key,en\r\nx,1\r\n");
            List<string[]> lf = Csv.Parse("key,en\nx,1");

            Assert.AreEqual("key", crlf[0][0]);
            Assert.AreEqual(2, crlf.Count);
            Assert.AreEqual(2, lf.Count);
            CollectionAssert.AreEqual(crlf[1], lf[1]);
        }

        [Test]
        public void Parse_QuoteAtTheStartOfTheFileAfterTheByteOrderMark_OpensAField()
        {
            Assert.AreEqual("a,b", Csv.Parse("﻿\"a,b\",c")[0][0]);
        }

        [Test]
        public void Parse_BlankLineIsOneEmptyField_AndATrailingLineBreakAddsNoRow()
        {
            List<string[]> rows = Csv.Parse("a\r\n\r\nb\r\n");

            Assert.AreEqual(3, rows.Count);
            CollectionAssert.AreEqual(new[] { "" }, rows[1]);
        }

        [Test]
        public void Parse_EmptyQuotedFieldAndTrailingComma()
        {
            CollectionAssert.AreEqual(new[] { "", "x", "" }, Csv.Parse("\"\",x,")[0]);
        }

        [Test]
        public void Parse_QuoteInsideAnUnquotedField_IsKept()
        {
            CollectionAssert.AreEqual(new[] { "5\" screen", "ok" }, Csv.Parse("5\" screen,ok")[0]);
        }

        [Test]
        public void Parse_UnclosedQuote_ThrowsNamingTheLine()
        {
            var e = Assert.Throws<FormatException>(() => Csv.Parse("key,en\r\nui.a,\"never closed\r\nui.b,x\r\n"));

            StringAssert.Contains("line 2", e.Message);
        }

        [Test]
        public void Parse_NullOrEmpty_HasNoRows()
        {
            Assert.AreEqual(0, Csv.Parse(null).Count);
            Assert.AreEqual(0, Csv.Parse(string.Empty).Count);
        }

        [TestCase("plain", "plain")]
        [TestCase("", "")]
        [TestCase(null, "")]
        [TestCase("a,b", "\"a,b\"")]
        [TestCase("say \"hi\"", "\"say \"\"hi\"\"\"")]
        [TestCase("two\nlines", "\"two\nlines\"")]
        [TestCase(" padded ", "\" padded \"")]
        [TestCase("Your turn · Beat {0}", "Your turn · Beat {0}")]
        public void Escape_QuotesOnlyWhenNeeded(string field, string expected)
        {
            Assert.AreEqual(expected, Csv.Escape(field));
        }

        [Test]
        public void Write_ThenParse_RoundTrips()
        {
            var rows = new List<IReadOnlyList<string>>
            {
                new[] { "key", "en", "ne" },
                new[] { "ui.a", "Hello, \"friend\"", "नमस्ते" },
                new[] { "ui.b", "line one\nline two", "" },
                new[] { "ui.c", " edge ", "" },
            };

            string text = Csv.Write(rows);
            List<string[]> parsed = Csv.Parse(text);

            Assert.AreEqual(rows.Count, parsed.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                CollectionAssert.AreEqual(rows[i], parsed[i]);
            }

            StringAssert.EndsWith("\r\n", text);
        }
    }
}
