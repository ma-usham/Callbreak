using System.Collections.Generic;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Rooms;
using Callbreak.Presentation.Screens;
using NUnit.Framework;
using Clock = Callbreak.Presentation.Screens.MatchmakingDisplay.ClockKind;

namespace Callbreak.Tests.EditMode.Presentation
{
    /// <summary>
    /// What the Quick Match screen shows for a search report (GDD §11 screen 05, ADR 029): every
    /// player sees themselves in the first seat circle and the others after them in the table's seat
    /// order; the pill counts the player's own search down, then the start countdown the whole table
    /// shares, and keeps the elapsed time when the search has no countdown; nobody can leave a found
    /// table during its countdown, and everyone can again once the search resumes. The copy is the
    /// shipped English.
    /// </summary>
    public sealed class MatchmakingDisplayTests
    {
        private const int Seats = 4;

        private readonly TableStrings _text = new TableStrings();

        [Test]
        public void HostScreen_TheHostIsYouInTheFirstCircle_ThenTheOthersInTableOrder()
        {
            MatchmakingScreenState state = Show(Report(Seat(0, "Hari", local: true), Seat(1, "Asha"), Empty(2), Empty(3)));

            AssertSeats(state, "You", "Asha", "", "");
            AssertFilled(state, true, true, false, false);
            Assert.AreEqual("2 of 4 players connected", state.Subtitle);
            Assert.AreEqual("Finding players...", state.Title);
        }

        [Test]
        public void GuestScreen_TheGuestIsYouInTheFirstCircle_AndTheHostComesSecond()
        {
            // The table's order is the host's seat first; on B's screen B comes first, then the host.
            MatchmakingScreenState state = Show(Report(Seat(0, "Hari"), Seat(1, "Asha", local: true), Empty(2), Empty(3)));

            AssertSeats(state, "You", "Hari", "", "");
            AssertFilled(state, true, true, false, false);
        }

        [Test]
        public void ThirdPlayer_TheOthersKeepTheTablesOrderAfterThem_EmptySeatsWhereTheyAre()
        {
            MatchmakingScreenState state = Show(Report(Seat(0, "Hari"), Empty(1), Seat(2, "Suman", local: true), Seat(3, "Asha")));

            AssertSeats(state, "You", "Hari", "", "Asha");
            AssertFilled(state, true, true, false, true);
            Assert.AreEqual("3 of 4 players connected", state.Subtitle);
        }

        [Test]
        public void InViewerOrder_KeepsTheTablesOrder_WhenTheViewerSitsFirst_OrNoSeatIsTheViewers()
        {
            IReadOnlyList<RoomSeat> hostFirst = new[] { Seat(0, "Hari", local: true), Seat(1, "Asha"), Empty(2), Empty(3) };
            Assert.AreSame(hostFirst, MatchmakingDisplay.InViewerOrder(hostFirst));

            IReadOnlyList<RoomSeat> noViewer = new[] { Seat(0, "Hari"), Seat(1, "Asha"), Empty(2), Empty(3) };
            Assert.AreSame(noViewer, MatchmakingDisplay.InViewerOrder(noViewer), "a search that marks no one keeps its order");
            AssertSeats(Show(Report(noViewer)), "You", "Asha", "", "");

            Assert.AreEqual(0, MatchmakingDisplay.InViewerOrder(null).Count);
        }

        [Test]
        public void BeforeTheFirstReport_OnlyThePlayerIsIn_AndThePillReadsZero()
        {
            MatchmakingScreenState state = Show(null);

            AssertSeats(state, "You", "", "", "");
            AssertFilled(state, true, false, false, false);
            Assert.AreEqual("Ready", state.Seats[0].Status);
            Assert.AreEqual("Searching", state.Seats[1].Status);
            Assert.AreEqual("1 of 4 players connected", state.Subtitle);
            Assert.AreEqual("00:00", state.ClockText);
        }

        [Test]
        public void ABotThatFillsTheTable_ReadsReadyUnderItsName_LikeAnyPlayer()
        {
            MatchmakingScreenState state = Show(Report(Seat(0, "Hari", local: true), Seat(1, "Bikash", bot: true), Seat(2, "Asha", bot: true), Seat(3, "Suman", bot: true)));

            AssertSeats(state, "You", "Bikash", "Asha", "Suman");
            foreach (MatchmakingScreenState.Seat seat in state.Seats)
            {
                Assert.AreEqual("Ready", seat.Status, "ADR 025: no BOT label");
            }
        }

        [TestCase(20.0, "00:20")]
        [TestCase(19.2, "00:20")]
        [TestCase(19.0, "00:19")]
        [TestCase(9.5, "00:10")]
        [TestCase(0.4, "00:01")]
        [TestCase(0.0, "00:00")]
        [TestCase(-0.5, "00:00")]
        public void Searching_ThePillShowsThePlayersOwnCountdown_RoundedUp(double secondsLeft, string expected)
        {
            MatchmakingScreenState state = Show(Report(10.0, false, secondsLeft, double.NaN, Seat(0, "Hari", local: true), Empty(1), Empty(2), Empty(3)));

            Assert.AreEqual(Clock.Search, state.Clock);
            Assert.AreEqual(expected, state.ClockText);
        }

        [TestCase(3.0, "00:03")]
        [TestCase(2.4, "00:03")]
        [TestCase(1.4, "00:02")]
        [TestCase(0.2, "00:01")]
        public void TableFound_ThePillShowsTheStartEveryoneShares_InsteadOfTheOwnCountdown(double startsIn, string expected)
        {
            MatchmakingScreenState state = Show(Report(14.0, true, 9.0, startsIn, FullTable()));

            Assert.AreEqual(Clock.Start, state.Clock);
            Assert.AreEqual(expected, state.ClockText);
            Assert.AreEqual("Table found", state.Title);
            Assert.AreEqual("4 of 4 players connected", state.Subtitle);
        }

        [Test]
        public void AFoundTable_HoldsEveryoneDuringItsStartCountdown_AndASearchThatResumesLetsThemLeave()
        {
            Assert.IsTrue(Show(null).CanLeave, "before the first report");
            Assert.IsTrue(Show(Report(10.0, false, 12.0, double.NaN, Seat(0, "Hari", local: true), Seat(1, "Asha"), Empty(2), Empty(3))).CanLeave, "while searching");
            Assert.IsFalse(Show(Report(14.0, true, 9.0, 2.4, FullTable())).CanLeave, "the table is locked: Cancel, back and Android back wait");
            Assert.IsFalse(Show(Report(14.0, true, double.NaN, double.NaN, FullTable())).CanLeave, "locked even without a start time");

            // The host left during the countdown: the seat empties and the search resumes.
            MatchmakingScreenState resumed = Show(Report(15.0, false, 6.0, double.NaN, Seat(0, "Asha", local: true), Seat(1, "Bikash"), Empty(2), Empty(3)));
            Assert.IsTrue(resumed.CanLeave, "the search is back: the player can leave again");
            Assert.AreEqual("Finding players...", resumed.Title);
        }

        [TestCase(12.7, "00:12")]
        [TestCase(75.2, "01:15")]
        [TestCase(0.0, "00:00")]
        public void NoCountdown_ThePillKeepsTheElapsedTime(double elapsed, string expected)
        {
            MatchmakingScreenState state = Show(Report(elapsed, false, double.NaN, double.NaN, Seat(0, "Hari", local: true), Empty(1), Empty(2), Empty(3)));

            Assert.AreEqual(Clock.Elapsed, state.Clock);
            Assert.AreEqual(expected, state.ClockText);
        }

        [Test]
        public void TableFoundWithoutAStartTime_ThePillStaysOnTheOwnCountdown_OrTheElapsedTime()
        {
            Assert.AreEqual(Clock.Search, Show(Report(14.0, true, 5.2, double.NaN, FullTable())).Clock);
            Assert.AreEqual("00:06", Show(Report(14.0, true, 5.2, double.NaN, FullTable())).ClockText);

            MatchmakingScreenState neither = Show(Report(14.0, true, double.NaN, double.NaN, FullTable()));
            Assert.AreEqual(Clock.Elapsed, neither.Clock);
            Assert.AreEqual("00:14", neither.ClockText);
            Assert.AreEqual("Table found", neither.Title);
        }

        [Test]
        public void TheSwitchToTheSharedStart_CountsDownQuickly_OnlyWhenItIsAJumpDown()
        {
            Assert.IsTrue(MatchmakingDisplay.RollsDown(9, 3, Clock.Start, false), "a guest's 00:09 rolls down to the table's 00:03");
            Assert.IsFalse(MatchmakingDisplay.RollsDown(9, 3, Clock.Start, true), "with Reduce motion it just changes");
            Assert.IsFalse(MatchmakingDisplay.RollsDown(4, 3, Clock.Start, false), "a second ticking by just changes");
            Assert.IsFalse(MatchmakingDisplay.RollsDown(1, 3, Clock.Start, false), "a clock that goes up just changes");
            Assert.IsFalse(MatchmakingDisplay.RollsDown(9, 3, Clock.Search, false), "only the start countdown rolls");
        }

        [Test]
        public void TheQuickCount_ShowsEveryNumberOnTheWayDown_AndEndsOnTheTarget()
        {
            Assert.AreEqual(9, MatchmakingDisplay.RollStep(9, 3, 0f));
            Assert.AreEqual(6, MatchmakingDisplay.RollStep(9, 3, 0.5f));
            Assert.AreEqual(3, MatchmakingDisplay.RollStep(9, 3, 1f));

            var seen = new List<int>();
            for (int step = 0; step <= 600; step++)
            {
                int shown = MatchmakingDisplay.RollStep(9, 3, step / 600f);
                if (seen.Count == 0 || seen[seen.Count - 1] != shown)
                {
                    seen.Add(shown);
                }
            }

            CollectionAssert.AreEqual(new[] { 9, 8, 7, 6, 5, 4, 3 }, seen);
        }

        [TestCase(0, "00:00")]
        [TestCase(59, "00:59")]
        [TestCase(61, "01:01")]
        [TestCase(5999, "99:59")]
        [TestCase(7200, "99:59")]
        [TestCase(-3, "00:00")]
        public void ThePill_ReadsMinutesAndSeconds(int seconds, string expected)
        {
            Assert.AreEqual(expected, MatchmakingDisplay.FormatClock(seconds));
        }

        private MatchmakingScreenState Show(MatchmakingProgress progress) => MatchmakingDisplay.StateOf(progress, _text, Seats);

        private static MatchmakingProgress Report(params RoomSeat[] seats) => new MatchmakingProgress(seats, 3.0, false);

        private static MatchmakingProgress Report(IReadOnlyList<RoomSeat> seats) => new MatchmakingProgress(seats, 3.0, false);

        private static MatchmakingProgress Report(double elapsed, bool found, double secondsLeft, double startsIn, params RoomSeat[] seats)
        {
            return new MatchmakingProgress(seats, elapsed, found, secondsLeft, startsIn);
        }

        private static RoomSeat[] FullTable()
        {
            return new[] { Seat(0, "Hari"), Seat(1, "Asha", local: true), Seat(2, "Bikash", bot: true), Seat(3, "Suman") };
        }

        private static RoomSeat Seat(int index, string name, bool local = false, bool bot = false)
        {
            return new RoomSeat(index, true, name, index + 1, "classic-crimson", index == 0, true, bot, local);
        }

        private static RoomSeat Empty(int index)
        {
            return new RoomSeat(index, false, string.Empty, 0, string.Empty, false, false, false, false);
        }

        private static void AssertSeats(MatchmakingScreenState state, params string[] names)
        {
            Assert.AreEqual(names.Length, state.Seats.Count);
            for (int i = 0; i < names.Length; i++)
            {
                Assert.AreEqual(names[i], state.Seats[i].Name, "seat circle " + i);
            }
        }

        private static void AssertFilled(MatchmakingScreenState state, params bool[] filled)
        {
            for (int i = 0; i < filled.Length; i++)
            {
                Assert.AreEqual(filled[i], state.Seats[i].Filled, "seat circle " + i + (filled[i] ? " is a player" : " is still searching"));
            }
        }
    }
}
