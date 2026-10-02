using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The loading curtain in the real Boot scene (GDD §21, §23; mockup 17): while a scene loads it
    /// shows the Play Online emblem (the ring turning around the still spade) and "Loading..."; the
    /// ring turns on unscaled time, stands still with Reduce motion, and stops once the curtain has
    /// opened. Saves go to a temporary folder.
    /// </summary>
    public sealed class LoadingCurtainTests
    {
        private const string LoadingKey = "ui.scene_loading.title";

        private GameFixture _game;

        [SetUp]
        public void SetUp()
        {
            _game = new GameFixture();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            yield return _game.TearDown();
        }

        [UnityTest]
        public IEnumerator SceneChange_TheCurtainShowsTheTurningRingTheSpadeAndLoading_UntilTheTableIsUp()
        {
            yield return _game.Boot();
            LoadingCurtain curtain = Curtain();
            Spinner ring = curtain.GetComponentInChildren<Spinner>(true);
            Assert.IsFalse(_game.Curtain.IsCovering, "the curtain opened onto the menu");
            Assert.IsFalse(ring.IsTurning, "the ring rests while the curtain is open");

            GameFixture.Press(_game.MenuRouter, "PlayOfflineButton");
            GameFixture.Press(_game.MenuRouter, "StartButton");
            yield return GameFixture.Until(() => _game.Curtain.IsCovering, "the curtain to cover the way to the table");

            Assert.AreEqual("Deco_SearchArc", Face(curtain, "Ring").sprite.name, "the Play Online ring");
            Assert.AreEqual("Icon_Suit_Spade", Face(curtain, "Spade").sprite.name, "the spade inside it");
            Assert.IsTrue(Face(curtain, "Logo").isActiveAndEnabled, "the logo");
            string loading = _game.Services.Resolve<ILocalization>().Get(LoadingKey);
            Assert.AreNotEqual(LoadingKey, loading, LoadingKey + " is in the string table");
            Assert.AreEqual(loading, Label(curtain, "Loading").text);
            Assert.AreEqual(_game.Services.Resolve<ILocalization>().Get("ui.main_menu.tagline"), Label(curtain, "Tagline").text);

            Assert.IsTrue(ring.IsTurning, "the ring turns while the curtain covers");
            float before = ring.transform.localEulerAngles.z;
            yield return new WaitForSecondsRealtime(0.1f);     // shorter than the curtain's own fade in
            Assert.IsTrue(_game.Curtain.IsCovering, "the curtain still covers a moment later");
            Assert.AreNotEqual(before, ring.transform.localEulerAngles.z, "the ring has turned");
            Assert.AreEqual(Quaternion.identity, Face(curtain, "Spade").transform.parent.localRotation, "the spade stays still");

            yield return _game.UntilTable();
            Assert.IsFalse(ring.IsTurning, "the ring stops once the curtain has opened");
        }

        [UnityTest]
        public IEnumerator PausedTimeScale_TheRingStillTurns_AndWithReduceMotionItStandsStill()
        {
            yield return _game.Boot();
            LoadingCurtain curtain = Curtain();
            Spinner ring = curtain.GetComponentInChildren<Spinner>(true);

            Time.timeScale = 0f;
            Task cover = _game.Curtain.CoverAsync(CancellationToken.None);
            yield return GameFixture.Until(() => cover.IsCompleted, "the curtain to fade in on unscaled time");
            Assert.IsTrue(_game.Curtain.IsCovering);
            float before = ring.transform.localEulerAngles.z;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreNotEqual(before, ring.transform.localEulerAngles.z, "a paused time scale does not stop the ring");

            _game.Settings.SetReduceMotion(true);
            Assert.IsFalse(ring.IsTurning, "Reduce motion: a static indicator");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.AreEqual(Quaternion.identity, ring.transform.localRotation, "the ring rests at its resting angle");
            Assert.IsTrue(_game.Curtain.IsCovering, "the curtain still covers");

            _game.Settings.SetReduceMotion(false);
            Assert.IsTrue(ring.IsTurning, "the ring turns again once Reduce motion is off");

            Task uncover = _game.Curtain.UncoverAsync(CancellationToken.None);
            yield return GameFixture.Until(() => uncover.IsCompleted, "the curtain to open on unscaled time");
            Assert.IsFalse(_game.Curtain.IsCovering);
            Assert.IsFalse(ring.IsTurning);
        }

        private LoadingCurtain Curtain()
        {
            var curtain = _game.Project.GetComponentInChildren<LoadingCurtain>(true);
            Assert.IsNotNull(curtain, "the Boot scene has the curtain");
            Assert.AreSame(curtain, _game.Curtain, "it is the curtain the scene loader uses");
            return curtain;
        }

        private static Image Face(LoadingCurtain curtain, string node)
        {
            foreach (Image image in curtain.GetComponentsInChildren<Image>(true))
            {
                if (image.name == "Face" && image.transform.parent.name == node)
                {
                    return image;
                }
            }

            Assert.Fail("The curtain has no " + node + ".");
            return null;
        }

        private static TMP_Text Label(LoadingCurtain curtain, string node)
        {
            foreach (TMP_Text label in curtain.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label.name == node)
                {
                    return label;
                }
            }

            Assert.Fail("The curtain has no label " + node + ".");
            return null;
        }
    }
}
