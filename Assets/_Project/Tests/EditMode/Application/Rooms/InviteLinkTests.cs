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
    }
}
