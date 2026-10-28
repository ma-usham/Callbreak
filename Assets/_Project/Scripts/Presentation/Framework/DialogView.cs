using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The one confirmation dialog (GDD §23 popup layer): a dimmed backdrop and a panel with a
    /// title, a divider, an optional line of body text, and a primary button with an optional
    /// secondary one beside it. The panel grows with its text. On unscaled time the dim fades in while the panel pops in
    /// (<see cref="UiMotion.PopIn"/>), and on closing the panel shrinks a touch as everything fades
    /// out; with Reduce motion it only fades. Its canvas is off while it is closed.
    /// <see cref="DialogService"/> decides what it shows and what the buttons mean.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public sealed class DialogView : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private Button _backdrop;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private Button _confirmButton;
        [SerializeField] private TMP_Text _confirmLabel;
        [SerializeField] private Button _cancelButton;
        [SerializeField] private TMP_Text _cancelLabel;
        [SerializeField] private RectTransform _divider;
        [SerializeField] private float _fadeSeconds = 0.18f;

        // V2's dialog, canvas units: its margins, the gap between its parts and between the
        // buttons, and each button's room round its words and least width.
        [SerializeField] private float _paddingTop = 56f;
        [SerializeField] private float _paddingBottom = 48f;
        [SerializeField] private float _paddingSide = 64f;
        [SerializeField] private float _gap = 28f;
        [SerializeField] private float _buttonGap = 24f;
        [SerializeField] private float _confirmPadding = 104f;
        [SerializeField] private float _confirmMinWidth = 280f;
        [SerializeField] private float _cancelPadding = 72f;
        [SerializeField] private float _cancelMinWidth = 200f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private float _confirmLabelSize;
        private float _cancelLabelSize;
        private MotionHandle _motion;
        private MotionHandle _panelMotion;

        public event Action Confirmed;

        public event Action Cancelled;

        /// <summary>A tap outside the panel: the dialog is closed without an answer.</summary>
        public event Action Dismissed;

        public bool IsShowing { get; private set; }

        /// <summary>With Reduce motion the dialog only fades (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        private Canvas Canvas => _canvas != null ? _canvas : _canvas = GetComponent<Canvas>();

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        private void Awake()
        {
            Canvas.enabled = false;
            Group.alpha = 0f;
        }

        private void OnEnable()
        {
            _backdrop.onClick.AddListener(OnDismiss);
            _confirmButton.onClick.AddListener(OnConfirm);
            _cancelButton.onClick.AddListener(OnCancel);
        }

        private void OnDisable()
        {
            _backdrop.onClick.RemoveListener(OnDismiss);
            _confirmButton.onClick.RemoveListener(OnConfirm);
            _cancelButton.onClick.RemoveListener(OnCancel);
        }

        public void Show(string title, string body, string confirmLabel, string cancelLabel)
        {
            bool hasBody = !string.IsNullOrEmpty(body);
            bool hasCancel = !string.IsNullOrEmpty(cancelLabel);
            _title.text = title ?? string.Empty;
            _body.text = body ?? string.Empty;
            _body.gameObject.SetActive(hasBody);
            _confirmLabel.text = confirmLabel ?? string.Empty;
            _cancelLabel.text = cancelLabel ?? string.Empty;
            _cancelButton.gameObject.SetActive(hasCancel);

            Canvas.enabled = true;
            Layout(hasBody, hasCancel);
            IsShowing = true;
            Group.interactable = true;
            Group.blocksRaycasts = true;
            Animate(1f, deactivate: false);
        }

        public void Hide()
        {
            if (!IsShowing)
            {
                return;
            }

            IsShowing = false;
            Group.interactable = false;
            Group.blocksRaycasts = false;
            Animate(0f, deactivate: true);
        }

        /// <summary>
        /// Stacks the title, the divider, the body and the row of buttons from the top of the panel
        /// and sizes it to fit (V2's dialog). The buttons sit side by side, the secondary one on the
        /// left, each as wide as its words and its margins but no narrower than its least width, and
        /// centred on one line; a row wider than the panel allows is narrowed and its words shrink.
        /// </summary>
        private void Layout(bool hasBody, bool hasCancel)
        {
            float y = _paddingTop;
            y = Place(_title.rectTransform, y, _title.GetPreferredValues(_title.text, _title.rectTransform.rect.width, 0f).y);
            if (_divider != null)
            {
                y += _gap;
                y = Place(_divider, y, _divider.rect.height);
            }

            if (hasBody)
            {
                y += _gap;
                y = Place(_body.rectTransform, y, _body.GetPreferredValues(_body.text, _body.rectTransform.rect.width, 0f).y);
            }

            y += _gap;
            var confirm = (RectTransform)_confirmButton.transform;
            var cancel = (RectTransform)_cancelButton.transform;

            // Each label is measured at its own size; an earlier long row may have shrunk it.
            RestoreSize(_confirmLabel, ref _confirmLabelSize);
            RestoreSize(_cancelLabel, ref _cancelLabelSize);
            float confirmWidth = Width(_confirmLabel, _confirmPadding, _confirmMinWidth);
            float cancelWidth = hasCancel ? Width(_cancelLabel, _cancelPadding, _cancelMinWidth) : 0f;
            float row = confirmWidth + (hasCancel ? _buttonGap + cancelWidth : 0f);
            float room = _panel.rect.width - 2f * _paddingSide;
            float scale = row > room ? room / row : 1f;
            _confirmLabel.enableAutoSizing = scale < 1f;
            _cancelLabel.enableAutoSizing = scale < 1f;
            confirmWidth *= scale;
            cancelWidth *= scale;
            row *= scale;

            float height = Mathf.Max(confirm.rect.height, hasCancel ? cancel.rect.height : 0f);
            if (hasCancel)
            {
                PlaceAt(cancel, -row / 2f + cancelWidth / 2f, y + (height - cancel.rect.height) / 2f, cancelWidth);
            }

            PlaceAt(confirm, row / 2f - confirmWidth / 2f, y + (height - confirm.rect.height) / 2f, confirmWidth);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, y + height + _paddingBottom);
        }

        private static float Width(TMP_Text label, float padding, float minWidth) =>
            Mathf.Max(minWidth, Mathf.Ceil(label.GetPreferredValues(label.text).x) + padding);

        /// <summary>The label at the size it was built with (noted the first time), not auto-sized.</summary>
        private static void RestoreSize(TMP_Text label, ref float size)
        {
            label.enableAutoSizing = false;
            if (size <= 0f)
            {
                size = label.fontSize;
            }

            label.fontSize = size;
        }

        private static float Place(RectTransform rect, float top, float height)
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.anchoredPosition = new Vector2(0f, -top);
            return top + height;
        }

        private static void PlaceAt(RectTransform rect, float centreX, float top, float width)
        {
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            rect.anchoredPosition = new Vector2(centreX, -top);
        }

        private void Animate(float target, bool deactivate)
        {
            UiMotion.Stop(ref _motion);
            UiMotion.Stop(ref _panelMotion);
            _panel.anchoredPosition = Vector2.zero;
            float from = Group.alpha;
            _motion = LMotion.Create(from, target, deactivate ? UiMotion.Quick : _fadeSeconds)
                .WithEase(deactivate ? Ease.InCubic : Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() =>
                {
                    if (deactivate)
                    {
                        Canvas.enabled = false;
                        _panel.localScale = Vector3.one;
                    }
                })
                .Bind(this, (alpha, view) => view.Group.alpha = alpha);
            _panelMotion = deactivate
                ? UiMotion.PopOut(_panel, null, ReduceMotion, null)
                : UiMotion.PopIn(_panel, null, ReduceMotion);
        }

        private void OnConfirm()
        {
            if (IsShowing)
            {
                Confirmed?.Invoke();
            }
        }

        private void OnCancel()
        {
            if (IsShowing)
            {
                Cancelled?.Invoke();
            }
        }

        private void OnDismiss()
        {
            if (IsShowing)
            {
                Dismissed?.Invoke();
            }
        }

        private void OnDestroy()
        {
            UiMotion.Stop(ref _motion);
            UiMotion.Stop(ref _panelMotion);
        }
    }
}
