using System.Threading;
using System.Threading.Tasks;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using Callbreak.Tests.EditMode.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using AppSettings = Callbreak.Application.Settings.SettingsService;     // UnityEditor has a SettingsService too

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The loading curtain as the Boot scene has it (the SceneLoading prefab, mockup 17): with Reduce
    /// motion its ring stands still at its resting angle, a static indicator (GDD §14); otherwise it
    /// turns while the curtain covers. The settings can reach the curtain after it has covered.
    /// These tests need the engine, so they live outside the folders the dotnet harness compiles.
    /// The prefab is opened in a preview scene, which leaves the Editor's open scenes untouched.
    /// Its ring is the Gold Arc (the owner, 31 Oct 2026), turning round its still seal, on the curtain and
    /// on its black look.
    /// </summary>
    public sealed class LoadingCurtainTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/UI/Screens/SceneLoading.prefab";

        private Scene _scene;
        private LoadingCurtain _curtain;
        private Spinner _ring;
        private AppSettings _settings;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, PrefabPath + " is built (Tools/Unity/screen.sh SceneLoading)");
            _scene = EditorSceneManager.NewPreviewScene();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            _curtain = instance.GetComponent<LoadingCurtain>();
            _ring = instance.GetComponentInChildren<Spinner>(true);
            Assert.IsNotNull(_curtain, "the curtain's root is the LoadingCurtain");
            Assert.IsNotNull(_ring, "the curtain has its ring");
            _settings = new AppSettings(new InMemorySaveStore());
        }

        [TearDown]
        public void TearDown()
        {
            // A motion started here runs on the Editor's own update; it must not outlive the ring.
            if (_ring != null)
            {
                _ring.Stop();
            }

            if (_scene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(_scene);
            }
        }

        /// <summary>
        /// The owner, 31 Oct 2026: "use golder arc for every loading screen" (Figma "Reconnecting C · Gold
        /// Arc", in the Dhaka Ring's place). The gold arc turns anticlockwise, its bright end leading,
        /// round the still crimson seal and about its centre, so it never wobbles; the art is
        /// uncompressed (no softening).
        /// </summary>
        [Test]
        public void TheRing_IsTheGoldArc_TurningRoundItsStillSeal()
        {
            Image ring = _ring.transform.Find("Face").GetComponent<Image>();
            Transform sealNode = _ring.transform.parent.Find("Seal");
            Assert.IsNotNull(sealNode, "the seal beside the ring");
            Image seal = sealNode.Find("Face").GetComponent<Image>();

            Assert.AreEqual("Deco_LoadingRing", ring.sprite.name, "the Gold Arc");
            Assert.AreEqual("Deco_LoadingSeal", seal.sprite.name, "its seal with the spade");
            AssertTurnsRoundItsStillSeal(_ring, ring, seal);
        }

        /// <summary>The black look over the store's sheet (ADR 042) shows the same Gold Arc, drawn for black.</summary>
        [Test]
        public void TheBlackLook_ShowsTheGoldArc_TurningRoundItsStillSeal()
        {
            var dark = (GameObject)new SerializedObject(_curtain).FindProperty("_dark").objectReferenceValue;
            Assert.IsNotNull(dark, "the curtain has its black look");
            Image ring = dark.transform.Find("Ring").GetComponent<Image>();
            Transform sealNode = dark.transform.Find("Seal");
            Assert.IsNotNull(sealNode, "the seal in the ring's middle");
            Image seal = sealNode.GetComponent<Image>();

            Assert.AreEqual("Deco_LoadingRing_Dark", ring.sprite.name, "the Gold Arc, drawn for black");
            Assert.AreEqual("Deco_LoadingSeal_Dark", seal.sprite.name, "its seal, drawn for black");
            AssertTurnsRoundItsStillSeal(ring.GetComponent<Spinner>(), ring, seal);
        }

        private static void AssertTurnsRoundItsStillSeal(Spinner spinner, Image ring, Image seal)
        {
            Assert.IsNotNull(spinner, "the arc turns");
            Assert.IsNull(seal.GetComponentInParent<Spinner>(true), "the seal stays still");
            Vector3 ringCentre = ring.rectTransform.TransformPoint(ring.rectTransform.rect.center);
            Vector3 sealCentre = seal.rectTransform.TransformPoint(seal.rectTransform.rect.center);
            Assert.That(Vector3.Distance(ringCentre, sealCentre), Is.LessThan(0.01f), "the arc turns about the seal's centre");
            Assert.IsFalse(new SerializedObject(spinner).FindProperty("_clockwise").boolValue, "the arc runs anticlockwise, its bright end leading");
            foreach (Image art in new[] { ring, seal })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(art.sprite));
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, art.sprite.name + " is not softened");
            }

            // Under the spade the seal is the logo's crimson (the Dhaka Ring's seal was ivory there).
            Color32 under = SealPixelUnderTheSpade(seal.sprite);
            Assert.That(under.r > 2 * under.g && under.r > 2 * under.b, Is.True, seal.sprite.name + " is crimson under its spade, not " + under);
        }

        private static Color32 SealPixelUnderTheSpade(Sprite sprite)
        {
            var picture = new Texture2D(2, 2);
            try
            {
                picture.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite)));

                // 45 of Figma's pixels below the middle: under the spade, inside the rim (gold_arc.py).
                float perFigmaPixel = picture.width / (2f * 92.3f);
                return picture.GetPixel(picture.width / 2, Mathf.RoundToInt(picture.height / 2f - 45f * perFigmaPixel));
            }
            finally
            {
                Object.DestroyImmediate(picture);
            }
        }

        [Test]
        public void ReduceMotion_TheRingStandsStillAtItsRestingAngle_WhileTheCurtainCovers()
        {
            _settings.SetReduceMotion(true);
            _curtain.Construct(_settings);
            Task cover = _curtain.CoverAsync(CancellationToken.None);

            Assert.IsTrue(cover.IsCompleted, "the curtain is built covering, so there is nothing to fade");
            Assert.IsTrue(_curtain.IsCovering);
            Assert.IsTrue(_ring.Still);
            Assert.IsFalse(_ring.IsTurning, "Reduce motion: a static indicator");
            Assert.AreEqual(Quaternion.identity, _ring.transform.localRotation, "the ring rests as the mockup draws it");
        }

        [Test]
        public void ReduceMotionSwitchedWhileCovering_TheRingStopsAtOnce_AndTurnsAgainWhenItIsOff()
        {
            _curtain.Construct(_settings);
            _curtain.CoverAsync(CancellationToken.None);
            Assert.IsTrue(_ring.IsTurning, "without Reduce motion the ring turns while the curtain covers");

            _settings.SetReduceMotion(true);
            Assert.IsFalse(_ring.IsTurning, "the curtain follows the setting at once");
            Assert.AreEqual(Quaternion.identity, _ring.transform.localRotation);

            _settings.SetReduceMotion(false);
            Assert.IsTrue(_ring.IsTurning);
        }

        [Test]
        public void SettingsArriveAfterTheCurtainCovers_TheRingStops_AndTheCurtainStillCovers()
        {
            // The curtain covers from the app's first frame; the settings come when the app scope is built.
            _curtain.CoverAsync(CancellationToken.None);
            Assert.IsTrue(_ring.IsTurning, "the ring turns until the curtain knows better");

            _settings.SetReduceMotion(true);
            _curtain.Construct(_settings);

            Assert.IsTrue(_curtain.IsCovering, "learning the setting does not open the curtain");
            Assert.IsFalse(_ring.IsTurning);
            Assert.AreEqual(Quaternion.identity, _ring.transform.localRotation);
        }
    }
}
