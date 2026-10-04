using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Matchmaking;
using Callbreak.Application.Ports;
using Callbreak.Application.Sessions;
using Callbreak.Infrastructure.Persistence;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The Play Online screen (the owner, 5 Oct 2026; mockups 23 and 24) in the real Boot and Menu
    /// scenes, with a match choice and a search the test watches: PLAY ONLINE opens it, with 50 coins
    /// chosen and the prizes closed, and Back returns; Play Quick searches as Quick Match; choosing
    /// 100 changes the chips and the button, and Play for 100 coins searches with that stake; without
    /// enough coins the "Not enough coins" question comes up and nothing is searched; the "i" shows
    /// the chosen stake's prizes, follows the stake, and closes with ×, the "i", a tap elsewhere or
    /// Android back, while the controls under it keep working; the stake chosen is kept for the next
    /// visit. Nothing on the screen takes coins. Saves go to a temporary folder.
    /// </summary>
    public sealed class PlayOnlineFlowTests
    {
        // Mockup 24's panel, to find places on the screen (mockup pixels).
        private const float PanelX = 305.9f;
        private const float PanelY = 187.1f;
        private const float PanelW = 1064.9f;
        private const float PanelH = 650.8f;

        private GameFixture _game;
        private RecordingChoice _choice;
        private WatchedSearch _search;

        private ScreenRouter Router => _game.MenuRouter;

        private PlayOnlineView View => Router.Get<PlayOnlineView>(ScreenId.PlayOnline);

        private IWallet Wallet => _game.Services.Resolve<IWallet>();

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _choice = new RecordingChoice();
            _search = new WatchedSearch(_choice);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator PlayOnline_OpensWithFiftyChosenAndThePrizesClosed_BackAndAndroidBackReturn()
        {
            yield return Open();

            Assert.AreEqual(ScreenId.PlayOnline, Router.Current, "PLAY ONLINE opens Play Online, not the search");
            Assert.AreEqual(2, Router.Depth);
            Assert.AreEqual(0, _search.Searches, "nothing is searched yet");
            Assert.AreEqual("500", Label("Balance").text, "the wallet, without its +");
            Assert.IsFalse(Has("EarnCoinsButton"));
            Assert.AreEqual("50", Label("ChipLabel0").text);
            Assert.AreEqual("100", Label("ChipLabel1").text);
            AssertChosen(0, "50 is chosen at first");
            Assert.AreEqual(Format("ui.play_online.stake_cta", "50"), Label("PlayStakeLabel").text);
            Assert.AreEqual("Play for 50 coins", Label("PlayStakeLabel").text);
            Assert.IsFalse(View.PrizesOpen, "the prizes open with the i");
            Assert.IsFalse(Shown("Popover"));

            GameFixture.Press(Router, "BackButton");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, Router.Current, "Back returns to the main menu");

            GameFixture.Press(Router, "PlayOnlineButton");
            Assert.AreEqual(ScreenId.PlayOnline, Router.Current);
            Assert.IsTrue(Router.Back());
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, Router.Current, "so does Android back");
            Assert.IsEmpty(_choice.Chosen, "looking chooses nothing");
            Assert.AreEqual(0, _search.Searches);
            Assert.AreEqual(500, Wallet.Coins);
        }

        [UnityTest]
        public IEnumerator PlayQuick_ChoosesQuickMatch_AndOpensTheSearch_WhoseCancelComesBack()
        {
            yield return Open();

            GameFixture.Press(Router, "PlayQuickButton");
            yield return GameFixture.Until(() => _search.Searches == 1, "the search to start");

            Assert.AreEqual(ScreenId.Matchmaking, Router.Current);
            CollectionAssert.AreEqual(new[] { StakeRules.None }, _choice.Chosen, "Quick Match: no stake");
            Assert.AreEqual(StakeRules.None, _search.StakeAtSearch, "chosen before the search starts");
            Assert.AreEqual(500, Wallet.Coins, "Quick Match takes no coins");

            GameFixture.Press(Router, "CancelButton");
            yield return null;
            Assert.AreEqual(ScreenId.PlayOnline, Router.Current, "Cancel goes back to Play Online");
        }

        [UnityTest]
        public IEnumerator Choosing100_ChangesTheChipsAndTheButton_AndPlayFor100SearchesWithThatStake()
        {
            yield return Open();

            GameFixture.Press(Router, "StakeChip1");
            AssertChosen(1, "the 100 chip turns crimson, the 50 chip ivory");
            Assert.AreEqual(Format("ui.play_online.stake_cta", "100"), Label("PlayStakeLabel").text);
            Assert.AreEqual("Play for 100 coins", Label("PlayStakeLabel").text);
            Assert.IsEmpty(_choice.Chosen, "a chip only shows the stake");
            Assert.AreEqual(500, Wallet.Coins, "and takes no coins");
            Assert.AreEqual(ScreenId.PlayOnline, Router.Current);

            GameFixture.Press(Router, "StakeChip0");
            AssertChosen(0, "and back");
            Assert.AreEqual("Play for 50 coins", Label("PlayStakeLabel").text);
            GameFixture.Press(Router, "StakeChip1");

            GameFixture.Press(Router, "PlayStakeButton");
            yield return GameFixture.Until(() => _search.Searches == 1, "the search to start");

            Assert.AreEqual(ScreenId.Matchmaking, Router.Current);
            CollectionAssert.AreEqual(new[] { 100 }, _choice.Chosen);
            Assert.AreEqual(100, _search.StakeAtSearch, "the search looks for 100-coin tables");
            Assert.AreEqual(500, Wallet.Coins, "the stake is the match's to take when it begins, not the screen's");
        }

        [UnityTest]
        public IEnumerator NotEnoughCoins_AsksAndSearchesNothing_NotNowStays_EarnCoinsOpensThePage()
        {
            yield return Open(saves => new Wallet(saves).TrySpend(460));
            Assert.AreEqual(40, Wallet.Coins);

            GameFixture.Press(Router, "PlayStakeButton");
            yield return null;
            Assert.IsTrue(_game.Dialogs.IsOpen, "fewer coins than the stake: a question instead of a search");
            Assert.IsTrue(DialogShows(Text("ui.play_online.not_enough.title")));
            Assert.IsTrue(DialogShows(Format("ui.play_online.not_enough.body", "50", "40")));
            Assert.IsTrue(DialogShows(Text("ui.play_online.not_enough.earn")) && DialogShows(Text("ui.play_online.not_enough.later")));
            Assert.AreEqual(ScreenId.PlayOnline, Router.Current);
            Assert.AreEqual(0, _search.Searches);
            Assert.IsEmpty(_choice.Chosen, "nothing is chosen");

            _game.PressDialog(confirm: false);
            yield return null;
            Assert.IsFalse(_game.Dialogs.IsOpen, "Not now closes it");
            Assert.AreEqual(ScreenId.PlayOnline, Router.Current);

            GameFixture.Press(Router, "PlayStakeButton");
            yield return null;
            _game.PressDialog(confirm: true);
            yield return null;
            Assert.AreEqual(ScreenId.EarnCoins, Router.Current, "Earn Coins opens the page");

            Assert.IsTrue(Router.Back());
            yield return null;
            Assert.AreEqual(ScreenId.PlayOnline, Router.Current, "and comes back to Play Online");
            AssertChosen(0, "with 50 still chosen");
            Assert.AreEqual(0, _search.Searches);
            Assert.IsEmpty(_choice.Chosen);
            Assert.AreEqual(40, Wallet.Coins);

            // Quick Match needs no coins.
            GameFixture.Press(Router, "PlayQuickButton");
            yield return GameFixture.Until(() => _search.Searches == 1, "the search to start");
            CollectionAssert.AreEqual(new[] { StakeRules.None }, _choice.Chosen);
        }

        [UnityTest]
        public IEnumerator ThePrizes_AreTheChosenStakes_FollowTheStake_AndCloseWithTheXOrATapElsewhere()
        {
            yield return Open();

            GameFixture.Press(Router, "InfoButton");
            Assert.IsTrue(View.PrizesOpen && Shown("Popover"), "the i opens the prizes");
            Assert.AreEqual(Format("ui.play_online.prizes_title", "50"), Label("PrizeHeading").text);
            Assert.AreEqual("50-coin prizes", Label("PrizeHeading").text);
            AssertPrizes(new[] { "100", "40", "30", "20" });
            Assert.AreEqual(Text("ui.play_online.place_1"), Label("Place0").text);
            Assert.AreEqual("4th", Label("Place3").text);

            GameFixture.Press(Router, "StakeChip1");
            Assert.IsTrue(View.PrizesOpen, "choosing a stake keeps them open");
            Assert.AreEqual("100-coin prizes", Label("PrizeHeading").text, "and shows that stake's");
            AssertPrizes(new[] { "200", "80", "60", "40" });

            GameFixture.Press(Router, "CloseButton");
            Assert.IsFalse(View.PrizesOpen, "x closes them");
            yield return GameFixture.Until(() => !Shown("Popover"), "the popover to close");

            GameFixture.Press(Router, "InfoButton");
            Assert.AreEqual("100-coin prizes", Label("PrizeHeading").text);
            GameFixture.Press(Router, "InfoButton");
            Assert.IsFalse(View.PrizesOpen, "the i closes them again");
            yield return GameFixture.Until(() => !Shown("Popover"), "the popover to close");

            GameFixture.Press(Router, "InfoButton");
            GameFixture.Press(Router, "PopoverDismiss");
            Assert.IsFalse(View.PrizesOpen, "a tap elsewhere closes them");
            yield return GameFixture.Until(() => !Shown("Popover"), "the popover to close");
            Assert.IsFalse(Shown("PopoverDismiss"), "and nothing catches taps any more");

            GameFixture.Press(Router, "InfoButton");
            Assert.IsTrue(Router.Back(), "Android back");
            Assert.IsFalse(View.PrizesOpen, "closes the prizes first");
            Assert.AreEqual(ScreenId.PlayOnline, Router.Current);

            Assert.IsEmpty(_choice.Chosen, "looking at prizes chooses nothing");
            Assert.AreEqual(0, _search.Searches);
            Assert.AreEqual(500, Wallet.Coins, "and takes no coins");
        }

        [UnityTest]
        public IEnumerator WhileThePrizesAreOpen_ATapOnTheScreenClosesThem_ButTheControlsStillTakeTheirTaps()
        {
            yield return Open();
            GameFixture.Press(Router, "InfoButton");
            yield return GameFixture.Until(() => Content().localScale == Vector3.one, "the screen to settle");

            // What a touch reaches, as the event system decides it.
            Assert.AreEqual("PopoverDismiss", TopHit(539f, 620f).name, "the empty panel under Quick Match");
            Assert.AreEqual("PopoverDismiss", TopHit(140f, 600f).name, "the scenery");
            Assert.AreEqual("StakeChip0", TopHit(914f, 523f).name, "a chip");
            Assert.AreEqual("PlayQuickButton", TopHit(470f, 738f).transform.parent.name, "Play Quick");
            Assert.AreEqual("PlayStakeButton", TopHit(930f, 738f).transform.parent.name, "Play for 50 coins");
            Assert.IsTrue(TopHit(1430f, 700f).transform.IsChildOf(Find("Popover")), "the popover itself");

            GameFixture.Press(Router, "InfoButton");
            yield return GameFixture.Until(() => !Shown("Popover"), "the popover to close");
            Assert.AreNotEqual("PopoverDismiss", TopHit(539f, 620f).name, "closed, nothing catches the tap");
        }

        [UnityTest]
        public IEnumerator TheChosenStake_IsKeptForTheNextVisit()
        {
            yield return Open();

            GameFixture.Press(Router, "StakeChip1");
            GameFixture.Press(Router, "InfoButton");
            GameFixture.Press(Router, "BackButton");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, Router.Current);

            GameFixture.Press(Router, "PlayOnlineButton");
            yield return null;
            AssertChosen(1, "100 is still chosen");
            Assert.AreEqual("Play for 100 coins", Label("PlayStakeLabel").text);
            Assert.IsFalse(View.PrizesOpen, "the prizes start closed again");
            Assert.IsEmpty(_choice.Chosen);
        }

        private IEnumerator Open(Action<ISaveStore> seed = null)
        {
            yield return _game.Boot(seed: seed, extra: builder =>
            {
                builder.RegisterInstance<INetworkStatus>(new FixedNetwork(online: true));
                builder.RegisterInstance<IOnlineMatchChoice>(_choice);
                builder.RegisterInstance<IMatchmakingService>(_search);
            });
            GameFixture.Press(Router, "PlayOnlineButton");
            Assert.AreEqual(ScreenId.PlayOnline, Router.Current, "PLAY ONLINE opens Play Online");
            yield return null;
        }

        private void AssertChosen(int chip, string message)
        {
            Assert.AreEqual(chip, View.SelectedChip, message);
            for (int i = 0; i < 2; i++)
            {
                Assert.AreEqual(i == chip, Shown("ChipSelected" + i), message + ": chip " + i + " crimson");
                Assert.AreEqual(i != chip, Shown("ChipNormal" + i), message + ": chip " + i + " ivory");
            }

            Assert.AreNotEqual(Label("ChipLabel0").color, Label("ChipLabel1").color, "the chosen amount is ivory, the other dark");
        }

        private void AssertPrizes(string[] expected)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], Label("PrizeAmount" + i).text, "place " + (i + 1));
            }
        }

        private Transform Find(string name)
        {
            foreach (Transform child in View.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            Assert.Fail("Play Online has no " + name + ".");
            return null;
        }

        private bool Has(string name)
        {
            foreach (Transform child in View.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return true;
                }
            }

            return false;
        }

        private bool Shown(string name) => Find(name).gameObject.activeInHierarchy;

        private TMP_Text Label(string name) => Find(name).GetComponent<TMP_Text>();

        private RectTransform Content() => View.SlideTarget;

        /// <summary>What a touch at this point of mockup 24 reaches first (the canvas is screen-space overlay).</summary>
        private GameObject TopHit(float mockX, float mockY)
        {
            var panel = (RectTransform)Find("Panel");
            Rect r = panel.rect;
            var local = new Vector2(r.xMin + ((mockX - PanelX) / PanelW * r.width), r.yMax - ((mockY - PanelY) / PanelH * r.height));
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, panel.TransformPoint(local));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.IsNotEmpty(hits, "something takes a tap at " + mockX + ", " + mockY);
            return hits[0].gameObject;
        }

        private string Text(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private string Format(string key, params object[] args) => _game.Services.Resolve<ILocalization>().Format(key, args);

        private bool DialogShows(string text)
        {
            DialogView dialog = _game.Project.GetComponentInChildren<DialogView>(true);
            foreach (TMP_Text label in dialog.GetComponentsInChildren<TMP_Text>(false))
            {
                if (label.text == text)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The player's online match choice, with every choice made on the screen.</summary>
        private sealed class RecordingChoice : IOnlineMatchChoice
        {
            public int Stake { get; private set; } = StakeRules.None;

            public List<int> Chosen { get; } = new List<int>();

            public void Choose(int stake)
            {
                if (stake != StakeRules.None && !StakeRules.IsStake(stake))
                {
                    throw new ArgumentOutOfRangeException(nameof(stake), stake, "Quick Match (0) or a stake.");
                }

                Stake = stake;
                Chosen.Add(stake);
            }
        }

        /// <summary>A search that never finds a table: it counts its searches and notes the stake chosen when each began.</summary>
        private sealed class WatchedSearch : IMatchmakingService
        {
            private readonly IOnlineMatchChoice _choice;

            public WatchedSearch(IOnlineMatchChoice choice)
            {
                _choice = choice;
            }

            public int Searches { get; private set; }

            public int StakeAtSearch { get; private set; } = -1;

            public IMatchSession Match => null;

            public async Task<MatchmakingResult> FindMatchAsync(IProgress<MatchmakingProgress> progress, CancellationToken ct)
            {
                Searches++;
                StakeAtSearch = _choice.Stake;
                try
                {
                    await Task.Delay(Timeout.Infinite, ct);
                }
                catch (OperationCanceledException)
                {
                    // Cancel on the search screen.
                }

                return new MatchmakingResult(MatchmakingError.Cancelled);
            }
        }
    }
}
