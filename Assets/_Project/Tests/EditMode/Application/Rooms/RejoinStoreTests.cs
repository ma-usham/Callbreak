using System;
using Callbreak.Application.Rooms;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>GDD §29 session.json: the online match to rejoin after an app kill, gone after ten minutes.</summary>
    public sealed class RejoinStoreTests
    {
        private static readonly DateTime Start = new DateTime(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

        private InMemorySaveStore _saves;
        private DateTime _utc;
        private RejoinStore _store;

        [SetUp]
        public void SetUp()
        {
            _saves = new InMemorySaveStore();
            _utc = Start;
            _store = new RejoinStore(_saves, () => _utc);
        }

        [Test]
        public void TryGet_NothingRemembered_IsFalse()
        {
            Assert.IsFalse(_store.TryGet(out string code));
            Assert.IsNull(code);
        }

        [Test]
        public void Remember_ThenTryGet_GivesTheCode()
        {
            _store.Remember("AB2CDE");
            _utc = Start.AddMinutes(9);

            Assert.IsTrue(_store.TryGet(out string code));
            Assert.AreEqual("AB2CDE", code);
        }

        [Test]
        public void Remember_Again_StartsTheTenMinutesAfresh()
        {
            _store.Remember("AB2CDE");
            _utc = Start.AddMinutes(8);
            _store.Remember("AB2CDE");
            _utc = Start.AddMinutes(17);

            Assert.IsTrue(_store.TryGet(out _));
        }

        [Test]
        public void TryGet_AfterTenMinutes_IsFalseAndTheTicketIsDeleted()
        {
            _store.Remember("AB2CDE");
            _utc = Start.AddSeconds(RejoinStore.LifetimeSeconds + 1);

            Assert.IsFalse(_store.TryGet(out string code));
            Assert.IsNull(code);
            Assert.IsFalse(_saves.Exists(RejoinStore.Key));
        }

        [Test]
        public void TryGet_SurvivesANewStoreOverTheSameFiles()
        {
            _store.Remember("AB2CDE");
            _utc = Start.AddMinutes(1);
            var afterRestart = new RejoinStore(_saves, () => _utc);

            Assert.IsTrue(afterRestart.TryGet(out string code));
            Assert.AreEqual("AB2CDE", code);
        }

        [Test]
        public void TryGet_TicketWithoutAValidCode_IsFalseAndTheTicketIsDeleted()
        {
            _saves.Put(RejoinStore.Key, new RejoinTicket { roomCode = "AB1", savedAtUnixSeconds = new DateTimeOffset(Start).ToUnixTimeSeconds() });

            Assert.IsFalse(_store.TryGet(out _));
            Assert.IsFalse(_saves.Exists(RejoinStore.Key));
        }

        [Test]
        public void TryGet_TicketFromANewerSchema_IsFalseAndTheTicketIsLeftAlone()
        {
            _saves.Put(RejoinStore.Key, new RejoinTicket { schemaVersion = 2, roomCode = "AB2CDE", savedAtUnixSeconds = new DateTimeOffset(Start).ToUnixTimeSeconds() });

            Assert.IsFalse(_store.TryGet(out _));
            Assert.IsTrue(_saves.Exists(RejoinStore.Key), "a newer build's file is ignored, not deleted");
        }

        [Test]
        public void Clear_RemovesTheTicket()
        {
            _store.Remember("AB2CDE");

            _store.Clear();

            Assert.IsFalse(_store.TryGet(out _));
            Assert.IsFalse(_saves.Exists(RejoinStore.Key));
        }

        [Test]
        public void Remember_InvalidCode_Throws()
        {
            Assert.Throws<ArgumentException>(() => _store.Remember("AB1"));
        }
    }
}
