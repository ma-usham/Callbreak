using System;
using Callbreak.Presentation.Framework;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The shop's right-hand drawer (mockups 15, 20 and 21; designer's spec): an ivory panel flush
    /// with the right edge holding the Cards, Backgrounds and Tables buttons, the active one a
    /// crimson disc with its label in crimson (each button has both looks, each as its mockup draws
    /// it). The storefront disc at its top stays where it is and opens or closes the drawer, which
    /// slides in from the right or back out in 250 ms (ease-out), at once with Reduce motion. While
    /// it is closed its buttons take no taps.
    /// </summary>
    public sealed class ShopDrawer : MonoBehaviour
    {
        /// <summary>The spec's slide: about 250 ms, ease-out.</summary>
        public const float SlideSeconds = 0.25f;

        [SerializeField] private Button _toggleButton;
        [SerializeField] private RectTransform _panel;
        [SerializeField] private CanvasGroup _panelGroup;
        [SerializeField] private float _openX;
        [SerializeField] private float _closedX;
        [SerializeField] private Button[] _categoryButton;
        [SerializeField] private GameObject[] _activeLook;
        [SerializeField] private GameObject[] _inactiveLook;

        private MotionHandle _motion;
        private bool _open = true;

        // The safe area's insets the drawer last fitted to (canvas units); -1 before the first fit.
        private float _bottomInset = -1f;
        private float _topInset = -1f;
        private float _rightInset;

        /// <summary>The storefront disc was tapped.</summary>
        public event Action ToggleClicked;

        /// <summary>A category button was tapped: 0 Cards, 1 Backgrounds, 2 Tables.</summary>
        public event Action<int> CategoryClicked;

        public bool IsOpen => _open;

        public bool IsMoving => _motion.IsActive();

        /// <summary>How far the panel has slid out: 0 open, 1 closed.</summary>
        public float Closedness => Mathf.Approximately(ClosedX, _openX) ? 0f : Mathf.InverseLerp(_openX, ClosedX, _panel.anchoredPosition.x);

        // Closed, the panel is off the screen's edge, beyond a safe-area inset on the right too.
        private float ClosedX => _closedX + _rightInset;

        /// <summary>The category that shows as active: 0 Cards, 1 Backgrounds, 2 Tables.</summary>
        public int Active { get; private set; }

        /// <summary>Opens or closes the drawer: slides, or with <paramref name="instant"/> jumps.</summary>
        public void SetOpen(bool open, bool instant)
        {
            _open = open;
            _panelGroup.interactable = open;
            _panelGroup.blocksRaycasts = open;
            UiMotion.Stop(ref _motion);
            float target = open ? _openX : ClosedX;
            if (instant || !isActiveAndEnabled)
            {
                SetX(target);
                return;
            }

            _motion = LMotion.Create(_panel.anchoredPosition.x, target, SlideSeconds)
                .WithEase(Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(this, (x, drawer) => drawer.SetX(x));
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
            _toggleButton.onClick.AddListener(OnToggle);
            for (int i = 0; i < _categoryButton.Length; i++)
            {
                int category = i;
                _categoryButton[i].onClick.AddListener(() => OnCategory(category));
            }
        }

        private void OnDisable()
        {
            _toggleButton.onClick.RemoveListener(OnToggle);
            foreach (Button button in _categoryButton)
            {
                button.onClick.RemoveAllListeners();
            }

            // Put away mid-slide: rest where it was going.
            UiMotion.Stop(ref _motion);
            SetX(_open ? _openX : ClosedX);
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
            if (!_motion.IsActive())
            {
                SetX(_open ? _openX : ClosedX);
            }
        }

        private void OnDestroy()
        {
            UiMotion.Stop(ref _motion);
        }

        private void SetX(float x)
        {
            _panel.anchoredPosition = new Vector2(x, _panel.anchoredPosition.y);
        }

        private void OnToggle() => ToggleClicked?.Invoke();

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
