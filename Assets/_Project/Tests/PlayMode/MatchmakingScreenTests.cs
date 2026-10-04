using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Ports;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The real Quick Match screen (GDD §11 screen 05, ADR 029) fed by a search the test reports
    /// itself: a guest sees themselves in the first seat circle and the host after them, the pill
    /// counts the guest's own search down, and when the table is found it counts quickly down to the
    /// start every player shares; with Reduce motion it just changes. During that start countdown
    /// nobody leaves: Cancel and the back arrow are greyed and Android back stays on the screen,
    /// until the search resumes. Saves go to a temporary folder.
    /// </summary>
    public sealed class MatchmakingScreenTests
    {
        private GameFixture _game;
        private ScriptedMatchmaking _search;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _search = new ScriptedMatchmaking();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator GuestScreen_TheGuestSitsFirst_ThePillCountsDown_ThenRollsDownToTheSharedStart()
        {
            yield return OpenTheSearch();
            UIScreen screen = _game.MenuRouter.Get<UIScreen>(ScreenId.Matchmaking);

            _search.Report(new MatchmakingProgress(TwoSeated(), 11.6, false, 8.4, double.NaN));
            Assert.AreEqual(_game.Services.Resolve<ILocalization>().Get("ui.common.you"), Label(screen, "SeatName0").text, "the guest is in the first circle");
            Assert.AreEqual("Hari", Label(screen, "SeatName1").text, "the host comes after them");
            Assert.AreEqual(string.Empty, Label(screen, "SeatName2").text);
            Assert.AreEqual("00:09", Label(screen, "Elapsed").text, "the guest's own countdown, rounded up");

            _search.Report(new MatchmakingProgress(FullTable(), 19.3, true, 0.7, 3.0));
            Assert.AreEqual("00:09", Label(screen, "Elapsed").text, "the switch to the shared start counts down from where the pill was");
            Assert.AreEqual(_game.Services.Resolve<ILocalization>().Get("ui.matchmaking.table_found"), Label(screen, "StatusTitle").text);

            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual("00:03", Label(screen, "Elapsed").text, "after the quick count the start everyone shares");
        }

        [UnityTest]
        public IEnumerator TableFound_CancelAndBackAreGreyed_AndAndroidBackStays_UntilTheSearchResumes()
        {
            yield return OpenTheSearch();
            ScreenRouter router = _game.MenuRouter;
            UIScreen screen = router.Get<UIScreen>(ScreenId.Matchmaking);
            _search.Report(new MatchmakingProgress(TwoSeated(), 11.6, false, 8.4, double.NaN));
            Assert.IsTrue(GameFixture.CanPress(screen, "CancelButton") && GameFixture.CanPress(screen, "BackButton"), "the search can be left");

            _search.Report(new MatchmakingProgress(FullTable(), 19.3, true, 0.7, 3.0));
            Assert.IsFalse(GameFixture.CanPress(screen, "CancelButton"), "no Cancel during the table's start countdown");
            Assert.IsFalse(GameFixture.CanPress(screen, "BackButton"), "no back arrow either");
            Assert.AreEqual(0.5f, BackArrow(screen).color.a, 0.001f, "the back arrow is greyed like the other disabled buttons");
            Assert.AreEqual(0.5f, Label(screen, "CancelLabel").color.a, 0.001f);
            Assert.IsTrue(router.Back(), "Android back is taken by the screen");
            yield return null;
            Assert.AreEqual(ScreenId.Matchmaking, router.Current, "and does not leave the found table");
            Assert.IsFalse(_search.Cancelled);

            // The host left: the seat empties and the search goes on.
            _search.Report(new MatchmakingProgress(TwoSeated(), 21.0, false, 5.2, double.NaN));
            Assert.IsTrue(GameFixture.CanPress(screen, "CancelButton") && GameFixture.CanPress(screen, "BackButton"), "the search can be left again");
            Assert.AreEqual(1f, BackArrow(screen).color.a, 0.001f);
            Assert.IsTrue(router.Back());
            yield return null;
            Assert.AreEqual(ScreenId.PlayOnline, router.Current, "Android back leaves the search again, for Play Online where it began");
            Assert.IsTrue(_search.Cancelled, "and withdraws it");
        }

        [UnityTest]
        public IEnumerator ReduceMotion_ThePillJustChangesToTheSharedStart()
        {
            yield return OpenTheSearch();
            _game.Settings.SetReduceMotion(true);
            UIScreen screen = _game.MenuRouter.Get<UIScreen>(ScreenId.Matchmaking);

            _search.Report(new MatchmakingProgress(TwoSeated(), 11.6, false, 8.4, double.NaN));
            _search.Report(new MatchmakingProgress(FullTable(), 19.3, true, 0.7, 3.0));

            Assert.AreEqual("00:03", Label(screen, "Elapsed").text);
        }

        [UnityTest]
        public IEnumerator HostLeavesDuringTheStartCountdown_TheGuestStaysAndSearches_OnTheirOwnClock()
        {
            yield return OpenTheSearch();
            ScreenRouter router = _game.MenuRouter;
            UIScreen screen = router.Get<UIScreen>(ScreenId.Matchmaking);
            var text = _game.Services.Resolve<ILocalization>();
            _search.Report(new MatchmakingProgress(FullTable(), 19.3, true, 6.4, 2.4));
            Assert.IsFalse(GameFixture.CanPress(screen, "CancelButton"), "the table is locked");

            // The host left (or went quiet): the service drops the table and searches again, with
            // this player alone in the first seat and their own countdown still running (ADR 030).
            _search.Report(new MatchmakingProgress(Alone(), 19.9, false, 5.8, double.NaN));
            yield return null;

            Assert.AreEqual(ScreenId.Matchmaking, router.Current, "the guest stays on the search");
            Assert.IsFalse(_game.Dialogs.IsOpen, "no \"Host left\", nothing to answer");
            Assert.IsFalse(_search.Cancelled, "and the search goes on");
            Assert.AreEqual(text.Get("ui.common.you"), Label(screen, "SeatName0").text);
            for (int i = 1; i < 4; i++)
            {
                Assert.AreEqual(string.Empty, Label(screen, "SeatName" + i).text, "the old table's seat " + i + " is cleared");
                Assert.AreEqual(text.Get("ui.matchmaking.seat_searching"), Label(screen, "SeatStatus" + i).text);
            }

            Assert.AreEqual(text.Get("ui.matchmaking.finding"), Label(screen, "StatusTitle").text);
            Assert.AreEqual("00:06", Label(screen, "Elapsed").text, "the guest's own countdown, not the table's 3-2-1");
            Assert.IsTrue(GameFixture.CanPress(screen, "CancelButton") && GameFixture.CanPress(screen, "BackButton"), "Cancel and back are back");
        }

        private IEnumerator OpenTheSearch()
        {
            yield return _game.Boot(extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IMatchmakingService>(_search);
            });
            GameFixture.Press(_game.MenuRouter, "PlayOnlineButton");
            GameFixture.Press(_game.MenuRouter, "PlayQuickButton");
            yield return GameFixture.Until(() => _search.Progress != null, "the search to start");
        }

        /// <summary>No table: the player alone in the first seat, as the search reports it between tables.</summary>
        private static List<RoomSeat> Alone()
        {
            return new List<RoomSeat>
            {
                new RoomSeat(0, true, "Asha", 2, "classic-crimson", false, true, false, true),
                new RoomSeat(1, false, string.Empty, 0, string.Empty, false, false, false, false),
                new RoomSeat(2, false, string.Empty, 0, string.Empty, false, false, false, false),
                new RoomSeat(3, false, string.Empty, 0, string.Empty, false, false, false, false),
            };
        }

        /// <summary>The table's order: the host Hari in seat 0, the guest Asha (the player) in seat 1.</summary>
        private static List<RoomSeat> TwoSeated()
        {
            return new List<RoomSeat>
            {
                new RoomSeat(0, true, "Hari", 1, "classic-crimson", true, true, false, false),
                new RoomSeat(1, true, "Asha", 2, "classic-crimson", false, true, false, true),
                new RoomSeat(2, false, string.Empty, 0, string.Empty, false, false, false, false),
                new RoomSeat(3, false, string.Empty, 0, string.Empty, false, false, false, false),
            };
        }

        private static List<RoomSeat> FullTable()
        {
            return new List<RoomSeat>
            {
                new RoomSeat(0, true, "Hari", 1, "classic-crimson", true, true, false, false),
                new RoomSeat(1, true, "Asha", 2, "classic-crimson", false, true, false, true),
                new RoomSeat(2, true, "Bikash", 3, "classic-crimson", false, true, true, false),
                new RoomSeat(3, true, "Suman", 4, "classic-crimson", false, true, true, false),
            };
        }

        /// <summary>The arrow on the back button (the BackIcon node's picture).</summary>
        private static UnityEngine.UI.Image BackArrow(Component screen)
        {
            foreach (UnityEngine.UI.Image image in screen.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if (image.transform.parent != null && image.transform.parent.name == "BackIcon")
                {
                    return image;
                }
            }

            Assert.Fail("The matchmaking screen has no back arrow.");
            return null;
        }

        private static TMP_Text Label(Component screen, string name)
        {
            foreach (TMP_Text label in screen.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == name)
                {
                    return label;
                }
            }

            Assert.Fail("The matchmaking screen has no label " + name + ".");
            return null;
        }

        /// <summary>A search that reports what the test tells it to and never ends by itself.</summary>
        private sealed class ScriptedMatchmaking : IMatchmakingService
        {
            public IProgress<MatchmakingProgress> Progress { get; private set; }

            /// <summary>The screen withdrew the search (Cancel, back).</summary>
            public bool Cancelled { get; private set; }

            public IMatchSession Match => null;

            public void Report(MatchmakingProgress value) => Progress.Report(value);

            public Task<MatchmakingResult> FindMatchAsync(IProgress<MatchmakingProgress> progress, CancellationToken ct)
            {
                Progress = progress;
                var search = new TaskCompletionSource<MatchmakingResult>();
                ct.Register(() =>
                {
                    Cancelled = true;
                    search.TrySetResult(new MatchmakingResult(MatchmakingError.Cancelled));
                });
                return search.Task;
            }
        }
    }
}
