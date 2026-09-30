using Callbreak.Application.Online;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §8 L649-650: the server owns the clock and clients only display it. A turn that opens
    /// while the table is still showing a deal or a finished trick starts its clock when that hold
    /// ends, so what a player sees runs out when the host's clock does, never a second and a half
    /// earlier.
    /// </summary>
    public sealed partial class TableClockTests
    {
        private const double FineStep = 0.05;

        [Test]
        public void Clock_FirstTurnAfterTheDeal_RunsOutOnScreenWhenItRunsOutOnTheHost()
        {
            RoomFixture room = FourPeople();

            // Nobody acts: the first call turn opened with the deal, while the deal is shown.
            Seat seat = room.Host.Table.ViewFor(Seat.South).CurrentSeat;
            IMatchSession session = SessionOf(room, seat);
            AssertShownClockEndsWithTheHosts(room, session, () => room.Host.Table.ViewFor(seat).HasCalled(seat));
        }

        [Test]
        public void Clock_LeadAfterAFinishedTrick_RunsOutOnScreenWhenItRunsOutOnTheHost()
        {
            RoomFixture room = FourPeople();

            // Everyone calls at once and never plays: the first trick is played out by timeouts, and
            // its winner's lead opens while the finished trick is shown.
            for (int i = 0; i < 4000 && room.Host.Table.ViewFor(Seat.South).CompletedTricks.Count == 0; i++)
            {
                foreach (RoomFixture.Member m in room.Members)
                {
                    if (m.Session.View.Phase == Phase.Calling)
                    {
                        RoomFixture.Act(m.Session);
                    }
                }

                room.Network.Pump();
                room.Step(FineStep);
            }

            Seat leader = room.Host.Table.ViewFor(Seat.South).CurrentSeat;
            Assert.AreEqual(1, room.Host.Table.ViewFor(Seat.South).CompletedTricks.Count, "the first trick is over");
            int cards = room.Host.Table.ViewFor(leader).Hand.Count;
            AssertShownClockEndsWithTheHosts(room, SessionOf(room, leader), () => room.Host.Table.ViewFor(leader).Hand.Count < cards);
        }

        private static RoomFixture FourPeople()
        {
            var room = new RoomFixture(turnSeconds: 15, timing: BotTiming.Hosted);
            room.Join("player-asha", "Asha");
            room.Join("player-bikash", "Bikash");
            room.Join("player-suman", "Suman");
            room.StartMatch(fillWithBots: false);
            return room;
        }

        private static IMatchSession SessionOf(RoomFixture room, Seat seat)
        {
            foreach (RoomFixture.Member m in room.Members)
            {
                if (m.Session.LocalSeat == seat)
                {
                    return m.Session;
                }
            }

            Assert.Fail("nobody sits at " + seat);
            return null;
        }

        /// <summary>
        /// Time passes with nobody acting. The clock <paramref name="session"/> shows must never
        /// show more than the turn's length, and must reach zero just when the host, half a second
        /// of grace later, plays the turn for the player (<paramref name="playedForThem"/>).
        /// </summary>
        private static void AssertShownClockEndsWithTheHosts(RoomFixture room, IMatchSession session, System.Func<bool> playedForThem)
        {
            double shownDeadline = double.NaN;
            for (int i = 0; i < 2000 && !playedForThem(); i++)
            {
                room.Step(FineStep);
                TurnTimerState timer = session.Timer;
                if (timer.IsRunning && timer.Seat == session.LocalSeat)
                {
                    Assert.LessOrEqual(timer.Deadline - room.Now, timer.Duration + FineStep, "the ring never shows more than the turn's length");
                    shownDeadline = timer.Deadline;
                }
            }

            Assert.IsTrue(playedForThem(), "the host played the turn for the player");
            Assert.IsFalse(double.IsNaN(shownDeadline), "a clock was shown");
            Assert.That(room.Now - shownDeadline, Is.InRange(Protocol.TurnGraceSeconds - FineStep - 0.01, Protocol.TurnGraceSeconds + FineStep + 0.01),
                "the shown clock ran out " + (room.Now - shownDeadline - Protocol.TurnGraceSeconds).ToString("0.00") + " s before the host's");
        }
    }
}
