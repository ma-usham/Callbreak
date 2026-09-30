using System;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// One toast on screen (GDD §23 toast layer): an ivory pill at the top centre that sizes itself
    /// to its text, fades in, stays for <c>_holdSeconds</c>, fades out, then raises
    /// <see cref="Finished"/>. Only fades, so it is the same with Reduce motion. It never takes
    /// input. <see cref="ToastService"/> queues the texts; this view shows one at a time.
    /// </summary>
    public sealed class ToastView : MonoBehaviour
    {
        [SerializeField] private RectTransform _pill;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _label;
        [SerializeField] private float _fadeSeconds = 0.2f;
        [SerializeField] private float _holdSeconds = 2.4f;
        [SerializeField] private float _maxTextWidth = 1100f;
        [SerializeField] private Vector2 _padding = new Vector2(36f, 16f);
        [SerializeField] private float _minHeight = 64f;

        private MotionHandle _motion;

        /// <summary>Raised when a toast has faded out completely (not when it is cleared).</summary>
        public event Action Finished;

        public bool IsShowing { get; private set; }

        public string Text => _label != null ? _label.text : string.Empty;

        /// <summary>Shows <paramref name="text"/> now, replacing any toast on screen.</summary>
        public void Show(string text)
        {
            CancelMotion();
            _label.text = text ?? string.Empty;
            Fit();
            _pill.gameObject.SetActive(true);
            IsShowing = true;
            _group.alpha = 0f;
            _motion = LMotion.Create(0f, 1f, _fadeSeconds)
                .WithEase(Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(FadeOut)
                .Bind(_group, (alpha, group) => group.alpha = alpha);
        }

        /// <summary>Hides at once without raising <see cref="Finished"/>.</summary>
        public void Clear()
        {
            CancelMotion();
            IsShowing = false;
            if (_group != null)
            {
                _group.alpha = 0f;
            }

            if (_pill != null)
            {
                _pill.gameObject.SetActive(false);
            }
        }

        private void FadeOut()
        {
            _motion = LMotion.Create(1f, 0f, _fadeSeconds)
                .WithDelay(_holdSeconds)
                .WithEase(Ease.InQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(Complete)
                .Bind(_group, (alpha, group) => group.alpha = alpha);
        }

        private void Complete()
        {
            // This runs inside the fade-out's completion; forget its handle so a Show from a
            // Finished handler doesn't try to cancel it.
            _motion = default;
            IsShowing = false;
            _pill.gameObject.SetActive(false);
            Finished?.Invoke();
        }

        /// <summary>One line when it fits in <c>_maxTextWidth</c>, wrapped lines when it doesn't; the pill hugs the text.</summary>
        private void Fit()
        {
            float natural = _label.GetPreferredValues(_label.text).x;
            float width = Mathf.Ceil(Mathf.Min(natural, _maxTextWidth)) + 2f;
            float height = _label.GetPreferredValues(_label.text, width, 0f).y;
            _pill.sizeDelta = new Vector2(width + _padding.x * 2f, Mathf.Max(_minHeight, Mathf.Ceil(height) + _padding.y * 2f));
        }

        private void CancelMotion()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }
        }

        private void Awake()
        {
            FillParentIfNested();
            if (!IsShowing)
            {
                Clear();
            }
        }

        private void OnTransformParentChanged() => FillParentIfNested();

        /// <summary>
        /// Instantiated at the root, the toast's own overlay canvas sizes itself. Placed under a
        /// scene's canvas it is an ordinary child, so it stretches over its parent (the prefab
        /// stores the overlay canvas's driven transform, which has zero scale).
        /// </summary>
        private void FillParentIfNested()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null || canvas.isRootCanvas || !(transform is RectTransform rect))
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            // A toast interrupted by its canvas going away is dropped; ToastService shows the next
            // waiting one on its next Show once the view is active again.
            Clear();
        }
    }
}
