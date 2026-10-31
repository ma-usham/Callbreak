using System;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The shop's right-hand drawer (mockups 15, 20 and 21; the owner, 5 Oct 2026; Figma "V1 Rebuild ·
    /// Shop", ADR 064): an ivory panel flush with the right edge, always open, with the Themes and Cards
    /// buttons and, under them, No Ads, stacked in its middle, and no storefront disc of its own. The
    /// active tab is a crimson disc with its label in crimson (each tab has both looks, smaller than
    /// the mockups draw them). No Ads is the store's one-time purchase, not a tab: it shows while the
    /// store sells it, as on the main menu.
    /// </summary>
    public sealed class ShopDrawer : MonoBehaviour
    {
        [SerializeField] private Button[] _categoryButton;
        [SerializeField] private GameObject[] _activeLook;
        [SerializeField] private GameObject[] _inactiveLook;
        [SerializeField] private Button _noAdsButton;

        // The panel art and its shadow, pinned to the rail's left edge, and their widths as built:
        // they grow by the safe area's right inset, so the drawer reaches the screen's edge (ADR 064).
        [SerializeField] private RectTransform _face;
        [SerializeField] private RectTransform _shadow;
        [SerializeField] private float _faceWidth;
        [SerializeField] private float _shadowWidth;

        // The safe area's insets the drawer last fitted to (canvas units); -1 before the first fit.
        private float _bottomInset = -1f;
        private float _topInset = -1f;
        private float _rightInset = -1f;

        /// <summary>A tab was tapped: 0 Themes, 1 Cards.</summary>
        public event Action<int> CategoryClicked;

        /// <summary>No Ads was tapped.</summary>
        public event Action NoAdsClicked;

        /// <summary>The tab that shows as active: 0 Themes, 1 Cards.</summary>
        public int Active { get; private set; }

        public Button NoAdsButton => _noAdsButton;

        /// <summary>No Ads shows while the store sells it (and while its purchase is under way).</summary>
        public void SetNoAdsVisible(bool visible)
        {
            if (_noAdsButton != null)
            {
                SetActiveObject(_noAdsButton.gameObject, visible);
            }
        }

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

            if (_noAdsButton != null)
            {
                _noAdsButton.onClick.AddListener(OnNoAds);
            }
        }

        private void OnDisable()
        {
            foreach (Button button in _categoryButton)
            {
                button.onClick.RemoveAllListeners();
            }

            if (_noAdsButton != null)
            {
                _noAdsButton.onClick.RemoveListener(OnNoAds);
            }
        }

        private void LateUpdate() => FitScreen();

        /// <summary>
        /// The drawer lives in the safe area, so a notch on the right never covers its buttons, but its
        /// panel reaches the screen's edges: the rail reaches past the safe area's top and bottom
        /// insets, so the panel's rounded corners sit at the screen's top and bottom, and the panel and
        /// its shadow grow by the right inset, so it runs on to the screen's right edge with no strip
        /// beside it (ADR 064). Follows the safe area when it changes.
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
            float right = (1f - safe.anchorMax.x) * full.width;
            if (Mathf.Approximately(bottom, _bottomInset) && Mathf.Approximately(top, _topInset) && Mathf.Approximately(right, _rightInset))
            {
                return;
            }

            _bottomInset = bottom;
            _topInset = top;
            _rightInset = right;
            rail.offsetMin = new Vector2(rail.offsetMin.x, -bottom);
            rail.offsetMax = new Vector2(rail.offsetMax.x, top);
            Widen(_face, _faceWidth, right);
            Widen(_shadow, _shadowWidth, right);
        }

        private static void Widen(RectTransform art, float width, float by)
        {
            if (art != null && width > 0f)
            {
                art.sizeDelta = new Vector2(width + by, art.sizeDelta.y);
            }
        }

        private void OnCategory(int category) => CategoryClicked?.Invoke(category);

        private void OnNoAds() => NoAdsClicked?.Invoke();

        private static void SetActiveObject(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
