using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Flow;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Ports;
using Callbreak.Application.Profile;
using Callbreak.Application.Rooms;
using Callbreak.Application.Sessions;
using Callbreak.Application.Settings;
using Callbreak.Core.Cards;
using Callbreak.Infrastructure.Audio;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// What the menu shows and asks in its less common states, in the real scenes: a saved match
    /// offered back by Play Offline (no Resume banner since ADR 057), dialogs that are closed rather than answered, a failed Quick
    /// Match search (screen 05), the volume slider (screen 11) and the UI sound cues (GDD §14).
    /// </summary>
    public sealed class MenuStateTests
    {
        private const float TurnTimeout = 40f;
        private const string PremultipliedShader = "Callbreak/UI/Premultiplied";

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
        public IEnumerator ASavedMatch_ShowsNoResumeBanner_PlayOfflineOffersItBack_AndResumesIt()
        {
            yield return Boot();
            yield return StartAndLeaveAnOfflineMatch();

            // ADR 057 (the owner, 28 Oct 2026): no "Resume match" banner at the bottom of the menu.
            UIScreen menu = MainMenu();
            Assert.IsFalse(GameFixture.CanPress(menu, "ResumeBanner"), "no Resume banner");
            Assert.IsTrue(Footer(menu).activeSelf, "the footer line stays");
            Assert.IsFalse(_game.Dialogs.IsOpen, "the question is only asked when the app opens");

            // Play Offline asks "Resume your offline match?"; Resume brings the match back.
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            yield return null;
            Assert.IsTrue(_game.Dialogs.IsOpen, "\"Resume your match?\"");
            _game.PressDialog(confirm: true);
            yield return _game.UntilTable();
            var resumed = _game.Table.Container.Resolve<IMatchSession>();
            yield return GameFixture.Until(() => resumed.Status == SessionStatus.Running, "the resumed match to run");
            Assert.AreEqual(13, resumed.View.Hand.Count, "the same hand is back");
        }

        [UnityTest]
        public IEnumerator ResumeOffer_ClosedWithoutAnAnswer_DoesNothing_NewGameOpensTheSetup()
        {
            yield return Boot();
            yield return StartAndLeaveAnOfflineMatch();
            ScreenRouter router = _game.MenuRouter;

            GameFixture.Press(router, "PlayOfflineButton");
            yield return null;
            Assert.IsTrue(_game.Dialogs.IsOpen, "\"Resume your match?\"");
            Assert.IsTrue(_game.Dialogs.HandleBack(), "back closes the question");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, router.Current, "closing the question is not \"New Game\"");

            GameFixture.Press(router, "PlayOfflineButton");
            yield return null;
            _game.PressDialog(confirm: false);
            yield return null;
            Assert.AreEqual(ScreenId.OfflineSetup, router.Current, "\"New Game\" opens the offline setup");
        }

        [UnityTest]
        public IEnumerator Dialog_StillOpenWhenItsSceneGoesAway_ClosesWithIt()
        {
            yield return Boot();
            _game.MenuRouter.Back();
            yield return null;
            Assert.IsTrue(_game.Dialogs.IsOpen, "\"Exit Callbreak?\"");

            _ = _game.Flow.StartMatchAsync(new MatchLaunchRequest(MatchMode.Offline, BotDifficulty.Easy), CancellationToken.None);
            yield return _game.UntilTable();

            Assert.IsFalse(_game.Dialogs.IsOpen, "a menu question does not stay over the table");
        }

        [UnityTest]
        public IEnumerator Matchmaking_SearchFails_AsksRetryOrPlayOffline()
        {
            var matchmaking = new FailingMatchmaking();
            yield return _game.Boot(extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IMatchmakingService>(matchmaking);
            });
            ScreenRouter router = _game.MenuRouter;

            GameFixture.Press(router, "PlayOnlineButton");
            GameFixture.Press(router, "PlayQuickButton");
            yield return GameFixture.Until(() => _game.Dialogs.IsOpen, "the search to fail and say so");
            Assert.AreEqual(ScreenId.Matchmaking, router.Current, "GDD 11 screen 05: the failure is answered on this screen");
            Assert.AreEqual(1, matchmaking.Searches);

            _game.PressDialog(confirm: true);
            yield return GameFixture.Until(() => matchmaking.Searches == 2 && _game.Dialogs.IsOpen, "Retry to search again");
            Assert.AreEqual(ScreenId.Matchmaking, router.Current);

            _game.PressDialog(confirm: false);
            yield return null;
            Assert.AreEqual(ScreenId.OfflineSetup, router.Current, "\"Play Offline\" opens the offline setup");
            Assert.AreEqual(3, router.Depth, "in the search screen's place, above Play Online");
            Assert.AreEqual(2, matchmaking.Searches);
        }

        [UnityTest]
        public IEnumerator Matchmaking_FailureClosedWithoutAnAnswer_ReturnsToPlayOnline()
        {
            var matchmaking = new FailingMatchmaking();
            yield return _game.Boot(extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IMatchmakingService>(matchmaking);
            });
            ScreenRouter router = _game.MenuRouter;

            GameFixture.Press(router, "PlayOnlineButton");
            GameFixture.Press(router, "PlayQuickButton");
            yield return GameFixture.Until(() => _game.Dialogs.IsOpen, "the search to fail and say so");
            Assert.IsTrue(_game.Dialogs.HandleBack());
            yield return null;

            Assert.AreEqual(ScreenId.PlayOnline, router.Current, "back where the search was started");
            Assert.AreEqual(1, matchmaking.Searches, "nothing searches behind the player's back");
        }

        [UnityTest]
        public IEnumerator Matchmaking_PlayersFound_EveryOneReadsReady_TheBotsToo()
        {
            // ADR 025: a bot that fills the table looks like any other player found.
            yield return _game.Boot(extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IMatchmakingService>(new BotTableMatchmaking());
            });
            ScreenRouter router = _game.MenuRouter;
            var text = _game.Services.Resolve<ILocalization>();
            string ready = text.Get("ui.matchmaking.seat_ready");

            GameFixture.Press(router, "PlayOnlineButton");
            GameFixture.Press(router, "PlayQuickButton");
            MatchmakingSeat[] seats = null;
            yield return GameFixture.Until(
                () =>
                {
                    Component screen = router.Get<UIScreen>(ScreenId.Matchmaking);
                    seats = screen != null ? screen.GetComponentsInChildren<MatchmakingSeat>(true) : null;
                    return seats != null && seats.Length == 4 && Array.TrueForAll(seats, s => s.IsFilled);
                },
                "the four players found to show");

            foreach (MatchmakingSeat seat in seats)
            {
                Assert.AreEqual(ready, seat.Status, seat.name + ": every player found reads \"Ready\", none \"BOT\"");
            }
        }

        [UnityTest]
        public IEnumerator Settings_MovingTheVolume_AppliesAtOnce_AndIsWrittenWhenTheScreenCloses()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "SettingsButton");
            yield return null;
            Slider slider = router.Get<UIScreen>(ScreenId.Settings).GetComponentInChildren<Slider>(true);
            Assert.AreEqual(SettingsService.DefaultVolume, slider.value, 0.001f);

            slider.value = 0.3f;

            Assert.AreEqual(0.3f, _game.Settings.Volume, 0.001f, "GDD 11 screen 11: changes apply instantly");
            Assert.AreEqual(SettingsService.DefaultVolume, SavedVolume(), 0.001f, "the file is not written on every movement");

            Assert.IsTrue(router.Back());
            yield return null;
            Assert.AreEqual(0.3f, SavedVolume(), 0.001f, "closing the screen writes it");
        }

        [UnityTest]
        public IEnumerator UiCues_TwoInOneFrame_SoundOnce_AndTheFirstOneWins()
        {
            yield return Boot();
            AudioService audio = Audio();
            yield return null;
            int before = audio.CuesPlayed;

            audio.Play(SoundCue.UiBack);
            audio.Play(SoundCue.UiTap);

            Assert.AreEqual(before + 1, audio.CuesPlayed, "one press, one sound");
            Assert.AreEqual(SoundCue.UiBack, audio.LastCue, "the first cue of the frame is the one heard");

            yield return null;
            audio.Play(SoundCue.UiTap);
            Assert.AreEqual(before + 2, audio.CuesPlayed, "the next frame's tap sounds again");

            audio.Play(SoundCue.CardPlay);
            audio.Play(SoundCue.CardPlay);
            Assert.AreEqual(before + 4, audio.CuesPlayed, "game sounds are not limited");
        }

        [UnityTest]
        public IEnumerator Buttons_TapBackAndToggle_EachHaveTheirOwnCue()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            AudioService audio = Audio();
            yield return null;

            int before = audio.CuesPlayed;
            Click(MainMenu(), "SettingsButton");
            Assert.AreEqual(ScreenId.Settings, router.Current);
            Assert.AreEqual(before + 1, audio.CuesPlayed, "GDD 14: a button press sounds");
            Assert.AreEqual(SoundCue.UiTap, audio.LastCue);

            yield return null;
            UIScreen settings = router.Get<UIScreen>(ScreenId.Settings);
            Click(settings, "ReduceMotionToggle");
            Assert.AreEqual(before + 2, audio.CuesPlayed);
            Assert.AreEqual(SoundCue.Toggle, audio.LastCue, "a switch has the toggle cue");

            yield return null;
            Click(settings, "BackButton");
            Assert.AreEqual(ScreenId.MainMenu, router.Current);
            Assert.AreEqual(before + 3, audio.CuesPlayed);
            Assert.AreEqual(SoundCue.UiBack, audio.LastCue, "back has its own cue");

            // A dialog button is heard once, although the dialog and the button both ask for a cue.
            yield return null;
            router.Back();
            yield return null;
            Assert.IsTrue(_game.Dialogs.IsOpen, "\"Exit Callbreak?\"");
            int beforeDialog = audio.CuesPlayed;
            Click(_game.Project.GetComponentInChildren<DialogView>(true), "CancelButton");
            Assert.IsFalse(_game.Dialogs.IsOpen);
            Assert.AreEqual(beforeDialog + 1, audio.CuesPlayed);
            Assert.AreEqual(SoundCue.UiBack, audio.LastCue);
        }

        [UnityTest]
        public IEnumerator Avatar_EveryPresetHasAPicture_AndTheHeadersShowThePickedOne()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            var library = _game.Services.Resolve<AvatarLibrary>();
            var profile = _game.Services.Resolve<IProfileService>();
            Assert.AreEqual(profile.AvatarCount, library.Count, "the picker's presets and the pictures are the same count");
            for (int i = 0; i < library.Count; i++)
            {
                Assert.IsNotNull(library.Get(i), "preset " + i + " has a picture");
            }

            profile.SetAvatar(2);
            Assert.IsTrue(Shows(MainMenu(), library.Get(2)), "GDD 11 screen 00: the menu shows who you are");
            Assert.IsFalse(Shows(MainMenu(), library.Get(0)), "and no longer the default picture");

            GameFixture.Press(router, "SettingsButton");
            yield return null;
            Assert.IsTrue(Shows(router.Get<UIScreen>(ScreenId.Settings), library.Get(2)), "Settings shows it");
            Assert.IsTrue(router.Back());
            GameFixture.Press(router, "PlayOfflineButton");
            yield return null;
            Assert.IsTrue(Shows(router.Get<UIScreen>(ScreenId.OfflineSetup), library.Get(2)), "Offline setup shows it");
        }

        [UnityTest]
        public IEnumerator Fan_ShowsTheFourAcesOfTheEquippedDeck()
        {
            yield return Boot();
            var cosmetics = _game.Services.Resolve<ICosmeticsService>();
            var cards = _game.Services.Resolve<CardSpriteProvider>();
            UIScreen menu = MainMenu();
            Assert.GreaterOrEqual(cosmetics.DeckIds.Count, 3, "three decks to choose from");

            // Every deck but the first costs coins since 6 Oct 2026 (500 and 750): buy them all.
            _game.Services.Resolve<IWallet>().Add(750);
            var shop = _game.Services.Resolve<ICosmeticsShop>();
            foreach (string deck in cosmetics.DeckIds)
            {
                Assert.AreNotEqual(PurchaseResult.NotEnoughCoins, shop.Buy(CosmeticKind.Deck, deck), deck);
            }

            foreach (string deck in cosmetics.DeckIds)
            {
                cosmetics.SelectDeck(deck);
                for (int suit = 0; suit < 4; suit++)
                {
                    Sprite ace = cards.Face(deck, new Card((Suit)suit, Rank.Ace));
                    Assert.IsNotNull(ace, deck + " has its ace of " + (Suit)suit);
                    Assert.IsTrue(Shows(menu, ace), "GDD 13: the fan shows the " + deck + " ace of " + (Suit)suit);
                    Assert.IsTrue(Shows(menu, ace, PremultipliedShader), "ADR 054: the shared ace of " + (Suit)suit + " draws premultiplied");
                }

                Assert.IsTrue(Shows(menu, cards.Base(deck)), "ADR 053: the aces lie on the " + deck + " base");
            }
        }

        /// <summary>True when an image on the screen draws that sprite with that shader.</summary>
        private static bool Shows(UIScreen screen, Sprite sprite, string shader)
        {
            foreach (Image image in screen.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == sprite && image.material.shader.name == shader)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when an image on the screen draws that sprite.</summary>
        private static bool Shows(UIScreen screen, Sprite sprite)
        {
            Assert.IsNotNull(sprite);
            foreach (Image image in screen.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == sprite)
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerator Boot()
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true)));
        }

        /// <summary>Starts an offline match and leaves it through the match menu, which keeps it for later.</summary>
        private IEnumerator StartAndLeaveAnOfflineMatch()
        {
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return _game.UntilTable();
            var session = _game.Table.Container.Resolve<IMatchSession>();
            yield return GameFixture.Until(() => session.Status == SessionStatus.Running && session.View.Hand.Count == 13, "the first hand to be dealt", TurnTimeout);

            GameFixture.PressIn(UnityEngine.Object.FindFirstObjectByType<TableView>(), "HomeButton");
            GameFixture.Press(_game.TableRouter, "LeaveButton");
            yield return null;
            _game.PressDialog(confirm: true);
            yield return _game.UntilMenu();
            yield return null;
        }

        private UIScreen MainMenu() => _game.MenuRouter.Get<UIScreen>(ScreenId.MainMenu);

        private AudioService Audio() => (AudioService)_game.Services.Resolve<IAudioService>();

        private static GameObject Footer(UIScreen menu)
        {
            Transform footer = menu.transform.Find("Content/Centre/Footer");
            Assert.IsNotNull(footer, "the main menu has its footer line");
            return footer.gameObject;
        }

        private float SavedVolume()
        {
            Assert.IsTrue(_game.Services.Resolve<ISaveStore>().TryLoad(SettingsService.SaveKey, out SettingsData saved), "a settings file exists");
            return saved.volume;
        }

        /// <summary>A tap as the event system delivers it (down, up, click): every handler on the button hears it.</summary>
        private static void Click(Component root, string buttonName)
        {
            Assert.IsNotNull(root, "the view with " + buttonName + " exists");
            foreach (Button button in root.GetComponentsInChildren<Button>(false))
            {
                if (button.name == buttonName)
                {
                    Assert.IsTrue(button.IsInteractable(), buttonName + " can be tapped");
                    var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerUpHandler);
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    return;
                }
            }

            Assert.Fail("No active button " + buttonName + " under " + root.name + ".");
        }

        /// <summary>A Quick Match that finds three bots at once, then keeps the search open until it is cancelled.</summary>
        private sealed class BotTableMatchmaking : IMatchmakingService
        {
            public IMatchSession Match => null;

            public Task<MatchmakingResult> FindMatchAsync(IProgress<MatchmakingProgress> progress, CancellationToken ct)
            {
                var seats = new List<RoomSeat>
                {
                    new RoomSeat(0, true, "Hari", 1, CosmeticsService.ClassicCrimson, false, true, false, true),
                    new RoomSeat(1, true, "Asha", 2, CosmeticsService.ClassicCrimson, false, true, true, false),
                    new RoomSeat(2, true, "Bikash", 3, CosmeticsService.ClassicCrimson, false, true, true, false),
                    new RoomSeat(3, true, "Suman", 4, CosmeticsService.ClassicCrimson, false, true, true, false),
                };
                progress?.Report(new MatchmakingProgress(seats, 2.0, false));
                var search = new TaskCompletionSource<MatchmakingResult>();
                ct.Register(() => search.TrySetResult(new MatchmakingResult(MatchmakingError.Cancelled)));
                return search.Task;
            }
        }

        /// <summary>A Quick Match that can never find a table.</summary>
        private sealed class FailingMatchmaking : IMatchmakingService
        {
            public int Searches { get; private set; }

            public IMatchSession Match => null;

            public Task<MatchmakingResult> FindMatchAsync(IProgress<MatchmakingProgress> progress, CancellationToken ct)
            {
                Searches++;
                return Task.FromResult(new MatchmakingResult(MatchmakingError.ConnectionFailed));
            }
        }
    }
}
