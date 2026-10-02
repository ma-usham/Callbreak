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
using AppSettings = Callbreak.Application.Settings.SettingsService;     // UnityEditor has a SettingsService too

namespace Callbreak.Tests.EditMode.Unity
{
    /// <summary>
    /// The loading curtain as the Boot scene has it (the SceneLoading prefab, mockup 17): with Reduce
    /// motion its ring stands still at its resting angle, a static indicator (GDD §14); otherwise it
    /// turns while the curtain covers. The settings can reach the curtain after it has covered.
    /// These tests need the engine, so they live outside the folders the dotnet harness compiles.
    /// The prefab is opened in a preview scene, which leaves the Editor's open scenes untouched.
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
