using System;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §11 screen 02: the room name is 3-20 characters, trimmed and filtered; the default is "{Name}'s Table".</summary>
    public sealed class RoomNamesTests
    {
        private const string Format = "{0}'s Table";

        [TestCase("Hari", "Hari's Table")]
        [TestCase("Player", "Player's Table")]
        [TestCase("Asha Rai", "Asha Rai's Table")]
        [TestCase("Bartholomeww", "Bartholomeww's Table")]
        public void DefaultFor_TheWholeNameFits_IsUsedWhole(string player, string expected)
        {
            Assert.AreEqual(expected, RoomNames.DefaultFor(player, Format, 20));
        }

        [TestCase("Bartholomew Smith", "Bartholomew's Table")]
        [TestCase("Ram Bahadur Thapa", "Ram's Table")]
        public void DefaultFor_TheWholeNameIsTooLong_UsesTheFirstWord(string player, string expected)
        {
            // Cutting "Bartholomew Smith's Table" at 20 characters used to leave "Bartholomew Smith's".
            Assert.AreEqual(expected, RoomNames.DefaultFor(player, Format, 20));
        }

        [TestCase("Bartholomewwwwww", "Bartholomeww's Table")]
        [TestCase("Bartholomewwwwww Smith", "Bartholomeww's Table")]
        public void DefaultFor_TheFirstWordIsTooLong_CutsTheNameSoThatTheFormatsWordsSurvive(string player, string expected)
        {
            Assert.AreEqual(expected, RoomNames.DefaultFor(player, Format, 20));
        }

        [TestCase("Table of {0}", "Hari", "Table of Hari")]
        [TestCase("Table of {0}", "Bartholomew Smith", "Table of Bartholomew")]
        [TestCase("Table of {0}", "Bartholomewwwwww", "Table of Bartholomew")]
        [TestCase("The {0} table", "Bartholomew Smith", "The Bartholome table")]
        [TestCase("{0} को टेबल", "Hari", "Hari को टेबल")]
        public void DefaultFor_FormatThatDoesNotStartWithTheName_IsFittedTheSameWay(string format, string player, string expected)
        {
            Assert.AreEqual(expected, RoomNames.DefaultFor(player, format, 20));
        }

        [Test]
        public void DefaultFor_CuttingAName_NeverSplitsALetterFromItsVowelSign()
        {
            // Twelve letters, a vowel sign on the twelfth, then one more: a cut after twelve
            // characters would strip the sign from its letter.
            string player = new string('क', 11) + "किक";

            Assert.AreEqual(new string('क', 11) + "'s Table", RoomNames.DefaultFor(player, Format, 20));
        }

        [Test]
        public void DefaultFor_CuttingAName_NeverSplitsASurrogatePair()
        {
            string player = "Bartholomew😀x";

            Assert.AreEqual("Bartholomew's Table", RoomNames.DefaultFor(player, Format, 20));
        }

        [TestCase("The grand tournament table of {0}", "Hari", "Hari")]
        [TestCase("The grand tournament table of {0}", "Bartholomew Smith", "Bartholomew Smith")]
        [TestCase("ui.create_room.default_name", "Hari", "Hari")]
        [TestCase("{0", "Hari", "Hari")]
        [TestCase("{1}'s Table", "Hari", "Hari")]
        [TestCase("", "Hari", "Hari")]
        [TestCase(null, "Hari", "Hari")]
        public void DefaultFor_FormatThatLeavesNoRoomOrIsBroken_FallsBackToTheNameAlone(string format, string player, string expected)
        {
            Assert.AreEqual(expected, RoomNames.DefaultFor(player, format, 20));
        }

        [Test]
        public void DefaultFor_FormatWithoutAPlaceholder_IsUsedAsItIsWhenItFits()
        {
            Assert.AreEqual("Our Table", RoomNames.DefaultFor("Hari", "Our Table", 20));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void DefaultFor_NoPlayerName_UsesTheDefaultDisplayName(string player)
        {
            Assert.AreEqual("Player's Table", RoomNames.DefaultFor(player, Format, 20));
        }

        [Test]
        public void DefaultFor_UntidyName_IsTrimmedLikeEveryRoomName()
        {
            Assert.AreEqual("Asha Rai's Table", RoomNames.DefaultFor("  Asha   Rai ", Format, 20));
        }

        [Test]
        public void DefaultFor_AnyNameFormatAndLimit_IsNeverLongerThanTheLimitOrShorterThanThree()
        {
            string[] players =
            {
                null, string.Empty, "   ", "A", "Al", "Hari", "Bartholomew Smith", "Wwwwwwwwwwwwwwww", "A B C D E F G H I",
                "अ", "विक्रमादित्यसिंह",
                "😀😀😀😀😀😀😀😀😀😀😀",
                "A veryveryveryveryveryverylongnamethatnoprofileallows",
            };
            string[] formats =
            {
                Format, "Table of {0}", "{0}", "{0}{0}{0}", "{0} — {0}", string.Empty, null, "T", "Tbl", "Our Table",
                "ui.create_room.default_name", "The grand tournament table of {0}", "{0} को टेबल",
                "{0", "{1}", "}{", "   {0}   ",
            };
            int[] limits = { 3, 4, 5, 8, 10, 20, 40 };

            foreach (string player in players)
            {
                foreach (string format in formats)
                {
                    foreach (int limit in limits)
                    {
                        string name = RoomNames.DefaultFor(player, format, limit);
                        string label = $"player '{player}', format '{format}', limit {limit}: '{name}'";

                        Assert.IsNotNull(name, label);
                        Assert.LessOrEqual(name.Length, limit, label);
                        Assert.GreaterOrEqual(name.Length, RoomNames.MinLength, label);
                        Assert.AreEqual(RoomNames.Normalize(name), name, "already trimmed: " + label);
                        Assert.IsFalse(char.IsLowSurrogate(name[0]) || char.IsHighSurrogate(name[name.Length - 1]), "no half character: " + label);
                    }
                }
            }
        }

        [Test]
        public void DefaultFor_WithoutALimit_UsesTheRoomNameLimit()
        {
            Assert.AreEqual(20, RoomNames.MaxLength);
            Assert.AreEqual("Bartholomew's Table", RoomNames.DefaultFor("Bartholomew Smith", Format));
        }

        [Test]
        public void DefaultFor_LimitBelowTheShortestRoomName_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RoomNames.DefaultFor("Hari", Format, RoomNames.MinLength - 1));
        }

        [TestCase("Hari's Table", true)]
        [TestCase("abc", true)]
        [TestCase("  Hari's   Table ", true)]
        [TestCase("Twenty characters ok", true)]
        [TestCase("हरिको टेबल", true)]
        [TestCase("ab", false)]
        [TestCase("  ab  ", false)]
        [TestCase("Twenty-one characters", false)]
        [TestCase("   ", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("Shit Table", false)]
        [TestCase("f u c k", false)]
        public void IsValid_ThreeToTwentyCharactersAfterTrimming_AndAllowedByTheFilter(string name, bool expected)
        {
            Assert.AreEqual(expected, RoomNames.IsValid(name, new NameFilter()));
        }

        [Test]
        public void IsValid_NeedsTheFilter()
        {
            Assert.Throws<ArgumentNullException>(() => RoomNames.IsValid("Hari's Table", null));
        }

        [Test]
        public void Normalize_TrimsAndCollapsesWhiteSpace()
        {
            Assert.AreEqual("Hari's Table", RoomNames.Normalize("  Hari's \t  Table\n"));
            Assert.AreEqual(string.Empty, RoomNames.Normalize(null));
        }
    }
}
