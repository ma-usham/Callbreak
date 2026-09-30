using Callbreak.Core.Scoring;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Scoring
{
    public sealed class ScoreTenthsTests
    {
        [TestCase(41, "4.1")]
        [TestCase(30, "3.0")]
        [TestCase(-30, "−3.0")]
        [TestCase(0, "0.0")]
        [TestCase(213, "21.3")]
        [TestCase(-5, "−0.5")]
        [TestCase(1305, "130.5")]
        [TestCase(-2147483648, "−214748364.8")]
        [TestCase(2147483647, "214748364.7")]
        public void Format_Tenths_OneDecimalWithTrueMinus(int tenths, string expected)
        {
            Assert.AreEqual(expected, ScoreTenths.Format(tenths));
        }

        [TestCase(41, "+4.1")]
        [TestCase(-30, "−3.0")]
        [TestCase(85, "+8.5")]
        [TestCase(130, "+13.0")]
        [TestCase(0, "0.0")]
        public void FormatSigned_RoundDelta_ExplicitSign(int tenths, string expected)
        {
            Assert.AreEqual(expected, ScoreTenths.FormatSigned(tenths));
        }

        [Test]
        public void Format_Negative_NeverUsesHyphenMinus()
        {
            Assert.IsFalse(ScoreTenths.Format(-30).Contains("-"));
            Assert.IsFalse(ScoreTenths.FormatSigned(-30).Contains("-"));
        }

        [TestCase("ar-SA")]
        [TestCase("sv-SE")]
        [TestCase("fr-FR")]
        [TestCase("de-DE")]
        public void Format_AnyCulture_SameDotAndMinus(string culture)
        {
            string text = CultureRunner.In(culture, () => ScoreTenths.Format(-1234) + "|" + ScoreTenths.FormatSigned(41));

            Assert.AreEqual("−123.4|+4.1", text);
        }
    }
}
