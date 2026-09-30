using Callbreak.Core.Match;
using Callbreak.Core.Scoring;
using Callbreak.Presentation.Table;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// The copy and row order of the result screens (GDD §11 screens 09, 10 and 16), against the
    /// shipped string table: the headline follows the local player's place including ties, the
    /// footer counts rounds, and the round table lists the local player first.
    /// </summary>
    public sealed class ResultsTextTests
    {
        private static readonly TableStrings Strings = new TableStrings();

        private static Standing Place(int rank, bool shared) => new Standing(Seat.South, rank, shared, 100, 3, 40);

        [TestCase(1, false, "You Win!")]
        [TestCase(1, true, "Shared Victory!")]
        [TestCase(2, false, "2nd Place · Well played!")]
        [TestCase(2, true, "2nd Place · Well played!")]
        [TestCase(3, false, "3rd Place · So close!")]
        [TestCase(4, false, "4th Place · Next time!")]
        public void TheHeadline_FollowsThePlace(int rank, bool shared, string expected)
        {
            Assert.AreEqual(expected, Strings.Get(ResultsText.HeadlineKey(Place(rank, shared))));
        }

        [Test]
        public void TheHeadline_OfATiedMatch_IsSharedForBothWinners()
        {
            // GDD §4: equal on total, rounds met and best round → the seats share first place.
            var sheet = ScoreSheet.Empty.Append(new RoundResult(1, new[]
            {
                new SeatRoundResult(Seat.South, 4, 4, 40, 40),
                new SeatRoundResult(Seat.East, 4, 4, 40, 40),
                new SeatRoundResult(Seat.North, 3, 3, 30, 30),
                new SeatRoundResult(Seat.West, 2, 2, 20, 20),
            }));
            foreach (Standing standing in Standings.Compute(sheet))
            {
                string headline = Strings.Get(ResultsText.HeadlineKey(standing));
                switch (standing.Seat)
                {
                    case Seat.South:
                    case Seat.East:
                        Assert.AreEqual("Shared Victory!", headline);
                        break;
                    case Seat.North:
                        Assert.AreEqual("3rd Place · So close!", headline, "two winners: the next place is third");
                        break;
                    default:
                        Assert.AreEqual("4th Place · Next time!", headline);
                        break;
                }
            }
        }

        [TestCase(5, "5 rounds completed")]
        [TestCase(2, "2 rounds completed")]
        [TestCase(1, "1 round completed")]
        [TestCase(0, "0 rounds completed")]
        public void TheFooter_CountsTheRounds(int rounds, string expected)
        {
            Assert.AreEqual(expected, ResultsText.RoundsCompleted(rounds, Strings));
        }

        [Test]
        public void TheScorecardsFinalFooter_IsTheMockups()
        {
            // Mockup 16: "Final standings · 5 rounds completed".
            Assert.AreEqual(
                "Final standings · 5 rounds completed",
                Strings.Format("ui.scorecard.footer_final", ResultsText.RoundsCompleted(5, Strings)));
        }

        [TestCase(Seat.South, Seat.South, Seat.North, Seat.West, Seat.East)]
        [TestCase(Seat.East, Seat.East, Seat.West, Seat.South, Seat.North)]
        [TestCase(Seat.North, Seat.North, Seat.South, Seat.East, Seat.West)]
        [TestCase(Seat.West, Seat.West, Seat.East, Seat.North, Seat.South)]
        public void RoundRows_ListYouThenTopLeftRight(Seat local, Seat first, Seat second, Seat third, Seat fourth)
        {
            var rows = new Seat[4];
            ResultsText.RoundRowOrder(local, rows);
            Assert.AreEqual(new[] { first, second, third, fourth }, rows);

            // Relative seats: 2 is across the table (top), 3 to the left, 1 to the right.
            Assert.AreEqual(2, rows[1].RelativeTo(local));
            Assert.AreEqual(3, rows[2].RelativeTo(local));
            Assert.AreEqual(1, rows[3].RelativeTo(local));
        }

        [Test]
        public void RoundRows_NeedFourPlaces()
        {
            Assert.Throws<System.ArgumentException>(() => ResultsText.RoundRowOrder(Seat.South, new Seat[3]));
            Assert.Throws<System.ArgumentException>(() => ResultsText.RoundRowOrder(Seat.South, null));
        }

        [Test]
        public void AnUnplayedRound_ShowsADash_NotAMinus()
        {
            Assert.AreEqual("–", ResultsText.Unplayed);
            Assert.AreNotEqual(ScoreTenths.MinusSign, ResultsText.Unplayed, "a dash for \"not played\" must not read as a negative score");
        }

        [TestCase("ui.round_results.round_title")]
        [TestCase("ui.round_results.next")]
        [TestCase("ui.round_results.next_countdown")]
        [TestCase("ui.round_results.see_final")]
        [TestCase("ui.round_results.footer_next")]
        [TestCase("ui.match_results.footer")]
        [TestCase("ui.match_results.footer_one")]
        [TestCase("ui.scorecard.footer_final")]
        [TestCase("ui.scorecard.back_to_results")]
        [TestCase("ui.scorecard.back_to_game")]
        [TestCase("ui.common.you")]
        [TestCase("ui.banner.im_back")]
        [TestCase("ui.toast.welcome_back")]
        [TestCase("ui.toast.left_table")]
        [TestCase("ui.toast.host_left")]
        [TestCase("ui.toast.redeal")]
        [TestCase("ui.dialog.leave_match.title")]
        [TestCase("ui.dialog.leave_match.body")]
        [TestCase("ui.dialog.leave_match.body_offline")]
        [TestCase("ui.dialog.leave_match.confirm")]
        [TestCase("ui.dialog.leave_match.cancel")]
        public void EveryKeyThePresentersUse_IsInTheTable(string key)
        {
            Assert.IsTrue(Strings.Has(key), key + " is missing from Assets/_Project/Localization/UI.csv");
        }

        [Test]
        public void TheRoundTitle_AndTheCountdown_ReadAsTheMockupAndTheGdd()
        {
            Assert.AreEqual("ROUND 1 OF 5", Strings.Format("ui.round_results.round_title", 1, 5));
            Assert.AreEqual("Round 2 begins next", Strings.Format("ui.round_results.footer_next", 2));
            Assert.AreEqual("Next Round (10)", Strings.Format("ui.round_results.next_countdown", 10));
        }
    }
}
