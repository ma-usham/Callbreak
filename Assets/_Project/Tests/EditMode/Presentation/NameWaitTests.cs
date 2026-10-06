using System;
using Callbreak.Presentation.Screens;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// ADR 041: how the Profile screen says how long a saved name stays, in the shipped English:
    /// whole days rounded up from two days on, hours below that, and the last hour in words.
    /// </summary>
    public sealed class NameWaitTests
    {
        private readonly TableStrings _text = new TableStrings();

        [TestCase(7 * 24 * 60, "You can change your name again in 7 days.")]
        [TestCase(6 * 24 * 60 + 1, "You can change your name again in 7 days.")]
        [TestCase(2 * 24 * 60, "You can change your name again in 2 days.")]
        [TestCase(24 * 60 + 1, "You can change your name again in 2 days.")]
        [TestCase(24 * 60, "You can change your name again in 24 hours.")]
        [TestCase(61, "You can change your name again in 2 hours.")]
        [TestCase(60, "You can change your name again within the hour.")]
        [TestCase(1, "You can change your name again within the hour.")]
        public void TheWait_IsSaidInDaysThenHours(int minutes, string expected)
        {
            Assert.AreEqual(expected, NameWait.Text(TimeSpan.FromMinutes(minutes), _text));
        }

        [Test]
        public void TheNoteBeforeSave_SaysTheNameThenStaysAWeek()
        {
            Assert.AreEqual("You can change your name once every 7 days.", _text.Get(NameWait.WeekNoteKey));
        }
    }
}
