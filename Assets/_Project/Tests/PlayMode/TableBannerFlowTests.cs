using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Sessions;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The banner ad at the top of the table (the owner, 26 Oct 2026, ADR 049) in the real Boot, Menu
    /// and Table scenes, over a scripted banner network (as AdMob is on phones) and the game's own No
    /// Ads, in an offline match. While No Ads isn't owned the table makes room when it opens (the logo
    /// goes, the round line moves beside Scores, the top seat and the trick come down) and the banner
    /// shows at the top of its slot while the bare table is in front; under the scorecard, settings and
    /// the match menu it is off the screen. Buying No Ads takes it away and the table keeps its look
    /// for the rest of the match; a player who owns No Ads opens the table as it always was and nothing
    /// is requested. Saves go to a temporary folder.
    /// </summary>
    public sealed class TableBannerFlowTests
    {
        private GameFixture _game;
        private ScriptedBannerNetwork _network;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _network = new ScriptedBannerNetwork();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator WithoutNoAds_TheTableMakesRoom_AndTheBannerShowsAtTheTop_OnlyOverTheBareTable()
        {
            yield return Boot();
            yield return StartOfflineMatch();
            TableView table = Object.FindFirstObjectByType<TableView>();

            Assert.IsTrue(table.HasBannerRoom, "room at the top for the banner");
            Assert.IsFalse(Find(table, "Logo").activeInHierarchy, "the logo makes way");
            Assert.IsFalse(Find(table, "RoundLabel").activeInHierarchy);
            GameObject beside = Find(table, "RoundBeside");
            Assert.IsTrue(beside.activeInHierarchy, "the round line stands beside Scores");
            StringAssert.Contains("1", beside.GetComponent<TMPro.TMP_Text>().text, "and says which round it is");

            yield return GameFixture.Until(() => _network.Banner != null && _network.Banner.OnScreen, "the banner on the screen");
            Assert.IsTrue(table.TryGetBannerSlot(out BannerSlot slot));
            Assert.AreEqual(slot.Y, _network.Banner.Y, "at the top of its slot");
            Assert.GreaterOrEqual(_network.Banner.X, slot.X);
            Assert.LessOrEqual(_network.Banner.X + _network.Banner.Width, slot.X + slot.Width);
            Assert.AreEqual(Screen.width / 2, slot.CentreX, "centred on the screen's middle");

            ScreenRouter router = _game.TableRouter;
            GameFixture.PressIn(table, "ScoresButton");
            Assert.AreEqual(ScreenId.Scorecard, router.Current);
            yield return null;
            Assert.IsFalse(_network.Banner.OnScreen, "never over the scorecard");
            Assert.IsTrue(router.Back());
            yield return null;
            Assert.IsTrue(_network.Banner.OnScreen, "back over the bare table");

            GameFixture.PressIn(table, "SettingsButton");
            yield return null;
            Assert.IsFalse(_network.Banner.OnScreen, "never over settings");
            Assert.IsTrue(router.Back());
            yield return null;

            GameFixture.PressIn(table, "HomeButton");
            Assert.AreEqual(ScreenId.MatchMenu, router.Current);
            yield return null;
            Assert.IsFalse(_network.Banner.OnScreen, "never over the match menu");
            Assert.AreEqual(1, _network.Loads, "one banner all along");
        }

        [UnityTest]
        public IEnumerator BuyingNoAds_TakesTheBannerAway_AndTheTableKeepsItsLookForTheMatch()
        {
            yield return Boot();
            yield return StartOfflineMatch();
            TableView table = Object.FindFirstObjectByType<TableView>();
            yield return GameFixture.Until(() => _network.Banner != null && _network.Banner.OnScreen, "the banner on the screen");

            yield return BuyNoAds();
            yield return null;

            Assert.IsFalse(_network.Banner.OnScreen);
            Assert.AreEqual(1, _network.Banner.Destroys, "the banner is released");
            Assert.IsTrue(table.HasBannerRoom, "nothing moves under the player mid-match");
            yield return null;
            Assert.AreEqual(1, _network.Loads, "and no other is requested");
        }

        [UnityTest]
        public IEnumerator APlayerWhoOwnsNoAds_GetsTheTableAsItAlwaysWas_AndNothingIsRequested()
        {
            yield return Boot();
            yield return BuyNoAds();
            yield return StartOfflineMatch();
            TableView table = Object.FindFirstObjectByType<TableView>();
            yield return null;

            Assert.IsFalse(table.HasBannerRoom);
            Assert.IsTrue(Find(table, "Logo").activeInHierarchy, "the logo stays");
            Assert.IsTrue(Find(table, "RoundLabel").activeInHierarchy, "the round line stays in the middle");
            Assert.IsFalse(Find(table, "RoundBeside").activeInHierarchy);
            Assert.IsFalse(table.TryGetBannerSlot(out _), "no slot");
            Assert.AreEqual(0, _network.Starts + _network.Loads, "nothing is requested");
        }

        private IEnumerator Boot()
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<IBannerAdNetwork>(_network));
        }

        private IEnumerator StartOfflineMatch()
        {
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return _game.UntilTable();
            IMatchSession session = _game.Table.Container.Resolve<IMatchSession>();
            yield return GameFixture.Until(() => session.Status == SessionStatus.Running && session.View.Hand.Count == 13, "the first hand to be dealt");
        }

        private IEnumerator BuyNoAds()
        {
            INoAds noAds = _game.Services.Resolve<INoAds>();
            Task<NoAdsPurchase> buying = noAds.BuyAsync(CancellationToken.None);
            yield return GameFixture.Until(() => buying.IsCompleted, "No Ads to be bought");
            Assert.IsTrue(noAds.Owned, "No Ads is owned");
        }

        private static GameObject Find(Component root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t.gameObject;
                }
            }

            Assert.Fail("the table has " + name);
            return null;
        }

        /// <summary>A banner network that starts and loads at once; its banner is tiny, so it fits any Game view.</summary>
        private sealed class ScriptedBannerNetwork : IBannerAdNetwork
        {
            public int Starts { get; private set; }

            public int Loads { get; private set; }

            public ScriptedBanner Banner { get; private set; }

            public Task<bool> StartAsync()
            {
                Starts++;
                return Task.FromResult(true);
            }

            public Task<IBannerAd> LoadAsync()
            {
                Loads++;
                Banner = new ScriptedBanner();
                return Task.FromResult<IBannerAd>(Banner);
            }
        }

        private sealed class ScriptedBanner : IBannerAd
        {
            public int Width => 32;

            public int Height => 5;

            public bool OnScreen { get; private set; }

            public int X { get; private set; }

            public int Y { get; private set; }

            public int Destroys { get; private set; }

            public void ShowAt(int x, int y)
            {
                X = x;
                Y = y;
                OnScreen = true;
            }

            public void Hide() => OnScreen = false;

            public void Destroy()
            {
                Destroys++;
                OnScreen = false;
            }
        }
    }
}
