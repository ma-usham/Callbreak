using System.Collections;
using Callbreak.Application.Flow;
using Callbreak.Application.Online;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Core.Match;
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
    /// scene plays a real <see cref="OnlineMatchSession"/>, fed here as the hosts would feed it
    /// (<see cref="ScriptedOnlineTable"/>). While the next host takes the match over, the guest sees
    /// the "Reconnecting…" cover, the turn clock waits under it and nothing says "Host left"; then the
    /// table goes on from the same round, with a bot playing the host's seat, silently: a leaver's
    /// seat has no chip (the owner, 3 Oct 2026). Only a hand-over that fails says "Host left", and
    /// leads to the main menu. Saves go to a temporary folder.
    /// </summary>
    public sealed class HandOverTableTests
    {
        private GameFixture _game;
        private ScriptedOnlineTable _table;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _table = new ScriptedOnlineTable();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator HostLeaves_TheGuestSeesReconnecting_ThenTheTableGoesOn_WithTheHostsSeatPlayedByABot()
        {
            yield return _table.Open(_game);
            OnlineMatchSession session = _table.Session;
            TableView table = Object.FindFirstObjectByType<TableView>();
            SeatView hostSeat = table.Seat(ScriptedOnlineTable.Host.RelativeTo(_table.Local));
            Assert.IsNull(hostSeat.Chip, "the host is at the table");
            yield return GameFixture.Until(() => table.Seat(0).TimerSeconds > 0, "the guest's turn clock to show");

            // The host left: the next host in line takes the match over, and this player goes there.
            session.OnLinkLost();
            yield return null;
            int clock = table.Seat(0).TimerSeconds;
            Assert.IsTrue(table.IsReconnecting, "the \"Reconnecting…\" cover, as for any reconnect");
            Assert.IsFalse(_game.Dialogs.IsOpen, "nothing says \"Host left\" while the match is handed over");

            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(clock, table.Seat(0).TimerSeconds, "the turn clock waits under the cover");
            Assert.IsFalse(_game.Dialogs.IsOpen);
            Assert.AreEqual(AppState.InMatch, _game.Flow.State, "the guest stays at the table");

            // The new host has rebuilt the round: the same table, the host's seat now played by a bot.
            session.OnSync(new TableUpdate(2, null, _table.View, _table.Status(SeatConnection.Left)));
            yield return null;

            Assert.IsFalse(table.IsReconnecting, "the table is back");
            Assert.AreEqual(SeatConnection.Left, session.Seats[(int)ScriptedOnlineTable.Host].Connection, "the host left: a bot plays the seat now");
            Assert.IsNull(hostSeat.Chip, "a bot plays a leaver's seat on silently, with no chip (the owner, 3 Oct 2026)");
            Assert.AreEqual(ScriptedOnlineTable.ClockMs / 1000, table.Seat(0).TimerSeconds, "the turn has a clock of its own again");
            Assert.IsFalse(_game.Dialogs.IsOpen);
            Assert.AreEqual(ScreenId.Gameplay, _game.TableRouter.Current);
            Assert.AreEqual(SessionStatus.Running, session.Status);
        }

        [UnityTest]
        public IEnumerator HandOverFails_HostLeftIsSaid_AndTheMainMenuFollows()
        {
            yield return _table.Open(_game);
            OnlineMatchSession session = _table.Session;
            TableView table = Object.FindFirstObjectByType<TableView>();
            session.OnLinkLost();
            yield return null;
            Assert.IsTrue(table.IsReconnecting);
            Assert.IsFalse(_game.Dialogs.IsOpen);

            // Nobody could take the match over.
            session.OnRoomClosed(RoomError.HostLeft);
            yield return GameFixture.Until(() => _game.Dialogs.IsOpen, "the table to say why it ends");
            Assert.IsTrue(DialogSays(Text("ui.toast.host_left")), "\"Host left\", now that the hand-over failed");

            _game.PressDialog(confirm: true);
            yield return _game.UntilMenu();
            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current);
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
    }
}
