using System;
using System.Collections;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The weekly login rewards (the owner, 8 Oct 2026; mockup 26; ADR 046) in the real Boot and Menu
    /// scenes: the small button below No Ads opens Daily Rewards and wears a red dot while today's
    /// reward is ready; Claim pays Day 1's 10 coins once; the page opens by itself once a day on the
    /// bare main menu, never over the name question. Saves go to a temporary folder; the fixture starts
    /// every game with today's opening used, so these tests seed their own file.
    /// </summary>
    public sealed class DailyRewardsFlowTests
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
        public IEnumerator TheButtonBelowNoAds_OpensDailyRewards_AndClaimPaysDay1Once()
        {
            yield return _game.Boot(seed: saves => saves.Save(DailyRewards.SaveKey, OpenedToday()));
            ScreenRouter router = _game.MenuRouter;
            UIScreen menu = Screen(ScreenId.MainMenu);
            var wallet = _game.Services.Resolve<IWallet>();
            Assert.IsTrue(Find<RectTransform>(menu, "DailyRewardsButton").Find("Dot").gameObject.activeSelf, "the red dot: today's reward is ready");

            GameFixture.Press(router, "DailyRewardsButton");
            yield return null;
            Assert.AreEqual(ScreenId.DailyRewards, router.Current, "the button opens Daily Rewards");
            UIScreen page = Screen(ScreenId.DailyRewards);
            Assert.AreEqual(Format("ui.daily_rewards.progress", 1, 7), Label(page, "DayHeading").text);
            Assert.AreEqual(Format("ui.daily_rewards.claim", 10), Label(page, "ClaimLabel").text, "mockup 26: \"Claim 10 Coins\"");
            Assert.AreEqual(Format("ui.daily_rewards.amount", 60), Label(page, "Amount6").text, "the owner: Day 7 pays 60");
            Assert.AreEqual(Format("ui.daily_rewards.next", 2), Label(page, "Footer").text);
            Assert.IsTrue(Child(page, "Tile0", "TodayFrame0").activeSelf, "Day 1 wears today's frame");
            Assert.IsFalse(Child(page, "Tile0", "ClaimedBadge").activeSelf);

            GameFixture.Press(router, "ClaimButton");
            yield return null;
            Assert.AreEqual(WalletRules.StartingCoins + 10, wallet.Coins, "Day 1 pays 10 coins");
            Assert.AreEqual(Get("ui.daily_rewards.claimed"), Label(page, "ClaimLabel").text);
            Assert.IsFalse(GameFixture.CanPress(page, "ClaimButton"), "one claim a day");
            Assert.IsTrue(Child(page, "Tile0", "ClaimedBadge").activeSelf, "Day 1 now carries the check");
            Assert.IsFalse(Child(page, "Tile0", "TodayFrame0").activeSelf);
            Assert.AreEqual(Format("ui.daily_rewards.next", 2), Label(page, "Footer").text, "tomorrow is Day 2");

            GameFixture.Press(router, "BackButton");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, router.Current);
            Assert.IsFalse(Find<RectTransform>(menu, "DailyRewardsButton").Find("Dot").gameObject.activeSelf, "no dot once claimed");
            Assert.AreEqual(WalletRules.StartingCoins + 10, wallet.Coins);
        }

        [UnityTest]
        public IEnumerator ThePage_OpensByItself_OnceADay_WhileARewardIsReady()
        {
            yield return _game.Boot(seed: saves => saves.Save(DailyRewards.SaveKey, new DailyRewardsData()));
            ScreenRouter router = _game.MenuRouter;

            yield return GameFixture.Until(() => router.Current == ScreenId.DailyRewards, "Daily Rewards to open by itself");
            Assert.IsTrue(router.Back(), "Android back closes it");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, router.Current);

            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreEqual(ScreenId.MainMenu, router.Current, "only once a day, even with the reward still waiting");
            Assert.IsTrue(_game.Services.Resolve<IDailyRewards>().CanClaim);
        }

        [UnityTest]
        public IEnumerator AFreshInstall_AsksTheNameFirst_ThePageWaits()
        {
            yield return _game.Boot(freshInstall: true, seed: saves => saves.Save(DailyRewards.SaveKey, new DailyRewardsData()));
            ScreenRouter router = _game.MenuRouter;

            yield return new WaitForSecondsRealtime(1.5f);
            Assert.AreNotEqual(ScreenId.DailyRewards, router.Current, "the name question comes first");
            Assert.IsTrue(_game.Services.Resolve<IDailyRewards>().TakeAutoOpen(), "today's opening is still to come");
        }

        /// <summary>A file of a player whose page already opened by itself today, Day 1 still to claim.</summary>
        private static DailyRewardsData OpenedToday() => new DailyRewardsData { autoOpenDay = DailyRewards.DayNumber(DateTime.Now) };

        private UIScreen Screen(ScreenId id) => _game.MenuRouter.Get<UIScreen>(id);

        private string Get(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private string Format(string key, params object[] args) => _game.Services.Resolve<ILocalization>().Format(key, args);

        private static TMP_Text Label(Component root, string name) => Find<TMP_Text>(root, name);

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

            Assert.Fail($"{root.name} has no {typeof(T).Name} named {name}");
            return null;
        }
    }
}
