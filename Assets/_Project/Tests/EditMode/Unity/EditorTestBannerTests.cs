using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Infrastructure.Platform;
using NUnit.Framework;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The banner in the Editor (the owner, 2 Nov 2026: "we have banner ads right? how am i gonna test
    /// that how the game gonna look on mobile device?"; ADR 071): a drawn stand-in of AdMob's test
    /// banner, 320 x 50 dp at the scale a phone gives it, so the table and the main menu make room and
    /// place it as they do on phones. In the Game view its scale is a common phone's for the view's
    /// height (1080 pixels high: 2.625, 840 x 131 pixels); in the Device Simulator, the simulated
    /// phone's own density. It goes on and off the screen where it is told and is gone once destroyed.
    /// The switch that turns it off is the Editor's own setting, put back as it was.
    /// </summary>
    public sealed class EditorTestBannerTests
    {
        [Test]
        public void InTheGameView_TheBanner_IsAsBigAsOnACommonPhoneOfThatHeight()
        {
            float scale = EditorTestBannerAdNetwork.DeviceScale(false, 96f, 1920, 1080);
            Assert.AreEqual(2.625f, scale, 0.001f, "a 1080-pixel-high phone at 420 dpi");
            Assert.AreEqual(840, EditorTestBannerAdNetwork.Pixels(EditorTestBannerAdNetwork.WidthDp, scale));
            Assert.AreEqual(131, EditorTestBannerAdNetwork.Pixels(EditorTestBannerAdNetwork.HeightDp, scale));

            Assert.AreEqual(scale, EditorTestBannerAdNetwork.DeviceScale(false, 96f, 1080, 1920), 0.001f, "the short side counts, either way up");
            Assert.AreEqual(scale / 2f, EditorTestBannerAdNetwork.DeviceScale(false, 144f, 1200, 540), 0.001f, "a smaller view, a smaller banner");
        }

        [Test]
        public void InTheDeviceSimulator_TheBanner_TakesThePhonesOwnDensity()
        {
            Assert.AreEqual(2.75f, EditorTestBannerAdNetwork.DeviceScale(true, 440f, 2400, 1080), 0.001f);
            Assert.AreEqual(2.625f, EditorTestBannerAdNetwork.DeviceScale(true, 0f, 2400, 1080), 0.001f, "a phone that reports no density counts as a common one");
        }

        [Test]
        public void TheBanner_ShowsWhereItIsPut_GoesOffTheScreen_AndIsGoneOnceDestroyed()
        {
            var network = new EditorTestBannerAdNetwork();
            Task<bool> start = network.StartAsync();
            Assert.IsTrue(start.IsCompleted && start.Result, "ready at once: no consent, no SDK");

            Task<IBannerAd> load = network.LoadAsync();
            Assert.IsTrue(load.IsCompleted, "loaded at once");
            IBannerAd banner = load.Result;
            Assert.IsNotNull(banner);
            EditorTestBannerView view = Object.FindFirstObjectByType<EditorTestBannerView>(FindObjectsInactive.Include);
            try
            {
                Assert.IsNotNull(view, "the banner draws itself");
                Assert.IsFalse(view.IsOnScreen, "off the screen until it is placed");
                Assert.Greater(banner.Width, 0);
                Assert.Greater(banner.Height, 0);

                banner.ShowAt(120, 8);
                Assert.IsTrue(view.IsOnScreen);
                Assert.AreEqual(new Rect(120, 8, banner.Width, banner.Height), view.Place, "its top-left corner where it was put, at its size");

                banner.Hide();
                Assert.IsFalse(view.IsOnScreen, "off the screen, still loaded");
                banner.ShowAt(40, 900);
                Assert.IsTrue(view.IsOnScreen);
                Assert.AreEqual(40f, view.Place.x);
                Assert.AreEqual(900f, view.Place.y);

                banner.Destroy();
                Assert.IsTrue(view == null, "released");
                banner.ShowAt(0, 0);
                banner.Hide();
                banner.Destroy();
            }
            finally
            {
                if (view != null)
                {
                    Object.DestroyImmediate(view.gameObject);
                }
            }
        }

        [Test]
        public void TheSwitch_IsTheEditorsSetting_PutBackAsItWas()
        {
            bool before = EditorTestBanner.On;
            try
            {
                using (EditorTestBanner.While(!before))
                {
                    Assert.AreEqual(!before, EditorTestBanner.On);
                }

                Assert.AreEqual(before, EditorTestBanner.On, "as it was");
            }
            finally
            {
                EditorTestBanner.On = before;
            }
        }
    }
}
