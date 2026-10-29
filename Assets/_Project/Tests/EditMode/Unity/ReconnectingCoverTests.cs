using Callbreak.Presentation.Table;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The table's "Reconnecting…" cover shows the Dhaka Ring (the owner, 30 Oct 2026: "use the dhaka
    /// ring as reconnecting image"): its ring of gold diamonds turns round the still seal, about the
    /// seal's own centre, so it never wobbles; the bright diamond runs anticlockwise, ahead of its
    /// fading trail; the ring keeps turning while the time scale is paused, and Reduce motion stops it.
    /// The ring, the seal and the words lie in the middle of the screen at 16:9, 20:9 and 4:3. The art
    /// is uncompressed (the owner wants no softening). The prefab is the built one, so these live
    /// outside the folders the dotnet harness compiles.
    /// </summary>
    public sealed class ReconnectingCoverTests
    {
        private const string TablePrefab = "Assets/_Project/Prefabs/UI/Screens/Table.prefab";
        private const string Art = "Assets/_Project/Art/UI/Table/UI_Reconnecting_";

        private GameObject _canvas;
        private TableView _view;
        private GameObject _cover;

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvas);
        }

        [Test]
        public void TheCover_ShowsTheDhakaRing_TurningRoundItsStillSeal()
        {
            Open(1920f, 1080f);
            Image ring = Piece("Ring"), seal = Piece("Seal");
            Assert.AreEqual("UI_Reconnecting_Ring", ring.sprite.name, "the Dhaka Ring");
            Assert.AreEqual("UI_Reconnecting_Seal", seal.sprite.name, "its seal with the spade");

            var spinner = ring.GetComponent<Spinner>();
            Assert.IsNotNull(spinner, "the ring turns");
            Assert.AreSame(spinner, Field<Spinner>("_reconnectingRing"), "the view's ring is the one that turns");
            Assert.IsNull(seal.GetComponentInParent<Spinner>(true), "the seal stays still");
            Assert.AreEqual(Vector2.zero, ring.rectTransform.pivot - new Vector2(0.5f, 0.5f), "the ring turns about its middle");
            Assert.That(Vector3.Distance(Centre(ring), Centre(seal)), Is.LessThan(0.01f), "the ring turns about the seal's centre");

            var settings = new SerializedObject(spinner);
            Assert.IsFalse(settings.FindProperty("_clockwise").boolValue, "the bright diamond runs anticlockwise, ahead of its trail");
            Assert.IsTrue(settings.FindProperty("_unscaledTime").boolValue, "it turns while the time scale is paused");
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2400f, 1080f)]
        [TestCase(1920f, 1440f)]
        public void TheRingAndTheWords_LieInTheMiddleOfTheScreen_TheWordsBelowTheSeal(float width, float height)
        {
            Open(width, height);
            Image ring = Piece("Ring"), seal = Piece("Seal");
            var words = _cover.transform.Find("Text") as RectTransform;
            Assert.IsNotNull(words, "the cover has its words");

            Rect screen = Box((RectTransform)_canvas.transform);
            Assert.AreEqual(screen.center.x, Centre(ring).x, 0.01f, "the ring is in the middle");
            // The words are placed by their ink, so their box can sit a unit or so off the middle.
            Assert.AreEqual(screen.center.x, Box(words).center.x, 2f, "the words are in the middle");
            Assert.Less(Box(words).yMax, Centre(seal).y - (seal.rectTransform.rect.height / 4f), "the words are below the seal");
            foreach (RectTransform piece in new[] { ring.rectTransform, seal.rectTransform, words })
            {
                Rect box = Box(piece);
                Assert.IsTrue(screen.Contains(box.min) && screen.Contains(box.max), $"{piece.name} is on the screen");
            }

            float top = Box(ring.rectTransform).yMax, bottom = Box(words).yMin;
            Assert.AreEqual(screen.center.y, (top + bottom) / 2f, 40f, "the ring and its words are centred together");
        }

        [Test]
        public void ReduceMotion_StopsTheRing()
        {
            Open(1920f, 1080f);
            _view.ReduceMotion = true;
            Assert.IsTrue(Field<Spinner>("_reconnectingRing").Still, "a static indicator (GDD §14)");
        }

        [TestCase("Ring")]
        [TestCase("Seal")]
        public void TheArt_IsUncompressed(string name)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(Art + name + ".png");
            Assert.IsNotNull(importer, Art + name + ".png");
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, "no softening");
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
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
            _cover = Field<GameObject>("_reconnecting");
            Assert.IsNotNull(_cover, "the table has its Reconnecting cover");
            Assert.IsFalse(_cover.activeSelf, "the cover is hidden until the session reconnects");
            Canvas.ForceUpdateCanvases();
        }

        private T Field<T>(string name)
            where T : Object
        {
            return new SerializedObject(_view).FindProperty(name).objectReferenceValue as T;
        }

        private Image Piece(string name)
        {
            Transform piece = _cover.transform.Find(name);
            Assert.IsNotNull(piece, "the cover has its " + name);
            var image = piece.GetComponent<Image>();
            Assert.IsNotNull(image, name + " is a picture");
            Assert.IsNotNull(image.sprite, name + " has its art");
            return image;
        }

        private static Vector3 Centre(Graphic graphic) => graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center);

        private static Rect Box(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
    }
}
