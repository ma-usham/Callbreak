using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Online;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Cards;
using Callbreak.Core.Commands;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    public sealed class OnlineRoomTests
    {
        [Test]
        public void Join_Guest_BothSeeTheSameLobbyWithTheHostAtSeatZero()
        {
            var room = new RoomFixture();

            RoomFixture.Member guest = room.Join("player-asha", "Asha");

            Assert.AreEqual(0, room.HostMember.Client.LocalSeatIndex);
            Assert.AreEqual(1, guest.Client.LocalSeatIndex);
            Assert.IsTrue(room.HostMember.Client.IsHost);
            Assert.IsFalse(guest.Client.IsHost);
            Assert.AreEqual(RoomFixture.Code, guest.Client.Code);
            Assert.AreEqual("Hari's Table", guest.Client.Settings.Name);
            foreach (RoomFixture.Member m in room.Members)
            {
                Assert.AreEqual("Hari", m.Client.Seats[0].Name);
                Assert.IsTrue(m.Client.Seats[0].IsHost);
                Assert.AreEqual("Asha", m.Client.Seats[1].Name);
                Assert.IsFalse(m.Client.Seats[2].Occupied);
            }

            Assert.IsTrue(guest.Client.Seats[1].IsLocal);
            Assert.IsFalse(guest.Client.Seats[0].IsLocal);
        }

        [Test]
        public void Join_FifthPlayer_IsTurnedAwayAsFull()
        {
            var room = new RoomFixture();
            room.Join("p1", "Asha");
            room.Join("p2", "Bikash");
            room.Join("p3", "Suman");

            room.Join("p4", "Gita", out RoomError error);

            Assert.AreEqual(RoomError.Full, error);
        }

        [Test]
        public void Join_OlderProtocol_IsToldToUpdate()
        {
            var room = new RoomFixture();

            room.Join("p1", "Asha", out RoomError error, version: (ushort)(Protocol.Version + 1));

            Assert.AreEqual(RoomError.UpdateRequired, error);
        }

        [Test]
        public void Join_AfterTheMatchStarted_IsTurnedAway()
        {
            var room = new RoomFixture();
            room.Join("p1", "Asha");
            room.StartMatch();

            room.Join("p2", "Bikash", out RoomError error);

            Assert.AreEqual(RoomError.AlreadyStarted, error);
        }

        [Test]
        public void Join_UnknownCode_IsNotFound()
        {
            var network = new InMemoryNetwork();

            ClientLinkResult result = network.JoinAsync("ZZZZZZ", CancellationToken.None).Result;

            Assert.AreEqual(RoomError.NotFound, result.Error);
        }

        [Test]
        public void Join_BadName_IsReplacedByTheHost()
        {
            var room = new RoomFixture();

            RoomFixture.Member guest = room.Join("p1", "x");

            Assert.AreEqual("Player 2", guest.Client.Seats[1].Name);
        }

        [Test]
        public void Start_GuestNotReady_IsRefused()
        {
            var room = new RoomFixture();
            room.Join("p1", "Asha");

            bool started = room.HostMember.Client.StartAsync(true, CancellationToken.None).Result;

            Assert.IsFalse(started);
        }

        [Test]
        public void Start_EmptySeatsWithoutBots_IsRefused()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            guest.Client.SetReadyAsync(true, CancellationToken.None);
            room.Network.Pump();

            Assert.IsFalse(room.HostMember.Client.StartAsync(false, CancellationToken.None).Result);
            Assert.IsTrue(room.HostMember.Client.StartAsync(true, CancellationToken.None).Result);
        }

        [Test]
        public void Start_ByAGuest_DoesNothing()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");

            Assert.IsFalse(guest.Client.StartAsync(true, CancellationToken.None).Result);
        }

        [Test]
        public void Kick_Guest_LeavesTheSeatEmptyAndTellsTheGuest()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            RoomError closed = RoomError.None;
            guest.Client.Closed += reason => closed = reason;

            room.HostMember.Client.KickAsync(1, CancellationToken.None);
            room.Network.Pump();

            Assert.AreEqual(RoomError.Kicked, closed);
            Assert.IsFalse(room.HostMember.Client.Seats[1].Occupied);
        }

        [Test]
        public void Leave_Host_ClosesTheRoomForEveryone()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            RoomError closed = RoomError.None;
            guest.Client.Closed += reason => closed = reason;

            room.HostMember.Client.LeaveAsync(CancellationToken.None);
            room.Network.Pump();

            Assert.AreEqual(RoomError.HostLeft, closed);
        }

        [Test]
        public void PlayToEnd_TwoPeopleTwoBots_EveryoneSeesOnlyTheirOwnHandAndTheSameResult()
        {
            var room = new RoomFixture();
            room.Join("p1", "Asha");
            room.StartMatch();

            room.PlayToEnd();

            Assert.IsTrue(room.Host.Table.IsOver);
            foreach (RoomFixture.Member m in room.Members)
            {
                Seat seat = m.Session.LocalSeat;
                Assert.AreEqual(SessionStatus.Ended, m.Session.Status, m.Name);
                Assert.IsInstanceOf<MatchStarted>(m.Events[0], m.Name);
                Assert.IsInstanceOf<MatchEnded>(m.Events[m.Events.Count - 1], m.Name);
                int hands = 0;
                foreach (MatchEvent e in m.Events)
                {
                    Assert.IsTrue(e.Visibility.IsVisibleTo(seat), m.Name + " must never receive " + e);
                    if (e is HandDealt dealt)
                    {
                        Assert.AreEqual(seat, dealt.Seat);
                        hands++;
                    }
                }

                Assert.GreaterOrEqual(hands, 5, "one hand per round");
                Assert.AreEqual(room.Host.Table.ViewFor(seat), m.Session.View, m.Name + " ends with the host's view of its seat");
                Assert.AreEqual(1, m.Stats.Current.gamesPlayed, m.Name);
                Assert.AreEqual(1, m.Settings.CompletedMatches, m.Name);
            }

            Assert.AreEqual(
                room.Members[0].Session.View.FinalStandings[0].Seat,
                room.Members[1].Session.View.FinalStandings[0].Seat,
                "both agree on the winner");
        }

        [Test]
        public void PlayToEnd_TheRoomIsALobbyAgainWithReadyFlagsReset()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();
            room.PlayToEnd();
            room.Step();

            Assert.IsFalse(room.Host.InMatch);
            Assert.IsFalse(guest.Client.InMatch);
            Assert.IsTrue(guest.Client.Seats[1].Occupied);
            Assert.IsFalse(guest.Client.Seats[1].IsReady);
            Assert.IsFalse(guest.Client.Seats[2].Occupied, "the bots are gone");
        }

        [Test]
        public void PlayCard_OutOfTurn_IsRejectedToTheSenderAndNothingChanges()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();
            RoomFixture.Member waiting = WaitForAPlayTurnOfSomeoneElse(room, guest);

            PlayerView before = room.Host.Table.ViewFor(waiting.Session.LocalSeat);
            var task = waiting.Session.PlayCardAsync(waiting.Session.View.Hand.Lowest(), CancellationToken.None);
            room.Network.Pump();

            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.Result.IsAccepted);
            Assert.AreEqual(before, room.Host.Table.ViewFor(waiting.Session.LocalSeat));
        }

        [Test]
        public void PlayCard_Legal_TaskCompletesAcceptedWhenTheCardComesBack()
        {
            var room = new RoomFixture();
            room.Join("p1", "Asha");
            room.StartMatch();
            RoomFixture.Member mover = WaitForAPlayTurnOfAPerson(room);

            var task = mover.Session.PlayCardAsync(mover.Session.View.LegalCards().Lowest(), CancellationToken.None);
            Assert.IsFalse(task.IsCompleted && mover != room.HostMember, "a guest's play waits for the host");
            room.Network.Pump();

            Assert.IsTrue(task.IsCompleted);
            Assert.IsTrue(task.Result.IsAccepted);
        }

        [Test]
        public void Timeout_OnePlayer_AutoActsAndTheSecondInARowMarksThemAway()
        {
            var room = new RoomFixture(turnSeconds: 15);
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();
            Seat guestSeat = guest.Session.LocalSeat;

            // The host always plays in time; the guest never does.
            int autoMoves = 0;
            guest.Session.EventReceived += e =>
            {
                if ((e is CallPlaced call && call.Seat == guestSeat && call.IsAuto)
                    || (e is CardPlayed played && played.Seat == guestSeat && played.IsAuto && !played.IsForced))
                {
                    autoMoves++;
                }
            };
            for (int i = 0; i < 400 && room.HostMember.Session.Seats[(int)guestSeat].Connection != SeatConnection.Away; i++)
            {
                room.Step(1.0);
                RoomFixture.Act(room.HostMember.Session);
                room.Network.Pump();
            }

            Assert.AreEqual(SeatConnection.Away, room.HostMember.Session.Seats[(int)guestSeat].Connection, "two timeouts in a row");
            Assert.AreEqual(SeatConnection.Away, guest.Session.Seats[(int)guestSeat].Connection);
            Assert.AreEqual(2, autoMoves, "each timeout played one auto move");

            guest.Session.ReportPresence();
            room.Network.Pump();

            Assert.AreEqual(SeatConnection.Connected, room.HostMember.Session.Seats[(int)guestSeat].Connection, "I'm back");
        }

        [Test]
        public void Timer_PersonsTurn_ClientsSeeTheClockAndBotsHaveNone()
        {
            var room = new RoomFixture(turnSeconds: 30);
            room.Join("p1", "Asha");
            room.StartMatch();
            RoomFixture.Member mover = WaitForATurnOfAPerson(room);

            TurnTimerState timer = room.HostMember.Session.Timer;

            Assert.IsTrue(timer.IsRunning);
            Assert.AreEqual(mover.Session.LocalSeat, timer.Seat);
            Assert.AreEqual(30.0, timer.Duration, 0.001);
            Assert.That(timer.Deadline - room.Now, Is.InRange(28.0, 30.0));
        }

        [Test]
        public void Disconnect_Guest_BotCoversTheSeatAndRejoinGivesControlBack()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();
            Seat guestSeat = guest.Session.LocalSeat;
            for (int i = 0; i < 20; i++)
            {
                room.Step();
                RoomFixture.Act(room.HostMember.Session);
                RoomFixture.Act(guest.Session);
                room.Network.Pump();
            }

            room.Network.Drop(guest.Link);
            room.Network.Pump();
            Assert.AreEqual(SessionStatus.Reconnecting, guest.Session.Status);
            Assert.AreEqual(SeatConnection.Reconnecting, room.HostMember.Session.Seats[(int)guestSeat].Connection);

            // The table keeps moving without the guest.
            int tricksBefore = room.Host.Table.ViewFor(Seat.South).CompletedTricks.Count + (room.Host.Table.ViewFor(Seat.South).Round * 100);
            for (int i = 0; i < 40; i++)
            {
                room.Step();
                RoomFixture.Act(room.HostMember.Session);
                room.Network.Pump();
            }

            PlayerView hostView = room.Host.Table.ViewFor(Seat.South);
            Assert.Greater(hostView.CompletedTricks.Count + (hostView.Round * 100), tricksBefore, "a bot covered the guest's turns");

            Assert.AreEqual(RoomError.None, room.Rejoin(guest));
            room.Step();

            Assert.AreEqual(SessionStatus.Running, guest.Session.Status);
            Assert.GreaterOrEqual(guest.Resyncs, 2, "the first state and the one after the rejoin");
            Assert.AreEqual(room.Host.Table.ViewFor(guestSeat), guest.Session.View);
            Assert.AreEqual(SeatConnection.Connected, room.HostMember.Session.Seats[(int)guestSeat].Connection);
            room.PlayToEnd();
            Assert.AreEqual(SessionStatus.Ended, guest.Session.Status);
        }

        [Test]
        public void Disconnect_GoneForNinetySeconds_TheSeatIsABotForGood()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();
            Seat guestSeat = guest.Session.LocalSeat;

            room.Network.Drop(guest.Link);
            room.Network.Pump();
            room.Step(Protocol.SeatHoldSeconds + 1.0);

            Assert.AreEqual(SeatConnection.BotControlled, room.HostMember.Session.Seats[(int)guestSeat].Connection);
            Assert.AreEqual(RoomError.AlreadyStarted, room.Rejoin(guest));
            room.Step();
            Assert.AreEqual(SeatConnection.BotControlled, room.HostMember.Session.Seats[(int)guestSeat].Connection, "too late to come back");
        }

        [Test]
        public void LostUpdate_Guest_AsksForTheStateAndCatchesUp()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();
            int resyncsBefore = guest.Resyncs;

            room.Network.LoseNext(guest.Link);
            for (int i = 0; i < 12; i++)
            {
                room.Step();
                RoomFixture.Act(room.HostMember.Session);
                RoomFixture.Act(guest.Session);
                room.Network.Pump();
            }

            Assert.Greater(guest.Resyncs, resyncsBefore, "the gap triggered a resync");
            Assert.AreEqual(room.Host.Table.ViewFor(guest.Session.LocalSeat), guest.Session.View);
            room.PlayToEnd();
            Assert.AreEqual(SessionStatus.Ended, guest.Session.Status);
        }

        [Test]
        public void LeaveMatch_Guest_CountsAsALossAndABotFinishesTheSeat()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();
            Seat guestSeat = guest.Session.LocalSeat;
            for (int i = 0; i < 10; i++)
            {
                room.Step();
                RoomFixture.Act(room.HostMember.Session);
                RoomFixture.Act(guest.Session);
                room.Network.Pump();
            }

            guest.Session.LeaveAsync(CancellationToken.None);
            room.Network.Pump();

            Assert.AreEqual(SessionStatus.Ended, guest.Session.Status);
            Assert.IsTrue(guest.Client.IsClosed);
            Assert.AreEqual(1, guest.Stats.Current.gamesPlayed);
            Assert.AreEqual(0, guest.Stats.Current.wins);
            Assert.AreEqual(SeatConnection.Left, room.HostMember.Session.Seats[(int)guestSeat].Connection);
            room.PlayToEnd();
            Assert.AreEqual(SessionStatus.Ended, room.HostMember.Session.Status);
        }

        [Test]
        public void LeaveMatch_Host_EndsItForTheGuests()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();

            room.HostMember.Session.LeaveAsync(CancellationToken.None);
            room.Network.Pump();

            Assert.AreEqual(SessionStatus.Failed, guest.Session.Status);
            Assert.AreEqual(RoomError.HostLeft, guest.Session.FailureReason, "the table can say why");
            Assert.IsTrue(guest.Client.IsClosed);
        }

        [Test]
        public void Emote_Sent_ReachesEveryoneAndIsRateLimited()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();

            guest.Session.SendEmoteAsync(3, CancellationToken.None);
            guest.Session.SendEmoteAsync(4, CancellationToken.None);
            room.Network.Pump();

            int expected = ((int)guest.Session.LocalSeat * 100) + 3;
            CollectionAssert.AreEqual(new[] { expected }, room.HostMember.Emotes, "the second one came too soon");
            CollectionAssert.AreEqual(new[] { expected }, guest.Emotes);

            room.Step(Protocol.EmoteIntervalSeconds + 0.1);
            guest.Session.SendEmoteAsync(4, CancellationToken.None);
            room.Network.Pump();

            Assert.AreEqual(2, room.HostMember.Emotes.Count);
        }

        [Test]
        public void BotsThatFillTheTable_SayAQuickPhraseNowAndThen_FromTheirOwnSeats()
        {
            // ADR 026: over a whole match the bots speak a few times, never for a person, and everyone hears the same.
            var room = new RoomFixture(botsChat: true);
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();
            room.PlayToEnd();

            var people = new HashSet<int> { (int)room.HostMember.Session.LocalSeat, (int)guest.Session.LocalSeat };
            Assert.Greater(guest.Emotes.Count, 0, "the bots said something in five rounds");
            foreach (int emote in guest.Emotes)
            {
                Assert.IsFalse(people.Contains(emote / 100), "only the bots' seats speak: " + emote);
                Assert.Less(emote % 100, TableHost.EmoteCount);
            }

            CollectionAssert.AreEqual(room.HostMember.Emotes, guest.Emotes, "everyone hears the same phrases");
        }

        [Test]
        public void Flood_FromOneConnection_IsDroppedWithoutHurtingTheTable()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            room.StartMatch();
            PlayerView before = room.Host.Table.ViewFor(Seat.South);

            for (int i = 0; i < 200; i++)
            {
                guest.Link.Send(new byte[] { 200, 1, 2, 3 });
                guest.Link.Send(ClientMessages.RequestResync(0));
            }

            room.Network.Pump();

            Assert.AreEqual(before, room.Host.Table.ViewFor(Seat.South));
            Assert.Less(guest.Resyncs, 20, "most of the flood was dropped");
        }

        private static RoomFixture.Member WaitForATurnOfAPerson(RoomFixture room)
        {
            for (int i = 0; i < 2000; i++)
            {
                foreach (RoomFixture.Member m in room.Members)
                {
                    if (m.Session.View.IsMyTurn)
                    {
                        return m;
                    }
                }

                room.Step();
            }

            Assert.Fail("no person ever got a turn");
            return null;
        }

        private static RoomFixture.Member WaitForAPlayTurnOfAPerson(RoomFixture room)
        {
            for (int i = 0; i < 2000; i++)
            {
                foreach (RoomFixture.Member m in room.Members)
                {
                    if (m.Session.View.IsMyTurn && m.Session.View.Phase == Phase.Playing)
                    {
                        return m;
                    }
                }

                room.Step();
                foreach (RoomFixture.Member m in room.Members)
                {
                    if (m.Session.View.Phase == Phase.Calling)
                    {
                        RoomFixture.Act(m.Session);
                    }
                }

                room.Network.Pump();
            }

            Assert.Fail("no person ever got a play turn");
            return null;
        }

        /// <summary>Plays until someone other than <paramref name="other"/> holds a play turn, and returns <paramref name="other"/>.</summary>
        private static RoomFixture.Member WaitForAPlayTurnOfSomeoneElse(RoomFixture room, RoomFixture.Member other)
        {
            for (int i = 0; i < 2000; i++)
            {
                PlayerView view = other.Session.View;
                if (view.Phase == Phase.Playing && !view.CurrentTurn.IsNone && view.CurrentSeat != other.Session.LocalSeat && view.Hand.Count > 1)
                {
                    return other;
                }

                room.Step();
                foreach (RoomFixture.Member m in room.Members)
                {
                    if (m == other || m.Session.View.Phase == Phase.Calling)
                    {
                        RoomFixture.Act(m.Session);
                    }
                }

                room.Network.Pump();
            }

            Assert.Fail("the other seats never held a play turn");
            return null;
        }
    }
}
