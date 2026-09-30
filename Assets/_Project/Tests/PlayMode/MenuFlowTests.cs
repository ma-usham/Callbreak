using System;
using System.Collections;
using System.Threading;
using Callbreak.Application.Flow;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Infrastructure.Platform;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The real Boot and Menu scenes: the startup sequence (GDD §21), the first-time experience
    /// (GDD §15), the menu's screen flow (GDD §10) and the ways into a match, driven by tapping the
    /// real buttons. Saves go to a temporary folder.
    /// </summary>
    public sealed class MenuFlowTests
    {
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
        public IEnumerator Boot_ReturningPlayer_OpensTheMainMenuWithNothingToAsk()
        {
            yield return Boot();

            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current);
            Assert.AreEqual(1, _game.MenuRouter.Depth);
            Assert.IsFalse(_game.Dialogs.IsOpen, "no match to resume or rejoin");
        }

        [UnityTest]
        public IEnumerator Boot_FreshInstall_ShowsThePrimerThenOffersPractice_SkipLeadsToTheMainMenu()
        {
            yield return Boot(freshInstall: true);
            ScreenRouter router = _game.MenuRouter;
            Assert.AreEqual(ScreenId.HowToPlay, router.Current, "GDD 15: How to Play comes up by itself, once");
            Assert.IsFalse(_game.Settings.HowToPlaySeen);

            GameFixture.Press(router, "GotItButton");
            yield return null;

            Assert.IsTrue(_game.Settings.HowToPlaySeen);
            Assert.AreEqual(ScreenId.MainMenu, router.Current);
            Assert.IsTrue(_game.Dialogs.IsOpen, "an easy practice match is offered");
            _game.PressDialog(confirm: false);
            yield return null;

            Assert.IsFalse(_game.Dialogs.IsOpen);
            Assert.AreEqual(ScreenId.MainMenu, router.Current);
            Assert.AreEqual(AppState.Menu, _game.Flow.State);
        }

        [UnityTest]
        public IEnumerator Boot_FreshInstall_PracticeAccepted_StartsAnEasyOfflineMatch()
        {
            yield return Boot(freshInstall: true);
            GameFixture.Press(_game.MenuRouter, "GotItButton");
            yield return null;

            _game.PressDialog(confirm: true);
            yield return _game.UntilTable();

            var session = _game.Table.Container.Resolve<IMatchSession>();
            Assert.AreEqual(MatchMode.Offline, session.Mode);
            Assert.AreEqual(BotDifficulty.Easy, _game.Flow.CurrentLaunch.Difficulty);
            Assert.AreEqual(BotDifficulty.Normal, _game.Settings.Difficulty, "the practice match does not change the player's own choice");
        }

        [UnityTest]
        public IEnumerator Back_OnTheMainMenu_AsksBeforeExiting()
        {
            yield return Boot();

            _game.MenuRouter.Back();
            yield return null;

            Assert.IsTrue(_game.Dialogs.IsOpen, "GDD 11 screen 00: Android back asks \"Exit Callbreak?\"");
            Assert.IsTrue(_game.Dialogs.HandleBack(), "back again closes the question");
            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current);
        }

        [UnityTest]
        public IEnumerator Menu_NoInternet_MutesTheOnlineButtonsAndPointsToPlayOffline()
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<INetworkStatus>(new Network(online: false)));
            ScreenRouter router = _game.MenuRouter;
            UIScreen menu = router.Get<UIScreen>(ScreenId.MainMenu);

            Assert.Less(GroupOf(menu, "PlayOnlineButton").alpha, 1f, "GDD 11 screen 00: the online buttons are muted");
            Assert.Less(GroupOf(menu, "PlayWithFriendsButton").alpha, 1f);
            GameFixture.Press(router, "PlayOnlineButton");
            GameFixture.Press(router, "PlayWithFriendsButton");
            yield return null;

            Assert.AreEqual(ScreenId.MainMenu, router.Current, "nothing online opens without a connection");
            ToastView toast = _game.Project.GetComponentInChildren<ToastView>(true);
            Assert.IsTrue(toast.IsShowing, "a toast says why");
            Assert.AreEqual(_game.Services.Resolve<ILocalization>().Get("ui.toast.no_internet"), toast.Text);

            GameFixture.Press(router, "PlayOfflineButton");
            Assert.AreEqual(ScreenId.OfflineSetup, router.Current, "offline play needs no connection");
        }

        [UnityTest]
        public IEnumerator Menu_EveryScreenOpensAndBackReturns()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;

            yield return OpenAndReturn(router, "SettingsButton", ScreenId.Settings);
            yield return OpenAndReturn(router, "ProfileButton", ScreenId.Profile);
            yield return OpenAndReturn(router, "ShopButton", ScreenId.CardCollection);
            yield return OpenAndReturn(router, "PlayOfflineButton", ScreenId.OfflineSetup);
            yield return OpenAndReturn(router, "PlayOnlineButton", ScreenId.Matchmaking);

            GameFixture.Press(router, "PlayWithFriendsButton");
            Assert.AreEqual(ScreenId.PlayWithFriends, router.Current);
            yield return OpenAndReturn(router, "CreateButton", ScreenId.CreateRoom);
            yield return OpenAndReturn(router, "JoinButton", ScreenId.JoinRoom);
            Assert.IsTrue(router.Back());
            Assert.AreEqual(ScreenId.MainMenu, router.Current);
            Assert.AreEqual(1, router.Depth);
        }

        [UnityTest]
        public IEnumerator InviteLink_FreshInstall_ShowsThePrimerThenJoinRoomWithTheCode()
        {
            yield return Boot(freshInstall: true);
            ScreenRouter router = _game.MenuRouter;
            var links = (DeepLinkService)_game.Services.Resolve<IDeepLinks>();

            links.Receive("callbreak://join/ab2-cde");
            yield return null;

            Assert.AreEqual(ScreenId.HowToPlay, router.Current, "the primer comes before a player's first match");
            GameFixture.Press(router, "GotItButton");
            yield return null;

            Assert.AreEqual(ScreenId.JoinRoom, router.Current);
            Assert.AreEqual("AB2CDE", router.Get<UIScreen>(ScreenId.JoinRoom).GetComponentInChildren<CodeInput>(true).Code);
            Assert.IsFalse(links.HasJoinCode, "the link is used up");
            Assert.IsFalse(_game.Dialogs.IsOpen, "no practice offer on the way to a friend's table");
            Assert.IsTrue(router.Back());
            Assert.AreEqual(ScreenId.PlayWithFriends, router.Current, "back leads to where Join Room normally comes from");
        }

        [UnityTest]
        public IEnumerator PlayOffline_ChooseHardAndStart_OpensTheTableWithARunningMatch()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;

            GameFixture.Press(router, "PlayOfflineButton");
            Assert.AreEqual(ScreenId.OfflineSetup, router.Current);
            GameFixture.Press(router, "CardHard");
            GameFixture.Press(router, "StartButton");
            yield return _game.UntilTable();

            var session = _game.Table.Container.Resolve<IMatchSession>();
            Assert.AreEqual(MatchMode.Offline, session.Mode);
            Assert.AreEqual(BotDifficulty.Hard, _game.Flow.CurrentLaunch.Difficulty);
            yield return GameFixture.Until(() => session.Status == SessionStatus.Running && session.View.Hand.Count == 13, "the first hand to be dealt");
        }

        [UnityTest]
        public IEnumerator NameNudge_BackFromTheFirstFinishedMatch_AsksOnceAndOpensTheProfile()
        {
            yield return _game.Boot(
                seed: saves => new SettingsService(saves).IncrementCompletedMatches(),
                extra: builder => builder.RegisterInstance<INetworkStatus>(new Network(online: true)));
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return _game.UntilTable();

            _ = _game.Flow.GoToMenuAsync(MenuEntry.MainMenu, CancellationToken.None);
            yield return _game.UntilMenu();
            yield return null;

            Assert.IsTrue(_game.Dialogs.IsOpen, "GDD 15: a nudge to pick a name and avatar");
            Assert.IsTrue(_game.Settings.NameNudgeSeen);
            _game.PressDialog(confirm: true);
            yield return null;
            Assert.AreEqual(ScreenId.Profile, _game.MenuRouter.Current);
        }

        private IEnumerator Boot(bool freshInstall = false)
        {
            yield return _game.Boot(freshInstall, extra: builder => builder.RegisterInstance<INetworkStatus>(new Network(online: true)));
        }

        private static IEnumerator OpenAndReturn(ScreenRouter router, string button, ScreenId expected)
        {
            ScreenId from = router.Current;
            GameFixture.Press(router, button);
            yield return null;
            Assert.AreEqual(expected, router.Current, button + " opens " + expected);
            Assert.IsTrue(router.Back(), "back leaves " + expected);
            yield return null;
            Assert.AreEqual(from, router.Current);
        }

        private static CanvasGroup GroupOf(UIScreen screen, string buttonName)
        {
            foreach (Button button in screen.GetComponentsInChildren<Button>(true))
            {
                if (button.name == buttonName)
                {
                    return button.GetComponent<CanvasGroup>();
                }
            }

            return null;
        }

        /// <summary>A connection that never changes, so the tests do not depend on the machine's.</summary>
        private sealed class Network : INetworkStatus
        {
            public Network(bool online)
            {
                IsOnline = online;
            }

            public event Action<bool> Changed
            {
                add { }
                remove { }
            }

            public bool IsOnline { get; }
        }
    }
}
