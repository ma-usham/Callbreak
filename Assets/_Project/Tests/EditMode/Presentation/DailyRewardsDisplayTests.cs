using Callbreak.Presentation.Screens;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>The Daily Rewards page's tiles and footer (mockup 26).</summary>
    public sealed class DailyRewardsDisplayTests
    {
        [Test]
        public void Day1Ready_IsTheOnlyTodayTile_AndTheRestWait()
        {
            Assert.AreEqual(DayTileState.Today, DailyRewardsDisplay.StateOf(0, 0, canClaim: true));
            for (int i = 1; i < 7; i++)
            {
                Assert.AreEqual(DayTileState.Upcoming, DailyRewardsDisplay.StateOf(i, 0, canClaim: true));
            }
        }

        [Test]
        public void DaysBeforeToday_AreClaimed()
        {
            Assert.AreEqual(DayTileState.Claimed, DailyRewardsDisplay.StateOf(0, 3, canClaim: true));
            Assert.AreEqual(DayTileState.Claimed, DailyRewardsDisplay.StateOf(2, 3, canClaim: true));
            Assert.AreEqual(DayTileState.Today, DailyRewardsDisplay.StateOf(3, 3, canClaim: true));
            Assert.AreEqual(DayTileState.Upcoming, DailyRewardsDisplay.StateOf(4, 3, canClaim: true));
        }

        [Test]
        public void Today_OnceClaimed_ShowsClaimed()
        {
            Assert.AreEqual(DayTileState.Claimed, DailyRewardsDisplay.StateOf(3, 3, canClaim: false));
        }

        [TestCase(0, 2)]
        [TestCase(5, 7)]
        [TestCase(6, 1)]
        public void TheFooter_NamesTomorrowsDay_Day1AfterDay7(int today, int expected)
        {
            // Mockup 26: "Come back tomorrow for Day 2" while Day 1 is still to claim.
            Assert.AreEqual(expected, DailyRewardsDisplay.NextDayNumber(today, 7));
        }
    }
}
