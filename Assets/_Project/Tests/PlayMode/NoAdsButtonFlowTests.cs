using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The No Ads button on the main menu (the owner, 5 Oct 2026) in the real Boot and Menu scenes:
    /// it shows while the store can sell No Ads and goes once it can't or it is owned; a tap opens the
    /// store's sheet once, and a purchase thanks the player in a toast and takes the button away.
    /// While the sheet is up the game lies under the curtain's black cover (ADR 042), which goes
    /// before the toast. One test runs with the game's own stand-in store, the others with a store
    /// the test answers. Saves go to a temporary folder.
    /// </summary>
    public sealed class NoAdsButtonFlowTests
    {
        private const string NoAdsButton = "NoAdsButton";

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
        public IEnumerator TheButton_ShowsWhileNoAdsCanBeBought_AndGoesWhenItCannotOrIsOwned()
        {
            var store = new ScriptedNoAds();
            yield return _game.Boot(extra: builder => builder.RegisterInstance<INoAds>(store));
            UIScreen menu = Menu();

            Assert.IsTrue(Shown(menu), "the store can sell it: the button shows");

            store.Set(canBuy: false, owned: false);
            yield return null;
            Assert.IsFalse(Shown(menu), "the store can't sell it now: no button");

            store.Set(canBuy: true, owned: false);
            yield return null;
            Assert.IsTrue(Shown(menu), "the store is back");

            store.Set(canBuy: false, owned: true);
            yield return null;
            Assert.IsFalse(Shown(menu), "owned: the button goes");
        }

        [UnityTest]
        public IEnumerator ATap_OpensTheSheetOnce_ThenThanksThePlayer_AndTheButtonGoes()
        {
            var store = new ScriptedNoAds();
            yield return _game.Boot(extra: builder => builder.RegisterInstance<INoAds>(store));
            UIScreen menu = Menu();

            GameFixture.PressIn(menu, NoAdsButton);
            yield return null;
            Assert.IsTrue(Curtain().IsDark, "the game goes black first: the sheet can turn it to portrait");
            yield return GameFixture.Until(() => store.Purchases == 1, "the store's sheet to open over the black cover");
            Assert.IsTrue(Shown(menu), "the button stays while its purchase is under way");
            GameFixture.PressIn(menu, NoAdsButton);
            yield return null;
            Assert.AreEqual(1, store.Purchases, "a second tap while the sheet is open does nothing");
            Assert.IsFalse(Toast().IsShowing);
            Assert.IsTrue(Curtain().IsDark, "black while the sheet is up");

            store.Grant();
            yield return GameFixture.Until(() => Toast().IsShowing, "the purchase's toast");

            Assert.IsFalse(Curtain().IsCovering, "the cover went before the toast");
            Assert.AreEqual(Get("ui.main_menu.no_ads.bought"), Toast().Text);
            Assert.IsFalse(Shown(menu), "bought: the button goes");
            Assert.AreEqual(ScreenId.MainMenu, _game.MenuRouter.Current, "the menu stays where it was");
        }

        [UnityTest]
        public IEnumerator AClosedSheet_SaysNothing_AndTheButtonStays()
        {
            var store = new ScriptedNoAds();
            yield return _game.Boot(extra: builder => builder.RegisterInstance<INoAds>(store));
            UIScreen menu = Menu();

            GameFixture.PressIn(menu, NoAdsButton);
            yield return GameFixture.Until(() => store.Purchases == 1, "the store's sheet to open over the black cover");
            store.Answer(NoAdsPurchase.Cancelled);
            yield return GameFixture.Until(() => !Curtain().IsCovering, "the black cover to go");

            Assert.IsFalse(Toast().IsShowing, "the player closed the sheet: nothing to say");
            Assert.IsTrue(Shown(menu));
            Assert.IsTrue(GameFixture.CanPress(menu, NoAdsButton), "it can be tapped again");
        }

        [UnityTest]
        public IEnumerator WithTheStandInStore_ATapBuysNoAdsAtOnce()
        {
            yield return _game.Boot();
            UIScreen menu = Menu();
            var noAds = _game.Services.Resolve<INoAds>();
            Assert.IsTrue(noAds.CanBuy, "the Editor's stand-in is ready to sell");
            Assert.IsTrue(Shown(menu));

            GameFixture.PressIn(menu, NoAdsButton);
            yield return GameFixture.Until(() => Toast().IsShowing, "the purchase's toast");

            Assert.IsFalse(Curtain().IsCovering, "the black cover came and went");
            Assert.IsTrue(noAds.Owned);
            Assert.AreEqual(Get("ui.main_menu.no_ads.bought"), Toast().Text);
            Assert.IsFalse(Shown(menu));
        }

        private UIScreen Menu() => _game.MenuRouter.Get<UIScreen>(ScreenId.MainMenu);

        private ToastView Toast() => _game.Project.GetComponentInChildren<ToastView>(true);

        private LoadingCurtain Curtain() => (LoadingCurtain)_game.Curtain;

        private string Get(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        private static bool Shown(UIScreen menu)
        {
            foreach (Button button in menu.GetComponentsInChildren<Button>(true))
            {
                if (button.name == NoAdsButton)
                {
                    return button.gameObject.activeInHierarchy;
                }
            }

            Assert.Fail("The main menu has no " + NoAdsButton + ".");
            return false;
        }

        /// <summary>A store whose purchase sheet stays open until the test answers it.</summary>
        private sealed class ScriptedNoAds : INoAds
        {
            private TaskCompletionSource<NoAdsPurchase> _sheet;

            public event Action Changed;

            public bool Owned { get; private set; }

            public bool CanBuy { get; private set; } = true;

            public string Price => "$0.99";

            public int Purchases { get; private set; }

            public Task<NoAdsPurchase> BuyAsync(CancellationToken ct)
            {
                Purchases++;
                _sheet = new TaskCompletionSource<NoAdsPurchase>();
                return _sheet.Task;
            }

            public void Set(bool canBuy, bool owned)
            {
                CanBuy = canBuy;
                Owned = owned;
                Changed?.Invoke();
            }

            /// <summary>The purchase goes through: owned, then the sheet answers Bought.</summary>
            public void Grant()
            {
                Set(canBuy: false, owned: true);
                Answer(NoAdsPurchase.Bought);
            }

            public void Answer(NoAdsPurchase purchase) => _sheet.SetResult(purchase);
        }
    }
}
