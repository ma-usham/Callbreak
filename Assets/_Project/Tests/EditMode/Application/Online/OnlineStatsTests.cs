using System.Threading;
using Callbreak.Application.Profile;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// GDD §15 "Tracked for later: calls-met %, average call, win streak": every member records
    /// its own seat's rounds and calls when an online match ends, and leaving early costs the streak.
    /// </summary>
    public sealed class OnlineStatsTests
    {
        [Test]
        public void PlayToEnd_EachMemberRecordsItsOwnRoundsCallsAndPlaceInTheWinStreak()
        {
            var room = new RoomFixture();
            room.Join("p1", "Asha");
            room.StartMatch();

            room.PlayToEnd();

            foreach (RoomFixture.Member m in room.Members)
            {
                Seat seat = m.Session.LocalSeat;
                PlayerView view = m.Session.View;
                PlayerStats stats = m.Stats.Current;
                bool won = RankOf(view, seat) == 1;

                Assert.AreEqual(view.Rules.Rounds, stats.roundsPlayed, m.Name);
                Assert.AreEqual(view.Scores.RoundsMetBy(seat), stats.roundsMet, m.Name);
                Assert.AreEqual(2 * view.Rules.Rounds, stats.callsTotal, m.Name + " calls 2 every round");
                Assert.AreEqual(won ? 1 : 0, stats.winStreak, m.Name);
                Assert.AreEqual(won ? 1 : 0, stats.bestWinStreak, m.Name);
            }
        }

        [Test]
        public void LeaveMatch_Guest_EndsTheWinStreakAndAddsNoRounds()
        {
            var room = new RoomFixture();
            RoomFixture.Member guest = room.Join("p1", "Asha");
            guest.Stats.RecordMatch("earlier", 1, 150, false, roundsPlayed: 5, roundsMet: 4, callsTotal: 12);
            room.StartMatch();
            for (int i = 0; i < 10; i++)
            {
                room.Step();
                RoomFixture.Act(room.HostMember.Session);
                RoomFixture.Act(guest.Session);
                room.Network.Pump();
            }

            guest.Session.LeaveAsync(CancellationToken.None);
            room.Network.Pump();

            PlayerStats stats = guest.Stats.Current;
            Assert.AreEqual(2, stats.gamesPlayed);
            Assert.AreEqual(1, stats.wins);
            Assert.AreEqual(0, stats.winStreak, "leaving ends the run");
            Assert.AreEqual(1, stats.bestWinStreak);
            Assert.AreEqual(5, stats.roundsPlayed, "a match left early adds no rounds");
            Assert.AreEqual(4, stats.roundsMet);
            Assert.AreEqual(12, stats.callsTotal);
        }

        private static int RankOf(PlayerView view, Seat seat)
        {
            foreach (Standing standing in view.FinalStandings)
            {
                if (standing.Seat == seat)
                {
                    return standing.Rank;
                }
            }

            Assert.Fail("the match has no standing for " + seat);
            return 0;
        }
    }
}
