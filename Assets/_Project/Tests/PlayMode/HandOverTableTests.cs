using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Flow;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
using Callbreak.Core.Randomness;
using Callbreak.Core.Rules;
using Callbreak.Core.Views;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// A Quick Match table on a guest's phone while its host is replaced (ADR 030): the real Table
    /// scene plays a real <see cref="OnlineMatchSession"/>, fed here as the hosts would feed it. While
    /// the next host takes the match over, the guest sees the "Reconnecting…" cover, the turn clock
    /// waits under it and nothing says "Host left"; then the table goes on from the same round, with
    /// the host's seat shown as played by a bot. Only a hand-over that fails says "Host left", and
    /// leads to the main menu. Saves go to a temporary folder.
    /// </summary>
    public sealed class HandOverTableTests
    {
        private const Seat Host = Seat.South;
        private const int ClockMs = 15000;

        private GameFixture _game;
        private FoundTable _search;
        private OnlineMatchSession _session;
        private PlayerView _view;
        private Seat _local;
        private int _sent;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _search = new FoundTable();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator HostLeaves_TheGuestSeesReconnecting_ThenTheTableGoesOn_WithTheHostsSeatPlayedByABot()
        {
            yield return OpenTheTable();
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();
            SeatView hostSeat = table.Seat(Host.RelativeTo(_local));
            Assert.IsNull(hostSeat.Chip, "the host is at the table");
            yield return GameFixture.Until(() => table.Seat(0).TimerSeconds > 0, "the guest's turn clock to show");

            // The host left: the next host in line takes the match over, and this player goes there.
            _session.OnLinkLost();
            yield return null;
            int clock = table.Seat(0).TimerSeconds;
            Assert.IsTrue(table.IsReconnecting, "the \"Reconnecting…\" cover, as for any reconnect");
            Assert.IsFalse(_game.Dialogs.IsOpen, "nothing says \"Host left\" while the match is handed over");

            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(clock, table.Seat(0).TimerSeconds, "the turn clock waits under the cover");
            Assert.IsFalse(_game.Dialogs.IsOpen);
            Assert.AreEqual(AppState.InMatch, _game.Flow.State, "the guest stays at the table");

            // The new host has rebuilt the round: the same table, the host's seat now played by a bot.
            _session.OnSync(new TableUpdate(2, null, _view, Status(SeatConnection.Left)));
            yield return null;

            Assert.IsFalse(table.IsReconnecting, "the table is back");
            Assert.AreEqual(Text(SeatStatus.BotPlayingKey), hostSeat.Chip, "the leaver's seat: a bot plays it now");
            Assert.AreEqual(ClockMs / 1000, table.Seat(0).TimerSeconds, "the turn has a clock of its own again");
            Assert.IsFalse(_game.Dialogs.IsOpen);
            Assert.AreEqual(ScreenId.Gameplay, _game.TableRouter.Current);
            Assert.AreEqual(SessionStatus.Running, _session.Status);
        }

        [UnityTest]
        public IEnumerator HandOverFails_HostLeftIsSaid_AndTheMainMenuFollows()
        {
            yield return OpenTheTable();
            TableView table = UnityEngine.Object.FindFirstObjectByType<TableView>();
            _session.OnLinkLost();
            yield return null;
            Assert.IsTrue(table.IsReconnecting);
            Assert.IsFalse(_game.Dialogs.IsOpen);

            // Nobody could take the match over.
            _session.OnRoomClosed(RoomError.HostLeft);
            yield return GameFixture.Until(() => _game.Dialogs.IsOpen, "the table to say why it ends");
            Assert.IsTrue(DialogSays(Text("ui.toast.host_left")), "\"Host left\", now that the hand-over failed");

            _game.PressDialog(confirm: true);
            yield return _game.UntilMenu();
            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current);
        }

        /// <summary>
        /// Boots, finds the table through Play Online (the search finds it at once) and gives the
        /// session the whole state the host sends a player that is ready: a round just dealt, this
        /// player's call turn open with its clock running.
        /// </summary>
        private IEnumerator OpenTheTable()
        {
            yield return _game.Boot(extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IMatchmakingService>(_search);
            });

            var seats = new[]
            {
                new SeatProfile("Hari", "1", CosmeticsService.ClassicCrimson, false),
                new SeatProfile("Asha", "2", CosmeticsService.ClassicCrimson, false),
                new SeatProfile("Bikash", "3", CosmeticsService.ClassicCrimson, true),
                new SeatProfile("Suman", "4", CosmeticsService.ClassicCrimson, true),
            };
            var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(7), new MatchSetup("quick-HANDOV-1", seats, Host));
            engine.Start();

            // This player is the first to call: the seat after the host, who deals.
            _local = engine.CurrentSeat;
            Assert.AreNotEqual(Host, _local);
            _view = engine.ViewFor(_local);
            _session = new OnlineMatchSession(
                MatchMode.QuickMatch,
                _view,
                Status(SeatConnection.Connected),
                payload => _sent++,
                _game.Services.Resolve<IStatsService>(),
                _game.Settings);
            _search.Match = _session;

            GameFixture.Press(_game.MenuRouter, "PlayOnlineButton");
            yield return _game.UntilTable();
            yield return GameFixture.Until(() => _sent > 0, "the table to tell the host it is ready");
            _session.OnSync(new TableUpdate(1, null, _view, Status(SeatConnection.Connected)));
            yield return null;
            Assert.AreEqual(SessionStatus.Running, _session.Status);
        }

        /// <summary>This player's call clock just started; the host's seat as given, everyone else here.</summary>
        private TableStatus Status(SeatConnection host)
        {
            var connections = new SeatConnection[SeatExtensions.Count];
            connections[(int)Host] = host;
            return new TableStatus(_local, ClockMs, ClockMs, TableStatus.NoClock, connections);
        }

        private string Text(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private bool DialogSays(string text)
        {
            DialogView dialog = _game.Project.GetComponentInChildren<DialogView>(true);
            foreach (TMP_Text label in dialog.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.text == text)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A Quick Match search that finds its table at once: the session the test drives.</summary>
        private sealed class FoundTable : IMatchmakingService
        {
            public IMatchSession Match { get; set; }

            public Task<MatchmakingResult> FindMatchAsync(IProgress<MatchmakingProgress> progress, CancellationToken ct)
            {
                return Task.FromResult(new MatchmakingResult(MatchmakingError.None));
            }
        }
    }
}
