using Callbreak.Presentation.Screens;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The main menu's room for the banner ad in its bottom-left corner (the owner, 27 Oct 2026, ADR
    /// 052), on the main menu prefab laid out on a 16:9, 21:9 and 4:3 canvas. A menu opens as it always
    /// was. With room made, the centre column is drawn a little smaller from its top, the footer line
    /// goes, the "Resume match" banner stands under Play Offline, and the banner's slot fills the
    /// bottom-left corner of the safe area, big enough for the largest standard banner (320 x 50 dp on
    /// a 360 dp-tall phone: 960 x 150 canvas units), with no button over it or over the banner. Giving
    /// the room back puts everything where it was. The prefab is the built one, so these live outside
    /// the folders the dotnet harness compiles.
    /// </summary>
    public sealed class MenuBannerRoomTests
    {
        private const string MenuPrefab = "Assets/_Project/Prefabs/UI/Screens/MainMenu.prefab";

        /// <summary>The largest standard banner on a phone, in canvas units (320 x 50 dp, 360 dp tall).</summary>
        private static readonly Vector2 PhoneBanner = new Vector2(960f, 150f);

        /// <summary>A 320 x 50 dp banner on a 4:3 tablet 1024 dp wide, in canvas units.</summary>
        private static readonly Vector2 TabletBanner = new Vector2(600f, 94f);

        private GameObject _canvas;
        private MainMenuView _view;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2520f, 1080f)]
        [TestCase(1920f, 1440f)]
        public void AMenu_OpensAsItAlwaysWas(float width, float height)
        {
            Open(width, height);

            Assert.IsFalse(_view.HasBannerRoom);
            Assert.AreEqual(Vector3.one, Find("Centre").localScale);
            Assert.IsTrue(Find("Footer").gameObject.activeSelf, "the footer line");
            Assert.IsFalse(Find("BannerSlot").gameObject.activeSelf);
            Assert.IsFalse(_view.TryGetBannerSlot(out _), "no slot, no banner");
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2520f, 1080f)]
        [TestCase(1920f, 1440f)]
        public void WithRoom_TheBannerFitsInTheBottomLeftCorner_UnderNoButton(float width, float height)
        {
            Open(width, height);

            _view.SetBannerRoom(true);
            Canvas.ForceUpdateCanvases();

            Assert.IsTrue(_view.HasBannerRoom);
            Assert.Less(Find("Centre").localScale.x, 1f, "the centre column is drawn a little smaller");
            Assert.IsFalse(Find("Footer").gameObject.activeSelf, "the footer line makes way");

            Rect canvas = Box(_canvas.transform);
            Rect slot = Box(Find("BannerSlot"));
            Assert.AreEqual(canvas.xMin, slot.xMin, 0.5f, "the slot starts at the screen's left edge");
            Assert.AreEqual(canvas.yMin, slot.yMin, 0.5f, "and its bottom edge");

            Vector2 size = height > 1080f ? TabletBanner : PhoneBanner;
            Assert.GreaterOrEqual(slot.width, size.x, "the banner fits across");
            Assert.GreaterOrEqual(slot.height, size.y, "and down");
            Rect banner = new Rect(slot.xMin, slot.yMin, size.x, size.y);

            // No button over the slot, nor over the banner in its corner.
            foreach (UnityEngine.UI.Button button in _canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false))
            {
                Rect box = Box(button.transform);
                Assert.IsFalse(box.Overlaps(slot), button.name + " clear of the banner's slot");
            }

            // The Resume banner, when it shows, stands under Play Offline, clear of the banner.
            _view.SetResumeAvailable(true);
            Canvas.ForceUpdateCanvases();
            Rect resume = Box(Find("ResumeBanner"));
            Assert.AreEqual(Box(Find("PlayOfflineButton")).center.x, resume.center.x, 0.5f, "under Play Offline");
            Assert.IsFalse(resume.Overlaps(banner), "clear of the banner");
            Assert.IsFalse(Find("Footer").gameObject.activeSelf, "the footer line stays away");
        }

        [Test]
        public void GivingTheRoomBack_PutsEverythingWhereItWas()
        {
            Open(1920f, 1080f);
            Vector2 centre = Find("Centre").anchoredPosition;
            Vector2 resume = Find("ResumeBanner").anchoredPosition;

            _view.SetBannerRoom(true);
            _view.SetBannerRoom(true);
            _view.SetBannerRoom(false);

            Assert.AreEqual(centre, Find("Centre").anchoredPosition, "the column back where it was");
            Assert.AreEqual(Vector3.one, Find("Centre").localScale, "and its size");
            Assert.AreEqual(resume, Find("ResumeBanner").anchoredPosition);
            Assert.IsTrue(Find("Footer").gameObject.activeSelf);
            Assert.IsFalse(Find("BannerSlot").gameObject.activeSelf);
        }

        /// <summary>The main menu prefab under a world-space canvas of the given size, as the capture tool lays it out.</summary>
        private void Open(float width, float height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPrefab);
            Assert.IsNotNull(prefab, MenuPrefab);
            _canvas = new GameObject("TestCanvas", typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(width, height);
            GameObject menu = Object.Instantiate(prefab, _canvas.transform, false);
            _view = menu.GetComponent<MainMenuView>();
            Assert.IsNotNull(_view, "the main menu prefab has its view");
            Canvas.ForceUpdateCanvases();
        }

        private RectTransform Find(string name)
        {
            foreach (RectTransform t in _canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            Assert.Fail("the main menu has " + name);
            return null;
        }

        private static Rect Box(Transform transform)
        {
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
