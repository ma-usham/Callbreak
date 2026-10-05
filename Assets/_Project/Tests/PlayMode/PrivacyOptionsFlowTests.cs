using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Economy;
using Callbreak.Application.Ports;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VContainer;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// Settings' "Privacy options" (Google's consent rules for ads; the owner, 5 Oct 2026) in the
    /// real Boot and Menu scenes, with scripted privacy options: while the ad network doesn't require
    /// the entry point, the page has no button and nothing else on it is any different; while it
    /// does, the button shows under the panel, with an 88 px touch target, and a tap opens the
    /// network's form once; when the network learns it is required while the page is open, the
    /// button appears. In the Editor the game's own privacy options are the stand-in, which never
    /// requires them. Saves go to a temporary folder.
    /// </summary>
    public sealed class PrivacyOptionsFlowTests
    {
        private const string ButtonName = "PrivacyOptionsButton";

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
        public IEnumerator InTheEditor_TheStandInNeverRequiresThem_AndSettingsHasNoButton()
        {
            yield return _game.Boot();
            Assert.IsInstanceOf<StandInAdPrivacy>(_game.Services.Resolve<IAdPrivacy>(), "no ad network asks for consent in the Editor");

            yield return OpenSettings();
            UIScreen page = Page();

            Assert.IsFalse(Named(page, ButtonName).gameObject.activeSelf, "no entry point that isn't required");
            Assert.IsFalse(GameFixture.CanPress(page, ButtonName));
        }

        [UnityTest]
        public IEnumerator NotRequired_NoButton_AndNothingElseOnThePageMoves()
        {
            var privacy = new ScriptedPrivacy(required: false);
            yield return Boot(privacy);
            yield return OpenSettings();
            UIScreen page = Page();

            Assert.IsFalse(Named(page, ButtonName).gameObject.activeSelf, "Google's guidance: shown only while required");
            Assert.IsFalse(GameFixture.CanPress(page, ButtonName));
            Dictionary<string, Rect> without = Layout(page);
            Assert.Greater(without.Count, 40, "the page's elements were measured");

            // The button comes and goes again; everything else stays where it was.
            privacy.Set(required: true);
            yield return null;
            Assert.IsTrue(GameFixture.CanPress(page, ButtonName));
            AssertSameLayout(without, Layout(page), "with the button showing");

            privacy.Set(required: false);
            yield return null;
            Assert.IsFalse(Named(page, ButtonName).gameObject.activeSelf, "hidden again once no longer required");
            AssertSameLayout(without, Layout(page), "with the button hidden again");
            Assert.AreEqual(0, privacy.Shows, "no form was opened");
        }

        [UnityTest]
        public IEnumerator Required_TheButtonShowsUnderThePanel_AndATapOpensTheFormOnce()
        {
            var privacy = new ScriptedPrivacy(required: true);
            yield return Boot(privacy);
            yield return OpenSettings();
            UIScreen page = Page();

            Assert.IsTrue(GameFixture.CanPress(page, ButtonName), "the entry point Google asks for");
            Assert.AreEqual(Text("ui.settings.privacy_options"), Named(page, "PrivacyOptionsLabel").GetComponent<TMP_Text>().text);
            AssertUnderThePanel(page);
            AssertTouchTarget(page);

            GameFixture.PressIn(page, ButtonName);
            Assert.AreEqual(1, privacy.Shows, "one tap, one form");
            GameFixture.PressIn(page, ButtonName);
            Assert.AreEqual(1, privacy.Shows, "a tap while the form is up opens no second one");
            Assert.AreEqual(ScreenId.Settings, _game.MenuRouter.Current, "the page stays under the form");

            // Once the form has closed, the button opens it again.
            privacy.CloseForm();
            yield return GameFixture.Until(
                () =>
                {
                    GameFixture.PressIn(page, ButtonName);
                    return privacy.Shows == 2;
                },
                "a tap to open the form again once it has closed");
            Assert.AreEqual(2, privacy.Shows);
        }

        [UnityTest]
        public IEnumerator BecomingRequired_WhileThePageIsOpen_ShowsTheButton()
        {
            var privacy = new ScriptedPrivacy(required: false);
            yield return Boot(privacy);
            yield return OpenSettings();
            UIScreen page = Page();
            Assert.IsFalse(GameFixture.CanPress(page, ButtonName));

            // As AdMob says once the consent information has arrived.
            privacy.Set(required: true);

            Assert.IsTrue(GameFixture.CanPress(page, ButtonName), "the button appears while the page is open");
            GameFixture.PressIn(page, ButtonName);
            Assert.AreEqual(1, privacy.Shows);
        }

        private IEnumerator Boot(IAdPrivacy privacy)
        {
            yield return _game.Boot(extra: builder => builder.RegisterInstance<IAdPrivacy>(privacy));
        }

        private IEnumerator OpenSettings()
        {
            ScreenRouter router = _game.MenuRouter;
            GameFixture.Press(router, "SettingsButton");
            Assert.AreEqual(ScreenId.Settings, router.Current);
            UIScreen page = Page();
            yield return GameFixture.Until(
                () => page.CanvasGroup.alpha >= 0.999f
                    && Mathf.Abs(page.SlideTarget.anchoredPosition.x) < 0.01f
                    && Mathf.Abs(page.SlideTarget.localScale.x - 1f) < 0.0001f,
                "Settings to finish its transition");
        }

        private UIScreen Page() => _game.MenuRouter.Get<UIScreen>(ScreenId.Settings);

        private string Text(string key) => _game.Services.Resolve<ILocalization>().Get(key);

        /// <summary>The button lies under the panel, centred on it, and on the screen.</summary>
        private static void AssertUnderThePanel(UIScreen page)
        {
            Rect button = ScreenRect(Named(page, ButtonName));
            Rect panel = ScreenRect(Named(page, "Panel"));
            Assert.Less(button.yMax, panel.yMin, "below the panel, so below Done");
            Assert.GreaterOrEqual(button.yMin, 0f, "on the screen");
            Assert.AreEqual(panel.center.x, button.center.x, 1f, "centred under the panel");
        }

        /// <summary>GDD: touch targets of at least 88 px (canvas units).</summary>
        private static void AssertTouchTarget(UIScreen page)
        {
            Image face = Named(Named(page, ButtonName), "Face").GetComponent<Image>();
            Assert.IsTrue(face.raycastTarget, "the pill takes the tap");
            float canvasScale = face.canvas.rootCanvas.transform.lossyScale.y;
            float height = (face.rectTransform.rect.height - face.raycastPadding.y - face.raycastPadding.w) * face.rectTransform.lossyScale.y / canvasScale;
            Assert.GreaterOrEqual(height, 87.5f, "an 88 px touch target");
        }

        /// <summary>Where every element of the page that shows is, by its path, without the privacy button.</summary>
        private static Dictionary<string, Rect> Layout(UIScreen page)
        {
            var layout = new Dictionary<string, Rect>();
            foreach (RectTransform rect in page.GetComponentsInChildren<RectTransform>(false))
            {
                if (!IsPartOfTheButton(rect, page.transform))
                {
                    layout[PathOf(rect, page.transform)] = ScreenRect(rect);
                }
            }

            return layout;
        }

        private static void AssertSameLayout(Dictionary<string, Rect> expected, Dictionary<string, Rect> actual, string when)
        {
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys, "the same elements show " + when);
            foreach (KeyValuePair<string, Rect> element in expected)
            {
                Rect now = actual[element.Key];
                Assert.AreEqual(element.Value.x, now.x, 0.01f, element.Key + " " + when);
                Assert.AreEqual(element.Value.y, now.y, 0.01f, element.Key + " " + when);
                Assert.AreEqual(element.Value.width, now.width, 0.01f, element.Key + " " + when);
                Assert.AreEqual(element.Value.height, now.height, 0.01f, element.Key + " " + when);
            }
        }

        private static bool IsPartOfTheButton(Transform transform, Transform page)
        {
            for (Transform t = transform; t != null && t != page; t = t.parent)
            {
                if (t.name == ButtonName)
                {
                    return true;
                }
            }

            return false;
        }

        private static string PathOf(Transform transform, Transform page)
        {
            string path = transform.name + "#" + transform.GetSiblingIndex();
            for (Transform t = transform.parent; t != null && t != page; t = t.parent)
            {
                path = t.name + "#" + t.GetSiblingIndex() + "/" + path;
            }

            return path;
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static RectTransform Named(Component root, string name)
        {
            foreach (RectTransform child in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            Assert.Fail(root.name + " has no " + name);
            return null;
        }

        /// <summary>
        /// Privacy options the test decides: whether they are required (and saying so changes them),
        /// and how many forms were opened; a form stays up until <see cref="CloseForm"/>.
        /// </summary>
        private sealed class ScriptedPrivacy : IAdPrivacy
        {
            private bool _required;
            private TaskCompletionSource<bool> _form;

            public ScriptedPrivacy(bool required)
            {
                _required = required;
            }

            public event Action Changed;

            public bool IsRequired => _required;

            public int Shows { get; private set; }

            public void Set(bool required)
            {
                _required = required;
                Changed?.Invoke();
            }

            public Task ShowAsync(CancellationToken ct)
            {
                Shows++;
                _form = new TaskCompletionSource<bool>();
                return _form.Task;
            }

            public void CloseForm() => _form?.TrySetResult(true);
        }
    }
}
