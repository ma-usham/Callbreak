using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// Quick Match between real players (ADR 029), each on a device of its own, over the in-memory
    /// peer network and its table directory, where a new table shows in searches only after 8.6 s
    /// as on EOS: players who tap together or a few seconds apart end at one table, the oldest; bots
    /// fill what is left from 14 s, one by one; a full table locks, and every screen counts the same
    /// 3 s down to the start; a late player is turned away from a full table; a host who leaves sends
    /// its guests back to searching on their own clocks; and with no directory to use, a player
    /// still gets a table of its own.
    /// </summary>
    public sealed partial class RoomServiceTests
    {
        private static readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly List<QuickPlayer> _quick = new List<QuickPlayer>();

        [Test]
        public void QuickMatch_OnePlayer_BotsFillFrom14sOneByOneAndTheMatchStartsBy20s()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");

            Tap(hari);
            SettleQuickUntil(() => hari.Search.IsCompleted, 30, step: 0.05);

            Assert.IsTrue(hari.Search.Result.Found);
            List<double> bots = TimesSeatsFilled(hari);
            Assert.AreEqual(3, bots.Count, "three bots sat down");
            Assert.That(bots[0] - hari.TappedAt, Is.InRange(14.0, 14.1), "the first at 14 s");
            for (int i = 1; i < bots.Count; i++)
            {
                Assert.That(bots[i] - bots[i - 1], Is.InRange(0.8, 1.25), "about a second after the one before");
            }

            double locked = FirstTableFound(hari);
            Assert.AreEqual(bots[2], locked, 0.001, "the table locked as it filled");
            Assert.AreEqual(3.0, StartsInAt(hari, locked), 0.06, "a 3 s countdown");
            Assert.That(hari.FoundAt - hari.TappedAt, Is.InRange(locked - hari.TappedAt + 2.95, 20.06), "the match started when it ran out, by 20 s");
            Assert.AreEqual(3, Bots(hari.Match));
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void QuickMatch_TwoPlayersTapTogether_TheyMeetAtTheOlderTableBeforeTheBots()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");

            Tap(hari);
            Tap(asha);
            SettleQuickUntil(() => hari.Search.IsCompleted && asha.Search.IsCompleted, 30);

            Assert.IsTrue(hari.Search.Result.Found && asha.Search.Result.Found);
            Assert.AreEqual(hari.Match.View.MatchId, asha.Match.View.MatchId, "one table");
            Assert.AreEqual(2, Bots(hari.Match), "two people, two bots");
            Assert.Less(TimeSeatsFilled(hari, 2) - hari.TappedAt, 14.0, "they met before the bots came");
            AssertSeatsInTableOrder(hari);
            AssertSeatsInTableOrder(asha);
            Assert.AreEqual(0, _peers.QuickTables, "no table is left in the directory");
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void QuickMatch_ThreePlayersSecondsApart_AllEndAtTheOldestTable()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            QuickPlayer suman = NewQuickPlayer("Suman");

            Tap(hari);
            SettleQuick(4);
            Tap(asha);
            SettleQuick(5);
            Tap(suman);
            SettleQuickUntil(() => hari.Search.IsCompleted && asha.Search.IsCompleted && suman.Search.IsCompleted, 30);

            Assert.IsTrue(hari.Search.Result.Found && asha.Search.Result.Found && suman.Search.Result.Found);
            Assert.AreEqual(hari.Match.View.MatchId, asha.Match.View.MatchId);
            Assert.AreEqual(hari.Match.View.MatchId, suman.Match.View.MatchId, "the first player's table, the oldest");
            Assert.AreSame(hari, HostOf(hari, asha, suman), "the first player hosts it");
            Assert.AreEqual(1, Bots(hari.Match), "three people and one bot");
            AssertOwnCountdownNeverBelowTheTables(asha, hari);
            AssertOwnCountdownNeverBelowTheTables(suman, hari);
            AssertSameStart(hari, asha, suman);
            AssertOwnCountdown(hari);
            AssertOwnCountdown(asha);
            AssertOwnCountdown(suman);
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void QuickMatch_FourPlayersTapTogether_TheTableLocksAtOnceAndStartsWithoutBots()
        {
            UseQuickMatch();
            var players = new List<QuickPlayer> { NewQuickPlayer("Hari"), NewQuickPlayer("Asha"), NewQuickPlayer("Suman"), NewQuickPlayer("Bikash") };

            foreach (QuickPlayer p in players)
            {
                Tap(p);
            }

            SettleQuickUntil(() => players.TrueForAll(p => p.Search.IsCompleted), 30);

            foreach (QuickPlayer p in players)
            {
                Assert.IsTrue(p.Search.Result.Found, p.Name + " found the table");
                Assert.AreEqual(players[0].Match.View.MatchId, p.Match.View.MatchId, p.Name + " is at the one table");
                Assert.Less(FirstTableFound(p) - p.TappedAt, 14.0, p.Name + ": locked before any bot could come");
                AssertSeatsInTableOrder(p);
            }

            Assert.AreEqual(0, Bots(players[0].Match), "four people, no bot");
            AssertSameStart(players.ToArray());
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void QuickMatch_TwoRaceForTheLastSeat_TheOneTurnedAwayPlaysAtATableOfItsOwn()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            QuickPlayer suman = NewQuickPlayer("Suman");
            Tap(hari);
            Tap(asha);
            Tap(suman);
            SettleQuickUntil(() => SeatsTaken(hari) == 3 && SeatsTaken(asha) == 3 && SeatsTaken(suman) == 3, 14);
            QuickPlayer host = HostOf(hari, asha, suman);
            Assert.AreEqual(1, _peers.QuickTables, "one table, open for one more");
            Assert.AreEqual(1, _peers.QuickFreeSeats(host.TableCode));

            QuickPlayer bikash = NewQuickPlayer("Bikash");
            QuickPlayer gita = NewQuickPlayer("Gita");
            Tap(bikash);
            Tap(gita);
            SettleQuickUntil(() => _quick.TrueForAll(p => p.Search.IsCompleted), 40);

            int atTheTable = (bikash.Match.View.MatchId == hari.Match.View.MatchId ? 1 : 0) + (gita.Match.View.MatchId == hari.Match.View.MatchId ? 1 : 0);
            Assert.AreEqual(1, atTheTable, "one of them took the last seat");
            QuickPlayer late = bikash.Match.View.MatchId == hari.Match.View.MatchId ? gita : bikash;
            Assert.AreEqual(0, Bots(hari.Match), "the table filled with people");
            Assert.IsTrue(late.Search.Result.Found, "the other was turned away and still plays");
            Assert.AreEqual(3, Bots(late.Match), "at a table of its own");
            Assert.That(late.FoundAt - late.TappedAt, Is.LessThanOrEqualTo(20.3), "by its own 20 s");
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void QuickMatch_HostLeavesBeforeTheStart_TheGuestSearchesAgainAndStillStartsByItsOwn20s()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            Tap(hari);
            SettleQuick(2);
            Tap(asha);
            SettleQuickUntil(() => SeatsTaken(hari) == 2, 14);
            Assert.AreEqual(2, SeatsTaken(asha), "Asha sits at Hari's table");

            hari.Cancel.Cancel();
            Assert.IsTrue(hari.Search.IsCompleted, "Hari withdrew at once");
            Assert.AreEqual(MatchmakingError.Cancelled, hari.Search.Result.Error);
            SettleQuickUntil(() => asha.Search.IsCompleted, 30);

            Assert.IsTrue(asha.Search.Result.Found, "Asha still plays");
            Assert.AreEqual(3, Bots(asha.Match), "at a table of her own, filled with bots");
            Assert.That(asha.FoundAt - asha.TappedAt, Is.LessThanOrEqualTo(20.3), "by her own 20 s");
            Assert.That(asha.Reports.TrueForAll(r => r.Progress.SecondsLeft <= 20.0 - (r.At - asha.TappedAt) + 0.001), "her own countdown kept running");
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void QuickMatch_GuestCancels_ItsSeatIsFreeAgainAndTheHostPlaysOn()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            Tap(hari);
            Tap(asha);
            SettleQuickUntil(() => SeatsTaken(hari) == 2, 14);
            QuickPlayer host = HostOf(hari, asha);
            QuickPlayer guest = host == hari ? asha : hari;
            Assert.AreEqual(2, _peers.QuickFreeSeats(host.TableCode));

            guest.Cancel.Cancel();
            Assert.IsTrue(guest.Search.IsCompleted, "withdrawn at once");
            Assert.AreEqual(MatchmakingError.Cancelled, guest.Search.Result.Error);
            SettleQuick(1);
            Assert.AreEqual(1, SeatsTaken(host), "the seat is free again");
            Assert.AreEqual(3, _peers.QuickFreeSeats(host.TableCode), "and the directory says so");

            SettleQuickUntil(() => host.Search.IsCompleted, 30);
            Assert.IsTrue(host.Search.Result.Found);
            Assert.AreEqual(3, Bots(host.Match));
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void QuickMatch_HostCancels_WithdrawsAtOnceAndTheTableLeavesTheDirectory()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");
            Tap(hari);
            SettleQuick(3);
            Assert.AreEqual(1, _peers.QuickTables, "listed");

            hari.Cancel.Cancel();

            Assert.IsTrue(hari.Search.IsCompleted);
            Assert.AreEqual(MatchmakingError.Cancelled, hari.Search.Result.Error);
            Assert.AreEqual(0, _peers.QuickTables, "taken down at once");
            Assert.IsNull(hari.Service.Match);
            SettleQuick(20);
            Assert.IsNull(hari.Service.Match, "nothing keeps running after a cancel");
            Assert.AreEqual(0, _peers.Links);
        }

        [Test]
        public void QuickMatch_DeviceCannotSignIn_PlaysAtATableOfItsOwnAfterTheShortDelay()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");
            hari.Network.CanSignIn = false;

            Tap(hari);
            SettleQuickUntil(() => hari.Search.IsCompleted, 30);

            Assert.IsTrue(hari.Search.Result.Found);
            Assert.AreEqual(3, Bots(hari.Match));
            Assert.That(hari.FoundAt - hari.TappedAt, Is.LessThan(12.0), "after the short delay of a table of its own");
            Assert.IsTrue(double.IsNaN(hari.Reports[hari.Reports.Count - 1].Progress.SecondsLeft), "with no countdown of real players");
            Assert.AreEqual(0, _peers.QuickTables, "never listed");
        }

        [TestCase(RoomError.Offline)]
        [TestCase(RoomError.Unknown)]
        public void QuickMatch_DirectoryUnreachable_PlaysAtATableOfItsOwn(RoomError error)
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");
            hari.Network.DirectoryError = error;

            Tap(hari);
            SettleQuickUntil(() => hari.Search.IsCompleted, 30);

            Assert.IsTrue(hari.Search.Result.Found);
            Assert.AreEqual(3, Bots(hari.Match));
            Assert.That(hari.FoundAt - hari.TappedAt, Is.LessThan(12.0));
        }

        [Test]
        public void QuickMatch_GuestLosesTheConnectionDuringTheMatch_ReconnectsAndPlaysToTheEnd()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            Tap(hari);
            Tap(asha);
            SettleQuickUntil(() => hari.Search.IsCompleted && asha.Search.IsCompleted, 30);
            QuickPlayer host = HostOf(hari, asha);
            QuickPlayer guest = host == hari ? asha : hari;
            host.Match.Start();
            guest.Match.Start();
            PlayQuick(40);

            _peers.Break(host.Network, guest.Network);
            SettleQuick(0.5);
            Assert.AreEqual(SessionStatus.Reconnecting, guest.Match.Status);
            SettleQuickUntil(() => guest.Match.Status == SessionStatus.Running, 30);

            Assert.AreEqual(SessionStatus.Running, guest.Match.Status, "back at the table");
            PlayQuick(20000);
            Assert.AreEqual(SessionStatus.Ended, guest.Match.Status);
            Assert.AreEqual(SessionStatus.Ended, host.Match.Status);
            Assert.AreEqual(1, guest.Device.Stats.Current.gamesPlayed);
            Assert.That(_peerFaults, Is.Empty);
        }

        [Test]
        public void QuickMatch_HostWithARealGuest_NeverMovesEvenWhenAnOlderTableShows()
        {
            UseQuickMatch(listingDelay: 2.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            QuickPlayer suman = NewQuickPlayer("Suman");

            // Hari's table is the oldest, but the directory cannot take it for a while.
            Tap(hari);
            SettleQuickUntil(() => hari.Network.QuickSearches > 0, 2);
            hari.Network.DirectoryError = RoomError.Offline;
            SettleQuick(0.5);
            Tap(asha);
            SettleQuick(3.5);
            Tap(suman);
            SettleQuickUntil(() => SeatsTaken(asha) == 2, 5);
            Assert.AreSame(asha, HostOf(asha, suman), "Suman joined Asha's table, the only one showing");
            hari.Network.DirectoryError = RoomError.None;
            SettleQuickUntil(() => _quick.TrueForAll(p => p.Search.IsCompleted), 30);

            Assert.AreEqual(asha.Match.View.MatchId, suman.Match.View.MatchId, "Asha kept her table: she had a real guest");
            Assert.AreNotEqual(hari.Match.View.MatchId, asha.Match.View.MatchId);
            Assert.AreEqual(2, Bots(asha.Match));
            Assert.AreEqual(3, Bots(hari.Match), "Hari's older table filled with bots");
            AssertOwnCountdown(asha);
            AssertOwnCountdown(suman);
        }

        [Test]
        public void QuickMatch_TableWhoseHostVanished_TheJoinIsGivenUpAt14sAndBotsFillOnTime()
        {
            UseQuickMatch(listingDelay: 1.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            Tap(hari);
            SettleQuick(3);
            Assert.AreEqual(1, _peers.QuickTables);

            // Hari's app is killed: his table stays in the directory, and nobody answers at his address.
            _peers.Unplug(hari.Network);
            _quick.Remove(hari);
            _devices.Remove(hari.Device);
            QuickPlayer asha = NewQuickPlayer("Asha");
            Tap(asha);
            SettleQuickUntil(() => asha.Search.IsCompleted, 30);

            Assert.IsTrue(asha.Search.Result.Found);
            Assert.AreEqual(3, Bots(asha.Match), "at a table of her own");
            Assert.That(TimesSeatsFilled(asha)[0] - asha.TappedAt, Is.InRange(14.0, 14.6), "her bots came from her own 14 s");
            Assert.That(asha.FoundAt - asha.TappedAt, Is.LessThanOrEqualTo(20.3));
            AssertOwnCountdown(asha);
        }

        [Test]
        public void QuickMatch_FullTableStillShowsForAMoment_TheLatePlayerIsTurnedAwayAndPlaysOnItsOwnClock()
        {
            UseQuickMatch(listingDelay: 2.0);
            _peers.FreeSeatsDelaySeconds = 1.7;
            var four = new List<QuickPlayer> { NewQuickPlayer("Hari"), NewQuickPlayer("Asha"), NewQuickPlayer("Suman"), NewQuickPlayer("Bikash") };
            foreach (QuickPlayer p in four)
            {
                Tap(p);
            }

            SettleQuickUntil(() => four.TrueForAll(p => p.Reports.Exists(r => r.Progress.TableFound)), 14);
            QuickPlayer host = HostOf(four.ToArray());
            Assert.Greater(_peers.QuickFreeSeats(host.TableCode), 0, "the directory still shows a free seat");

            QuickPlayer gita = NewQuickPlayer("Gita");
            Tap(gita);
            SettleQuickUntil(() => _quick.TrueForAll(p => p.Search.IsCompleted), 40);

            Assert.IsTrue(SentOpen(gita, host), "Gita tried the full table");
            Assert.AreNotEqual(host.Match.View.MatchId, gita.Match.View.MatchId, "and was turned away");
            Assert.AreEqual(0, Bots(host.Match));
            Assert.AreEqual(3, Bots(gita.Match), "she plays at a table of her own");
            Assert.That(gita.FoundAt - gita.TappedAt, Is.LessThanOrEqualTo(20.3), "by her own 20 s");
            AssertOwnCountdown(gita);
        }

        [Test]
        public void QuickMatch_TableListingLost_ItIsListedAgainAndStillFound()
        {
            UseQuickMatch(listingDelay: 1.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            Tap(hari);
            SettleQuick(3);
            string code = hari.TableCode;
            Assert.IsNotNull(code);

            _peers.LoseQuickListing(code);
            Assert.AreEqual(0, _peers.QuickTables);
            SettleQuick(0.5);

            Assert.AreEqual(code, hari.TableCode, "listed again, under the same code");
            Assert.AreEqual(3, _peers.QuickFreeSeats(code));
            QuickPlayer asha = NewQuickPlayer("Asha");
            Tap(asha);
            SettleQuickUntil(() => hari.Search.IsCompleted && asha.Search.IsCompleted, 30);
            Assert.AreEqual(hari.Match.View.MatchId, asha.Match.View.MatchId, "Asha found it");
        }

        [Test]
        public void QuickMatch_WarmUp_SignsInAndMakesTheFirstDirectoryCallBeforeTheTap()
        {
            UseQuickMatch();
            QuickPlayer hari = NewQuickPlayer("Hari");

            hari.Service.WarmUp();
            hari.Service.WarmUp();
            SettleQuick(1);

            Assert.IsTrue(hari.Network.IsSignedIn, "signed in ahead of the tap");
            Assert.AreEqual(1, hari.Network.QuickSearches, "and the directory was called once, whatever it found");
            Tap(hari);
            SettleQuickUntil(() => hari.Search.IsCompleted, 30);
            Assert.IsTrue(hari.Search.Result.Found);
            int signIns = hari.Network.SignInCalls;
            hari.Service.WarmUp();
            Assert.AreEqual(signIns, hari.Network.SignInCalls, "warmed once: it is not done again");
        }

        [Test]
        public void QuickMatch_ATablesAge_IsItsHostsTapNotWhenItWasListed()
        {
            UseQuickMatch(listingDelay: 2.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            SettleQuick(2);

            Tap(hari);
            SettleQuick(3);

            Assert.IsNotNull(hari.TableCode, "listed");
            Assert.AreEqual(2000, _peers.QuickOpenedAt(hari.TableCode), "dated by the tap at 2 s, not by the listing after it");
        }

        [Test]
        public void QuickMatch_HostLeaves_TheGuestJoinsATableWhoseHostTappedEarlier()
        {
            UseQuickMatch(listingDelay: 2.0);
            QuickPlayer bikash = NewQuickPlayer("Bikash");
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");

            // Bikash taps first, but the directory cannot take his table until Asha sits at Hari's.
            Tap(bikash);
            SettleQuickUntil(() => bikash.Network.QuickSearches > 0, 2);
            bikash.Network.DirectoryError = RoomError.Offline;
            SettleQuick(0.5);
            Tap(hari);
            SettleQuick(0.5);
            Tap(asha);
            SettleQuickUntil(() => SeatsTaken(asha) == 2, 8);
            Assert.AreSame(hari, HostOf(hari, asha));
            bikash.Network.DirectoryError = RoomError.None;
            SettleQuickUntil(() => _peers.QuickOpenedAt(bikash.TableCode ?? string.Empty) == 0 && bikash.TableCode != null, 5);
            SettleQuick(2.5);

            hari.Cancel.Cancel();
            SettleQuickUntil(() => asha.Search.IsCompleted && bikash.Search.IsCompleted, 30);

            Assert.AreEqual(bikash.Match.View.MatchId, asha.Match.View.MatchId, "Asha went to the table whose host tapped before her");
            Assert.AreEqual(2, Bots(asha.Match));
            AssertOwnCountdownNeverBelowItsTables(asha);
            AssertOwnCountdown(asha);
        }

        [Test]
        public void QuickMatch_HostLeaves_OnlyANewerTableShows_TheGuestHostsItsOwnAndStartsByItsOwn20s()
        {
            UseQuickMatch(listingDelay: 2.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            Tap(hari);
            SettleQuick(2);
            Tap(asha);
            SettleQuickUntil(() => SeatsTaken(asha) == 2, 8);
            Assert.AreSame(hari, HostOf(hari, asha));

            // A table whose host tapped three seconds after Asha.
            InMemoryPeerNetwork.Device newer = ListBareTable("Q7NEWR", asha.TappedAt + 3.0);
            SettleQuick(2.5);
            hari.Cancel.Cancel();
            SettleQuick(1);
            Assert.AreEqual((long)Math.Round(asha.TappedAt * 1000.0), _peers.QuickOpenedAt(asha.TableCode ?? string.Empty), "Asha hosts a table dated by her own tap");
            SettleQuickUntil(() => asha.Search.IsCompleted, 30);

            Assert.IsFalse(SentOpenTo(asha, newer.Address), "she never sat down at the newer table");
            Assert.IsTrue(asha.Search.Result.Found);
            Assert.AreEqual(3, Bots(asha.Match));
            Assert.That(asha.FoundAt - asha.TappedAt, Is.LessThanOrEqualTo(20.3), "by her own 20 s");
            AssertOwnCountdown(asha);
            AssertOwnCountdownNeverBelowItsTables(asha);
        }

        [Test]
        public void QuickMatch_FirstSearchFindsOnlyANewerTable_ThePlayerHostsItsOwn()
        {
            UseQuickMatch(listingDelay: 0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            InMemoryPeerNetwork.Device newer = ListBareTable("Q7NEWR", 5.0);

            Tap(hari);
            SettleQuick(3);

            Assert.IsFalse(SentOpenTo(hari, newer.Address), "a table whose host tapped later is not joined");
            Assert.AreEqual(0, _peers.QuickOpenedAt(hari.TableCode ?? string.Empty), "Hari hosts his own, the older table");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void QuickMatch_HostGoesQuietInTheWaitingRoom_GuestsRegroupAtOneTableAndEachStartsByItsOwn20s(bool unplugged)
        {
            UseQuickMatch(listingDelay: 2.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            QuickPlayer suman = NewQuickPlayer("Suman");
            Tap(hari);
            SettleQuick(0.5);
            Tap(asha);
            SettleQuick(0.5);
            Tap(suman);
            SettleQuickUntil(() => SeatsTaken(asha) == 3 && SeatsTaken(suman) == 3, 10);
            Assert.AreSame(hari, HostOf(hari, asha, suman));

            double quietAt = _now;
            GoQuiet(hari, unplugged);
            SettleQuickUntil(() => asha.Search.IsCompleted && suman.Search.IsCompleted, 30);

            Assert.That(LeftTableAt(asha, hari, quietAt) - quietAt, Is.LessThanOrEqualTo(3.5), "Asha gave the quiet host up within about 3 s");
            Assert.That(LeftTableAt(suman, hari, quietAt) - quietAt, Is.LessThanOrEqualTo(3.5), "and so did Suman");
            if (!unplugged)
            {
                Assert.AreEqual(1, OpensTo(asha, hari), "a host that went quiet is not tried again on that search");
                Assert.AreEqual(1, OpensTo(suman, hari));
            }

            Assert.IsTrue(asha.Search.Result.Found && suman.Search.Result.Found);
            Assert.AreEqual(asha.Match.View.MatchId, suman.Match.View.MatchId, "they regrouped at one table");
            Assert.AreSame(asha, HostOf(asha, suman), "hosted by the earlier tapper");
            Assert.That(asha.FoundAt - asha.TappedAt, Is.LessThanOrEqualTo(20.3), "Asha starts by her own 20 s");
            Assert.That(suman.FoundAt - suman.TappedAt, Is.LessThanOrEqualTo(20.3), "Suman by his");
            AssertOwnCountdown(asha);
            AssertOwnCountdown(suman);
            AssertOwnCountdownNeverBelowItsTables(suman);
        }

        [Test]
        public void QuickMatch_HostGoesQuietDuringTheCountdown_GuestsSearchAgainAndNoMatchStartsWithoutIt()
        {
            UseQuickMatch(listingDelay: 2.0);
            var four = new List<QuickPlayer> { NewQuickPlayer("Hari"), NewQuickPlayer("Asha"), NewQuickPlayer("Suman"), NewQuickPlayer("Bikash") };
            foreach (QuickPlayer p in four)
            {
                Tap(p);
            }

            SettleQuickUntil(() => four.TrueForAll(p => p.Reports.Exists(r => r.Progress.TableFound)), 14);
            QuickPlayer host = HostOf(four.ToArray());
            List<QuickPlayer> guests = four.FindAll(p => p != host);

            SettleQuick(0.5);
            Pause(host);
            SettleQuickUntil(() => guests.TrueForAll(p => p.Search.IsCompleted), 30);

            Assert.IsFalse(host.Search.IsCompleted, "the quiet host started nothing");
            foreach (QuickPlayer g in guests)
            {
                Assert.IsTrue(g.Search.Result.Found, g.Name + " still plays");
                Assert.AreEqual(guests[0].Match.View.MatchId, g.Match.View.MatchId, g.Name + " regrouped with the others");
                Assert.That(g.FoundAt - g.TappedAt, Is.LessThanOrEqualTo(20.3), g.Name + " by its own 20 s");
                foreach (SessionSeat seat in g.Match.Seats)
                {
                    Assert.AreNotEqual(host.Device.Profile.DisplayName, seat.Name, "without the quiet host");
                }
            }

            Assert.AreEqual(1, Bots(guests[0].Match), "three people and a bot");
        }

        [Test]
        public void QuickMatch_StaleTable_TheJoinerGivesItUpInAbout3s()
        {
            UseQuickMatch(listingDelay: 1.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            Tap(hari);
            SettleQuick(3);
            Pause(hari);
            QuickPlayer asha = NewQuickPlayer("Asha");

            Tap(asha);
            SettleQuickUntil(() => asha.TableCode != null, 8);
            double hosting = _now - asha.TappedAt;
            SettleQuickUntil(() => asha.Search.IsCompleted, 30);

            Assert.IsTrue(SentOpen(asha, hari), "she tried the table that still showed");
            Assert.That(hosting, Is.LessThanOrEqualTo(4.5), "and gave it up in about 3 s, for a table of her own");
            Assert.IsFalse(asha.Reports.Exists(r => r.Progress.Seats[0].Name == "Hari"), "she never sat at it");
            Assert.AreEqual(3, Bots(asha.Match));
            Assert.That(asha.FoundAt - asha.TappedAt, Is.LessThanOrEqualTo(20.3));
        }

        [Test]
        public void QuickMatch_HostComesBackBeforeIts20s_ItsTableCarriesOnWithBots()
        {
            UseQuickMatch(listingDelay: 2.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            Tap(hari);
            SettleQuick(0.5);
            Tap(asha);
            SettleQuickUntil(() => SeatsTaken(asha) == 2, 10);
            string code = hari.TableCode;

            Pause(hari);
            SettleQuickUntil(() => !asha.Reports[asha.Reports.Count - 1].Progress.Seats[0].Name.Equals("Hari"), 6);
            SettleQuick(1);
            Resume(hari);
            SettleQuick(0.5);

            Assert.AreEqual(1, SeatsTaken(hari), "the guest who left is gone");
            Assert.AreEqual(3, _peers.QuickFreeSeats(code), "and the directory says the seats are free");
            SettleQuickUntil(() => hari.Search.IsCompleted && asha.Search.IsCompleted, 30);
            Assert.IsTrue(hari.Search.Result.Found);
            Assert.AreEqual(3, Bots(hari.Match), "Hari's table carried on, with bots");
            Assert.That(hari.FoundAt - hari.TappedAt, Is.LessThanOrEqualTo(20.3));
            Assert.AreNotEqual(hari.Match.View.MatchId, asha.Match.View.MatchId, "Asha plays elsewhere");
        }

        [Test]
        public void QuickMatch_HostComesBackAfterIts20s_BotsFillAtOnceAndItStarts()
        {
            UseQuickMatch(listingDelay: 2.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            Tap(hari);
            SettleQuick(5);

            Pause(hari);
            SettleQuick(20);
            Resume(hari);
            double back = _now;
            SettleQuickUntil(() => hari.Search.IsCompleted, 5);

            Assert.IsTrue(hari.Search.Result.Found);
            Assert.AreEqual(3, Bots(hari.Match));
            Assert.That(hari.FoundAt - back, Is.LessThanOrEqualTo(0.5), "at once");
        }

        [Test]
        public void QuickMatch_Heartbeat_WhileTheTableWaitsAndThroughTheMatch_NoneAfterIt()
        {
            UseQuickMatch(listingDelay: 2.0);
            QuickPlayer hari = NewQuickPlayer("Hari");
            QuickPlayer asha = NewQuickPlayer("Asha");
            Tap(hari);
            Tap(asha);
            SettleQuickUntil(() => hari.Search.IsCompleted && asha.Search.IsCompleted, 30);
            QuickPlayer host = HostOf(hari, asha);
            QuickPlayer guest = host == hari ? asha : hari;
            int whileWaiting = Heartbeats(host, guest);
            double startedAt = _now;
            host.Match.Start();
            guest.Match.Start();
            PlayQuick(200);
            int duringMatch = Heartbeats(host, guest) - whileWaiting;
            double played = _now - startedAt;

            Assert.Greater(whileWaiting, 3, "about one a second while the table waited");
            Assert.That(duringMatch, Is.InRange((int)(played * 0.8), (int)(played * 1.2) + 1), "and about one a second through the match, so a host that went quiet is noticed (ADR 030)");
            Assert.AreEqual(SessionStatus.Running, guest.Match.Status, "disturbing nothing");
            PlayQuick(20000);
            Assert.AreEqual(SessionStatus.Ended, guest.Match.Status);
            int atTheEnd = Heartbeats(host, guest);
            SettleQuick(5);
            Assert.AreEqual(atTheEnd, Heartbeats(host, guest), "and none once it is over");
            Assert.That(_peerFaults, Is.Empty);
        }

        /// <summary>The player's app goes to the background: it runs nothing, and what reaches it waits.</summary>
        private void Pause(QuickPlayer player) => _peers.Pause(player.Network);

        private void Resume(QuickPlayer player) => _peers.Resume(player.Network);

        /// <summary>The player's device goes quiet: its app in the background, or gone for good (unplugged).</summary>
        private void GoQuiet(QuickPlayer player, bool unplugged)
        {
            if (!unplugged)
            {
                Pause(player);
                return;
            }

            _peers.Unplug(player.Network);
            player.Stopped = true;
        }

        /// <summary>When <paramref name="player"/>'s screen first stopped showing it at <paramref name="host"/>'s table after <paramref name="since"/>.</summary>
        private static double LeftTableAt(QuickPlayer player, QuickPlayer host, double since)
        {
            foreach (var report in player.Reports)
            {
                if (report.At >= since && report.Progress.Seats[0].Name != host.Name)
                {
                    return report.At;
                }
            }

            return double.PositiveInfinity;
        }

        /// <summary>How often <paramref name="player"/> asked <paramref name="host"/>'s device for a connection.</summary>
        private int OpensTo(QuickPlayer player, QuickPlayer host)
        {
            int count = 0;
            foreach (var sent in _peers.Sent)
            {
                if (sent.From == player.Network.Address && sent.To == host.Network.Address && sent.Packet.Length == 1 && sent.Packet[0] == (byte)PacketType.Open)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The heartbeats that went from <paramref name="host"/>'s device to <paramref name="guest"/>'s.</summary>
        private int Heartbeats(QuickPlayer host, QuickPlayer guest)
        {
            int count = 0;
            foreach (var sent in _peers.Sent)
            {
                if (sent.From == host.Network.Address && sent.To == guest.Network.Address && sent.Packet.Length == 2
                    && sent.Packet[0] == (byte)PacketType.Whole && sent.Packet[1] == (byte)HostMessageKind.Heartbeat)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The devices made from now on meet over a fresh peer network, whose new listings show after <paramref name="listingDelay"/> s (EOS: about 8.6 s).</summary>
        private void UseQuickMatch(double listingDelay = 8.6)
        {
            UsePeers();
            _peers.ListingDelaySeconds = listingDelay;
            _quick.Clear();
        }

        private QuickPlayer NewQuickPlayer(string name)
        {
            Device device = NewPeerDevice(name);
            ulong next = (ulong)(_quick.Count + 1) * 1000003UL;
            var service = new QuickMatchService(
                new QuickMatchConfig(), device.Profile, device.Cosmetics, device.Auth, new TestBots(), device.Stats, device.Settings, device.Network,
                new NameFilter(), () => next++ * 7919UL, BotTiming.Instant, (IQuickMatchTransport)device.Transport, _peerOf[device], () => UnixEpoch.AddSeconds(_now));
            var player = new QuickPlayer(name, device, service, _peerOf[device], () => _now);
            _quick.Add(player);

            // In the app the service is ticked every frame from the start, so it knows the time when the player taps.
            service.Tick(_now);
            return player;
        }

        /// <summary>The player taps Quick Match now.</summary>
        private void Tap(QuickPlayer player)
        {
            player.TappedAt = _now;
            player.Cancel = new CancellationTokenSource();
            player.Search = player.Service.FindMatchAsync(player, player.Cancel.Token);
        }

        /// <summary>
        /// Time passes, in steps of <paramref name="step"/>: as in the app's frame, the transports take
        /// what arrived, then every Quick Match search and table is driven, and the network settles.
        /// A paused or stopped player's device runs nothing.
        /// </summary>
        private void SettleQuick(double seconds, double step = 0.25)
        {
            double until = _now + seconds;
            do
            {
                _now = Math.Min(until, _now + step);
                _peers.Now = _now;
                Pump();
                foreach (QuickPlayer p in _quick)
                {
                    if (!p.Stopped && !p.Network.IsPaused)
                    {
                        p.Service.Tick(_now);
                    }
                }

                foreach (Device d in _devices)
                {
                    d.Rooms.Tick(_now);
                }

                Pump();
                foreach (QuickPlayer p in _quick)
                {
                    if (p.Search != null && p.Search.IsCompleted && double.IsNaN(p.FoundAt))
                    {
                        p.FoundAt = _now;
                    }
                }
            }
            while (_now < until);
        }

        private void SettleQuickUntil(Func<bool> done, double seconds, double step = 0.25)
        {
            double until = _now + seconds;
            while (!done() && _now < until)
            {
                SettleQuick(step, step);
            }

            Assert.IsTrue(done(), "it came about within " + seconds + " s");
        }

        /// <summary>Every person plays like the test bots, while time passes and the network settles; a paused or stopped player's app does nothing.</summary>
        private void PlayQuick(int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                bool over = true;
                foreach (QuickPlayer p in _quick)
                {
                    IMatchSession session = p.Service.Match;
                    if (session != null && session.Status != SessionStatus.Ended && session.Status != SessionStatus.Failed && !p.Stopped && !p.Network.IsPaused)
                    {
                        over = false;
                        RoomFixture.Act(session);
                    }
                }

                if (over)
                {
                    return;
                }

                SettleQuick(0.25);
            }
        }

        private static int Bots(IMatchSession match)
        {
            int bots = 0;
            for (int s = 0; s < match.Seats.Count; s++)
            {
                bots += match.Seats[s].IsBot ? 1 : 0;
            }

            return bots;
        }

        private static int SeatsTaken(QuickPlayer player)
        {
            if (player.Reports.Count == 0)
            {
                return 0;
            }

            int taken = 0;
            foreach (RoomSeat seat in player.Reports[player.Reports.Count - 1].Progress.Seats)
            {
                taken += seat.Occupied ? 1 : 0;
            }

            return taken;
        }

        /// <summary>The player hosting the table the others sit at: the one whose own seat is the table's first.</summary>
        private static QuickPlayer HostOf(params QuickPlayer[] players)
        {
            foreach (QuickPlayer p in players)
            {
                IReadOnlyList<RoomSeat> seats = p.Reports[p.Reports.Count - 1].Progress.Seats;
                if (seats[0].IsLocal)
                {
                    return p;
                }
            }

            Assert.Fail("nobody hosts");
            return null;
        }

        /// <summary>When the player's screen showed each seat beyond its own taken, in order.</summary>
        private static List<double> TimesSeatsFilled(QuickPlayer player)
        {
            var times = new List<double>();
            int before = 1;
            foreach (var report in player.Reports)
            {
                int taken = 0;
                foreach (RoomSeat seat in report.Progress.Seats)
                {
                    taken += seat.Occupied ? 1 : 0;
                }

                for (int t = before + 1; t <= taken; t++)
                {
                    times.Add(report.At);
                }

                before = Math.Max(before, taken);
            }

            return times;
        }

        private static double TimeSeatsFilled(QuickPlayer player, int seats)
        {
            foreach (var report in player.Reports)
            {
                int taken = 0;
                foreach (RoomSeat seat in report.Progress.Seats)
                {
                    taken += seat.Occupied ? 1 : 0;
                }

                if (taken >= seats)
                {
                    return report.At;
                }
            }

            return double.PositiveInfinity;
        }

        private static double FirstTableFound(QuickPlayer player)
        {
            foreach (var report in player.Reports)
            {
                if (report.Progress.TableFound)
                {
                    return report.At;
                }
            }

            Assert.Fail(player.Name + "'s table was never found");
            return double.NaN;
        }

        private static double StartsInAt(QuickPlayer player, double at)
        {
            foreach (var report in player.Reports)
            {
                if (report.At >= at && report.Progress.TableFound)
                {
                    return report.Progress.StartsInSeconds;
                }
            }

            return double.NaN;
        }

        /// <summary>The seats come in the table's order, the player's own marked, and the host's seat first.</summary>
        private static void AssertSeatsInTableOrder(QuickPlayer player)
        {
            MatchmakingProgress last = null;
            foreach (var report in player.Reports)
            {
                if (report.Progress.TableFound)
                {
                    last = report.Progress;
                }
            }

            Assert.IsNotNull(last, player.Name + " saw the table found");
            int local = -1;
            for (int i = 0; i < last.Seats.Count; i++)
            {
                Assert.AreEqual(i, last.Seats[i].Index, "table order");
                Assert.IsTrue(last.Seats[i].Occupied, "all four seats taken");
                if (last.Seats[i].IsLocal)
                {
                    Assert.AreEqual(-1, local, "one seat is the player's own");
                    local = i;
                }
            }

            Assert.AreEqual((int)player.Match.LocalSeat, local, player.Name + "'s own seat is the one it plays");
            Assert.AreEqual(player.Device.Profile.DisplayName, last.Seats[local].Name);
            Assert.IsTrue(last.Seats[0].IsHost, "the host sits first");
        }

        /// <summary>Every report gives the player's own countdown from its own tap.</summary>
        private static void AssertOwnCountdown(QuickPlayer player)
        {
            Assert.Greater(player.Reports.Count, 0);
            foreach (var report in player.Reports)
            {
                double own = Math.Max(0.0, 20.0 - (report.At - player.TappedAt));
                Assert.AreEqual(own, report.Progress.SecondsLeft, 0.001, player.Name + " at " + report.At);
            }
        }

        /// <summary>True once <paramref name="player"/> asked the host of <paramref name="table"/>'s device for a connection.</summary>
        private bool SentOpen(QuickPlayer player, QuickPlayer table) => SentOpenTo(player, table.Network.Address);

        /// <summary>True once <paramref name="player"/> asked the device at <paramref name="address"/> for a connection.</summary>
        private bool SentOpenTo(QuickPlayer player, string address)
        {
            foreach (var sent in _peers.Sent)
            {
                if (sent.From == player.Network.Address && sent.To == address && sent.Packet.Length == 1 && sent.Packet[0] == (byte)PacketType.Open)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// A Quick Match table in the directory whose host tapped at <paramref name="tappedAt"/> s,
        /// listed by a device of its own that answers nobody.
        /// </summary>
        private InMemoryPeerNetwork.Device ListBareTable(string code, double tappedAt)
        {
            InMemoryPeerNetwork.Device device = _peers.AddDevice();
            device.SignInAsync(CancellationToken.None);
            _peers.Pump();
            device.ListQuickAsync(code, 3, (long)Math.Round(tappedAt * 1000.0), CancellationToken.None);
            _peers.Pump();
            return device;
        }

        /// <summary>
        /// Wherever the player sat as a guest, until that table locked its own countdown never showed
        /// less time than the table's (its host's), and it had not run out when the table locked.
        /// </summary>
        private void AssertOwnCountdownNeverBelowItsTables(QuickPlayer player)
        {
            var lockedSeen = new HashSet<string>();
            foreach (var report in player.Reports)
            {
                RoomSeat first = report.Progress.Seats[0];
                if (first.IsLocal || !first.Occupied)
                {
                    // At a table of its own, or at none yet.
                    continue;
                }

                QuickPlayer host = _quick.Find(p => p.Device.Profile.DisplayName == first.Name);
                Assert.IsNotNull(host, first.Name + " hosts the table " + player.Name + " sits at");
                if (report.Progress.TableFound)
                {
                    if (lockedSeen.Add(host.Name))
                    {
                        Assert.Greater(report.Progress.SecondsLeft, 0.0, player.Name + "'s own countdown had not run out when " + host.Name + "'s table locked");
                    }

                    continue;
                }

                double tables = Math.Max(0.0, host.TappedAt + 20.0 - report.At);
                Assert.GreaterOrEqual(report.Progress.SecondsLeft + 0.001, tables, player.Name + " at " + host.Name + "'s table at " + report.At);
            }
        }

        /// <summary>Until the table locks, the guest's own countdown never shows less time than the table's (its host's).</summary>
        private static void AssertOwnCountdownNeverBelowTheTables(QuickPlayer guest, QuickPlayer host)
        {
            Assert.GreaterOrEqual(guest.TappedAt, host.TappedAt);
            foreach (var report in guest.Reports)
            {
                if (report.Progress.TableFound)
                {
                    break;
                }

                double tables = Math.Max(0.0, host.TappedAt + 20.0 - report.At);
                Assert.GreaterOrEqual(report.Progress.SecondsLeft + 0.001, tables, guest.Name + " at " + report.At);
            }
        }

        /// <summary>Once the table locks, every screen counts down to the same moment.</summary>
        private static void AssertSameStart(params QuickPlayer[] players)
        {
            double start = double.NaN;
            foreach (QuickPlayer p in players)
            {
                double found = FirstTableFound(p);
                double startsAt = found + StartsInAt(p, found);
                if (double.IsNaN(start))
                {
                    start = startsAt;
                }

                Assert.AreEqual(start, startsAt, 0.3, p.Name + "'s countdown ends with the others'");
            }
        }

        /// <summary>One player tapping Quick Match on a device of its own, and what its screen was told.</summary>
        private sealed class QuickPlayer : IProgress<MatchmakingProgress>
        {
            private readonly Func<double> _clock;

            public QuickPlayer(string name, Device device, QuickMatchService service, InMemoryPeerNetwork.Device network, Func<double> clock)
            {
                Name = name;
                Device = device;
                Service = service;
                Network = network;
                _clock = clock;
            }

            public string Name { get; }

            public Device Device { get; }

            public QuickMatchService Service { get; }

            public InMemoryPeerNetwork.Device Network { get; }

            public IMatchSession Match => Service.Match;

            public List<(double At, MatchmakingProgress Progress)> Reports { get; } = new List<(double At, MatchmakingProgress Progress)>();

            public Task<MatchmakingResult> Search { get; set; }

            public CancellationTokenSource Cancel { get; set; }

            public double TappedAt { get; set; }

            public double FoundAt { get; set; } = double.NaN;

            /// <summary>The player's app is gone for good (its device unplugged): nothing of it runs any more.</summary>
            public bool Stopped { get; set; }

            /// <summary>The code of the table this player hosts, as the directory lists it; null when it lists none.</summary>
            public string TableCode => Network.QuickCode;

            public void Report(MatchmakingProgress value) => Reports.Add((_clock(), value));
        }
    }
}
