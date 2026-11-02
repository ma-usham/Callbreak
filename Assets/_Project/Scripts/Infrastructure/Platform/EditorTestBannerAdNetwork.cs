#if UNITY_EDITOR
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using UnityEngine;

namespace Callbreak.Infrastructure.Platform
{
    /// <summary>
    /// The banner network of a game played in the Editor (the owner, 2 Nov 2026: "how am i gonna test that
    /// how the game gonna look on mobile device?"; ADR 071): no ad and no SDK, but a drawn stand-in of
    /// AdMob's test banner (<see cref="EditorTestBannerView"/>), so <see cref="BannerAds"/> runs as on
    /// phones: the table and the main menu make room, the banner sits in their slots, goes off the
    /// screen under every page and goes for good with No Ads.
    /// <para>
    /// It is AdMob's 320 x 50 dp banner at the scale a phone gives it. In the Device Simulator that is
    /// the simulated phone's density; in the Game view, a common phone's for the view's short side
    /// (<see cref="PhoneShortSideDp"/>), so a 1080-pixel-high view gets the 840 x 131 pixels a 1080p
    /// phone draws. The size follows the view as it is resized.
    /// </para>
    /// </summary>
    public sealed class EditorTestBannerAdNetwork : IBannerAdNetwork
    {
        /// <summary>AdMob's standard banner (<c>AdSize.Banner</c>), in density-independent pixels.</summary>
        public const float WidthDp = 320f;

        public const float HeightDp = 50f;

        /// <summary>
        /// A common phone's short side in density-independent pixels: 1080 pixels at 2.625 (420 dpi),
        /// as many Android phones are.
        /// </summary>
        public const float PhoneShortSideDp = 1080f / 2.625f;

        /// <summary>Android's baseline density: a scale of 1 at 160 dpi.</summary>
        private const float BaselineDpi = 160f;

        public Task<bool> StartAsync() => Task.FromResult(true);

        public Task<IBannerAd> LoadAsync() => Task.FromResult<IBannerAd>(new Banner());

        /// <summary>
        /// Screen pixels per density-independent pixel: the simulated phone's density in the Device
        /// Simulator (<paramref name="mobile"/>), otherwise a common phone's for the screen's short side.
        /// </summary>
        public static float DeviceScale(bool mobile, float dpi, int width, int height)
        {
            if (mobile && dpi > 0f)
            {
                return dpi / BaselineDpi;
            }

            return Mathf.Min(width, height) / PhoneShortSideDp;
        }

        /// <summary>Density-independent pixels on the screen, at <paramref name="scale"/>.</summary>
        public static int Pixels(float dp, float scale) => Mathf.RoundToInt(dp * scale);

        private static float CurrentScale()
        {
            return DeviceScale(UnityEngine.Device.Application.isMobilePlatform, UnityEngine.Device.Screen.dpi, Screen.width, Screen.height);
        }

        /// <summary>One drawn banner; nothing here throws, and a released banner does nothing.</summary>
        private sealed class Banner : IBannerAd
        {
            private EditorTestBannerView _view;

            public Banner()
            {
                var holder = new GameObject("EditorTestBanner");
                if (UnityEngine.Application.isPlaying)
                {
                    // Like AdMob's view, it outlives scene loads until it is released.
                    Object.DontDestroyOnLoad(holder);
                }

                _view = holder.AddComponent<EditorTestBannerView>();
            }

            public int Width => Pixels(WidthDp, CurrentScale());

            public int Height => Pixels(HeightDp, CurrentScale());

            public void ShowAt(int x, int y)
            {
                if (_view != null)
                {
                    _view.Show(new Rect(x, y, Width, Height));
                }
            }

            public void Hide()
            {
                if (_view != null)
                {
                    _view.Hide();
                }
            }

            public void Destroy()
            {
                if (_view == null)
                {
                    return;
                }

                // Off the screen at once: in Play mode the object goes only at the frame's end.
                _view.Hide();
                GameObject holder = _view.gameObject;
                _view = null;
                if (UnityEngine.Application.isPlaying)
                {
                    Object.Destroy(holder);
                }
                else
                {
                    Object.DestroyImmediate(holder);
                }
            }
        }
    }
}
#endif
