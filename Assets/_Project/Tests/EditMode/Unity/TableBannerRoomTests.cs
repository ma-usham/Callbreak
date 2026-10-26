using Callbreak.Presentation.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The table's room for the banner ad (the owner, 26 Oct 2026, ADR 049), on the table prefab laid
    /// out on a 16:9, 21:9 and 4:3 canvas. A table opens as it always was. With room made, the logo
    /// and the round line's ornaments go, the round line stands beside the Scores button, the top
    /// seat and the trick come down, and the banner's slot spans the top of the safe area from right
    /// of Home to left of the round line's words, tall enough for the largest standard banner (320 x
    /// 50 dp on a 360 dp-tall phone: 960 x 150 canvas units) and clear of the top seat. Giving the
    /// room back puts everything where it was. The prefab is the built one, so these live outside
    /// the folders the dotnet harness compiles.
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
        public void ATable_OpensAsItAlwaysWas(float width, float height)
        {
            Open(width, height);

            Assert.IsFalse(_view.HasBannerRoom);
            Assert.IsTrue(Find("Logo").gameObject.activeSelf);
            Assert.IsTrue(Find("RoundOrnamentLeft").gameObject.activeSelf);
            Assert.IsTrue(Find("RoundOrnamentRight").gameObject.activeSelf);
            Assert.IsTrue(Find("RoundLabel").gameObject.activeSelf);
            Assert.IsFalse(Find("RoundBeside").gameObject.activeSelf);
            Assert.IsFalse(Find("BannerSlot").gameObject.activeSelf);
            Assert.IsFalse(_view.TryGetBannerSlot(out _), "no slot, no banner");
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
            Assert.IsFalse(Find("Logo").gameObject.activeSelf, "the logo makes way");
            Assert.IsFalse(Find("RoundOrnamentLeft").gameObject.activeSelf);
            Assert.IsFalse(Find("RoundOrnamentRight").gameObject.activeSelf);
            Assert.IsFalse(Find("RoundLabel").gameObject.activeSelf);
            RectTransform beside = Find("RoundBeside");
            Assert.IsTrue(beside.gameObject.activeSelf, "the round line stands beside Scores");
            Assert.AreEqual("ROUND 1 / 5", TextOf(beside));

            Rect canvas = Box(_canvas.transform);
            Rect slot = Box(Find("BannerSlot"));
            Rect scores = Box(Find("ScoresButton"));
            Rect home = Box(Find("HomeButton"));
            Bounds ink = InkOf(beside);
            float wordsLeft = beside.TransformPoint(ink.min).x;
            float wordsRight = beside.TransformPoint(ink.max).x;

            Assert.AreEqual(canvas.yMax, slot.yMax, 0.5f, "the slot starts at the top of the screen");
            Assert.AreEqual(155f, slot.height, 0.5f);
            Assert.AreEqual(home.xMax + 48f, slot.xMin, 0.5f, "clear of Home");
            Assert.LessOrEqual(slot.xMax, wordsLeft - 47.5f, "clear of the round line's words");
            Assert.Less(wordsRight, scores.xMin, "the words end before the Scores button");
            Assert.Greater(wordsRight, scores.xMin - 48f, "beside it");

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

            Assert.AreEqual(top + new Vector2(0f, -52f), Anchored("Seat2"));
            Assert.AreEqual(trick + new Vector2(0f, -25f), Anchored("TrickArea"));
            Assert.AreEqual(left, Anchored("Seat3"));
            Assert.AreEqual(right, Anchored("Seat1"));
            Assert.AreEqual(local, Anchored("Seat0"));

            _view.SetBannerRoom(true);
            Assert.AreEqual(top + new Vector2(0f, -52f), Anchored("Seat2"), "asked twice, it comes down once");

            _view.SetBannerRoom(false);

            Assert.AreEqual(top, Anchored("Seat2"), "everything back where it was");
            Assert.AreEqual(trick, Anchored("TrickArea"));
            Assert.IsTrue(Find("Logo").gameObject.activeSelf);
            Assert.IsTrue(Find("RoundLabel").gameObject.activeSelf);
            Assert.IsFalse(Find("RoundBeside").gameObject.activeSelf);
            Assert.IsFalse(Find("BannerSlot").gameObject.activeSelf);
        }

        [Test]
        public void TheRound_IsWrittenInBothPlaces()
        {
            Open(1920f, 1080f);

            _view.SetRound("ROUND 10 / 10");

            Assert.AreEqual("ROUND 10 / 10", TextOf(Find("RoundLabel")));
            Assert.AreEqual("ROUND 10 / 10", TextOf(Find("RoundBeside")));
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
            foreach (RectTransform t in _canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            Assert.Fail("the table has " + name);
            return null;
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
