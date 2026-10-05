using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using Callbreak.Application.Updates;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// Forced updates (the owner, 5 Oct 2026: "any new Play version") in the real Boot and Menu
    /// scenes, with the store a stand-in. In the Editor it never has a newer version and the menu
    /// opens as always. Given one, the launch starts the update before the menu, and leaving it (the
    /// stand-in's update goes at once, as when the player backs out of Google Play's screen) holds the
    /// player at "Update required": one button and no close; a tap outside, Android back and a newer
    /// dialog all leave it up. Update starts the update again; once the store has nothing newer, the
    /// player plays on. A rejoin ticket holds the update while a rejoin may still come; after that,
    /// the main menu on top again or the app back in front asks the store. Saves go to a temporary
    /// folder.
    /// </summary>
    public sealed class ForcedUpdateFlowTests
    {
        private const string Code = "AB2CDE";
        private const string MatchId = "room-AB2CDE-1122334455667788";

        private GameFixture _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator InTheEditor_TheStandInHasNoNewerVersion_AndTheMenuOpensAsAlways()
        {
            yield return _game.Boot();

            var store = _game.Services.Resolve<IAppUpdates>() as StandInAppUpdates;
            Assert.IsNotNull(store, "no Google Play in the Editor: the stand-in");
            Assert.AreEqual(1, store.Checks, "the launch asked once; the main menu right after it waits 30 s");
            Assert.AreEqual(0, store.Starts);
            Assert.AreEqual(ForcedUpdateState.Idle, Update().State);
            Assert.IsFalse(_game.Dialogs.IsOpen);
            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current);
        }

        [UnityTest]
        public IEnumerator ANewerVersion_StartsTheUpdateAtLaunch_AndLeavingItHoldsThePlayerAtUpdateRequired()
        {
            var store = new StandInAppUpdates { Availability = AppUpdateAvailability.Available };
            yield return Boot(store);

            Assert.AreEqual(1, store.Starts, "the update started before the menu");
            Assert.AreEqual(ForcedUpdateState.Blocked, Update().State);
            Assert.IsTrue(_game.Dialogs.IsOpen, "\"Update required\" holds the player");
            Assert.IsTrue(Dialog().IsShowing);
            Assert.AreEqual(Text("ui.dialog.update_required.title"), Label(Dialog(), "Title"));
            Assert.AreEqual(Text("ui.dialog.update_required.body"), Label(Dialog(), "Body"));
            Assert.AreEqual(Text("ui.dialog.update_required.confirm"), Label(Named(Dialog(), "ConfirmButton"), "Label"));
            Assert.IsFalse(Named(Dialog(), "CancelButton").gameObject.activeSelf, "only Update: no second button");
            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current, "over the main menu");
        }

        [UnityTest]
        public IEnumerator UpdateRequired_StaysUp_ForATapOutside_AndroidBack_AndANewerDialog()
        {
            yield return Boot(new StandInAppUpdates { Availability = AppUpdateAvailability.Available });
            Assert.IsTrue(_game.Dialogs.IsOpen);

            GameFixture.PressIn(Dialog(), "Backdrop");
            yield return null;
            Assert.IsTrue(_game.Dialogs.IsOpen, "a tap outside the panel leaves it up");
            Assert.IsTrue(Dialog().IsShowing);

            Assert.IsTrue(_game.Dialogs.HandleBack(), "Android back is taken, so nothing under the dialog hears it");
            yield return null;
            Assert.IsTrue(_game.Dialogs.IsOpen, "and leaves it up");
            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current);

            Task<bool> newer = _game.Dialogs.ConfirmAsync(new DialogRequest("Exit Callbreak?", string.Empty, "Exit", "Cancel"), CancellationToken.None);
            Assert.IsTrue(newer.IsCompleted, "a newer dialog is answered at once");
            Assert.IsFalse(newer.Result, "as dismissed");
            Assert.AreEqual(Text("ui.dialog.update_required.title"), Label(Dialog(), "Title"), "\"Update required\" is still the one showing");
            Assert.AreEqual(ForcedUpdateState.Blocked, Update().State);
        }

        [UnityTest]
        public IEnumerator Update_StartsTheUpdateAgain_AndWithNothingNewer_ThePlayerPlaysOn()
        {
            var store = new StandInAppUpdates { Availability = AppUpdateAvailability.Available };
            yield return Boot(store);

            _game.PressDialog(confirm: true);
            yield return GameFixture.Until(() => store.Starts == 2 && Update().State == ForcedUpdateState.Blocked, "the update to start again and be left again");
            Assert.AreEqual(2, store.Checks, "Update asks Google Play again");
            Assert.IsTrue(_game.Dialogs.IsOpen, "left again: held again");
            Assert.IsTrue(Dialog().IsShowing);

            // The newer version is installed elsewhere, or Play no longer offers it.
            store.Availability = AppUpdateAvailability.None;
            _game.PressDialog(confirm: true);
            yield return GameFixture.Until(() => !Dialog().IsShowing, "\"Update required\" to close");

            Assert.IsFalse(_game.Dialogs.IsOpen);
            Assert.AreEqual(ForcedUpdateState.Idle, Update().State);
            Assert.AreEqual(2, store.Starts);
            GameFixture.Press(_game.MenuRouter, "SettingsButton");
            Assert.AreEqual(ScreenId.Settings, _game.MenuRouter.Current, "the player plays on");
        }

        [UnityTest]
        public IEnumerator ARejoinTicket_HoldsTheUpdate_ThenTheMainMenuOnTopAgainStartsIt()
        {
            var store = new StandInAppUpdates { Availability = AppUpdateAvailability.Available };
            yield return BootWithTicket(store);

            Assert.AreEqual(Text("ui.dialog.rejoin_match.title"), Label(Dialog(), "Title"), "the rejoin offer, not the update");
            Assert.AreEqual(0, store.Checks, "never in the way of a rejoin into a running match");

            _game.PressDialog(confirm: false);
            yield return null;
            Assert.AreEqual(0, store.Checks, "\"Not now\" keeps the ticket, and the rejoin may still come");

            // The ticket is settled (the match is over, or it ran out).
            Tickets().Clear();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "SettingsButton");
            Assert.AreEqual(0, store.Checks, "Settings is not the main menu");
            Assert.IsTrue(router.Pop());
            yield return GameFixture.Until(() => Update().State == ForcedUpdateState.Blocked, "\"Update required\"");

            Assert.AreEqual(1, store.Checks, "the main menu on top again asks");
            Assert.AreEqual(1, store.Starts);
            Assert.IsTrue(_game.Dialogs.IsOpen);
            Assert.AreEqual(Text("ui.dialog.update_required.title"), Label(Dialog(), "Title"));
        }

        [UnityTest]
        public IEnumerator TheAppBackInFront_OnTheMainMenu_AsksTheStore()
        {
            var store = new StandInAppUpdates { Availability = AppUpdateAvailability.Available };
            yield return BootWithTicket(store);
            _game.PressDialog(confirm: false);
            yield return null;
            Tickets().Clear();

            ComeBackToTheFront();
            yield return GameFixture.Until(() => Update().State == ForcedUpdateState.Blocked, "\"Update required\"");

            Assert.AreEqual(1, store.Checks);
            Assert.AreEqual(1, store.Starts);
            Assert.AreEqual(Text("ui.dialog.update_required.title"), Label(Dialog(), "Title"));
        }

        [UnityTest]
        public IEnumerator TheAppBackInFront_OnAnotherScreen_AsksNothing()
        {
            var store = new StandInAppUpdates { Availability = AppUpdateAvailability.Available };
            yield return BootWithTicket(store);
            _game.PressDialog(confirm: false);
            yield return null;
            Tickets().Clear();
            GameFixture.Press(_game.MenuRouter, "SettingsButton");

            ComeBackToTheFront();
            yield return null;

            Assert.AreEqual(0, store.Checks, "only the main menu asks");
            Assert.IsFalse(_game.Dialogs.IsOpen);
            Assert.AreEqual(ScreenId.Settings, _game.MenuRouter.Current);
        }

        private IEnumerator Boot(StandInAppUpdates store)
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<IAppUpdates>(store));
        }

        /// <summary>Boots online with a rejoin ticket that still holds: the menu opens with "Rejoin match" up.</summary>
        private IEnumerator BootWithTicket(StandInAppUpdates store)
        {
            yield return _game.Boot(
                seed: saves => new RejoinStore(saves, () => DateTime.UtcNow).Remember(Code, MatchId),
                extra: builder =>
                {
                    builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                    builder.RegisterInstance<IAppUpdates>(store);
                });
            yield return GameFixture.Until(() => _game.Dialogs.IsOpen, "the rejoin offer");
        }

        /// <summary>The app comes back to the front, as the engine reports it (Application.focusChanged).</summary>
        private static void ComeBackToTheFront()
        {
            MethodInfo raise = typeof(UnityEngine.Application).GetMethod("InvokeFocusChanged", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(raise, "the engine raises Application.focusChanged through Application.InvokeFocusChanged");
            raise.Invoke(null, new object[] { true });
        }

        private ForcedUpdate Update() => _game.Services.Resolve<ForcedUpdate>();

        private IRejoinStore Tickets() => _game.Services.Resolve<IRejoinStore>();

        private DialogView Dialog() => _game.Project.GetComponentInChildren<DialogView>(true);

        private string Text(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private static string Label(Component root, string name) => Named(root, name).GetComponent<TMP_Text>().text;

        private static RectTransform Named(Component root, string name)
        {
            foreach (RectTransform child in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            Assert.Fail(root.name + " has no " + name);
            return null;
        }
    }
}
