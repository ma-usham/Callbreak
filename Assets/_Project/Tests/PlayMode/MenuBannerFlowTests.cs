using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The banner ad in the bottom-left corner of the main menu (the owner, 27 Oct 2026, ADR 052) in the
    /// real Boot, Menu and Table scenes, over a scripted banner network (as AdMob is on phones) and the
    /// game's own No Ads. While No Ads isn't owned the menu makes room before it first shows (the
    /// centre column a little smaller, the footer line gone) and the banner sits in the slot's
    /// bottom-left corner while the bare main menu is in front; under settings and the shop it is off
    /// the screen. Buying No Ads takes it away and the menu keeps its look while it is up; the next
    /// main menu, for a player who owns No Ads, is as it always was and requests nothing. Saves go to a
    /// temporary folder.
    /// </summary>
    public sealed class MenuBannerFlowTests
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
        public IEnumerator WithoutNoAds_TheMenuMakesRoom_AndTheBannerSitsBottomLeft_OnlyOverTheBareMenu()
        {
            yield return Boot();
            MainMenuView menu = Object.FindFirstObjectByType<MainMenuView>();

            Assert.IsTrue(menu.HasBannerRoom, "room in the bottom-left corner for the banner");
            Assert.Less(Find(menu, "Centre").transform.localScale.x, 1f, "the centre column is drawn a little smaller");
            Assert.IsFalse(Find(menu, "Footer").activeInHierarchy, "the footer line makes way");

            yield return GameFixture.Until(() => _network.Banner != null && _network.Banner.OnScreen, "the banner on the screen");
            Assert.IsTrue(menu.TryGetBannerSlot(out BannerSlot slot));
            Assert.IsTrue(slot.AtBottom);
            Assert.AreEqual(slot.X, _network.Banner.X, "in the slot's left corner");
            Assert.AreEqual(slot.Y + slot.Height, _network.Banner.Y + _network.Banner.Height, "on its bottom edge");

            ScreenRouter router = _game.MenuRouter;
            GameFixture.PressIn(menu, "SettingsButton");
            yield return null;
            Assert.AreNotEqual(ScreenId.MainMenu, router.Current, "settings open");
            Assert.IsFalse(_network.Banner.OnScreen, "never over settings");
            Assert.IsTrue(router.Back());
            yield return null;
            Assert.IsTrue(_network.Banner.OnScreen, "back over the bare main menu");

            GameFixture.PressIn(menu, "ShopButton");
            yield return null;
            Assert.IsFalse(_network.Banner.OnScreen, "never over the shop");
            Assert.IsTrue(router.Back());
            yield return null;
            Assert.IsTrue(_network.Banner.OnScreen);
            Assert.AreEqual(1, _network.Loads, "one banner all along");
        }

        [UnityTest]
        public IEnumerator BuyingNoAds_TakesTheBannerAway_AndTheMenuKeepsItsLook()
        {
            yield return Boot();
            MainMenuView menu = Object.FindFirstObjectByType<MainMenuView>();
            yield return GameFixture.Until(() => _network.Banner != null && _network.Banner.OnScreen, "the banner on the screen");

            yield return BuyNoAds();
            yield return null;

            Assert.IsFalse(_network.Banner.OnScreen);
            Assert.AreEqual(1, _network.Banner.Destroys, "the banner is released");
            Assert.IsTrue(menu.HasBannerRoom, "nothing moves under the player");
            yield return null;
            Assert.AreEqual(1, _network.Loads, "and no other is requested");
        }

        [UnityTest]
        public IEnumerator TheNextMainMenu_ForAPlayerWhoOwnsNoAds_IsAsItAlwaysWas_AndRequestsNothing()
        {
            yield return Boot();
            yield return BuyNoAds();

            // To the table and back: the Menu scene loads again.
            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return _game.UntilTable();
            int requested = _network.Starts + _network.Loads;
            GameFixture.PressIn(Object.FindFirstObjectByType<TableView>(), "HomeButton");
            GameFixture.Press(_game.TableRouter, "LeaveButton");
            yield return null;
            _game.PressDialog(confirm: true);
            yield return _game.UntilMenu();
            yield return null;

            MainMenuView menu = Object.FindFirstObjectByType<MainMenuView>();
            Assert.IsFalse(menu.HasBannerRoom);
            Assert.AreEqual(Vector3.one, Find(menu, "Centre").transform.localScale, "the centre column as it always was");
            Assert.IsFalse(menu.TryGetBannerSlot(out _), "no slot");
            Assert.AreEqual(requested, _network.Starts + _network.Loads, "nothing is requested");
        }

        private IEnumerator Boot()
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<IBannerAdNetwork>(_network));
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

            Assert.Fail("the main menu has " + name);
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
