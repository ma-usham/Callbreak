using System;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The shop's right-hand drawer (mockups 15, 20 and 21; the owner, 5 Oct 2026): an ivory panel
    /// flush with the right edge, always open, with the Cards, Backgrounds and Tables buttons stacked
    /// in its middle and no storefront disc of its own. The active button is a crimson disc with its
    /// label in crimson (each button has both looks, each as its mockup draws it, smaller than the
    /// mockups draw them).
    /// </summary>
    public sealed class ShopDrawer : MonoBehaviour
    {
        [SerializeField] private Button[] _categoryButton;
        [SerializeField] private GameObject[] _activeLook;
        [SerializeField] private GameObject[] _inactiveLook;

        // The safe area's insets the drawer last fitted to (canvas units); -1 before the first fit.
        private float _bottomInset = -1f;
        private float _topInset = -1f;

        /// <summary>A category button was tapped: 0 Cards, 1 Backgrounds, 2 Tables.</summary>
        public event Action<int> CategoryClicked;

        /// <summary>The category that shows as active: 0 Cards, 1 Backgrounds, 2 Tables.</summary>
        public int Active { get; private set; }

        /// <summary>Lights the category that shows: its disc and its label crimson.</summary>
        public void SetActive(int category)
        {
            Active = category;
            for (int i = 0; i < _categoryButton.Length; i++)
            {
                bool active = i == category;
                SetActiveObject(_activeLook[i], active);
                SetActiveObject(_inactiveLook[i], !active);
            }
        }

        private void OnEnable()
        {
            for (int i = 0; i < _categoryButton.Length; i++)
            {
                int category = i;
                _categoryButton[i].onClick.AddListener(() => OnCategory(category));
            }
        }

        private void OnDisable()
        {
            foreach (Button button in _categoryButton)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        private void LateUpdate() => FitScreen();

        /// <summary>
        /// The drawer lives in the safe area, so a notch on the right never covers its buttons (its
        /// plain bleed fills the strip beyond), but its panel is as tall as the screen, its rounded
        /// corners at the screen's top and bottom: the rail reaches past the safe area's top and
        /// bottom insets. Follows the safe area when it changes.
        /// </summary>
        private void FitScreen()
        {
            var rail = (RectTransform)transform;
            var safe = rail.parent as RectTransform;
            var screen = safe != null ? safe.parent as RectTransform : null;
            if (screen == null)
            {
                return;
            }

            Rect full = screen.rect;
            float bottom = safe.anchorMin.y * full.height;
            float top = (1f - safe.anchorMax.y) * full.height;
            if (Mathf.Approximately(bottom, _bottomInset) && Mathf.Approximately(top, _topInset))
            {
                return;
            }

            _bottomInset = bottom;
            _topInset = top;
            rail.offsetMin = new Vector2(rail.offsetMin.x, -bottom);
            rail.offsetMax = new Vector2(rail.offsetMax.x, top);
        }

        private void OnCategory(int category) => CategoryClicked?.Invoke(category);

        private static void SetActiveObject(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
