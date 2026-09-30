using System;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §8 presence at a hosted table: an Away seat, a dropped connection and a return never stop
    /// the other players and never buy the returning player time (L638 "MUST keep the other three
    /// players moving", L664-665 "you take control from the next turn", L686 "Disconnecting never
    /// pauses an online table").
    /// </summary>
    public sealed class TablePresenceTests
    {
        private const double FineStep = 0.05;

        [TestCase(new byte[] { 5 }, TestName = "AwaySeat_PlaceCallWithoutItsBody_ChangesNothingAndTheTableMovesOn")]
        [TestCase(new byte[] { 6, 200, 0, 0, 0, 0 }, TestName = "AwaySeat_PlayCardOfACardThatDoesNotExist_ChangesNothingAndTheTableMovesOn")]
        [TestCase(new byte[] { 8 }, TestName = "AwaySeat_EmoteWithoutItsId_ChangesNothingAndTheTableMovesOn")]
        public void AwaySeat_UnreadableMessageOnItsOwnTurn_ChangesNothingAndTheTableMovesOn(byte[] payload)
        {
            var room = new RoomFixture(turnSeconds: 15, timing: BotTiming.Hosted);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            Seat seat = guest.Session.LocalSeat;

            // The guest never plays: two timeouts in a row and the seat is Away, a bot playing it.
            StepUntil(room, () => HostPresenceOf(room, seat) == SeatConnection.Away, 0.25, 400.0);
            StepUntil(room, () => room.Host.Table.ViewFor(seat).IsMyTurn, FineStep, 120.0);
            TurnId turn = room.Host.Table.ViewFor(seat).CurrentTurn;

            guest.Link.Send(payload);
            room.Network.Pump();

            Assert.AreEqual(SeatConnection.Away, HostPresenceOf(room, seat), "a message that cannot be read is not an intent");
            Assert.AreEqual(SeatConnection.Away, room.HostMember.Session.Seats[(int)seat].Connection, "and everyone still sees the seat Away");
            StepUntil(room, () => room.Host.Table.ViewFor(seat).CurrentTurn != turn, FineStep, 5.0);
        }

        [Test]
        public void OwnTurn_DropWithASecondLeftAndComeStraightBack_NoFreshClockAndTheTurnIsOverSoon()
        {
            var room = new RoomFixture(turnSeconds: 15, timing: BotTiming.Hosted);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            Seat seat = guest.Session.LocalSeat;
            TurnId turn = HoldAPlayTurnWithItsClockRunning(room, guest);
            StepUntil(room, () => room.Host.Table.StatusAt(room.Now).TimerRemainingMs <= 1000, FineStep, 20.0, hostPlays: false);

            room.Network.Drop(guest.Link);
            room.Network.Pump();
            room.Step(FineStep);
            room.Step(FineStep);
            Assert.AreEqual(RoomError.None, room.Rejoin(guest));
            room.Step(FineStep);

            TableStatus after = room.Host.Table.StatusAt(room.Now);
            bool freshClock = after.HasTimer && after.TimerSeat == seat && after.TimerRemainingMs > 1000;
            Assert.IsFalse(freshClock, "coming back starts no new clock on the open turn (" + after.TimerRemainingMs + " ms)");
            StepUntil(room, () => room.Host.Table.ViewFor(seat).CurrentTurn != turn, FineStep, BotTiming.Hosted.ThinkMaxSeconds + 0.5, hostPlays: false);
        }

        [Test]
        public void OwnTurn_DropAndComeBackAgainAndAgain_TheTurnStillEndsWithinOneThinkTime()
        {
            var room = new RoomFixture(turnSeconds: 15, timing: BotTiming.Hosted);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            Seat seat = guest.Session.LocalSeat;
            TurnId turn = HoldAPlayTurnWithItsClockRunning(room, guest);
            double firstDrop = room.Now;

            for (int cycle = 0; cycle < 12 && room.Host.Table.ViewFor(seat).CurrentTurn == turn; cycle++)
            {
                room.Network.Drop(guest.Link);
                room.Network.Pump();
                room.Step(FineStep);
                room.Rejoin(guest);
                room.Step(FineStep);
            }

            StepUntil(room, () => room.Host.Table.ViewFor(seat).CurrentTurn != turn, FineStep, 1.0, hostPlays: false);
            Assert.LessOrEqual(room.Now - firstDrop, BotTiming.Hosted.ThinkMaxSeconds + 1.0, "the bot covering the turn finished it on its first think time");
        }

        [Test]
        public void OwnTurn_BackDuringIt_TheBotFinishesItAndThePlayerHasTheNextTurnWithAClock()
        {
            var room = new RoomFixture(turnSeconds: 15, timing: BotTiming.Hosted);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();
            Seat seat = guest.Session.LocalSeat;
            TurnId turn = HoldAPlayTurnWithItsClockRunning(room, guest);
            room.Network.Drop(guest.Link);
            room.Network.Pump();
            room.Step(FineStep);
            Assert.AreEqual(RoomError.None, room.Rejoin(guest));
            room.Step(FineStep);

            StepUntil(room, () => room.Host.Table.ViewFor(seat).CurrentTurn != turn, FineStep, 3.0, hostPlays: false);
            StepUntil(room, () => room.Host.Table.ViewFor(seat).IsMyTurn && room.Host.Table.StatusAt(room.Now).HasTimer, FineStep, 120.0);

            TableStatus status = room.Host.Table.StatusAt(room.Now);
            Assert.AreEqual(seat, status.TimerSeat, "the player's next turn is the player's, on the clock");
            Assert.AreEqual(SeatConnection.Connected, status.ConnectionOf(seat));
        }

        [Test]
        public void BotTurn_AnotherMemberDropsAndComesBackAgainAndAgain_TheBotStillMovesOnTime()
        {
            var room = new RoomFixture(timing: BotTiming.Hosted);
            RoomFixture.Member asha = room.Join("player-asha", "Asha");
            RoomFixture.Member bikash = room.Join("player-bikash", "Bikash");
            room.StartMatch();
            Seat botSeat = Seat.West;
            Assert.IsTrue(asha.Session.Seats[(int)botSeat].IsBot, "the fourth seat was filled with a bot");

            StepUntil(room, () => room.Host.Table.ViewFor(botSeat).IsMyTurn, FineStep, 120.0, everyonePlays: true);
            TurnId turn = room.Host.Table.ViewFor(botSeat).CurrentTurn;
            double opened = room.Now;
            for (int cycle = 0; cycle < 60 && room.Host.Table.ViewFor(botSeat).CurrentTurn == turn; cycle++)
            {
                room.Network.Drop(bikash.Link);
                room.Network.Pump();
                room.Step(FineStep);
                room.Rejoin(bikash);
                room.Step(FineStep);
            }

            StepUntil(room, () => room.Host.Table.ViewFor(botSeat).CurrentTurn != turn, FineStep, 1.0, hostPlays: false);
            double hold = BotTiming.Hosted.TrickDisplaySeconds + BotTiming.Hosted.DealSeconds;
            Assert.LessOrEqual(room.Now - opened, hold + BotTiming.Hosted.ThinkMaxSeconds + 0.5, "someone else coming and going never restarts a bot's think time");
        }

        private static SeatConnection HostPresenceOf(RoomFixture room, Seat seat) => room.Host.Table.StatusAt(room.Now).ConnectionOf(seat);

        /// <summary>Plays on (the guest only calls and readies) until the guest holds a play turn with its clock running; returns that turn.</summary>
        private static TurnId HoldAPlayTurnWithItsClockRunning(RoomFixture room, RoomFixture.Member guest)
        {
            Seat seat = guest.Session.LocalSeat;
            for (int i = 0; i < 20000; i++)
            {
                TableStatus status = room.Host.Table.StatusAt(room.Now);
                if (room.Host.Table.ViewFor(seat).Phase == Phase.Playing && status.HasTimer && status.TimerSeat == seat
                    && status.TimerRemainingMs > 10000)
                {
                    return room.Host.Table.ViewFor(seat).CurrentTurn;
                }

                room.Step(FineStep);
                RoomFixture.Act(room.HostMember.Session);
                Phase phase = guest.Session.View.Phase;
                if (phase == Phase.Calling || phase == Phase.RoundSummary)
                {
                    RoomFixture.Act(guest.Session);
                }

                room.Network.Pump();
            }

            Assert.Fail("the guest never held a play turn on the clock");
            return TurnId.None;
        }

        /// <summary>Time passes (the host's player plays its turns) until <paramref name="reached"/>, which must happen within <paramref name="seconds"/>.</summary>
        private static void StepUntil(RoomFixture room, Func<bool> reached, double step, double seconds, bool hostPlays = true, bool everyonePlays = false)
        {
            double until = room.Now + seconds;
            while (!reached() && room.Now < until)
            {
                room.Step(step);
                if (everyonePlays)
                {
                    foreach (RoomFixture.Member m in room.Members)
                    {
                        RoomFixture.Act(m.Session);
                    }
                }
                else if (hostPlays)
                {
                    RoomFixture.Act(room.HostMember.Session);
                }

                room.Network.Pump();
            }

            Assert.IsTrue(reached(), "not reached within " + seconds + " s");
        }
    }
}
