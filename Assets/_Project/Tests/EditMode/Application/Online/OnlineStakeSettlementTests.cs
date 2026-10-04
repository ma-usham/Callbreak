using System.Collections.Generic;
using System.Threading;
using Callbreak.Application.Economy;
using Callbreak.Application.Online;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Events;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// How an online match settles a Stake Match's stake on this device (the owner, 5 Oct 2026): its
    /// end pays this player's place after Core's tie-breaks (a shared place as that place; a bot's
    /// place pays nobody), once, even when the end is seen again or by the app back after a kill;
    /// leaving, or not coming back in time, loses the stake; a match called off with nobody to take
    /// it over gives the stake back; and a match with no stake taken moves no coins.
    /// </summary>
    public sealed class OnlineStakeSettlementTests
    {
        private const int Stake = 50;

        private InMemorySaveStore _saves;
        private Wallet _wallet;
        private StakeLedger _stakes;

        [SetUp]
        public void SetUp()
        {
            _saves = new InMemorySaveStore();
            _wallet = new Wallet(_saves);
            _stakes = new StakeLedger(_saves, _wallet);
        }

        [Test]
        public void MatchEnds_ThisPlayersPlaceIsPaid_ABotAheadOfItWinsNothing()
        {
            OnlineMatchSession session = StakedSessionInPlay(out PlayerView view);
            Seat me = view.Viewer;
            Seat bot = me.Next(view.Rules.Direction);
            Assert.IsTrue(view.Seats[(int)bot].IsBot, "the seat after this player's is a bot's");

            End(session, view, Totals(me, 200, bot, 300));

            Assert.AreEqual(500 - Stake + 40, _wallet.Coins, "2nd behind the bot: the 2nd prize, and the bot's 1st prize goes to nobody");
            Assert.AreEqual(SessionStatus.Ended, session.Status);
            Assert.IsTrue(_stakes.TryGetStake(view.MatchId, out StakedMatch match));
            Assert.AreEqual(StakeState.Paid, match.State);
            Assert.AreEqual(2, match.Place);
        }

        [Test]
        public void MatchEnds_SharingFirstPlace_PaysTheFirstPrize()
        {
            OnlineMatchSession session = StakedSessionInPlay(out PlayerView view);
            Seat me = view.Viewer;
            Seat other = me.Next(view.Rules.Direction);

            End(session, view, Totals(me, 250, other, 250));

            Assert.AreEqual(500 - Stake + 100, _wallet.Coins, "tied 1st after every tie-break: the 1st prize");
        }

        [Test]
        public void TheEndSeenAgain_OrByTheAppBackAfterAKill_PaysOnce()
        {
            var room = new RoomFixture();
            room.StartMatch();
            room.PlayToEnd();
            PlayerView final = room.HostMember.Session.View;
            Assert.AreEqual(Phase.MatchOver, final.Phase);
            _stakes.TakeStake(final.MatchId, Stake);

            // The app was killed during the match and is back at it (a rejoin), just as it ended: a
            // new wallet and ledger read the files, and the new session is sent the finished table.
            var wallet = new Wallet(_saves);
            var stakes = new StakeLedger(_saves, wallet);
            OnlineMatchSession back = NewSession(final, stakes);
            back.Start();
            back.OnSync(new TableUpdate(40, null, final, NoClocks()));
            back.OnSync(new TableUpdate(41, null, final, NoClocks()));

            int place = PlaceOf(final.FinalStandings, final.Viewer);
            Assert.AreEqual(SessionStatus.Ended, back.Status);
            Assert.AreEqual(500 - Stake + StakeRules.PrizeFor(Stake, place), wallet.Coins, "paid for place " + place);

            // And the same end again on yet another session pays nothing more.
            OnlineMatchSession again = NewSession(final, new StakeLedger(_saves, wallet));
            again.Start();
            again.OnSync(new TableUpdate(42, null, final, NoClocks()));
            Assert.AreEqual(500 - Stake + StakeRules.PrizeFor(Stake, place), wallet.Coins);
        }

        [Test]
        public void LeavingTheMatch_LosesTheStake_AndItsEndPaysNothing()
        {
            OnlineMatchSession session = StakedSessionInPlay(out PlayerView view);

            session.LeaveAsync(CancellationToken.None);
            Assert.AreEqual(0, _stakes.PayPrize(view.MatchId, 1), "the end of a match left pays nothing");

            Assert.AreEqual(500 - Stake, _wallet.Coins);
            Assert.IsTrue(_stakes.TryGetStake(view.MatchId, out StakedMatch match));
            Assert.AreEqual(StakeState.Forfeited, match.State);
        }

        [Test]
        public void NotBackInTime_LosesTheStake()
        {
            OnlineMatchSession session = StakedSessionInPlay(out PlayerView view);

            session.OnRoomClosed(RoomError.ConnectionLost);

            Assert.AreEqual(500 - Stake, _wallet.Coins);
            Assert.IsTrue(_stakes.TryGetStake(view.MatchId, out StakedMatch match));
            Assert.AreEqual(StakeState.Forfeited, match.State);
        }

        [Test]
        public void CalledOff_TheHostLeftWithNobodyToTakeOver_GivesTheStakeBack()
        {
            OnlineMatchSession session = StakedSessionInPlay(out PlayerView view);

            session.OnRoomClosed(RoomError.HostLeft);

            Assert.AreEqual(500, _wallet.Coins, "nobody could take the match over: it isn't recorded, and the stake comes back");
            Assert.IsTrue(_stakes.TryGetStake(view.MatchId, out StakedMatch match));
            Assert.AreEqual(StakeState.Refunded, match.State);
        }

        [Test]
        public void CalledOff_TheHandOverGaveUpWhileThisDeviceWasOnline_GivesTheStakeBack()
        {
            OnlineMatchSession session = StakedSessionInPlay(out PlayerView view);

            // ADR 032: the device was online all along; it was left behind, not the one who dropped.
            session.OnRoomClosed(RoomError.ConnectionLost, countsAsLeft: false);

            Assert.AreEqual(500, _wallet.Coins);
            Assert.IsTrue(_stakes.TryGetStake(view.MatchId, out StakedMatch match));
            Assert.AreEqual(StakeState.Refunded, match.State);
        }

        [Test]
        public void ReplacedByTheSamePlayerElsewhere_LeavesTheStakeToTheMatchItPlaysOn()
        {
            OnlineMatchSession session = StakedSessionInPlay(out PlayerView view);

            session.OnRoomClosed(RoomError.Replaced);

            Assert.AreEqual(500 - Stake, _wallet.Coins);
            Assert.IsTrue(_stakes.TryGetStake(view.MatchId, out StakedMatch match));
            Assert.AreEqual(StakeState.Taken, match.State, "the other session settles it");
        }

        [Test]
        public void AMatchWithNoStakeTaken_MovesNoCoins()
        {
            // Quick Match, or a Stake Match whose wallet couldn't cover the stake as it began.
            OnlineMatchSession session = SessionInPlay(_stakes, out PlayerView view);

            End(session, view, Totals(view.Viewer, 300, view.Viewer.Next(view.Rules.Direction), 100));

            Assert.AreEqual(500, _wallet.Coins);
            Assert.IsFalse(_stakes.TryGetStake(view.MatchId, out _));
        }

        /// <summary>A session at a table whose first hand is dealt, this player's stake taken as it began.</summary>
        private OnlineMatchSession StakedSessionInPlay(out PlayerView view)
        {
            OnlineMatchSession session = SessionInPlay(_stakes, out view);
            Assert.IsTrue(_stakes.TakeStake(view.MatchId, Stake));
            Assert.AreEqual(500 - Stake, _wallet.Coins);
            return session;
        }

        /// <summary>A session following a private room's first hand (the host's seat and three bots), started and in sync.</summary>
        private static OnlineMatchSession SessionInPlay(IStakeLedger stakes, out PlayerView view)
        {
            var room = new RoomFixture();
            room.StartMatch();
            view = room.HostMember.Session.View;
            Assert.AreNotEqual(Phase.Lobby, view.Phase, "dealt");
            OnlineMatchSession session = NewSession(view, stakes);
            session.Start();
            session.OnSync(new TableUpdate(1, null, view, NoClocks()));
            Assert.AreEqual(SessionStatus.Running, session.Status);
            return session;
        }

        private static OnlineMatchSession NewSession(PlayerView view, IStakeLedger stakes)
        {
            var saves = new InMemorySaveStore();
            var sent = new List<byte[]>();
            return new OnlineMatchSession(MatchMode.QuickMatch, view, NoClocks(), sent.Add, new StatsService(saves), new SettingsService(saves), stakes: stakes);
        }

        /// <summary>The host's next update ends the match with <paramref name="standings"/>.</summary>
        private static void End(OnlineMatchSession session, PlayerView view, IReadOnlyList<Standing> standings)
        {
            session.OnBatch(new TableUpdate(2, new MatchEvent[] { new MatchEnded(standings) }, view, NoClocks()));
        }

        /// <summary>Standings where the two seats named have those totals and the others less; level seats share their place.</summary>
        private static IReadOnlyList<Standing> Totals(Seat a, int totalA, Seat b, int totalB)
        {
            var totals = new[] { 30, 20, 10, 0 };
            totals[(int)a] = totalA;
            totals[(int)b] = totalB;
            return Standings.Compute(totals, new[] { 1, 1, 1, 1 }, new[] { 10, 10, 10, 10 });
        }

        private static int PlaceOf(IReadOnlyList<Standing> standings, Seat seat)
        {
            foreach (Standing standing in standings)
            {
                if (standing.Seat == seat)
                {
                    return standing.Rank;
                }
            }

            Assert.Fail("no standing for " + seat);
            return 0;
        }

        private static TableStatus NoClocks() => new TableStatus(Seat.South, TableStatus.NoClock, 0, TableStatus.NoClock, null);
    }
}
