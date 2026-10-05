using System.Threading;
using Callbreak.Application.Economy;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Profile;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// Stake Match (the owner, 5 Oct 2026; mockups 23 and 24) between real players, each on a device of
    /// its own with its own wallet, over the in-memory peer network and its table directory: players
    /// only ever meet players who chose the same stake, never Quick Match or the other stake; bots fill
    /// as in Quick Match; the stake is taken once, when the match begins, and never by a search that
    /// ends without one; the end pays each person the prize of their place; leaving loses the stake;
    /// and coins spent during the search leave the match unstaked.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        [Test]
        public void StakeMatch_FiftyHundredAndQuickTapTogether_OnlyTheSameStakeMeets()
        {
            UseQuickMatch();
            StakePlayer hari = NewStakePlayer("Hari", 50);
            StakePlayer asha = NewStakePlayer("Asha", 100);
            StakePlayer suman = NewStakePlayer("Suman", StakeRules.None);
            StakePlayer bikash = NewStakePlayer("Bikash", 50);

            Tap(hari.Quick);
            Tap(asha.Quick);
            Tap(suman.Quick);
            Tap(bikash.Quick);
            SettleQuick(2);
            Assert.AreEqual(50, _peers.QuickStakeOf(hari.Quick.TableCode), "each lists its table for its own stake");
            Assert.AreEqual(100, _peers.QuickStakeOf(asha.Quick.TableCode));
            Assert.AreEqual(0, _peers.QuickStakeOf(suman.Quick.TableCode), "Quick Match's own");
            SettleQuickUntil(() => _quick.TrueForAll(p => p.Search.IsCompleted), 30);

            foreach (QuickPlayer p in _quick)
            {
                Assert.IsTrue(p.Search.Result.Found, p.Name + " plays");
            }

            Assert.AreEqual(hari.Match.View.MatchId, bikash.Match.View.MatchId, "the two who chose 50 meet");
            Assert.AreEqual(2, Bots(hari.Match), "two people and two bots");
            Assert.AreNotEqual(hari.Match.View.MatchId, asha.Match.View.MatchId, "never a 100 player");
            Assert.AreNotEqual(hari.Match.View.MatchId, suman.Match.View.MatchId, "never a Quick Match player");
            Assert.AreNotEqual(asha.Match.View.MatchId, suman.Match.View.MatchId);
            Assert.AreEqual(3, Bots(asha.Match), "nobody else chose 100: bots fill her table");
            Assert.AreEqual(3, Bots(suman.Match), "nobody else chose Quick Match");
            Assert.AreEqual(450, hari.Wallet.Coins);
            Assert.AreEqual(450, bikash.Wallet.Coins);
            Assert.AreEqual(400, asha.Wallet.Coins);
            Assert.AreEqual(500, suman.Wallet.Coins, "Quick Match is free");
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void StakeMatch_TheStakeIsTakenOnce_AsTheMatchBegins_NotDuringTheSearch()
        {
            UseQuickMatch();
            StakePlayer hari = NewStakePlayer("Hari", 50);

            Tap(hari.Quick);
            SettleQuick(12);
            Assert.AreEqual(500, hari.Wallet.Coins, "nothing is taken while the search runs");
            SettleQuickUntil(() => hari.Quick.Search.IsCompleted, 30);

            Assert.IsTrue(hari.Quick.Search.Result.Found);
            Assert.AreEqual(3, Bots(hari.Match), "bots fill as in Quick Match");
            Assert.AreEqual(450, hari.Wallet.Coins, "taken as the match began");
            Assert.IsTrue(hari.Stakes.TryGetStake(hari.Match.View.MatchId, out StakedMatch staked));
            Assert.AreEqual(50, staked.Stake);
            Assert.AreEqual(StakeState.Taken, staked.State);

            hari.Match.Start();
            PlayQuick(60);
            Assert.AreEqual(450, hari.Wallet.Coins, "and only once");
        }

        [Test]
        public void StakeMatch_PlayedToTheEnd_EachPersonIsPaidThePrizeOfTheirPlace_BotsNothing()
        {
            UseQuickMatch();
            StakePlayer hari = NewStakePlayer("Hari", 100);
            StakePlayer asha = NewStakePlayer("Asha", 100);
            Tap(hari.Quick);
            Tap(asha.Quick);
            SettleQuickUntil(() => hari.Quick.Search.IsCompleted && asha.Quick.Search.IsCompleted, 30);
            Assert.AreEqual(hari.Match.View.MatchId, asha.Match.View.MatchId);
            Assert.AreEqual(2, Bots(hari.Match));

            hari.Match.Start();
            asha.Match.Start();
            PlayQuick(20000);

            foreach (StakePlayer p in new[] { hari, asha })
            {
                Assert.AreEqual(SessionStatus.Ended, p.Match.Status, p.Name);
                int place = PlaceOf(p.Match);
                Assert.AreEqual(400 + StakeRules.PrizeFor(100, place), p.Wallet.Coins, p.Name + " finished " + place + ", bots included");
                Assert.IsTrue(p.Stakes.TryGetStake(p.Match.View.MatchId, out StakedMatch staked));
                Assert.AreEqual(StakeState.Paid, staked.State);
                Assert.AreEqual(place, staked.Place);
            }

            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void StakeMatch_CancelledBeforeTheStart_TakesNothing()
        {
            UseQuickMatch();
            StakePlayer hari = NewStakePlayer("Hari", 100);
            Tap(hari.Quick);
            SettleQuick(15);

            hari.Quick.Cancel.Cancel();
            SettleQuick(10);

            Assert.AreEqual(MatchmakingError.Cancelled, hari.Quick.Search.Result.Error);
            Assert.AreEqual(500, hari.Wallet.Coins);
        }

        [Test]
        public void StakeMatch_HostLeavesBeforeTheStart_TheGuestPaysOnlyForTheMatchItPlays()
        {
            UseQuickMatch();
            StakePlayer hari = NewStakePlayer("Hari", 50);
            StakePlayer asha = NewStakePlayer("Asha", 50);
            Tap(hari.Quick);
            SettleQuick(2);
            Tap(asha.Quick);
            SettleQuickUntil(() => SeatsTaken(hari.Quick) == 2, 14);
            Assert.AreEqual(2, SeatsTaken(asha.Quick), "Asha sits at Hari's table");

            hari.Quick.Cancel.Cancel();
            SettleQuickUntil(() => asha.Quick.Search.IsCompleted, 30);

            Assert.IsTrue(asha.Quick.Search.Result.Found, "Asha still plays");
            Assert.AreEqual(500, hari.Wallet.Coins, "Hari's table never started");
            Assert.AreEqual(450, asha.Wallet.Coins, "Asha's stake once, for the match she plays");
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void StakeMatch_LeftDuringTheMatch_LosesTheStake_AndWinsNothingAtTheEnd()
        {
            UseQuickMatch();
            StakePlayer hari = NewStakePlayer("Hari", 50);
            StakePlayer asha = NewStakePlayer("Asha", 50);
            Tap(hari.Quick);
            Tap(asha.Quick);
            SettleQuickUntil(() => hari.Quick.Search.IsCompleted && asha.Quick.Search.IsCompleted, 30);
            QuickPlayer host = HostOf(hari.Quick, asha.Quick);
            StakePlayer guest = host == hari.Quick ? asha : hari;
            StakePlayer stays = host == hari.Quick ? hari : asha;
            hari.Match.Start();
            asha.Match.Start();
            PlayQuick(40);
            string matchId = guest.Match.View.MatchId;

            guest.Match.LeaveAsync(CancellationToken.None);
            PlayQuick(20000);

            Assert.AreEqual(SessionStatus.Ended, stays.Match.Status, "the match went on to its end");
            Assert.AreEqual(450, guest.Wallet.Coins, "the stake is lost and nothing is won");
            Assert.IsTrue(guest.Stakes.TryGetStake(matchId, out StakedMatch staked));
            Assert.AreEqual(StakeState.Forfeited, staked.State);
            Assert.AreEqual(450 + StakeRules.PrizeFor(50, PlaceOf(stays.Match)), stays.Wallet.Coins, "the one who stayed is paid by place");
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void StakeMatch_CoinsSpentDuringTheSearch_TheMatchIsNotStaked()
        {
            UseQuickMatch();
            StakePlayer hari = NewStakePlayer("Hari", 100);
            Tap(hari.Quick);
            SettleQuick(5);

            // The matchmaking screen's shop: a table bought while the search runs.
            Assert.IsTrue(hari.Wallet.TrySpend(450));
            SettleQuickUntil(() => hari.Quick.Search.IsCompleted, 30);

            Assert.IsTrue(hari.Quick.Search.Result.Found, "the match goes ahead");
            Assert.AreEqual(50, hari.Wallet.Coins, "nothing taken");
            Assert.IsFalse(hari.Stakes.TryGetStake(hari.Match.View.MatchId, out _), "the match isn't staked");
            hari.Match.Start();
            PlayQuick(20000);
            Assert.AreEqual(SessionStatus.Ended, hari.Match.Status);
            Assert.AreEqual(50, hari.Wallet.Coins, "and it wins nothing");
        }

        [Test]
        public void StakeMatch_NoTableDirectory_PlaysWithBotsAtATableOfItsOwn_AndStillStakes()
        {
            UseQuickMatch();
            StakePlayer hari = NewStakePlayer("Hari", 50);
            hari.Quick.Network.CanSignIn = false;

            Tap(hari.Quick);
            SettleQuickUntil(() => hari.Quick.Search.IsCompleted, 30);

            Assert.IsTrue(hari.Quick.Search.Result.Found);
            Assert.AreEqual(3, Bots(hari.Match));
            Assert.AreEqual(450, hari.Wallet.Coins);
            Assert.AreEqual(0, _peers.QuickTables, "never listed");
        }

        /// <summary>A player with a wallet of its own who chose <paramref name="stake"/> on Play Online (0: Quick Match).</summary>
        private StakePlayer NewStakePlayer(string name, int stake)
        {
            Device device = NewPeerDevice(name);
            var wallet = new Wallet(device.Saves);
            var stakes = new StakeLedger(device.Saves, wallet);
            var choice = new OnlineMatchChoice(device.Saves);
            choice.Choose(stake);
            ulong next = (ulong)(_quick.Count + 1) * 1000003UL;
            var service = new QuickMatchService(
                new QuickMatchConfig(), device.Profile, device.Cosmetics, device.Auth, new TestBots(), device.Stats, device.Settings, device.Network,
                new NameFilter(), () => next++ * 7919UL, BotTiming.Instant, (IQuickMatchTransport)device.Transport, _peerOf[device], () => UnixEpoch.AddSeconds(_now),
                choice, stakes);
            var player = new QuickPlayer(name, device, service, _peerOf[device], () => _now);
            _quick.Add(player);

            // In the app the service is ticked every frame from the start, so it knows the time when the player taps.
            service.Tick(_now);
            return new StakePlayer(player, wallet, stakes);
        }

        /// <summary>The place this player's seat finished in, bots included.</summary>
        private static int PlaceOf(IMatchSession match)
        {
            foreach (Standing standing in match.View.FinalStandings)
            {
                if (standing.Seat == match.LocalSeat)
                {
                    return standing.Rank;
                }
            }

            Assert.Fail("the match has no standing for " + match.LocalSeat);
            return 0;
        }

        /// <summary>A Quick Match player whose service plays Stake Match, with the coins it has.</summary>
        private sealed class StakePlayer
        {
            public StakePlayer(QuickPlayer quick, Wallet wallet, StakeLedger stakes)
            {
                Quick = quick;
                Wallet = wallet;
                Stakes = stakes;
            }

            public QuickPlayer Quick { get; }

            public Wallet Wallet { get; }

            public StakeLedger Stakes { get; }

            public string Name => Quick.Name;

            public IMatchSession Match => Quick.Match;
        }
    }
}
