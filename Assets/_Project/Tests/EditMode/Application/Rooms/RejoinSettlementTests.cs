using System;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// GDD §8 (leaving always costs the match) and §27 (after an app kill, "Rejoin match"): the
    /// ticket names the match, and a match the player does not go back to counts as left, once.
    /// </summary>
    public sealed class RejoinSettlementTests
    {
        private const string Code = "AB2CDE";
        private const string Match = "room-AB2CDE-0123456789abcdef";
        private static readonly DateTime Start = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStore _saves;
        private StatsService _stats;
        private DateTime _utc;
        private RejoinStore _store;

        [SetUp]
        public void SetUp()
        {
            _saves = new InMemorySaveStore();
            _stats = new StatsService(_saves);
            _utc = Start;
            _store = new RejoinStore(_saves, () => _utc, _stats);
        }

        [Test]
        public void Remember_KeepsTheMatchWithTheCode()
        {
            _store.Remember(Code, Match);

            Assert.AreEqual(Match, _saves.Peek<RejoinTicket>(RejoinStore.Key).matchId);
            Assert.IsTrue(_store.TryGet(out string code));
            Assert.AreEqual(Code, code);
        }

        [Test]
        public void Abandon_TheMatchCountsAsLeftAndTheTicketIsGone()
        {
            _store.Remember(Code, Match);

            _store.Abandon();

            Assert.AreEqual(1, _stats.Current.gamesPlayed, "played");
            Assert.AreEqual(0, _stats.Current.wins, "and lost");
            Assert.IsFalse(_stats.Current.hasBest, "without a best score");
            Assert.IsFalse(_saves.Exists(RejoinStore.Key));
        }

        [Test]
        public void Clear_RecordsNothing()
        {
            _store.Remember(Code, Match);

            _store.Clear();

            Assert.AreEqual(0, _stats.Current.gamesPlayed);
            Assert.IsFalse(_saves.Exists(RejoinStore.Key));
        }

        [Test]
        public void TryGet_TicketRanOut_TheMatchCountsAsLeft()
        {
            _store.Remember(Code, Match);
            _utc = Start.AddSeconds(RejoinStore.LifetimeSeconds + 1);

            Assert.IsFalse(_store.TryGet(out _));

            Assert.AreEqual(1, _stats.Current.gamesPlayed);
            Assert.AreEqual(0, _stats.Current.wins);
        }

        [Test]
        public void Abandon_AMatchAlreadyRecorded_ChangesNothing()
        {
            _store.Remember(Code, Match);
            _stats.RecordMatch(Match, 1, 250, abandoned: false, roundsPlayed: 5, roundsMet: 5, callsTotal: 12);

            _store.Abandon();

            Assert.AreEqual(1, _stats.Current.gamesPlayed, "a match is recorded once");
            Assert.AreEqual(1, _stats.Current.wins);
        }

        [Test]
        public void Remember_TheSameMatchAgain_RecordsNothing()
        {
            _store.Remember(Code, Match);
            _utc = Start.AddMinutes(1);
            _store.Remember(Code, Match);

            Assert.AreEqual(0, _stats.Current.gamesPlayed);
        }

        [Test]
        public void Remember_AnotherMatch_TheOneNeverGoneBackToCountsAsLeft()
        {
            _store.Remember(Code, Match);

            _store.Remember("ZZ2ZZZ", "room-ZZ2ZZZ-1");

            Assert.AreEqual(1, _stats.Current.gamesPlayed);
            Assert.AreEqual("room-ZZ2ZZZ-1", _saves.Peek<RejoinTicket>(RejoinStore.Key).matchId);
        }

        [Test]
        public void TicketFromBeforeMatchIdsWereKept_StillRejoinsAndSettlesNothing()
        {
            _saves.Put(RejoinStore.Key, new RejoinTicket { roomCode = Code, savedAtUnixSeconds = new DateTimeOffset(Start).ToUnixTimeSeconds() });

            Assert.IsTrue(_store.TryGet(out string code));
            Assert.AreEqual(Code, code);
            _store.Abandon();
            Assert.AreEqual(0, _stats.Current.gamesPlayed);
            Assert.IsFalse(_saves.Exists(RejoinStore.Key));
        }

        [Test]
        public void Abandon_WithoutStats_StillRemovesTheTicket()
        {
            var store = new RejoinStore(_saves, () => _utc);
            store.Remember(Code, Match);

            store.Abandon();

            Assert.IsFalse(_saves.Exists(RejoinStore.Key));
        }
    }
}
