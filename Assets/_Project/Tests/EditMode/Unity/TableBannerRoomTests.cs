using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The table's top bar and its room for the banner ad (the owner, 26 Oct 2026, ADR 051; 28-29 Oct
    /// 2026, ADR 057), on the table prefab laid out on a 16:9, 21:9 and 4:3 canvas. Scores and
    /// Settings stand under Home as icons, clear of the left seat. The CALLBREAK logo stands right of
    /// Home, and "ROUND 1 / 5" under it on a status pill that keeps the width of its words; the top
    /// centre has neither. With room made, the logo and the round line stay, the top seat and the trick
    /// come down, and the banner's slot spans the top of the safe area from right of the logo and the
    /// round line to the right edge, tall enough for the largest standard banner (320 x 50 dp on a 360
    /// dp-tall phone: 960 x 150 canvas units), clear of every button and of the top seat; the banner
    /// sits in its top-right corner. Giving the room back puts everything where it was. The prefab is
    /// the built one, so these live outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class TableBannerRoomTests
    {
        private const string TablePrefab = "Assets/_Project/Prefabs/UI/Screens/Table.prefab";

        /// <summary>The largest standard banner on a phone, in canvas units (320 x 50 dp, 360 dp tall).</summary>
        private static readonly Vector2 PhoneBanner = new Vector2(960f, 150f);

        /// <summary>A 320 x 50 dp banner on a 4:3 tablet 1024 dp wide, in canvas units.</summary>
        private static readonly Vector2 TabletBanner = new Vector2(600f, 94f);

        private GameObject _canvas;
        private TableView _view;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2520f, 1080f)]
        [TestCase(1920f, 1440f)]
        public void ATable_ShowsTheLogoOnTheLeft_AndTheRoundUnderIt(float width, float height)
        {
            Open(width, height);
            _view.SetRound("ROUND 1 / 5");
            Canvas.ForceUpdateCanvases();

            Assert.IsFalse(_view.HasBannerRoom);
            Rect home = Box(Find("HomeButton"));
            Rect logo = Box(Find("Logo"));
            Rect words = InkBox(Find("RoundLabel"));
            Rect pill = Box(Find("RoundPill"));
            Assert.IsTrue(Find("Logo").gameObject.activeSelf, "the logo shows");
            Assert.Greater(logo.xMin, home.xMax, "right of Home, on the left side");
            Assert.Less(logo.center.x, Box(_canvas.transform).center.x);
            Assert.IsTrue(Find("RoundLabel").gameObject.activeSelf, "the round shows");
            // The round's letter spacing sits after its last letter too, so its ink is a few units off
            // the centre of what TextMeshPro centres.
            Assert.AreEqual(logo.center.x, words.center.x, 4f, "centred under the logo");
            Assert.Less(pill.yMax, logo.yMin, "below it");
            Assert.Greater(pill.yMax, logo.yMin - 20f, "just below it");
            Assert.Less(pill.xMin, words.xMin, "on a pill that holds the words");
            Assert.Greater(pill.xMax, words.xMax);
            Assert.Less(pill.yMin, words.yMin);
            Assert.Greater(pill.yMax, words.yMax);
            Assert.IsNull(FindOrNull("RoundOrnamentLeft"), "no ornaments: they flanked the round at the top centre");
            Assert.IsNull(FindOrNull("RoundOrnamentRight"));
            Assert.IsNull(FindOrNull("RoundBeside"), "one round line, always in its place");
            Assert.IsFalse(Find("BannerSlot").gameObject.activeSelf);
            Assert.IsFalse(_view.TryGetBannerSlot(out _), "no slot, no banner");

            // Nothing of the game under the logo and the round line.
            Rect corner = Rect.MinMaxRect(logo.xMin, pill.yMin, Mathf.Max(logo.xMax, pill.xMax), logo.yMax);
            foreach (string piece in new[] { "SeatPlate2", "SeatPill2", "SeatRing2", "SeatPlate3", "SeatRing3", "ScoresButton", "SettingsButton" })
            {
                Assert.IsFalse(corner.Overlaps(Box(Find(piece))), piece + " clear of the logo and the round");
            }
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2520f, 1080f)]
        [TestCase(1920f, 1440f)]
        public void ScoresAndSettings_StandUnderHome_ClearOfTheLeftSeat(float width, float height)
        {
            Open(width, height);
            Rect home = Box(Find("HomeButton"));
            Rect scores = Box(Find("ScoresButton"));
            Rect settings = Box(Find("SettingsButton"));
            Assert.AreEqual(home.center.x, scores.center.x, 1f, "Scores in Home's column");
            Assert.AreEqual(home.center.x, settings.center.x, 1f, "Settings too");
            Assert.Less(scores.yMax, home.yMin, "Scores below Home");
            Assert.Less(settings.yMax, scores.yMin, "Settings below Scores");
            Assert.IsNull(FindOrNull("ScoresLabel"), "icons only: no \"Scores\" under the button");
            Assert.IsNull(FindOrNull("SettingsLabel"), "nor \"Settings\"");
            Assert.IsNull(FindOrNull("HeaderDivider"), "no divider between buttons that are one above the other");

            Rect column = Rect.MinMaxRect(Mathf.Min(home.xMin, scores.xMin, settings.xMin), settings.yMin, Mathf.Max(home.xMax, scores.xMax, settings.xMax), home.yMax);
            foreach (string piece in new[] { "SeatPlate3", "SeatPill3", "SeatRing3", "SeatFan3", "SeatPlate2", "SeatRing2" })
            {
                Assert.IsFalse(column.Overlaps(Box(Find(piece))), piece + " clear of the left column");
            }
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2520f, 1080f)]
        [TestCase(1920f, 1440f)]
        public void WithRoom_TheBannerFitsAtTheTop_CoveringNothing(float width, float height)
        {
            Open(width, height);
            _view.SetRound("ROUND 1 / 5");

            _view.SetBannerRoom(true);

            Assert.IsTrue(_view.HasBannerRoom);
            Assert.IsTrue(Find("Logo").gameObject.activeSelf, "the logo stays");
            Assert.IsTrue(Find("RoundLabel").gameObject.activeSelf, "and so does the round under it");
            Assert.AreEqual("ROUND 1 / 5", TextOf(Find("RoundLabel")));

            Rect canvas = Box(_canvas.transform);
            Rect slot = Box(Find("BannerSlot"));
            Rect logo = Box(Find("Logo"));
            Rect pill = Box(Find("RoundPill"));
            Assert.AreEqual(canvas.yMax, slot.yMax, 0.5f, "the slot starts at the top of the screen");
            Assert.AreEqual(canvas.xMax, slot.xMax, 0.5f, "and reaches its right edge: the banner sits in the top-right corner");
            Assert.AreEqual(155f, slot.height, 0.5f);
            Assert.GreaterOrEqual(slot.xMin, logo.xMax + 47.5f, "clear of the logo");
            Assert.GreaterOrEqual(slot.xMin, pill.xMax + 47.5f, "and of the round line's pill");

            // No button under the slot (AdMob: no ad next to buttons). The table's own tap catchers
            // cover the whole screen and are not buttons a player aims at.
            foreach (UnityEngine.UI.Button button in _canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false))
            {
                Rect box = Box(button.transform);
                if (box.width >= canvas.width - 1f && box.height >= canvas.height - 1f)
                {
                    continue;
                }

                Assert.IsFalse(box.Overlaps(slot), button.name + " clear of the banner's slot");
            }

            Vector2 banner = height > 1080f ? TabletBanner : PhoneBanner;
            Assert.GreaterOrEqual(slot.width, banner.x, "the banner fits across");
            Assert.GreaterOrEqual(slot.height, banner.y, "and down");

            // Nothing of the game under the slot: the top seat (name plate, status pill, avatar,
            // fan, speech box) and the trick are below it.
            foreach (string piece in new[] { "SeatPlate2", "SeatPill2", "SeatRing2", "SeatFan2", "TrickArea" })
            {
                Assert.LessOrEqual(Box(Find(piece)).yMax, slot.yMin, piece + " below the banner");
            }
        }

        [Test]
        public void WithRoom_TheTopSeatAndTheTrickComeDown_AndNothingElseMoves()
        {
            Open(1920f, 1080f);
            Vector2 top = Anchored("Seat2");
            Vector2 trick = Anchored("TrickArea");
            Vector2 left = Anchored("Seat3");
            Vector2 right = Anchored("Seat1");
            Vector2 local = Anchored("Seat0");

            _view.SetBannerRoom(true);

            Assert.AreEqual(top + new Vector2(0f, -80f), Anchored("Seat2"));
            Assert.AreEqual(trick + new Vector2(0f, -40f), Anchored("TrickArea"));
            Assert.AreEqual(left, Anchored("Seat3"));
            Assert.AreEqual(right, Anchored("Seat1"));
            Assert.AreEqual(local, Anchored("Seat0"));

            _view.SetBannerRoom(true);
            Assert.AreEqual(top + new Vector2(0f, -80f), Anchored("Seat2"), "asked twice, it comes down once");

            _view.SetBannerRoom(false);

            Assert.AreEqual(top, Anchored("Seat2"), "everything back where it was");
            Assert.AreEqual(trick, Anchored("TrickArea"));
            Assert.IsTrue(Find("Logo").gameObject.activeSelf);
            Assert.IsTrue(Find("RoundLabel").gameObject.activeSelf);
            Assert.IsFalse(Find("BannerSlot").gameObject.activeSelf);
        }

        [Test]
        public void TheRound_IsWritten_AndItsPillGrowsWithTheWords()
        {
            Open(1920f, 1080f);

            _view.SetRound("ROUND 10 / 10");
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual("ROUND 10 / 10", TextOf(Find("RoundLabel")));
            Rect words = InkBox(Find("RoundLabel"));
            Rect pill = Box(Find("RoundPill"));
            Assert.Less(pill.xMin, words.xMin, "the pill grows with the words");
            Assert.Greater(pill.xMax, words.xMax);
        }

        /// <summary>The table prefab under a world-space canvas of the given size, as the capture tool lays it out.</summary>
        private void Open(float width, float height)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TablePrefab);
            Assert.IsNotNull(prefab, TablePrefab);
            _canvas = new GameObject("TestCanvas", typeof(Canvas));
            _canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)_canvas.transform).sizeDelta = new Vector2(width, height);
            GameObject table = Object.Instantiate(prefab, _canvas.transform, false);
            _view = table.GetComponentInChildren<TableView>(true);
            Assert.IsNotNull(_view, "the table prefab has its view");
            Canvas.ForceUpdateCanvases();
        }

        private RectTransform Find(string name)
        {
            RectTransform found = FindOrNull(name);
            Assert.IsNotNull(found, "the table has " + name);
            return found;
        }

        private RectTransform FindOrNull(string name)
        {
            foreach (RectTransform t in _canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
        }

        /// <summary>A label's ink in canvas units.</summary>
        private static Rect InkBox(RectTransform label)
        {
            Bounds ink = InkOf(label);
            Vector3 min = label.TransformPoint(ink.min), max = label.TransformPoint(ink.max);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Vector2 Anchored(string name) => Find(name).anchoredPosition;

        // This assembly has no TextMeshPro reference: the label is read through its component's type.
        private static Component Label(RectTransform rect)
        {
            foreach (Component c in rect.GetComponents<Component>())
            {
                if (c != null && c.GetType().GetProperty("textBounds") != null)
                {
                    return c;
                }
            }

            Assert.Fail(rect.name + " is a label");
            return null;
        }

        private static string TextOf(RectTransform rect)
        {
            Component label = Label(rect);
            return (string)label.GetType().GetProperty("text").GetValue(label);
        }

        /// <summary>The words' ink, in the label's own space, once laid out.</summary>
        private static Bounds InkOf(RectTransform rect)
        {
            Component label = Label(rect);
            label.GetType().GetMethod("ForceMeshUpdate", new[] { typeof(bool), typeof(bool) }).Invoke(label, new object[] { true, true });
            return (Bounds)label.GetType().GetProperty("textBounds").GetValue(label);
        }

        private static Rect Box(Transform transform)
        {
            var corners = new Vector3[4];
            ((RectTransform)transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
