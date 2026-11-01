using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Economy;
using Callbreak.Application.Offers;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Screens;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using VContainer;
using static Callbreak.Tests.PlayMode.ClaimGlowAsserts;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// A festival offer in the real Boot and Menu scenes, with the real shop and save files (the owner,
    /// 31 Oct 2026; ADR 067), the backend's Tihar 2026 offer given by a test source: it opens by itself
    /// once on the bare main menu, as Tihar Lights' preview with "Watch ad • Free"; one ad watched makes
    /// the theme the player's and the button turns to Equip theme in the same place; its cross goes back
    /// to the menu. Left unclaimed, the Shop button has a red dot and Daily Rewards' gold glow, which
    /// breathes while the shop's icon nudges (the owner, 2 Nov 2026; ADR 069; still with Reduce motion),
    /// and the theme's tile says Festival and Free. No ad to show gives nothing and says so. An offer that
    /// ends while its preview is up turns back to the theme's price, and the Shop button's dot and glow
    /// go. Saves go to a temporary folder.
    /// </summary>
    public sealed class FestivalOfferFlowTests
    {
        private GameFixture _game;
        private ScriptedAds _ads;

        // The offers' clock, for a test that moves it; null for the real one.
        private DateTime? _utc;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
            _ads = new ScriptedAds();
            _utc = null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator TheOffer_OpensByItselfOnce_AndOneAdMakesTiharLightsThePlayers_ThenItIsEquippedInTheSamePlace()
        {
            yield return BootWithTihar();
            yield return UntilOfferOpens();

            Assert.AreEqual(Text("ui.shop.theme.tihar_lights"), Preview.Title);
            Assert.AreEqual(Text("ui.offer.tihar"), Preview.Description, "the backend's words");
            Assert.AreEqual(ThemeAction.Claim, Preview.Action);
            Assert.AreEqual(Text("ui.offer.claim.watch_ad"), Preview.ClaimLabel);
            Assert.AreEqual(Preview.WatchAdIcon, Preview.ClaimIcon, "the ad's play mark");
            var shop = _game.Services.Resolve<ICosmeticsShop>();
            int coins = _game.Services.Resolve<IWallet>().Coins;

            GameFixture.PressIn(View, "ClaimButton");
            yield return GameFixture.Until(() => shop.IsOwned(CosmeticKind.Theme, CosmeticCatalog.Themes.TiharLights), "the theme to be the player's");
            yield return null;

            Assert.AreEqual(1, _ads.Shows, "one ad");
            Assert.AreEqual(coins, _game.Services.Resolve<IWallet>().Coins, "free");
            Assert.AreEqual(ThemeAction.Equip, Preview.Action, "the button turns to Equip theme in the same place");
            Assert.IsTrue(ToastShows(Format("ui.shop.theme.bought", Text("ui.shop.theme.tihar_lights"))));
            Assert.AreEqual(CosmeticCatalog.Themes.DurbarSquare, shop.Equipped(CosmeticKind.Theme), "claiming doesn't equip");

            GameFixture.PressIn(View, "EquipButton");
            yield return null;
            Assert.AreEqual(CosmeticCatalog.Themes.TiharLights, shop.Equipped(CosmeticKind.Theme), "Equip theme puts Tihar Lights on");
            Assert.AreEqual(ThemeAction.Equipped, Preview.Action);

            GameFixture.PressIn(View, "CloseButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.MainMenu, "the cross to go back to the menu");
            Assert.IsFalse(Menu.ShowsShopDot, "nothing left to claim");
            Assert.IsFalse(ShopGlow.On, "no glow once it is claimed");
            AssertAtRest(ShopIcon, "the shop's icon rests");

            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(ScreenId.MainMenu, Router.Current, "it opens by itself once");
        }

        [UnityTest]
        public IEnumerator LeftUnclaimed_TheShopButtonHasADot_AndTheTileSaysFestivalAndFree()
        {
            yield return BootWithTihar();
            yield return UntilOfferOpens();
            GameFixture.PressIn(View, "CloseButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.MainMenu, "the cross to go back to the menu");

            Assert.IsTrue(Menu.ShowsShopDot, "a red dot on Shop");
            Assert.IsTrue(ShopGlow.On, "and Daily Rewards' glow");
            yield return AssertMoves(ShopGlow, ShopIcon, "the Shop button's glow breathes and its icon nudges");
            yield return new WaitForSecondsRealtime(2f);
            Assert.AreEqual(ScreenId.MainMenu, Router.Current, "the offer doesn't open again");

            GameFixture.Press(Router, "ShopButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.Shop && !_game.Curtain.IsCovering, "the shop to open");
            Assert.IsFalse(Preview.IsOpen, "the Shop button opens the shop itself");
            ThemeTile tihar = TileNamed(Text("ui.shop.theme.tihar_lights"));
            Assert.IsTrue(tihar.ShowsFestival, "the Festival tag");
            Assert.IsFalse(tihar.ShowsNew, "in the NEW tag's place");
            Assert.IsTrue(tihar.ShowsOffer && !tihar.ShowsPrice, "Free where the price goes");
            Assert.AreEqual(Text("ui.shop.status.free"), tihar.OfferText.text);
            ThemeTile mithila = TileNamed(Text("ui.shop.theme.mithila_garden"));
            Assert.IsFalse(mithila.ShowsFestival);
            Assert.IsTrue(mithila.ShowsPrice);

            tihar.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(ThemeAction.Claim, Preview.Action, "its preview claims it too");
        }

        [UnityTest]
        public IEnumerator AnOfferThatEnds_WhileItsPreviewIsUp_TurnsBackToItsPrice_AndTheDotGoes()
        {
            _utc = new DateTime(2026, 11, 1, 6, 0, 0, DateTimeKind.Utc);
            yield return BootWithTihar();
            yield return UntilOfferOpens();
            Assert.AreEqual(ThemeAction.Claim, Preview.Action);

            _utc = new DateTime(2026, 11, 16, 0, 0, 0, DateTimeKind.Utc);
            yield return GameFixture.Until(() => Preview.Action == ThemeAction.Buy, "the preview to turn back to the theme's price");
            Assert.AreEqual("1,000", Preview.BuyPrice);
            Assert.AreEqual(Text("ui.shop.theme.tihar_lights.about"), Preview.Description, "the theme's own words again");

            GameFixture.PressIn(View, "CloseButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.MainMenu, "the cross to go back to the menu");
            Assert.IsFalse(Menu.ShowsShopDot, "no dot for an offer that has ended");
            Assert.IsFalse(ShopGlow.On, "no glow either");
            Assert.IsFalse(_game.Services.Resolve<ICosmeticsShop>().IsOwned(CosmeticKind.Theme, CosmeticCatalog.Themes.TiharLights));
        }

        [UnityTest]
        public IEnumerator WithReduceMotion_TheShopButtonsGlowStaysLit_AndNothingMoves()
        {
            yield return BootWithTihar();
            _game.Settings.SetReduceMotion(true);
            yield return UntilOfferOpens();
            GameFixture.PressIn(View, "CloseButton");
            yield return GameFixture.Until(() => Router.Current == ScreenId.MainMenu, "the cross to go back to the menu");

            Assert.IsTrue(ShopGlow.On, "the offer still stands out");
            yield return AssertStill(ShopGlow, ShopIcon, "the Shop button's glow");
        }

        [UnityTest]
        public IEnumerator NoAdToShow_GivesNothing_AndSaysSo()
        {
            _ads.Available = false;
            yield return BootWithTihar();
            yield return UntilOfferOpens();

            GameFixture.PressIn(View, "ClaimButton");
            yield return null;
            yield return null;

            Assert.AreEqual(0, _ads.Shows);
            Assert.IsTrue(ToastShows(Text("ui.offer.ad_unavailable")));
            Assert.IsFalse(_game.Services.Resolve<ICosmeticsShop>().IsOwned(CosmeticKind.Theme, CosmeticCatalog.Themes.TiharLights));
            Assert.AreEqual(ThemeAction.Claim, Preview.Action, "the offer is still there");
        }

        private ScreenRouter Router => _game.MenuRouter;

        private ShopView View => Router.Get<ShopView>(ScreenId.Shop);

        private ThemePreview Preview => View.Preview;

        private MainMenuView Menu => Router.Get<MainMenuView>(ScreenId.MainMenu);

        private ClaimHighlight ShopGlow => GlowOf(Menu, "ShopButton");

        private RectTransform ShopIcon => (RectTransform)Glyph(Menu, "ShopButton").Find("Icon");

        /// <summary>Boots with Tihar 2026 live: for all time on the real clock, or its own dates on the test's.</summary>
        private IEnumerator BootWithTihar()
        {
            var source = _utc.HasValue
                ? new TiharSource("2026-10-31T00:00:00+05:45", "2026-11-16T00:00:00+05:45")
                : new TiharSource("2000-01-01T00:00:00+05:45", "2100-01-01T00:00:00+05:45");
            yield return _game.Boot(extra: builder =>
            {
                builder.RegisterInstance<IFestivalOfferSource>(source);
                builder.RegisterInstance<IRewardedAds>(_ads);
                if (_utc.HasValue)
                {
                    builder.RegisterInstance<IWallClock>(new TestClock(() => _utc.Value));
                }
            });
        }

        private IEnumerator UntilOfferOpens()
        {
            yield return GameFixture.Until(() => Router.Current == ScreenId.Shop && Preview.IsOpen && !_game.Curtain.IsCovering, "the offer to open by itself");
        }

        private ThemeTile TileNamed(string themeName)
        {
            for (int i = 0; i < View.Shelf.Count; i++)
            {
                ThemeTile tile = View.Shelf.Tile(i);
                if (tile.Name.text == themeName)
                {
                    return tile;
                }
            }

            Assert.Fail($"no tile says {themeName}");
            return null;
        }

        private string Text(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private string Format(string key, params object[] args) => _game.Services.Resolve<ILocalization>().Format(key, args);

        private bool ToastShows(string text)
        {
            foreach (ToastView toast in _game.Project.GetComponentsInChildren<ToastView>(true))
            {
                foreach (TMP_Text label in toast.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.text == text)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>The backend's Tihar 2026 offer, between the given instants.</summary>
        private sealed class TiharSource : IFestivalOfferSource
        {
            public TiharSource(string start, string end)
            {
                Config = new FestivalOffersConfig
                {
                    offers = new List<FestivalOfferEntry>
                    {
                        new FestivalOfferEntry
                        {
                            id = "tihar-2026",
                            theme = "tihar-lights",
                            kind = "watch-ad",
                            start = start,
                            end = end,
                            title = "ui.offer.tihar",
                        },
                    },
                };
            }

            public event Action Changed
            {
                add { }
                remove { }
            }

            public FestivalOffersConfig Config { get; }
        }

        /// <summary>The offers' wall clock, set by the test.</summary>
        private sealed class TestClock : IWallClock
        {
            private readonly Func<DateTime> _utc;

            public TestClock(Func<DateTime> utc) => _utc = utc;

            public DateTime UtcNow => _utc();
        }

        /// <summary>A rewarded ad watched to the end at once, or none to show.</summary>
        private sealed class ScriptedAds : IRewardedAds
        {
            public bool Available { get; set; } = true;

            public int Shows { get; private set; }

            public bool IsAvailable => Available;

            public Task<bool> ShowAsync(CancellationToken ct)
            {
                Shows++;
                return Task.FromResult(true);
            }
        }
    }
}
