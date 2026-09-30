using Callbreak.Application.Rooms;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §11 screens 00 and 04: the invite link <c>callbreak://join/CODE</c>.</summary>
    public sealed class InviteLinkTests
    {
        [TestCase("callbreak://join/AB2CDE", "AB2CDE")]
        [TestCase("callbreak://join/ab2cde", "AB2CDE")]
        [TestCase("CALLBREAK://JOIN/ab2-cde", "AB2CDE")]
        [TestCase("callbreak://join/AB2CDE?utm=x", "AB2CDE")]
        [TestCase("callbreak://join/AB2CDE/", "AB2CDE")]
        [TestCase("callbreak://join/AB2CDE#top", "AB2CDE")]
        public void TryParse_JoinLink_GivesTheNormalisedCode(string url, string expected)
        {
            Assert.IsTrue(InviteLink.TryParse(url, out string code));
            Assert.AreEqual(expected, code);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("https://example.com/join/AB2CDE")]
        [TestCase("callbreak://join/")]
        [TestCase("callbreak://join/AB2")]
        [TestCase("callbreak://join/AB1CDE")]
        [TestCase("callbreak://join/AB2CD?E=1")]
        [TestCase("callbreak://play/AB2CDE")]
        public void TryParse_AnythingElse_IsRefused(string url)
        {
            Assert.IsFalse(InviteLink.TryParse(url, out string code));
            Assert.IsNull(code);
        }

        // GDD §11 screen 03 "Paste Code": whatever the clipboard holds, the boxes get the code in it.

        [Test]
        public void FromPastedText_TheInviteMessage_GivesItsCode_NotTheFirstSixLettersOfTheSentence()
        {
            const string message = "Join my Callbreak table! Code AB2CDE · callbreak://join/AB2CDE";

            Assert.AreEqual("JNMYCA", RoomCode.Normalize(message), "what Paste used to fill in");
            Assert.AreEqual("AB2CDE", InviteLink.FromPastedText(message));
        }

        [TestCase("callbreak://join/AB2CDE", "AB2CDE")]
        [TestCase("Tap to join: CALLBREAK://JOIN/ab2-cde\nSee you there!", "AB2CDE")]
        [TestCase("(callbreak://join/AB2CDE).", "AB2CDE")]
        [TestCase("callbreak://join/AB2CDE?utm=x and some more words after it", "AB2CDE")]
        [TestCase("HJKMNP was yesterday's table, today it is callbreak://join/AB2CDE", "AB2CDE")]
        [TestCase("callbreak://join/AB2 is cut short, callbreak://join/XY3ZW4 is whole", "XY3ZW4")]
        public void FromPastedText_InviteLinkAnywhereInTheText_GivesTheLinksCode(string text, string expected)
        {
            Assert.AreEqual(expected, InviteLink.FromPastedText(text));
        }

        [TestCase("ab-2c de", "AB2CDE")]
        [TestCase("AB2CDE", "AB2CDE")]
        [TestCase("  ab2cde\n", "AB2CDE")]
        [TestCase("Ab2cde", "AB2CDE")]
        [TestCase("A B 2 - C D E", "AB2CDE")]
        [TestCase("ab2–cde", "AB2CDE")]
        [TestCase("hjkmnp", "HJKMNP")]
        public void FromPastedText_NothingButOneCode_IgnoresSpacesDashesAndCase(string text, string expected)
        {
            Assert.AreEqual(expected, InviteLink.FromPastedText(text));
        }

        [TestCase("Code: AB2CDE", "AB2CDE")]
        [TestCase("Join my table, the code is AB2CDE!", "AB2CDE")]
        [TestCase("Room HJKMNP tonight at eight", "HJKMNP")]
        [TestCase("कोड AB2CDE हो", "AB2CDE")]
        [TestCase("callbreak://join/ did not copy fully, the code is AB2CDE", "AB2CDE")]
        [TestCase("Code🎉AB2CDE🎉", "AB2CDE")]
        public void FromPastedText_ASixSymbolWordInCapitalsAndDigits_IsTheCode(string text, string expected)
        {
            Assert.AreEqual(expected, InviteLink.FromPastedText(text));
        }

        [TestCase("PLEASE JOIN AB2CDE", "AB2CDE")]
        [TestCase("TABLES ARE OPEN, CODE XY3ZW4 THANKS", "XY3ZW4")]
        [TestCase("FIRST TRY HJKMNP THEN QRSTUV", "HJKMNP")]
        public void FromPastedText_SeveralCandidateWords_PrefersOneWithADigit_ElseTheFirst(string text, string expected)
        {
            Assert.AreEqual(expected, InviteLink.FromPastedText(text));
        }

        [TestCase("Player Hari invites you to a table")]
        [TestCase("Hello Player, are you coming tonight?")]
        [TestCase("the code is ab2cde, all in small letters")]
        [TestCase("Code: AB2CDEF")]
        [TestCase("Code: XAB2CDE9 again")]
        [TestCase("Call me on 9845123456 before eight")]
        [TestCase("AB2CDEAB2CDE twice in a row is not a code")]
        [TestCase("Codes never hold I, O, 0 or 1: AB1CDE")]
        [TestCase("a - b - 2 - c - d - and then it stops")]
        public void FromPastedText_TextWithoutACode_GivesNothing(string text)
        {
            Assert.AreEqual(string.Empty, InviteLink.FromPastedText(text));
        }

        [TestCase("ab-2c", "AB2C")]
        [TestCase("AB", "AB")]
        [TestCase("ab2cde.", "AB2CDE")]
        [TestCase("ab-12 cd", "AB2CD")]
        [TestCase("I0O1", "")]
        public void FromPastedText_AShortPieceOfACode_StillFillsTheBoxes(string text, string expected)
        {
            Assert.AreEqual(expected, InviteLink.FromPastedText(text));
        }

        [TestCase("Room AB2-CDE")]
        [TestCase("Hi Player 1")]
        [TestCase("abcdefgh")]
        [TestCase("AB2CDEX")]
        public void FromPastedText_ShortTextThatIsMoreThanACode_IsNotGuessedAt(string text)
        {
            Assert.AreEqual(string.Empty, InviteLink.FromPastedText(text));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \n")]
        public void FromPastedText_NothingPasted_GivesNothing(string text)
        {
            Assert.AreEqual(string.Empty, InviteLink.FromPastedText(text));
        }
    }
}
