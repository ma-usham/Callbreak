using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using Callbreak.Application.Updates;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;

namespace Callbreak.Tests.EditMode.Application
{
    /// <summary>
    /// Forced updates (the owner, 5 Oct 2026: "any new Play version"). At the moments the app offers,
    /// the store is asked at most once per 30 s, only where the place allows it, and never while a
    /// rejoin into a running match may still come. A newer version starts Google Play's immediate
    /// update, and an update in progress is brought back. No newer version, one Play won't install now,
    /// or a store that can't be asked lets the player play on. An update that goes without updating
    /// holds the player at "Update required": its only button asks the store again and starts the
    /// update again, or lets the player play on once there is nothing to update.
    /// </summary>
    public sealed class ForcedUpdateTests
    {
        private static readonly DateTime Start = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);

        private ScriptedStore _store;
        private ScriptedDialogs _dialogs;
        private RejoinStore _tickets;
        private Network _network;
        private List<string> _log;
        private DateTime _now;
        private bool _here;
        private SynchronizationContext _context;
        private ForcedUpdate _update;

        [SetUp]
        public void SetUp()
        {
            // The store's answers resume the update on the test's own thread, at once, in the Editor
            // as in the dotnet harness.
            _context = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            _store = new ScriptedStore();
            _dialogs = new ScriptedDialogs();
            _now = Start;
            _tickets = new RejoinStore(new InMemorySaveStore(), () => _now);
            _network = new Network { IsOnline = true };
            _log = new List<string>();
            _here = true;
            _update = new ForcedUpdate(_store, _dialogs, new Keys(), _tickets, _network, () => _now, _log.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _update.Dispose();
            SynchronizationContext.SetSynchronizationContext(_context);
        }

        [Test]
        public void TheNewestVersion_ThePlayerPlaysOn()
        {
            Check();
            Assert.AreEqual(1, _store.Checks, "the store is asked");
            Assert.AreEqual(ForcedUpdateState.Checking, _update.State);

            _store.Answer(AppUpdateAvailability.None);

            Assert.AreEqual(0, _store.Starts);
            Assert.IsFalse(_dialogs.IsOpen);
            Assert.AreEqual(ForcedUpdateState.Idle, _update.State);
        }

        [Test]
        public void ANewerVersion_StartsGooglePlaysImmediateUpdate()
        {
            Check();
            _store.Answer(AppUpdateAvailability.Available);

            Assert.AreEqual(1, _store.Starts);
            Assert.AreEqual(ForcedUpdateState.Updating, _update.State);
            Assert.IsFalse(_dialogs.IsOpen, "Play's own full-screen update, nothing of the game's");
        }

        [Test]
        public void AnUpdateInProgress_IsBroughtBack()
        {
            Check();
            _store.Answer(AppUpdateAvailability.InProgress);

            Assert.AreEqual(1, _store.Starts);
            Assert.AreEqual(ForcedUpdateState.Updating, _update.State);
        }

        [TestCase(AppUpdateAvailability.Available)]
        [TestCase(AppUpdateAvailability.InProgress)]
        public void AnUpdate_ThatPlayWontRunNow_ThePlayerPlaysOn(AppUpdateAvailability answer)
        {
            // E.g. too little storage: Update could start nothing, so it would only trap the player.
            Check();
            _store.Answer(answer, immediateAllowed: false);

            Assert.AreEqual(0, _store.Starts);
            Assert.IsFalse(_dialogs.IsOpen);
            Assert.AreEqual(ForcedUpdateState.Idle, _update.State);
        }

        [Test]
        public void AFailedCheck_ThePlayerPlaysOn()
        {
            // No Play Store, an app not installed from it, offline.
            Check();
            _store.Answer(AppUpdateAvailability.Failed);

            Assert.AreEqual(0, _store.Starts);
            Assert.IsFalse(_dialogs.IsOpen);
            Assert.AreEqual(ForcedUpdateState.Idle, _update.State);
        }

        [Test]
        public void AStoreThatBreaks_ThePlayerPlaysOn()
        {
            Check();
            Assert.DoesNotThrow(() => _store.Break());

            Assert.AreEqual(0, _store.Starts);
            Assert.IsFalse(_dialogs.IsOpen);
            Assert.AreEqual(ForcedUpdateState.Idle, _update.State);
        }

        [Test]
        public void TheUpdate_LeftWithoutUpdating_HoldsThePlayerAtUpdateRequired()
        {
            StartTheUpdate();

            _store.LeaveUpdate();

            Assert.AreEqual(ForcedUpdateState.Blocked, _update.State);
            DialogRequest dialog = _dialogs.Showing;
            Assert.IsNotNull(dialog, "\"Update required\" is up");
            Assert.AreEqual("ui.dialog.update_required.title", dialog.Title);
            Assert.AreEqual("ui.dialog.update_required.body", dialog.Body);
            Assert.AreEqual("ui.dialog.update_required.confirm", dialog.ConfirmLabel);
            Assert.IsTrue(string.IsNullOrEmpty(dialog.CancelLabel), "only the Update button");
            Assert.IsFalse(dialog.Dismissable, "no close: a tap outside and Android back leave it up");
        }

        [Test]
        public void TheGamesOtherDialogs_StayDismissable()
        {
            Assert.IsTrue(new DialogRequest("Exit Callbreak?", string.Empty, "Exit", "Cancel").Dismissable);
        }

        [Test]
        public void Update_AsksTheStoreAgain_AndStartsTheUpdateAgain()
        {
            HoldAtUpdateRequired();

            _dialogs.Press();

            Assert.IsTrue(_dialogs.IsOpen, "the dialog stays up while the store is asked again");
            Assert.AreEqual(2, _store.Checks, "a fresh answer: each of Play's answers starts one update only");
            _store.Answer(AppUpdateAvailability.Available);
            Assert.AreEqual(2, _store.Starts);
            Assert.AreEqual(ForcedUpdateState.Updating, _update.State);
            Assert.IsTrue(_dialogs.IsOpen, "\"Update required\" waits under Play's screen");
        }

        [Test]
        public void Update_BringsBackAnUpdateInProgress()
        {
            HoldAtUpdateRequired();

            _dialogs.Press();
            _store.Answer(AppUpdateAvailability.InProgress);

            Assert.AreEqual(2, _store.Starts);
            Assert.AreEqual(ForcedUpdateState.Updating, _update.State);
        }

        [Test]
        public void Update_LeftAgain_HoldsAgain()
        {
            HoldAtUpdateRequired();
            _dialogs.Press();
            _store.Answer(AppUpdateAvailability.Available);

            _store.LeaveUpdate();

            Assert.AreEqual(ForcedUpdateState.Blocked, _update.State);
            Assert.IsTrue(_dialogs.IsOpen);
            _dialogs.Press();
            Assert.AreEqual(3, _store.Checks, "Update works every time");
        }

        [Test]
        public void Update_IsNeverKeptWaiting_ByTheInterval()
        {
            HoldAtUpdateRequired();
            _now = Start.AddSeconds(1);

            _dialogs.Press();

            Assert.AreEqual(2, _store.Checks, "the player's own tap asks at once");
        }

        [TestCase(AppUpdateAvailability.None, true)]
        [TestCase(AppUpdateAvailability.Failed, false)]
        [TestCase(AppUpdateAvailability.Available, false)]
        [TestCase(AppUpdateAvailability.InProgress, false)]
        public void Update_WithNothingToInstallNow_LetsThePlayerPlayOn(AppUpdateAvailability answer, bool immediateAllowed)
        {
            HoldAtUpdateRequired();

            _dialogs.Press();
            _store.Answer(answer, immediateAllowed);

            Assert.IsFalse(_dialogs.IsOpen, "\"Update required\" closes");
            Assert.AreEqual(ForcedUpdateState.Idle, _update.State);
            Assert.AreEqual(1, _store.Starts, "no update starts");
        }

        [Test]
        public void AtMostOneCheck_Every30Seconds()
        {
            Check();
            _store.Answer(AppUpdateAvailability.None);

            _now = Start.AddSeconds(29);
            Check();
            Assert.AreEqual(1, _store.Checks, "too soon");

            _now = Start.AddSeconds(30);
            Check();
            Assert.AreEqual(2, _store.Checks);
        }

        [Test]
        public void AFailedCheck_AlsoWaits30Seconds()
        {
            Check();
            _store.Answer(AppUpdateAvailability.Failed);

            _now = Start.AddSeconds(10);
            Check();

            Assert.AreEqual(1, _store.Checks);
        }

        [Test]
        public void AClockSetBack_DoesNotStopTheChecks()
        {
            Check();
            _store.Answer(AppUpdateAvailability.None);

            _now = Start.AddHours(-1);
            Check();

            Assert.AreEqual(2, _store.Checks);
        }

        [Test]
        public void AMomentDuringACheck_JoinsIt()
        {
            Task first = _update.CheckAsync(() => _here);
            Task second = _update.CheckAsync(() => _here);

            Assert.AreEqual(1, _store.Checks);
            Assert.AreSame(first, second);
            Assert.IsFalse(first.IsCompleted, "waiting for the store");
            _store.Answer(AppUpdateAvailability.None);
            Assert.IsTrue(first.IsCompleted);
        }

        [Test]
        public void ACheck_CompletesOnceTheAnswerIsActedOn()
        {
            // The launch waits for this behind the curtain, so it must not wait for the update itself.
            Task check = _update.CheckAsync(() => _here);

            _store.Answer(AppUpdateAvailability.Available);

            Assert.IsTrue(check.IsCompleted, "the update is up: the launch goes on behind it");
            Assert.AreEqual(ForcedUpdateState.Updating, _update.State);
        }

        [Test]
        public void NoCheck_WhereThePlaceDoesNotAllowIt()
        {
            // A match, matchmaking, a room: the caller says.
            _here = false;

            Check();

            Assert.AreEqual(0, _store.Checks);
            Assert.AreEqual(ForcedUpdateState.Idle, _update.State);
        }

        [Test]
        public void APlaceWhoseRuleBreaks_AllowsNothing()
        {
            Assert.DoesNotThrow(() => _update.CheckAsync(() => throw new InvalidOperationException("no router")));

            Assert.AreEqual(0, _store.Checks);
        }

        [Test]
        public void ThePlayerMovedOn_DuringTheCheck_NoUpdate_AndTheNextMomentAsksAgain()
        {
            Check();
            _here = false;
            _store.Answer(AppUpdateAvailability.Available);

            Assert.AreEqual(0, _store.Starts, "never in a match, in matchmaking or in a room");
            Assert.AreEqual(ForcedUpdateState.Idle, _update.State);

            _here = true;
            _now = Start.AddSeconds(1);
            Check();
            Assert.AreEqual(2, _store.Checks, "a newer version that could not start doesn't count against the 30 s");
            _store.Answer(AppUpdateAvailability.Available);
            Assert.AreEqual(1, _store.Starts);
        }

        [Test]
        public void TheNewestMomentsRule_JudgesTheAnswer()
        {
            // The launch's check, answered only once the menu is up on the main menu.
            bool launching = true;
            _update.CheckAsync(() => launching);
            launching = false;
            _update.CheckAsync(() => _here);
            Assert.AreEqual(1, _store.Checks);

            _store.Answer(AppUpdateAvailability.Available);

            Assert.AreEqual(1, _store.Starts, "judged by the main menu's rule, not the launch's");
        }

        [Test]
        public void NoCheck_WhileARejoinMayStillCome()
        {
            // Never interrupts a rejoin into a running match: the menu offers it while the ticket holds.
            _tickets.Remember("AB2CDE", "match-1");

            Check();
            Assert.AreEqual(0, _store.Checks);

            // The rejoined match is over.
            _tickets.Clear();
            Check();
            Assert.AreEqual(1, _store.Checks);
        }

        [Test]
        public void ARejoinTicket_Offline_DoesNotHoldTheCheck()
        {
            // Offline the menu offers no rejoin.
            _tickets.Remember("AB2CDE", "match-1");
            _network.IsOnline = false;

            Check();

            Assert.AreEqual(1, _store.Checks);
        }

        [Test]
        public void ARejoinTicket_ThatRanOut_NoLongerHoldsTheCheck()
        {
            _tickets.Remember("AB2CDE", "match-1");
            _now = Start.AddSeconds(RejoinStore.LifetimeSeconds + 1);

            Check();

            Assert.AreEqual(1, _store.Checks);
        }

        [Test]
        public void ARejoinTicket_ThatArrivesDuringTheCheck_HoldsTheUpdate()
        {
            Check();
            _tickets.Remember("AB2CDE", "match-1");

            _store.Answer(AppUpdateAvailability.Available);

            Assert.AreEqual(0, _store.Starts);
            Assert.AreEqual(ForcedUpdateState.Idle, _update.State);
        }

        [Test]
        public void WhileTheUpdateIsUp_MomentsAskNothing()
        {
            StartTheUpdate();
            _now = Start.AddMinutes(5);

            Check();

            Assert.AreEqual(1, _store.Checks);
            Assert.AreEqual(1, _store.Starts);
        }

        [Test]
        public void WhileHeld_AMoment_BringsBackAnUpdateInProgress()
        {
            // The player left Play's screen during the download, then came back to the game.
            HoldAtUpdateRequired();
            _now = Start.AddSeconds(30);

            Check();
            Assert.AreEqual(2, _store.Checks);
            _store.Answer(AppUpdateAvailability.InProgress);

            Assert.AreEqual(2, _store.Starts);
            Assert.AreEqual(ForcedUpdateState.Updating, _update.State);
            Assert.IsTrue(_dialogs.IsOpen, "\"Update required\" stays under Play's screen");
        }

        [Test]
        public void WhileHeld_AMoment_LeavesTheDialog_WhenNoUpdateIsInProgress()
        {
            // Only Update starts it again.
            HoldAtUpdateRequired();
            int asked = _dialogs.Asked.Count;
            _now = Start.AddSeconds(30);

            Check();
            _store.Answer(AppUpdateAvailability.Available);

            Assert.AreEqual(1, _store.Starts);
            Assert.AreEqual(ForcedUpdateState.Blocked, _update.State);
            Assert.IsTrue(_dialogs.IsOpen);
            Assert.AreEqual(asked, _dialogs.Asked.Count, "the same dialog, not asked again");
            _dialogs.Press();
            Assert.AreEqual(3, _store.Checks, "Update still works");
        }

        [Test]
        public void WhileHeld_AMoment_LeavesTheDialog_WhenPlayWontBringTheUpdateBack()
        {
            HoldAtUpdateRequired();
            _now = Start.AddSeconds(30);

            Check();
            _store.Answer(AppUpdateAvailability.InProgress, immediateAllowed: false);

            Assert.AreEqual(1, _store.Starts);
            Assert.AreEqual(ForcedUpdateState.Blocked, _update.State);
            Assert.IsTrue(_dialogs.IsOpen);
        }

        [Test]
        public void WhileHeld_MomentsWait30Seconds_TooSoon()
        {
            HoldAtUpdateRequired();
            _now = Start.AddSeconds(5);

            Check();

            Assert.AreEqual(1, _store.Checks);
        }

        [Test]
        public void UpdateRequired_ClosedFromOutside_TheNextMomentAsksAgain()
        {
            HoldAtUpdateRequired();

            _dialogs.CloseFromOutside();

            Assert.AreEqual(ForcedUpdateState.Idle, _update.State);
            _now = Start.AddSeconds(30);
            Check();
            Assert.AreEqual(2, _store.Checks, "the update is not forgotten");
        }

        [Test]
        public void Dispose_ClosesUpdateRequired_AndStops()
        {
            HoldAtUpdateRequired();

            _update.Dispose();

            Assert.IsFalse(_dialogs.IsOpen);
            _now = Start.AddMinutes(5);
            Check();
            Assert.AreEqual(1, _store.Checks);
        }

        [Test]
        public void TheLog_SaysWhatHappened()
        {
            HoldAtUpdateRequired();

            CollectionAssert.IsNotEmpty(_log);
            Assert.IsTrue(_log.TrueForAll(line => line.StartsWith("App update: ", StringComparison.Ordinal)), string.Join("\n", _log));
        }

        private void Check() => _update.CheckAsync(() => _here);

        private void StartTheUpdate()
        {
            Check();
            _store.Answer(AppUpdateAvailability.Available);
            Assert.AreEqual(ForcedUpdateState.Updating, _update.State);
        }

        private void HoldAtUpdateRequired()
        {
            StartTheUpdate();
            _store.LeaveUpdate();
            Assert.AreEqual(ForcedUpdateState.Blocked, _update.State);
            Assert.IsTrue(_dialogs.IsOpen);
        }

        /// <summary>A store that answers when the test says: every check and every update waits for it.</summary>
        private sealed class ScriptedStore : IAppUpdates
        {
            private TaskCompletionSource<AppUpdateCheck> _check;
            private TaskCompletionSource<bool> _update;

            public int Checks { get; private set; }

            public int Starts { get; private set; }

            public Task<AppUpdateCheck> CheckAsync(CancellationToken ct)
            {
                Checks++;
                _check = new TaskCompletionSource<AppUpdateCheck>();
                return _check.Task;
            }

            public Task StartImmediateAsync(CancellationToken ct)
            {
                Starts++;
                _update = new TaskCompletionSource<bool>();
                return _update.Task;
            }

            public void Answer(AppUpdateAvailability availability, bool immediateAllowed = true)
            {
                _check.SetResult(new AppUpdateCheck(availability, immediateAllowed));
            }

            /// <summary>The store breaks instead of answering.</summary>
            public void Break() => _check.SetException(new InvalidOperationException("the store broke"));

            /// <summary>Play's screen goes without updating: the player backed out.</summary>
            public void LeaveUpdate() => _update.SetResult(true);
        }

        /// <summary>Dialogs like the game's: one at a time, closed when the asker's token is cancelled.</summary>
        private sealed class ScriptedDialogs : IDialogs
        {
            private TaskCompletionSource<DialogChoice> _open;

            public List<DialogRequest> Asked { get; } = new List<DialogRequest>();

            public bool IsOpen => _open != null && !_open.Task.IsCompleted;

            public DialogRequest Showing => IsOpen ? Asked[Asked.Count - 1] : null;

            public async Task<bool> ConfirmAsync(DialogRequest request, CancellationToken ct)
            {
                return await AskAsync(request, ct) == DialogChoice.Confirm;
            }

            public Task<DialogChoice> AskAsync(DialogRequest request, CancellationToken ct)
            {
                _open?.TrySetResult(DialogChoice.Dismissed);
                var open = new TaskCompletionSource<DialogChoice>();
                _open = open;
                Asked.Add(request);
                ct.Register(() => open.TrySetResult(DialogChoice.Dismissed));
                return open.Task;
            }

            public bool HandleBack() => IsOpen;

            /// <summary>The primary button.</summary>
            public void Press()
            {
                Assert.IsTrue(IsOpen, "a dialog is open");
                _open.SetResult(DialogChoice.Confirm);
            }

            /// <summary>The dialog layer goes away under the open dialog.</summary>
            public void CloseFromOutside() => _open.SetResult(DialogChoice.Dismissed);
        }

        /// <summary>Text as its key.</summary>
        private sealed class Keys : ILocalization
        {
            public string Language => "en";

            public IReadOnlyList<string> Languages => new[] { "en" };

            public event Action LanguageChanged
            {
                add { }
                remove { }
            }

            public string Get(string key) => key;

            public string Format(string key, params object[] args) => key;

            public void SetLanguage(string language)
            {
            }
        }

        private sealed class Network : INetworkStatus
        {
            public bool IsOnline { get; set; }

            public event Action<bool> Changed
            {
                add { }
                remove { }
            }
        }
    }
}
