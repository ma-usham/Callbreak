using System.Collections;
using Callbreak.Presentation.Framework;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Callbreak.Tests.PlayMode
{
    /// <summary>
    /// The button press (GDD §12/§14, ADR 023) works on the size a button rests at. A button its
    /// layout made smaller (the shop's drawer builds its buttons at 0.68) shrinks a little while held
    /// and settles back at its own size, never at full size: the owner, 5 Oct 2026, found the drawer
    /// buttons growing as they were tapped. A full-size button behaves as before, quick taps one
    /// after another end at the button's own size, and a button hidden under the finger, or hidden and
    /// shown untouched, keeps its size.
    /// </summary>
    public sealed class ButtonFeedbackTests
    {
        private const float Small = 0.68f;

        private GameObject _button;

        [TearDown]
        public void TearDown()
        {
            if (_button != null)
            {
                Object.Destroy(_button);
            }
        }

        [UnityTest]
        public IEnumerator ASmallerButton_ShrinksALittle_ThenSettlesBackAtItsOwnSize()
        {
            ButtonFeedback feedback = NewButton(Small);

            feedback.OnPointerDown(Pointer());
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.Less(_button.transform.localScale.x, Small, "held, it shrinks a little");
            Assert.Greater(_button.transform.localScale.x, Small * 0.96f, "by 3% of its own size");

            feedback.OnPointerUp(Pointer());
            yield return new WaitForSecondsRealtime(0.35f);
            AssertSize(Small, "let go, it is back at its own size, not full size");
        }

        [UnityTest]
        public IEnumerator AFullSizeButton_SettlesBackAtFullSize()
        {
            ButtonFeedback feedback = NewButton(1f);

            feedback.OnPointerDown(Pointer());
            yield return new WaitForSecondsRealtime(0.15f);
            feedback.OnPointerUp(Pointer());
            yield return new WaitForSecondsRealtime(0.35f);

            AssertSize(1f, "as before");
        }

        [UnityTest]
        public IEnumerator QuickTapsOneAfterAnother_EndAtItsOwnSize()
        {
            ButtonFeedback feedback = NewButton(Small);

            for (int tap = 0; tap < 3; tap++)
            {
                feedback.OnPointerDown(Pointer());
                yield return new WaitForSecondsRealtime(0.04f);
                feedback.OnPointerUp(Pointer());
                yield return new WaitForSecondsRealtime(0.06f);
            }

            yield return new WaitForSecondsRealtime(0.35f);
            AssertSize(Small, "each tap began while the last one still settled");
        }

        [UnityTest]
        public IEnumerator HiddenWhileHeld_IsBackAtItsOwnSize()
        {
            ButtonFeedback feedback = NewButton(Small);
            feedback.OnPointerDown(Pointer());
            yield return new WaitForSecondsRealtime(0.05f);

            _button.SetActive(false);
            AssertSize(Small, "its screen went away under the finger");

            _button.SetActive(true);
            yield return null;
            AssertSize(Small);
        }

        [UnityTest]
        public IEnumerator HiddenAndShownUntouched_KeepsItsSize()
        {
            NewButton(Small);
            yield return null;

            _button.SetActive(false);
            _button.SetActive(true);
            yield return null;

            AssertSize(Small, "nothing makes an untouched button full size");
        }

        private ButtonFeedback NewButton(float scale)
        {
            _button = new GameObject("TestButton", typeof(RectTransform));
            _button.transform.localScale = new Vector3(scale, scale, 1f);
            return _button.AddComponent<ButtonFeedback>();
        }

        private static PointerEventData Pointer()
        {
            return new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        }

        private void AssertSize(float expected, string why = null)
        {
            Assert.AreEqual(expected, _button.transform.localScale.x, 0.001f, why);
            Assert.AreEqual(expected, _button.transform.localScale.y, 0.001f, why);
        }
    }
}
