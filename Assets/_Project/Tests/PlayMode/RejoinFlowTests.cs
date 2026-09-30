using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// GDD §27 and §8 in the real Menu scene: after an app kill the next launch offers "Rejoin
    /// match". A host that refuses because the seat hold is over means the match counts as left;
    /// a room that is gone records nothing; "Not now" and a connection problem keep the ticket for
    /// the next launch. The room service is a stand-in, so nothing here reaches the network.
    /// </summary>
    public sealed class RejoinFlowTests
    {
        private const string Code = "AB2CDE";
        private const string MatchId = "room-AB2CDE-1122334455667788";

        private GameFixture _game;
        private Rooms _rooms;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _rooms = new Rooms();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator Refused_TheSeatHoldIsOver_TheMatchCountsAsLeft()
        {
            _rooms.JoinError = RoomError.AlreadyStarted;
            yield return BootWithTicket();

            _game.PressDialog(confirm: true);
            yield return null;

            Assert.AreEqual(Code, _rooms.JoinedCode, "Rejoin goes back to the ticket's room");
            PlayerStats stats = _game.Services.Resolve<IStatsService>().Current;
            Assert.AreEqual(1, stats.gamesPlayed, "GDD §8: after the seat hold the result records \"left\"");
            Assert.AreEqual(0, stats.wins);
            Assert.IsFalse(Tickets().TryGet(out _), "the ticket is settled");
        }

        [UnityTest]
        public IEnumerator RoomGone_NothingIsRecorded_AndTheTicketGoes()
        {
            _rooms.JoinError = RoomError.NotFound;
            yield return BootWithTicket();

            _game.PressDialog(confirm: true);
            yield return null;

            Assert.AreEqual(0, _game.Services.Resolve<IStatsService>().Current.gamesPlayed, "a room whose host left costs nobody the match");
            Assert.IsFalse(Tickets().TryGet(out _));
        }

        [UnityTest]
        public IEnumerator NotNow_KeepsTheTicket_ForTheNextLaunch()
        {
            yield return BootWithTicket();

            _game.PressDialog(confirm: false);
            yield return null;

            Assert.IsNull(_rooms.JoinedCode);
            Assert.AreEqual(0, _game.Services.Resolve<IStatsService>().Current.gamesPlayed);
            Assert.IsTrue(Tickets().TryGet(out string code), "\"Not now\" is asked again at the next launch");
            Assert.AreEqual(Code, code);
        }

        [UnityTest]
        public IEnumerator ConnectionProblem_KeepsTheTicket()
        {
            _rooms.JoinError = RoomError.Unknown;
            yield return BootWithTicket();

            _game.PressDialog(confirm: true);
            yield return null;

            Assert.AreEqual(Code, _rooms.JoinedCode);
            Assert.AreEqual(0, _game.Services.Resolve<IStatsService>().Current.gamesPlayed);
            Assert.IsTrue(Tickets().TryGet(out _), "the match may still be waiting");
        }

        private IRejoinStore Tickets() => _game.Services.Resolve<IRejoinStore>();

        private IEnumerator BootWithTicket()
        {
            yield return _game.Boot(
                seed: saves => new RejoinStore(saves, () => DateTime.UtcNow).Remember(Code, MatchId),
                extra: builder =>
                {
                    builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                    builder.RegisterInstance<IRoomService>(_rooms);
                });
            yield return GameFixture.Until(() => _game.Dialogs.IsOpen, "the rejoin offer");
        }

        /// <summary>A room service that opens no rooms: every join ends with the error it is told to give.</summary>
        private sealed class Rooms : IRoomService
        {
            public RoomError JoinError = RoomError.NotFound;

            public string JoinedCode { get; private set; }

            public IRoomSession Current => null;

            public Task<RoomResult> CreateRoomAsync(RoomSettings settings, CancellationToken ct)
            {
                return Task.FromResult(new RoomResult(null, RoomError.Unknown));
            }

            public Task<RoomResult> JoinRoomAsync(string code, CancellationToken ct)
            {
                JoinedCode = code;
                return Task.FromResult(new RoomResult(null, JoinError));
            }
        }
    }
}
