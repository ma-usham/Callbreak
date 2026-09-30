using System;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §6 "Timing defaults": Quick Match gives 20 s to call and 15 s to play a card, a
    /// private room the host's choice for both; hosted bots think 0.8-1.6 s; the round results
    /// count down 10 s in Quick Match and 15 s in a private room.
    /// </summary>
    public sealed class TableClockTests
    {
        private const double StepSeconds = 0.25;

        [Test]
        public void Clocks_QuickMatch_TwentyToCall_FifteenToPlay_TenOfRoundResults()
        {
            Assert.AreEqual(20, TableClocks.QuickMatch.CallSeconds);
            Assert.AreEqual(15, TableClocks.QuickMatch.PlaySeconds);
            Assert.AreEqual(10.0, TableClocks.QuickMatch.SummarySeconds);
        }

        [TestCase(15)]
        [TestCase(30)]
        public void Clocks_PrivateRoom_TheHostsChoiceForBothTurns_FifteenOfRoundResults(int turnSeconds)
        {
            TableClocks clocks = TableClocks.PrivateRoom(turnSeconds);

            Assert.AreEqual(turnSeconds, clocks.CallSeconds);
            Assert.AreEqual(turnSeconds, clocks.PlaySeconds);
            Assert.AreEqual(15.0, clocks.SummarySeconds);
        }

        [Test]
        public void Clocks_NothingBelowZero_MeansNoClock()
        {
            var clocks = new TableClocks(-5, -1, -2.0);

            Assert.AreEqual(0, clocks.CallSeconds);
            Assert.AreEqual(0, clocks.PlaySeconds);
            Assert.AreEqual(0.0, clocks.SummarySeconds);
        }

        [Test]
        public void QuickMatchConfig_Default_HasTheQuickMatchClocks()
        {
            Assert.AreSame(TableClocks.QuickMatch, new QuickMatchConfig().Clocks);

            var tuned = new TableClocks(25, 12, 8.0);
            Assert.AreSame(tuned, new QuickMatchConfig(clocks: tuned).Clocks);
        }

        [Test]
        public void Setup_OneTurnTime_RunsBothKindsOfTurnOnIt_WithThePrivateRoomCountdown()
        {
            var setup = new TableHostSetup(RuleSet.NepaliStandard, "m", BotSeats(), new bool[4], 30, 1UL);

            Assert.AreEqual(30, setup.CallSeconds);
            Assert.AreEqual(30, setup.PlaySeconds);
            Assert.AreEqual(15.0, setup.SummarySeconds);

            var tuned = new TableHostSetup(RuleSet.NepaliStandard, "m", BotSeats(), new bool[4], 15, 1UL, summarySeconds: 7.0);

            Assert.AreEqual(15, tuned.CallSeconds);
            Assert.AreEqual(15, tuned.PlaySeconds);
            Assert.AreEqual(7.0, tuned.SummarySeconds);
        }

        [Test]
        public void Setup_Clocks_KeepTheCallTimeAndTheCardTimeApart()
        {
            var setup = new TableHostSetup(RuleSet.NepaliStandard, "m", BotSeats(), new bool[4], TableClocks.QuickMatch, 1UL);

            Assert.AreSame(TableClocks.QuickMatch, setup.Clocks);
            Assert.AreEqual(20, setup.CallSeconds);
            Assert.AreEqual(15, setup.PlaySeconds);
            Assert.AreEqual(10.0, setup.SummarySeconds);
        }

        [Test]
        public void Setup_NoTimingGiven_UsesTheHostedBotTiming()
        {
            Assert.AreSame(BotTiming.Hosted, new TableHostSetup(RuleSet.NepaliStandard, "m", BotSeats(), new bool[4], 0, 1UL).Timing);
            Assert.AreSame(BotTiming.Hosted, new TableHostSetup(RuleSet.NepaliStandard, "m", BotSeats(), new bool[4], TableClocks.QuickMatch, 1UL).Timing);
            Assert.AreSame(
                BotTiming.Instant,
                new TableHostSetup(RuleSet.NepaliStandard, "m", BotSeats(), new bool[4], 0, 1UL, timing: BotTiming.Instant).Timing);
        }

        [Test]
        public void BotTiming_Hosted_ThinksLongerThanOffline_AndIsOtherwiseTheSame()
        {
            Assert.AreEqual(0.8, BotTiming.Hosted.ThinkMinSeconds, 1e-9);
            Assert.AreEqual(1.6, BotTiming.Hosted.ThinkMaxSeconds, 1e-9);
            Assert.AreEqual(0.6, BotTiming.Offline.ThinkMinSeconds, 1e-9, "offline bots keep their 0.6-1.4 s");
            Assert.AreEqual(1.4, BotTiming.Offline.ThinkMaxSeconds, 1e-9);
            Assert.AreEqual(BotTiming.Offline.ForcedSeconds, BotTiming.Hosted.ForcedSeconds);
            Assert.AreEqual(BotTiming.Offline.TrickDisplaySeconds, BotTiming.Hosted.TrickDisplaySeconds);
            Assert.AreEqual(BotTiming.Offline.DealSeconds, BotTiming.Hosted.DealSeconds);
        }

        [Test]
        public void HostedTable_NoTimingGiven_TheFirstBotCallsPointEightToOnePointSixSecondsAfterTheDeal()
        {
            const double tick = 0.02;
            double deal = BotTiming.Hosted.DealSeconds;
            double earliest = double.MaxValue;
            double latest = 0.0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var table = new TableHost(new TableHostSetup(RuleSet.NepaliStandard, "bots-" + seed, BotSeats(), new bool[4], 0, seed), new TestBots());
                table.Start(0.0);
                double calledAt = -1.0;
                for (double now = tick; now < 6.0 && calledAt < 0.0; now += tick)
                {
                    table.Tick(now);
                    if (CallsPlaced(table.ViewFor(Seat.South)) > 0)
                    {
                        calledAt = now;
                    }
                }

                Assert.GreaterOrEqual(calledAt, 0.0, "a bot called, seed " + seed);
                earliest = Math.Min(earliest, calledAt);
                latest = Math.Max(latest, calledAt);
            }

            Assert.GreaterOrEqual(earliest, deal + 0.8, "no hosted bot thinks less than 0.8 s");
            Assert.LessOrEqual(latest, deal + 1.6 + tick, "no hosted bot thinks more than 1.6 s");
            Assert.Greater(latest, deal + 1.4, "some think longer than an offline bot ever does");
        }

        [Test]
        public void QuickMatch_Guest_SeesTwentySecondsToCallAndFifteenToPlayACard()
        {
            var room = new RoomFixture(clocks: TableClocks.QuickMatch);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();

            AssertClockOnTurn(room, guest, Phase.Calling, 20.0);
            AssertClockOnTurn(room, guest, Phase.Playing, 15.0);
        }

        [TestCase(15)]
        [TestCase(30)]
        public void PrivateRoom_Guest_SeesTheHostsChoiceOnBothKindsOfTurn(int turnSeconds)
        {
            var room = new RoomFixture(turnSeconds: turnSeconds);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();

            AssertClockOnTurn(room, guest, Phase.Calling, turnSeconds);
            AssertClockOnTurn(room, guest, Phase.Playing, turnSeconds);
        }

        [TestCase(true, 20, 15)]
        [TestCase(false, 30, 30)]
        public void Timeout_TheCallAndTheCardEachRunOutOnTheirOwnClock(bool quickMatch, int callSeconds, int playSeconds)
        {
            RoomFixture room = quickMatch ? new RoomFixture(clocks: TableClocks.QuickMatch) : new RoomFixture(turnSeconds: 30);
            room.StartMatch();
            IMatchSession session = room.HostMember.Session;
            Seat seat = session.LocalSeat;

            // The only person at the table never acts; the three bots do, at once.
            WaitFor(room, () => session.View.IsMyTurn && session.View.Phase == Phase.Calling);
            room.Step(callSeconds - 1.0);
            Assert.IsFalse(session.View.HasCalled(seat), "a second before the call timer ends nothing is done for the player");
            room.Step(2.0);
            Assert.IsTrue(session.View.HasCalled(seat), "when the call timer ends the call is placed for the player");

            WaitFor(room, () => session.View.IsMyTurn && session.View.Phase == Phase.Playing);
            int cards = session.View.Hand.Count;
            room.Step(playSeconds - 1.0);
            Assert.AreEqual(cards, session.View.Hand.Count, "a second before the card timer ends nothing is played for the player");
            room.Step(2.0);
            Assert.AreEqual(cards - 1, session.View.Hand.Count, "when the card timer ends a card is played for the player");
        }

        [TestCase(true, 10.0)]
        [TestCase(false, 15.0)]
        public void RoundResults_TheNextRoundStartsByItselfAfterTheModesCountdown(bool quickMatch, double seconds)
        {
            RoomFixture room = quickMatch ? new RoomFixture(clocks: TableClocks.QuickMatch) : new RoomFixture(turnSeconds: 30);
            RoomFixture.Member guest = room.Join("player-asha", "Asha");
            room.StartMatch();

            // Both people play their turns but never say they are ready for the next round.
            PlayUntil(room, () => guest.Session.View.Phase == Phase.RoundSummary);
            TurnTimerState countdown = guest.Session.Timer;

            Assert.IsTrue(countdown.IsRunning, "the round results count down");
            Assert.AreEqual(seconds, countdown.Duration, 0.001, "the host says how long the countdown is");
            Assert.That(countdown.Deadline - room.Now, Is.InRange(seconds - StepSeconds - 0.05, seconds + 0.001));

            room.Step(countdown.Deadline - room.Now - 0.5);
            Assert.AreEqual(Phase.RoundSummary, guest.Session.View.Phase, "half a second before the end the results still show");
            Assert.AreEqual(1, guest.Session.View.Round);

            room.Step(1.0);
            Assert.AreEqual(2, guest.Session.View.Round, "the next round was dealt without anyone saying ready");
        }

        [Test]
        public void HostedPacing_AWholeQuickMatch_PlaysThroughWithoutPlayingForAnyoneWhoIsInTime()
        {
            // The real delays together: a 1.6 s deal, bots that think, tricks that stay on the
            // table, and the two turn clocks, which only start once the table has caught up.
            var room = new RoomFixture(timing: BotTiming.Hosted, clocks: TableClocks.QuickMatch);
            room.Join("player-asha", "Asha");
            room.StartMatch();

            room.PlayToEnd();

            Assert.IsTrue(room.Host.Table.IsOver, "the match finished");
            foreach (RoomFixture.Member m in room.Members)
            {
                Seat seat = m.Session.LocalSeat;
                Assert.AreEqual(SessionStatus.Ended, m.Session.Status, m.Name);
                foreach (MatchEvent e in m.Events)
                {
                    bool autoCall = e is CallPlaced call && call.Seat == seat && call.IsAuto;
                    bool autoCard = e is CardPlayed played && played.Seat == seat && played.IsAuto && !played.IsForced;
                    Assert.IsFalse(autoCall || autoCard, m.Name + " was in time, yet the host played for them: " + e);
                }
            }
        }

        [Test]
        public void QuickMatchService_DefaultConfig_RunsItsTableOnTheQuickMatchClocks()
        {
            var saves = new InMemorySaveStore();
            var config = new QuickMatchConfig(botFillDelaySeconds: 1, seatRevealSeconds: 0.25, tableFoundSeconds: 0.5);
            var quick = new QuickMatchService(
                config, new ProfileService(saves, new NameFilter()), new CosmeticsService(saves), null, new TestBots(),
                new StatsService(saves), new SettingsService(saves), new AlwaysOnline(), new NameFilter(), () => 77UL, BotTiming.Instant);
            double now = 0.0;
            Task<MatchmakingResult> search = quick.FindMatchAsync(null, CancellationToken.None);
            while (!search.IsCompleted && now < 30.0)
            {
                now += StepSeconds;
                quick.Tick(now);
            }

            Assert.IsTrue(search.IsCompleted && search.Result.Found, "a table was found");
            IMatchSession session = quick.Match;
            session.Start();

            TurnTimerState call = TimerOnTurn(quick, session, Phase.Calling, ref now);
            TurnTimerState play = TimerOnTurn(quick, session, Phase.Playing, ref now);
            quick.Dispose();

            Assert.AreEqual(session.LocalSeat, call.Seat);
            Assert.AreEqual(20.0, call.Duration, 0.001, "20 s to call");
            Assert.AreEqual(15.0, play.Duration, 0.001, "15 s to play a card");
        }

        /// <summary>Plays on until the member holds a turn of the given kind, then checks the clock the member sees.</summary>
        private static void AssertClockOnTurn(RoomFixture room, RoomFixture.Member member, Phase phase, double seconds)
        {
            PlayUntil(room, () => member.Session.View.IsMyTurn && member.Session.View.Phase == phase);
            TurnTimerState timer = member.Session.Timer;
            string turn = phase == Phase.Calling ? "call turn" : "play turn";

            Assert.IsTrue(timer.IsRunning, "a clock runs on the " + turn);
            Assert.AreEqual(member.Session.LocalSeat, timer.Seat, "the clock is on the seat whose turn it is");
            Assert.AreEqual(seconds, timer.Duration, 0.001, "the length of the " + turn);
            Assert.That(timer.Deadline - room.Now, Is.InRange(seconds - StepSeconds - 0.05, seconds + 0.001), "the " + turn + " has only just started");
        }

        /// <summary>Every person plays its turns (call 2, lowest legal card) and nobody says ready, until <paramref name="reached"/>.</summary>
        private static void PlayUntil(RoomFixture room, Func<bool> reached)
        {
            for (int i = 0; i < 4000 && !reached(); i++)
            {
                foreach (RoomFixture.Member m in room.Members)
                {
                    ActOnTurn(m.Session);
                }

                room.Network.Pump();
                if (!reached())
                {
                    room.Step(StepSeconds);
                }
            }

            Assert.IsTrue(reached(), "the table got that far");
        }

        /// <summary>Time passes and nobody acts, until <paramref name="reached"/>.</summary>
        private static void WaitFor(RoomFixture room, Func<bool> reached)
        {
            for (int i = 0; i < 4000 && !reached(); i++)
            {
                room.Step(StepSeconds);
            }

            Assert.IsTrue(reached(), "the table got that far");
        }

        private static TurnTimerState TimerOnTurn(QuickMatchService quick, IMatchSession session, Phase phase, ref double now)
        {
            for (int i = 0; i < 4000; i++)
            {
                if (session.View.IsMyTurn && session.View.Phase == phase)
                {
                    return session.Timer;
                }

                ActOnTurn(session);
                now += StepSeconds;
                quick.Tick(now);
            }

            Assert.Fail("the player never held that kind of turn");
            return TurnTimerState.None;
        }

        private static void ActOnTurn(IMatchSession session)
        {
            if (session == null || session.Status != SessionStatus.Running || !session.View.IsMyTurn)
            {
                return;
            }

            PlayerView view = session.View;
            if (view.Phase == Phase.Calling)
            {
                session.PlaceCallAsync(2, CancellationToken.None);
            }
            else
            {
                session.PlayCardAsync(view.LegalCards().Lowest(), CancellationToken.None);
            }
        }

        private static int CallsPlaced(PlayerView view)
        {
            int calls = 0;
            for (int s = 0; s < SeatExtensions.Count; s++)
            {
                if (view.HasCalled((Seat)s))
                {
                    calls++;
                }
            }

            return calls;
        }

        private static SeatProfile[] BotSeats()
        {
            return new[]
            {
                new SeatProfile("Asha", "0", "classic-crimson", true),
                new SeatProfile("Bikash", "1", "himalayan-ivory", true),
                new SeatProfile("Suman", "2", "kathmandu-gold", true),
                new SeatProfile("Sita", "3", "classic-crimson", true),
            };
        }

        private sealed class AlwaysOnline : INetworkStatus
        {
            public event Action<bool> Changed
            {
                add { }
                remove { }
            }

            public bool IsOnline => true;
        }
    }
}
