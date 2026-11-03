using System;
using Callbreak.Presentation.Framework;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The first-hand tutorial over the table (ADR 084), on a canvas of its own above the pages and
    /// below dialogs and toasts: an ivory caption card with a title, a line or two, Next and "Skip
    /// tutorial", and a pointing hand on what to tap. While the match plays, the card sits in the
    /// top-right corner, clear of the hand, the trick and the call panel, and nothing else of the
    /// overlay takes a touch; for the welcome and the score it is centred over a dim that takes
    /// every touch. On a card the hand shows the pull up that plays it; on a chip or a button it
    /// taps. With Reduce motion it stands still.
    /// </summary>
    public sealed class TutorialView : MonoBehaviour
    {
        // Pointer motion: a tap bobs, a pull rises from the card and fades, over and over.
        private const float TapSeconds = 0.9f;
        private const float TapDistance = 22f;
        private const float PullSeconds = 1.6f;
        private const float PullDistance = 170f;

        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _overlay;
        [SerializeField] private GameObject _dim;
        [SerializeField] private RectTransform _card;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private Button _nextButton;
        [SerializeField] private TMP_Text _nextLabel;
        [SerializeField] private Button _skipButton;
        [SerializeField] private TMP_Text _skipLabel;
        [SerializeField] private RectTransform _pointer;
        [SerializeField] private CanvasGroup _pointerGroup;

        // The card's layout (canvas units at the 1080p reference).
        // In play the card stays above the right-hand seat and right of the top seat's fan, under the banner.
        [SerializeField] private float _cornerWidth = 740f;
        [SerializeField] private float _centreWidth = 760f;
        [SerializeField] private Vector2 _cornerMargin = new Vector2(70f, 160f);
        [SerializeField] private Vector2 _padding = new Vector2(34f, 22f);
        [SerializeField] private float _titleGap = 6f;
        [SerializeField] private float _buttonGap = 14f;
        [SerializeField] private float _buttonHeight = 66f;

        private RectTransform _target;
        private Vector2 _targetPoint;
        private bool _pull;
        private float _clock;
        private bool _centred;
        private bool _hiding;
        private MotionHandle _motion;

        public event Action NextClicked;

        public event Action SkipClicked;

        public bool ReduceMotion { get; set; }

        /// <summary>A caption is on screen (and not on its way out).</summary>
        public bool IsShown => _card.gameObject.activeSelf && !_hiding;

        /// <summary>The dim behind a centred card is up: the table under it takes no touch.</summary>
        public bool BlocksTable => _dim.activeSelf;

        public string Title => _title.text;

        public string Body => _body.text;

        /// <summary>The Next button's label, or null while it is hidden.</summary>
        public string NextLabel => _nextButton.gameObject.activeSelf ? _nextLabel.text : null;

        /// <summary>The pointer is on screen.</summary>
        public bool IsPointing => _pointer.gameObject.activeSelf;

        private void Awake()
        {
            _nextButton.onClick.AddListener(() => NextClicked?.Invoke());
            _skipButton.onClick.AddListener(() => SkipClicked?.Invoke());
            HideNow();
        }

        private void OnDestroy()
        {
            Cancel();
        }

        /// <summary>
        /// Shows a caption: <paramref name="next"/> null hides Next (the step waits for a call or
        /// a card instead). Centred, it stands over a dim that takes every touch.
        /// </summary>
        public void Show(string title, string body, string next, string skip, bool centred)
        {
            bool wasShown = IsShown && _centred == centred;
            Cancel();
            _hiding = false;
            _centred = centred;
            _title.text = title ?? string.Empty;
            _body.text = body ?? string.Empty;
            _nextButton.gameObject.SetActive(!string.IsNullOrEmpty(next));
            _nextLabel.text = next ?? string.Empty;
            _skipButton.gameObject.SetActive(!string.IsNullOrEmpty(skip));
            _skipLabel.text = skip ?? string.Empty;
            _dim.SetActive(centred);
            _group.blocksRaycasts = true;
            _card.gameObject.SetActive(true);
            Fit();
            if (wasShown)
            {
                // The next caption of the same card: only its words change.
                _group.alpha = 1f;
                _card.localScale = Vector3.one;
            }
            else
            {
                _motion = UiMotion.PopIn(_card, _group, ReduceMotion);
            }
        }

        /// <summary>Takes the caption and the pointer away.</summary>
        public void Hide()
        {
            if (!IsShown)
            {
                return;
            }

            Cancel();
            _hiding = true;
            _group.blocksRaycasts = false;
            _motion = UiMotion.PopOut(_card, _group, ReduceMotion, HideNow);
            ClearPointer();
        }

        /// <summary>
        /// Points at <paramref name="target"/>, at <paramref name="point"/> in its rect (0 to 1 from
        /// the bottom left). <paramref name="pull"/> shows a card being pulled up; otherwise a tap.
        /// </summary>
        public void PointAt(RectTransform target, Vector2 point, bool pull)
        {
            if (target == null)
            {
                ClearPointer();
                return;
            }

            if (_target != target || _pull != pull)
            {
                _clock = 0f;
            }

            _target = target;
            _targetPoint = point;
            _pull = pull;
            _pointer.gameObject.SetActive(true);
            Place();
        }

        public void ClearPointer()
        {
            _target = null;
            _pointer.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            if (!_target.gameObject.activeInHierarchy)
            {
                _pointer.gameObject.SetActive(false);
                return;
            }

            _pointer.gameObject.SetActive(true);
            _clock += Time.unscaledDeltaTime;
            Place();
        }

        /// <summary>The fingertip on the target, moving as the pointer's gesture goes.</summary>
        private void Place()
        {
            Rect rect = _target.rect;
            Vector3 world = _target.TransformPoint(new Vector3(
                rect.xMin + rect.width * _targetPoint.x,
                rect.yMin + rect.height * _targetPoint.y,
                0f));
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, world);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_overlay, screen, null, out Vector2 local))
            {
                return;
            }

            float lift = 0f;
            float alpha = 1f;
            if (!ReduceMotion)
            {
                if (_pull)
                {
                    // In, up and away: the pull that plays a card.
                    float t = Mathf.Repeat(_clock, PullSeconds) / PullSeconds;
                    float rise = Mathf.InverseLerp(0.15f, 0.75f, t);
                    lift = PullDistance * (1f - (1f - rise) * (1f - rise));
                    alpha = t < 0.12f ? t / 0.12f : t > 0.8f ? Mathf.Clamp01((1f - t) / 0.2f) : 1f;
                }
                else
                {
                    float t = Mathf.Repeat(_clock, TapSeconds) / TapSeconds;
                    lift = -TapDistance * 0.5f * (1f + Mathf.Cos(t * Mathf.PI * 2f));
                    lift += TapDistance;
                }
            }

            _pointer.anchoredPosition = local + new Vector2(0f, lift);
            _pointerGroup.alpha = alpha;
        }

        /// <summary>Lays the card out around its text: title, body, then the buttons' row.</summary>
        private void Fit()
        {
            float width = _centred ? _centreWidth : _cornerWidth;
            float textWidth = width - 2f * _padding.x;
            float titleHeight = _title.GetPreferredValues(_title.text, textWidth, 0f).y;
            float bodyHeight = string.IsNullOrEmpty(_body.text) ? 0f : _body.GetPreferredValues(_body.text, textWidth, 0f).y;
            float height = _padding.y + titleHeight + _titleGap + bodyHeight + _buttonGap + _buttonHeight + _padding.y;

            if (_centred)
            {
                _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(0.5f, 0.5f);
                _card.anchoredPosition = Vector2.zero;
            }
            else
            {
                _card.anchorMin = _card.anchorMax = _card.pivot = new Vector2(1f, 1f);
                _card.anchoredPosition = new Vector2(-_cornerMargin.x, -_cornerMargin.y);
            }

            _card.sizeDelta = new Vector2(width, height);
            Place(_title.rectTransform, _padding.y, textWidth, titleHeight);
            Place(_body.rectTransform, _padding.y + titleHeight + _titleGap, textWidth, bodyHeight);

            // Next on the right, Skip on the left; either alone keeps its side.
            var next = (RectTransform)_nextButton.transform;
            var skip = (RectTransform)_skipButton.transform;
            float buttonsTop = height - _padding.y - _buttonHeight;
            next.anchorMin = next.anchorMax = next.pivot = new Vector2(1f, 1f);
            next.anchoredPosition = new Vector2(-_padding.x, -buttonsTop);
            skip.anchorMin = skip.anchorMax = skip.pivot = new Vector2(0f, 1f);
            skip.anchoredPosition = new Vector2(_padding.x, -buttonsTop - (_buttonHeight - skip.sizeDelta.y) / 2f);
        }

        private static void Place(RectTransform text, float top, float width, float height)
        {
            text.anchorMin = text.anchorMax = text.pivot = new Vector2(0.5f, 1f);
            text.anchoredPosition = new Vector2(0f, -top);
            text.sizeDelta = new Vector2(width, height);
        }

        private void HideNow()
        {
            _hiding = false;
            _card.gameObject.SetActive(false);
            _dim.SetActive(false);
            _group.alpha = 1f;
            _group.blocksRaycasts = false;
            _card.localScale = Vector3.one;
            ClearPointer();
        }

        private void Cancel()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }
        }
    }
}
