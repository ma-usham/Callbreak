using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Callbreak.Application.Online;
using Callbreak.Core.Cards;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Views;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application.Online
{
    /// <summary>
    /// Rebuilding a match from what its players held when the host was lost (ADR 030), at every moment
    /// of whole matches: the public state of the latest update, each person's own hand, each bot's
    /// dealt hand without what it played since, and the one hand nobody brought worked out from the
    /// rest give back the host's state exactly, whichever person was lost. Two hands nobody brought,
    /// or reports that do not add up, and the round is dealt again.
    /// </summary>
    public sealed class TableRebuildTests
    {
        [TestCase(4, 11UL)]
        [TestCase(3, 23UL)]
        [TestCase(2, 37UL)]
        public void EveryMomentOfAMatch_TheOthersRebuildItExactly_WhicheverPersonIsLost(int people, ulong seed)
        {
            RoomFixture room = Room(people, seed);
            int checks = 0;
            int midTrick = 0;
            for (int step = 0; step < 20000 && !room.Host.Table.IsOver; step++)
            {
                Play(room);
                MatchSnapshot truth = room.Host.Table.Snapshot();
                SuccessionPackage package = PackageOf(room.Host.Table);
                bool open = truth.phase == (int)Phase.Calling || truth.phase == (int)Phase.Playing;
                foreach (RoomFixture.Member lost in room.Members)
                {
                    var reports = new List<SeatReport>();
                    foreach (RoomFixture.Member m in room.Members)
                    {
                        if (m != lost)
                        {
                            reports.Add(new SeatReport(m.Session.LocalSeat, m.Session.LastSeq, m.Session.View));
                        }
                    }

                    RebuiltTable rebuilt = TableRebuild.Rebuild(reports, package);

                    Assert.IsFalse(rebuilt.MustRedeal, "one hand nobody brought is worked out, at step " + step);
                    Assert.AreEqual(Describe(truth, open), Describe(rebuilt.Snapshot, open), "the host's state, at step " + step + " without " + lost.Name);
                    Assert.AreEqual(lost.Session.LastSeq, rebuilt.Seq, "numbered on from the last update");
                    checks++;
                    midTrick += truth.phase == (int)Phase.Playing && truth.currentTrick.cards.Length > 0 ? 1 : 0;
                }
            }

            Assert.IsTrue(room.Host.Table.IsOver);
            Assert.Greater(checks, 250 * people, "every moment of the match");
            Assert.Greater(midTrick, 100, "many of them in the middle of a trick");
        }

        [Test]
        public void ThePlayerWhoTakesOverIsBehind_ThePublicStateComesFromTheLatestUpdate()
        {
            RoomFixture room = Room(3, 5UL);
            PlayUntilMidTrick(room, 2);
            RoomFixture.Member late = room.Members[1];
            PlayerView older = late.Session.View;
            uint olderSeq = late.Session.LastSeq;
            Play(room);
            while (room.Host.Table.Snapshot().phase != (int)Phase.Playing)
            {
                Play(room);
            }

            MatchSnapshot truth = room.Host.Table.Snapshot();
            var reports = new List<SeatReport>
            {
                // The new host's own view is the one that missed the last updates (it reports first).
                new SeatReport(late.Session.LocalSeat, olderSeq, older),
                new SeatReport(room.Members[2].Session.LocalSeat, room.Members[2].Session.LastSeq, room.Members[2].Session.View),
            };

            RebuiltTable rebuilt = TableRebuild.Rebuild(reports, PackageOf(room.Host.Table));

            Assert.Less(olderSeq, room.Members[2].Session.LastSeq, "the test needs a player that is behind");
            Assert.AreEqual(Describe(truth, true), Describe(rebuilt.Snapshot, true));
        }

        [Test]
        public void TwoHandsNobodyBrought_TheRoundIsDealtAgainByTheSameDealerWithTheScoresKept()
        {
            RoomFixture room = Room(4, 7UL);
            PlayUntilMidTrick(room, 3);
            MatchSnapshot before = room.Host.Table.Snapshot();
            var reports = new List<SeatReport>
            {
                new SeatReport(room.Members[1].Session.LocalSeat, room.Members[1].Session.LastSeq, room.Members[1].Session.View),
                new SeatReport(room.Members[2].Session.LocalSeat, room.Members[2].Session.LastSeq, room.Members[2].Session.View),
            };

            RebuiltTable rebuilt = TableRebuild.Rebuild(reports, PackageOf(room.Host.Table));
            MatchSnapshot dealt = TableRebuild.Redeal(rebuilt.Latest, new Pcg32Random(99));

            Assert.IsTrue(rebuilt.MustRedeal, "the host and the fourth player both lost");
            Assert.IsTrue(MatchEngine.TryRestore(dealt, rebuilt.Latest.Rules, new Pcg32Random(1), out MatchEngine engine, out string error), error);
            Assert.AreEqual(Phase.Calling, engine.Phase);
            Assert.AreEqual(3, engine.Round);
            Assert.AreEqual(before.dealer, dealt.dealer);
            Assert.Greater(dealt.currentTurn, before.turnCounter, "turn ids only ever grow");
            Assert.AreEqual(Describe(Rounds(before)), Describe(Rounds(dealt)), "the finished rounds keep their scores");
            var cards = new HashSet<int>();
            foreach (SeatSnapshot seat in dealt.seats)
            {
                Assert.AreEqual(13, seat.hand.Length);
                cards.UnionWith(seat.hand);
            }

            Assert.AreEqual(52, cards.Count);
        }

        [Test]
        public void ReportsThatDoNotAddUp_TheRoundIsDealtAgainRatherThanRepaired()
        {
            RoomFixture room = Room(3, 9UL);
            PlayUntilMidTrick(room, 1);
            PlayerView honest = room.Members[1].Session.View;
            PlayerView other = room.Members[2].Session.View;

            // The player in seat 1 claims one of seat 2's cards instead of one of its own.
            Card mine = honest.Hand.Lowest();
            Card theirs = other.Hand.Lowest();
            PlayerView cheat = WithHand(honest, honest.Hand.Without(mine).With(theirs));
            var reports = new List<SeatReport>
            {
                new SeatReport(cheat.Viewer, room.Members[1].Session.LastSeq, cheat),
                new SeatReport(other.Viewer, room.Members[2].Session.LastSeq, other),
            };

            Assert.IsTrue(TableRebuild.Rebuild(reports, PackageOf(room.Host.Table)).MustRedeal);
        }

        [Test]
        public void BetweenRounds_NobodyHoldsACard_AndTheMatchIsRebuiltWithAnyHandsMissing()
        {
            RoomFixture room = Room(4, 3UL);
            for (int step = 0; step < 20000 && room.Host.Table.Snapshot().phase != (int)Phase.RoundSummary; step++)
            {
                Play(room);
            }

            MatchSnapshot truth = room.Host.Table.Snapshot();
            var reports = new List<SeatReport> { new SeatReport(room.Members[2].Session.LocalSeat, room.Members[2].Session.LastSeq, room.Members[2].Session.View) };

            RebuiltTable rebuilt = TableRebuild.Rebuild(reports, null);

            Assert.IsFalse(rebuilt.MustRedeal);
            Assert.AreEqual(Describe(truth, false), Describe(rebuilt.Snapshot, false));
        }

        /// <summary>A private room with <paramref name="people"/> people, bots in the other seats, dealt from seeds of its own.</summary>
        private static RoomFixture Room(int people, ulong seed)
        {
            ulong next = seed;
            var room = new RoomFixture(seeds: () => next++ * 0x9E3779B97F4A7C15UL);
            string[] names = { "Asha", "Suman", "Bikash" };
            for (int i = 1; i < people; i++)
            {
                room.Join("player-" + names[i - 1].ToLowerInvariant(), names[i - 1]);
            }

            room.StartMatch();
            return room;
        }

        private static void Play(RoomFixture room)
        {
            room.Step();
            foreach (RoomFixture.Member m in room.Members)
            {
                RoomFixture.Act(m.Session);
            }

            room.Network.Pump();
        }

        private static void PlayUntilMidTrick(RoomFixture room, int round)
        {
            for (int step = 0; step < 20000; step++)
            {
                MatchSnapshot now = room.Host.Table.Snapshot();
                if (now.round == round && now.phase == (int)Phase.Playing && now.currentTrick.cards.Length > 0)
                {
                    return;
                }

                Play(room);
            }

            Assert.Fail("never reached the middle of a trick in round " + round);
        }

        /// <summary>What the host would send the next host in line: identities for the people, the bots' dealt hands.</summary>
        private static SuccessionPackage PackageOf(TableHost table)
        {
            var identities = new string[4];
            var dealt = new CardSet[4];
            for (int s = 0; s < 4; s++)
            {
                if (table.IsHuman((Seat)s))
                {
                    identities[s] = "id-" + s;
                }
                else
                {
                    dealt[s] = table.DealtHandOf((Seat)s);
                }
            }

            return new SuccessionPackage(0, table.Round, identities, dealt);
        }

        private static PlayerView WithHand(PlayerView v, CardSet hand)
        {
            var calls = new int[4];
            var won = new int[4];
            var counts = new int[4];
            var ready = new bool[4];
            for (int s = 0; s < 4; s++)
            {
                calls[s] = v.CallOf((Seat)s);
                won[s] = v.TricksWonBy((Seat)s);
                counts[s] = v.HandCountOf((Seat)s);
                ready[s] = v.IsReady((Seat)s);
            }

            return new PlayerView(v.Viewer, v.Rules, v.MatchId, v.Seats, v.Phase, v.Round, v.Dealer, v.FirstDealer, v.CurrentSeat, v.CurrentTurn,
                hand, calls, won, counts, ready, v.CurrentTrick, v.CompletedTricks, v.Scores, v.FinalStandings);
        }

        private static MatchSnapshot Rounds(MatchSnapshot s) => new MatchSnapshot
        {
            seats = new SeatSnapshot[0],
            currentTrick = new TrickSnapshot { cards = new int[0] },
            completedTricks = new TrickSnapshot[0],
            rounds = s.rounds,
        };

        /// <summary>A snapshot as text, field by field; between rounds without the turn count, which no view tells.</summary>
        private static string Describe(MatchSnapshot s, bool withTurnCount = true)
        {
            var text = new StringBuilder();
            text.Append("phase=").Append(s.phase).Append(" round=").Append(s.round).Append(" fixed=").Append(s.hasFixedFirstDealer)
                .Append(" first=").Append(s.firstDealer).Append(" dealer=").Append(s.dealer).Append(" seat=").Append(s.currentSeat)
                .Append(" turn=").Append(s.currentTurn).Append(" ended=").Append(s.matchEnded).Append(" match=").Append(s.matchId).Append(" rules=").Append(s.ruleSetId);
            if (withTurnCount)
            {
                text.Append(" counter=").Append(s.turnCounter);
            }

            text.Append('\n');
            foreach (SeatSnapshot seat in s.seats)
            {
                text.Append(seat.name).Append(' ').Append(seat.avatarId).Append(' ').Append(seat.deckThemeId).Append(" bot=").Append(seat.isBot)
                    .Append(" hand=").Append(Ids(seat.hand)).Append(" call=").Append(seat.call).Append(" won=").Append(seat.tricksWon).Append(" ready=").Append(seat.ready).Append('\n');
            }

            text.Append("trick ").Append(s.currentTrick.leader).Append(' ').Append(Ids(s.currentTrick.cards)).Append('\n');
            foreach (TrickSnapshot trick in s.completedTricks)
            {
                text.Append("done ").Append(trick.leader).Append(' ').Append(Ids(trick.cards)).Append('\n');
            }

            foreach (RoundSnapshot round in s.rounds)
            {
                text.Append("round ").Append(round.round).Append(' ').Append(Ids(round.calls)).Append('/').Append(Ids(round.won)).Append('/').Append(Ids(round.deltas)).Append('\n');
            }

            return text.ToString();
        }

        private static string Ids(int[] values)
        {
            var parts = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                parts[i] = values[i].ToString(CultureInfo.InvariantCulture);
            }

            return string.Join(",", parts);
        }
    }
}
