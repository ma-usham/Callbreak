using System.Collections;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// Prize coins arriving on the main menu (the owner, 9 Oct 2026): coins won at the table (a Stake
    /// Match prize, <see cref="IUnseenCoins"/>) fly from the middle of the screen into the main menu's
    /// wallet once it is on screen; the wallet first shows the balance without them and counts up as
    /// they land, with one coin sound. With Reduce motion they are added at once, sound and all. The
    /// Daily Rewards page waits until they have landed. Saves go to a temporary folder; the game
    /// starts with 500 coins and the prize of 200 already in the wallet, as the table pays it.
    /// </summary>
    public sealed class PrizeCoinsFlowTests
    {
        private const int Prize = 200;

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
        public IEnumerator APrizeWonAtTheTable_FliesIntoTheMainMenuWallet_WhichCountsUp_WithTheCoinSound()
        {
            var audio = new HeardSounds();
            var unseen = Unseen();
            yield return _game.Boot(seed: saves => new Wallet(saves).Add(Prize), extra: builder => Register(builder, unseen, audio));
            UIScreen menu = _game.MenuRouter.Get<UIScreen>(ScreenId.MainMenu);
            CoinFlight flight = menu.GetComponentInChildren<CoinFlight>(true);
            Assert.IsNotNull(flight, "the main menu has its coin flight");

            Assert.AreEqual("500", Label(menu, "Balance").text, "the wallet first shows the balance without the prize");
            yield return GameFixture.Until(() => flight.IsPlaying, "the prize coins to fly once the menu is on screen", 2f);
            Assert.AreEqual("500", Label(menu, "Balance").text, "the number waits for the coins");
            Assert.AreEqual(0, audio.Count(SoundCue.CoinCollect), "no sound before a coin lands");

            yield return GameFixture.Until(() => Label(menu, "Balance").text == "700", "the prize coins to land in the wallet", 5f);
            Assert.IsFalse(flight.IsPlaying);
            Assert.AreEqual(1, audio.Count(SoundCue.CoinCollect), "one coin sound as they land");
            Assert.AreEqual(0, unseen.Amount, "the prize has been shown");
            Assert.AreEqual(700, _game.Services.Resolve<IWallet>().Coins);
        }

        [UnityTest]
        public IEnumerator WithReduceMotion_ThePrizeIsAddedAtOnce_WithTheCoinSound()
        {
            var audio = new HeardSounds();
            var unseen = Unseen();
            yield return _game.Boot(
                seed: saves =>
                {
                    new Wallet(saves).Add(Prize);
                    new SettingsService(saves).SetReduceMotion(true);
                },
                extra: builder => Register(builder, unseen, audio));
            UIScreen menu = _game.MenuRouter.Get<UIScreen>(ScreenId.MainMenu);
            yield return GameFixture.Until(() => Label(menu, "Balance").text == "700", "the prize to be added", 2f);

            Assert.IsFalse(menu.GetComponentInChildren<CoinFlight>(true).IsPlaying, "nothing flies");
            Assert.AreEqual("700", Label(menu, "Balance").text);
            Assert.AreEqual(1, audio.Count(SoundCue.CoinCollect));
            Assert.AreEqual(0, unseen.Amount);
        }

        [UnityTest]
        public IEnumerator TheDailyRewardsPage_WaitsForThePrizeCoins()
        {
            var unseen = Unseen();
            yield return _game.Boot(
                seed: saves =>
                {
                    new Wallet(saves).Add(Prize);
                    saves.Save(DailyRewards.SaveKey, new DailyRewardsData());
                },
                extra: builder => Register(builder, unseen, new HeardSounds()));
            ScreenRouter router = _game.MenuRouter;
            UIScreen menu = router.Get<UIScreen>(ScreenId.MainMenu);

            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(ScreenId.MainMenu, router.Current, "the coins fly first");
            yield return GameFixture.Until(() => Label(menu, "Balance").text == "700", "the prize coins to land", 5f);
            yield return GameFixture.Until(() => router.Current == ScreenId.DailyRewards, "Daily Rewards to open after them", 3f);
        }

        /// <summary>A prize of 200 won at the table and not yet shown.</summary>
        private static PrizeWon Unseen()
        {
            var unseen = new PrizeWon();
            unseen.Add(Prize);
            return unseen;
        }

        private static void Register(IContainerBuilder builder, IUnseenCoins unseen, IAudioService audio)
        {
            builder.RegisterInstance(unseen);
            builder.RegisterInstance(audio);
        }

        /// <summary>
        /// The coins not yet shown, in a type of its own: the container refuses a second registration
        /// of the game's own type (as GameFixture's TemporarySaves does).
        /// </summary>
        private sealed class PrizeWon : IUnseenCoins
        {
            private readonly UnseenCoins _inner = new UnseenCoins();

            public int Amount => _inner.Amount;

            public void Add(int coins) => _inner.Add(coins);

            public void Clear() => _inner.Clear();
        }

        private static TMP_Text Label(Component root, string name)
        {
            foreach (TMP_Text found in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (found.name == name)
                {
                    return found;
                }
            }

            Assert.Fail($"{root.name} has no label named {name}");
            return null;
        }
    }
}
