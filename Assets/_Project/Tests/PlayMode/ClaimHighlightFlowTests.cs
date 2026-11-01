using System;
using System.Collections;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Callbreak.Tests.PlayMode.ClaimGlowAsserts;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// A reward ready to collect stands out (the owner, 2 Nov 2026: "show some effect if its not been
    /// collected so it will be easy to identify we should click or claim it"; ADR 069), in the real Boot
    /// and Menu scenes. While today's reward is ready the main menu's Daily Rewards button wears a gold
    /// glow that breathes and its gift gives a nudge, and on the Daily Rewards page so does the day to
    /// collect, its coins hopping; the other days stay calm. Collected, the glow goes at once; the next
    /// day brings it back, also while the menu or the page stays open. With Reduce motion the glow stays
    /// lit and nothing moves. It runs on unscaled time, so a paused time scale doesn't stop it. Saves go
    /// to a temporary folder; the days come from a clock the test turns. The Shop button's glow while a
    /// festival offer waits is in <see cref="FestivalOfferFlowTests"/>.
    /// </summary>
    public sealed class ClaimHighlightFlowTests
    {
        private GameFixture _game;
        private TurnedClock _clock;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _clock = new TurnedClock(DateTime.Now);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator TheMenuButton_GlowsAndNudges_WhileTodaysRewardIsReady()
        {
            yield return Boot();
            ClaimHighlight highlight = MenuHighlight();

            Assert.IsTrue(highlight.On, "today's reward is ready: the button glows");
            Assert.IsTrue(highlight.gameObject.activeInHierarchy, "the glow is on screen");
            Assert.IsTrue(DailyRewardsButton().Find("Dot").gameObject.activeSelf, "the red dot stays");
            yield return AssertMoves(highlight, Glyph(DailyRewardsButton(), "Icon"), "the glow breathes and the gift nudges");
        }

        [UnityTest]
        public IEnumerator TheMenuButton_StopsGlowingWhenCollected_AndGlowsAgainTheNextDay_WithTheMenuOpen()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            ClaimHighlight highlight = MenuHighlight();
            RectTransform gift = Glyph(DailyRewardsButton(), "Icon");

            GameFixture.Press(router, "DailyRewardsButton");
            yield return null;
            GameFixture.Press(router, "ClaimButton");
            yield return null;

            // The coins land in the page's wallet before the player goes back.
            CoinFlight flight = router.Get<UIScreen>(ScreenId.DailyRewards).GetComponentInChildren<CoinFlight>(true);
            yield return GameFixture.Until(() => !flight.IsPlaying, "the claimed coins to land", 5f);
            GameFixture.Press(router, "BackButton");
            yield return null;
            Assert.AreEqual(ScreenId.MainMenu, router.Current);
            Assert.IsFalse(highlight.On, "collected: no glow");
            Assert.IsFalse(highlight.gameObject.activeInHierarchy, "the glow is gone from the button");
            AssertAtRest(gift, "the gift rests");
            yield return new WaitForSecondsRealtime(1.5f);
            Assert.IsFalse(highlight.On, "still collected the same day");

            // Local midnight comes without an event while the player sits on the menu.
            _clock.Now = _clock.Now.AddDays(1);
            yield return GameFixture.Until(() => highlight.On, "the next day's glow on the open menu", 3f);
            Assert.AreEqual(ScreenId.MainMenu, router.Current, "the menu stayed open");
            yield return AssertMoves(highlight, gift, "the next day's reward glows and nudges");
        }

        [UnityTest]
        public IEnumerator ThePage_TheDayToCollectGlows_TheOtherDaysStayCalm_AndTheNextDayGlowsWithThePageOpen()
        {
            yield return Boot();
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "DailyRewardsButton");
            yield return null;
            UIScreen page = router.Get<UIScreen>(ScreenId.DailyRewards);
            ClaimHighlight[] days = DayHighlights(page);
            Assert.AreEqual(7, days.Length, "each day's tile has its glow");

            Assert.IsTrue(days[0].On, "Day 1 is the one to collect");
            for (int i = 1; i < days.Length; i++)
            {
                Assert.IsFalse(days[i].On, $"Day {i + 1} stays calm");
            }

            yield return AssertMoves(days[0], Glyph(page, "Coins0"), "Day 1's glow breathes and its coins hop");

            GameFixture.Press(router, "Tile0");
            yield return null;
            foreach (ClaimHighlight day in days)
            {
                Assert.IsFalse(day.On, "collected: every day is calm");
            }

            AssertAtRest(Glyph(page, "Coins0"), "Day 1's coins rest");

            // The page stays up across midnight: Day 2 is the one to collect now.
            _clock.Now = _clock.Now.AddDays(1);
            yield return GameFixture.Until(() => days[1].On, "Day 2's glow on the open page", 3f);
            Assert.IsFalse(days[0].On, "Day 1 is collected");
            yield return AssertMoves(days[1], Glyph(page, "Coins1"), "Day 2's glow breathes and its coins hop");
        }

        [UnityTest]
        public IEnumerator WithReduceMotion_TheGlowStaysLit_AndNothingMoves()
        {
            yield return Boot();
            _game.Settings.SetReduceMotion(true);
            yield return null;
            ScreenRouter router = _game.MenuRouter;
            ClaimHighlight highlight = MenuHighlight();

            Assert.IsTrue(highlight.On, "the reward still stands out");
            yield return AssertStill(highlight, Glyph(DailyRewardsButton(), "Icon"), "the menu's glow");

            GameFixture.Press(router, "DailyRewardsButton");
            yield return null;
            UIScreen page = router.Get<UIScreen>(ScreenId.DailyRewards);
            ClaimHighlight day = DayHighlights(page)[0];
            Assert.IsTrue(day.On, "Day 1 still stands out");
            yield return AssertStill(day, Glyph(page, "Coins0"), "Day 1's glow");

            // Reduce motion turned off again: the glow moves at once.
            _game.Settings.SetReduceMotion(false);
            yield return AssertMoves(day, Glyph(page, "Coins0"), "Day 1 moves again");
        }

        [UnityTest]
        public IEnumerator TheGlow_RunsOnUnscaledTime()
        {
            yield return Boot();
            Time.timeScale = 0f;
            yield return AssertMoves(MenuHighlight(), Glyph(DailyRewardsButton(), "Icon"), "a paused time scale doesn't stop it");
        }

        /// <summary>
        /// Boots with Day 1 ready and the page's own opening far off, so it never opens by itself, and the
        /// rewards on the test's clock.
        /// </summary>
        private IEnumerator Boot()
        {
            yield return _game.Boot(
                seed: saves => saves.Save(DailyRewards.SaveKey, new DailyRewardsData { autoOpenDay = DailyRewards.DayNumber(DateTime.Now) + 30 }),
                extra: builder => builder.Register(
                    resolver => new DailyRewards(resolver.Resolve<IWallet>(), resolver.Resolve<ISaveStore>(), () => _clock.Now),
                    Lifetime.Singleton).As<IDailyRewards>());
        }

        private RectTransform DailyRewardsButton() => Find<RectTransform>(_game.MenuRouter.Get<UIScreen>(ScreenId.MainMenu), "DailyRewardsButton");

        private ClaimHighlight MenuHighlight() => GlowOf(_game.MenuRouter.Get<UIScreen>(ScreenId.MainMenu), "DailyRewardsButton");

        private static ClaimHighlight[] DayHighlights(Component page)
        {
            var days = new ClaimHighlight[7];
            for (int i = 0; i < days.Length; i++)
            {
                days[i] = GlowOf(page, $"Tile{i}");
            }

            return days;
        }

        /// <summary>A wall clock the test turns: midnight comes when the test says.</summary>
        private sealed class TurnedClock
        {
            public TurnedClock(DateTime now) => Now = now;

            public DateTime Now { get; set; }
        }
    }
}
