using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The coin wallet on the main menu (mockup 23) and the Earn Coins page (mockup 22) in the real
    /// Boot and Menu scenes, with the real wallet and ad rewards (the owner, 4 Oct 2026): the balance
    /// follows the wallet, "+" opens Earn Coins and Back returns, three ads pay 20, 30 and 50 and the
    /// button then waits for the set's reset with a live countdown, the set resets at its deadline,
    /// a double tap pays once, an ad that isn't available says so, and the shop button opens the shop.
    /// The ads are the development stand-in (each completes after 1.5 s) unless a test brings its own.
    /// Saves go to a temporary folder.
    /// </summary>
    public sealed class EarnCoinsFlowTests
    {
        private const float AdTimeout = 10f;

        // The shop the main menu's storefront button opens.
        private const ScreenId Shop = ScreenId.Shop;

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
        public IEnumerator MainMenuWallet_ShowsTheBalance_AndFollowsIt()
        {
            yield return _game.Boot();
            UIScreen menu = Screen(ScreenId.MainMenu);
            var wallet = _game.Services.Resolve<IWallet>();

            Assert.AreEqual(WalletRules.StartingCoins, wallet.Coins, "a new player's coins");
            Assert.AreEqual("500", Label(menu, "Balance").text, "mockup 23: the wallet shows the balance");

            wallet.Add(750);
            yield return null;
            Assert.AreEqual("1,250", Label(menu, "Balance").text, "the balance follows the wallet as it changes");

            Assert.IsTrue(wallet.TrySpend(1000));
            yield return null;
            Assert.AreEqual("250", Label(menu, "Balance").text);
        }

        [UnityTest]
        public IEnumerator Plus_OpensEarnCoins_BackAndAndroidBackReturn()
        {
            yield return _game.Boot();
            ScreenRouter router = _game.MenuRouter;

            GameFixture.Press(router, "EarnCoinsButton");
            yield return null;
            Assert.AreEqual(ScreenId.EarnCoins, router.Current, "the wallet's + opens Earn Coins");
            AssertFreshSet(Screen(ScreenId.EarnCoins), coins: 500);

            GameFixture.Press(router, "BackButton");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, router.Current, "Back returns to the main menu");

            GameFixture.Press(router, "EarnCoinsButton");
            yield return null;
            Assert.IsTrue(router.Back(), "Android back leaves Earn Coins");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, router.Current);
            Assert.AreEqual(1, router.Depth);
        }

        [UnityTest]
        public IEnumerator ThreeAds_Pay20Then30Then50_ThenTheButtonWaitsForTheReset_WithALiveCountdown()
        {
            yield return _game.Boot();
            var wallet = _game.Services.Resolve<IWallet>();
            var rewards = _game.Services.Resolve<IAdRewards>();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "EarnCoinsButton");
            yield return null;
            UIScreen page = Screen(ScreenId.EarnCoins);

            int[] payouts = { 20, 30, 50 };
            int coins = 500;
            for (int ad = 0; ad < payouts.Length; ad++)
            {
                GameFixture.Press(router, "WatchButton");
                yield return null;
                Assert.IsFalse(GameFixture.CanPress(page, "WatchButton"), "the button is busy while the ad plays");

                coins += payouts[ad];
                int expected = coins;
                yield return GameFixture.Until(() => wallet.Coins == expected && !Busy(rewards, page), "ad " + (ad + 1) + " to pay", AdTimeout);
                yield return null;

                Assert.AreEqual(ad + 1, rewards.Watched);
                Assert.AreEqual(Format("ui.earn_coins.progress", ad + 1, 3), Label(page, "Progress").text);

                // The ad's coins fly into the page's wallet, which counts up as they land (the owner, 8 Oct 2026).
                string paid = CoinsFormat.Coins(coins);
                yield return GameFixture.Until(() => Label(page, "Balance").text == paid, "the coins to land in the page's wallet", 5f);
                Assert.AreEqual(CoinsFormat.Coins(coins), Label(page, "Balance").text, "the page's wallet follows the coins credited");
                for (int block = 0; block < 3; block++)
                {
                    Assert.AreEqual(block <= ad, Badge(page, block), "block " + block + " carries a check once its ad is watched");
                    Assert.AreEqual(block == ad + 1, NextLook(page, block), "the crimson look moves to the block the next ad pays");
                }

                StringAssert.StartsWith(Format("ui.earn_coins.resets_in", string.Empty), Label(page, "ResetLabel").text,
                    "from the set's first ad on, the line under the button counts down to the reset");
                if (ad + 1 < payouts.Length)
                {
                    Assert.AreEqual(Format("ui.earn_coins.watch", payouts[ad + 1]), Label(page, "WatchLabel").text, "the button says what the next ad pays");
                    Assert.IsTrue(GameFixture.CanPress(page, "WatchButton"));
                }
            }

            // 3 / 3: the button is off until the reset, and the countdown runs.
            Assert.AreEqual(0, rewards.NextReward);
            Assert.IsFalse(GameFixture.CanPress(page, "WatchButton"), "no fourth ad before the reset");
            Assert.AreEqual(Get("ui.earn_coins.all_claimed"), Label(page, "WatchLabel").text);
            Assert.IsFalse(PlayIcon(page).activeInHierarchy, "no play triangle on a button that plays nothing");
            Assert.IsTrue(rewards.ResetsAtUtc.HasValue);
            long secondsLeft = CoinsFormat.SecondsLeft(rewards.ResetsAtUtc.Value - DateTime.UtcNow);
            Assert.That(secondsLeft, Is.InRange(5L * 3600L - 30L, 5L * 3600L), "the set resets five hours after its first ad");
            string shown = Label(page, "ResetLabel").text;
            string[] nearby = { CountdownText(secondsLeft - 1), CountdownText(secondsLeft), CountdownText(secondsLeft + 1) };
            CollectionAssert.Contains(nearby, shown, "the countdown shows the time left as hours, minutes and seconds");
            yield return GameFixture.Until(() => Label(page, "ResetLabel").text != shown, "the countdown to tick", 3f);

            // Back on the main menu, its wallet has the coins too.
            GameFixture.Press(router, "BackButton");
            yield return null;
            Assert.AreEqual("600", Label(Screen(ScreenId.MainMenu), "Balance").text);
        }

        [UnityTest]
        public IEnumerator AtTheDeadline_TheSetResets_WhileThePageIsOpen()
        {
            // A set of three ads watched earlier, which resets in a few seconds.
            long deadline = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 12;
            yield return _game.Boot(seed: saves => saves.Save(AdRewards.SaveKey, new AdRewardsData { watched = 3, resetsAtUnixSeconds = deadline }));
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "EarnCoinsButton");
            yield return null;
            UIScreen page = Screen(ScreenId.EarnCoins);

            Assert.AreEqual(Format("ui.earn_coins.progress", 3, 3), Label(page, "Progress").text, "the set is used up");
            Assert.IsFalse(GameFixture.CanPress(page, "WatchButton"));
            StringAssert.StartsWith(Format("ui.earn_coins.resets_in", "00:00:"), Label(page, "ResetLabel").text);

            yield return GameFixture.Until(() => GameFixture.CanPress(page, "WatchButton"), "the set to reset at its deadline", 20f);
            yield return null;

            Assert.Greater(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), deadline - 1, "not before the deadline");
            AssertFreshSet(page, coins: 500);
        }

        [UnityTest]
        public IEnumerator ADoubleTap_PaysOnce()
        {
            yield return _game.Boot();
            var wallet = _game.Services.Resolve<IWallet>();
            var rewards = _game.Services.Resolve<IAdRewards>();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "EarnCoinsButton");
            yield return null;
            UIScreen page = Screen(ScreenId.EarnCoins);
            Button watch = Find<Button>(page, "WatchButton");

            // Two taps in the same frame, before the button has had a chance to look busy.
            watch.onClick.Invoke();
            watch.onClick.Invoke();
            yield return null;
            Assert.IsFalse(GameFixture.CanPress(page, "WatchButton"), "busy while the ad plays");

            yield return GameFixture.Until(() => !Busy(rewards, page), "the ad to finish", AdTimeout);
            yield return new WaitForSeconds(StandInSeconds + 0.5f);

            Assert.AreEqual(520, wallet.Coins, "one ad, paid once");
            Assert.AreEqual(1, rewards.Watched);
            Assert.AreEqual(Format("ui.earn_coins.progress", 1, 3), Label(page, "Progress").text);
            Assert.IsFalse(Toast().IsShowing, "the second tap was not answered as an unavailable ad");
        }

        [UnityTest]
        public IEnumerator AnAdThatIsNotAvailable_SaysSo_AndPaysNothing()
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<IRewardedAds>(new NoAds()));
            var wallet = _game.Services.Resolve<IWallet>();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "EarnCoinsButton");
            yield return null;
            UIScreen page = Screen(ScreenId.EarnCoins);

            GameFixture.Press(router, "WatchButton");
            yield return GameFixture.Until(() => Toast().IsShowing, "a toast to say there is no ad", AdTimeout);

            Assert.AreEqual(Get("ui.earn_coins.unavailable"), Toast().Text);
            Assert.AreEqual(500, wallet.Coins);
            AssertFreshSet(page, coins: 500);
        }

        [UnityTest]
        public IEnumerator ShopButton_OpensTheShop()
        {
            yield return _game.Boot();
            ScreenRouter router = _game.MenuRouter;

            GameFixture.Press(router, "ShopButton");
            yield return null;
            Assert.AreEqual(Shop, router.Current, "mockup 23: the storefront opens the shop");
            Assert.IsTrue(router.Back());
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, router.Current);
        }

        private const float StandInSeconds = 1.5f;

        /// <summary>What a set nobody has watched an ad of looks like (mockup 22).</summary>
        private void AssertFreshSet(UIScreen page, int coins)
        {
            Assert.AreEqual(CoinsFormat.Coins(coins), Label(page, "Balance").text, "mockup 22: the wallet shows the balance");
            Assert.AreEqual(Format("ui.earn_coins.progress", 0, 3), Label(page, "Progress").text);
            Assert.AreEqual(Format("ui.earn_coins.watch", 20), Label(page, "WatchLabel").text);
            Assert.IsTrue(GameFixture.CanPress(page, "WatchButton"));
            Assert.IsTrue(PlayIcon(page).activeInHierarchy);
            Assert.AreEqual(Get("ui.earn_coins.reset_every"), Label(page, "ResetLabel").text);
            int[] payouts = { 20, 30, 50 };
            for (int block = 0; block < 3; block++)
            {
                Assert.AreEqual(Format("ui.earn_coins.ad", block + 1), Label(page, "AdTitle" + block).text);
                Assert.AreEqual(Format("ui.earn_coins.reward", payouts[block]), Label(page, "Amount" + block).text);
                Assert.AreEqual(block == 0, NextLook(page, block), "the first block is the next one");
                Assert.IsFalse(Badge(page, block), "nothing watched");
            }
        }

        private bool Busy(IAdRewards rewards, UIScreen page) => !GameFixture.CanPress(page, "WatchButton") && rewards.NextReward > 0;

        private string CountdownText(long seconds) => Format("ui.earn_coins.resets_in", CoinsFormat.TimeLeft(seconds));

        private UIScreen Screen(ScreenId id) => _game.MenuRouter.Get<UIScreen>(id);

        private ToastView Toast() => _game.Project.GetComponentInChildren<ToastView>(true);

        private string Get(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private string Format(string key, params object[] args) => _game.Services.Resolve<ILocalization>().Format(key, args);

        private static TMP_Text Label(Component root, string name) => Find<TMP_Text>(root, name);

        private static bool NextLook(UIScreen page, int block) => Child(page, "Block" + block, "NextLook").activeSelf;

        private static bool Badge(UIScreen page, int block) => Child(page, "Block" + block, "WatchedBadge").activeSelf;

        private static GameObject PlayIcon(UIScreen page) => Find<RectTransform>(page, "PlayIcon").GetComponentInChildren<Image>(true).gameObject;

        private static GameObject Child(Component root, string parent, string name)
        {
            Transform child = Find<RectTransform>(root, parent).Find(name);
            Assert.IsNotNull(child, parent + " has " + name);
            return child.gameObject;
        }

        private static T Find<T>(Component root, string name) where T : Component
        {
            foreach (T found in root.GetComponentsInChildren<T>(true))
            {
                if (found.name == name)
                {
                    return found;
                }
            }

            Assert.Fail("No " + typeof(T).Name + " called " + name + " under " + root.name + ".");
            return null;
        }

        /// <summary>An ad network with nothing to show.</summary>
        private sealed class NoAds : IRewardedAds
        {
            public bool IsAvailable => false;

            public Task<bool> ShowAsync(CancellationToken ct) => Task.FromResult(false);
        }
    }
}
