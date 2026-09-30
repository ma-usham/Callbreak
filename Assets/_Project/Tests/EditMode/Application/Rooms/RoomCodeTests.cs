using System;
using System.Linq;
using Callbreak.Application.Rooms;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §11 screen 03 and Appendix A: normalising pasted codes, the alphabet without look-alikes, validity.</summary>
    public sealed class RoomCodeTests
    {
        [Test]
        public void Alphabet_Has32SymbolsWithoutTheLookAlikes()
        {
            Assert.AreEqual(32, RoomCode.Alphabet.Length);
            Assert.AreEqual(32, RoomCode.Alphabet.Distinct().Count());
            foreach (char lookAlike in "IO01")
            {
                Assert.AreEqual(-1, RoomCode.Alphabet.IndexOf(lookAlike), $"'{lookAlike}' must not be a room-code symbol");
            }
        }

        [Test]
        public void Normalize_PastedCode_StripsSpacesAndDashesAndUpperCases()
        {
            Assert.AreEqual("AB23CD", RoomCode.Normalize("ab-23 cd"));
            Assert.AreEqual("AB23CD", RoomCode.Normalize("  AB 23-CD\n"));
        }

        [Test]
        public void Normalize_TheGddSample_DropsTheOne_BecauseOneIsNotInTheAlphabet()
        {
            // GDD §11 screen 03 says pasting "ab-12 cd" fills AB12CD, but 1 is one of the excluded
            // look-alikes (the mockup's AB12CD is illustrative only), so it is dropped.
            string code = RoomCode.Normalize("ab-12 cd");

            Assert.AreEqual("AB2CD", code);
            Assert.IsFalse(RoomCode.IsValid(code));
        }

        [TestCase("abcdefgh", "ABCDEF")]
        [TestCase("I0O1ab", "AB")]
        [TestCase("çab", "AB")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void Normalize_KeepsOnlyAlphabetSymbols_UpToSix(string raw, string expected)
        {
            Assert.AreEqual(expected, RoomCode.Normalize(raw));
        }

        [TestCase("AB23CD")]
        [TestCase("ZZ9999")]
        [TestCase("HJKMNP")]
        public void IsValid_SixAlphabetSymbols(string code)
        {
            Assert.IsTrue(RoomCode.IsValid(code));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("AB23C")]
        [TestCase("AB23CDE")]
        [TestCase("AB23CI")]
        [TestCase("AB23CO")]
        [TestCase("AB23C0")]
        [TestCase("AB23C1")]
        [TestCase("ab23cd")]
        [TestCase("AB-3CD")]
        public void IsValid_RejectsEverythingElse(string code)
        {
            Assert.IsFalse(RoomCode.IsValid(code));
        }

        [Test]
        public void Generate_UsesTheRandomSource()
        {
            Assert.AreEqual("AAAAAA", RoomCode.Generate(max => 0));
            Assert.AreEqual("999999", RoomCode.Generate(max => max - 1));
        }

        [Test]
        public void Generate_AlwaysGivesAValidCode()
        {
            var random = new Random(1234);
            for (int i = 0; i < 500; i++)
            {
                string code = RoomCode.Generate(random.Next);
                Assert.IsTrue(RoomCode.IsValid(code), code);
            }
        }
    }
}
