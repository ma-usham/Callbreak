using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The one confirmation dialog (GDD §23 popup layer): a dimmed backdrop and a panel with a
    /// title, an optional line of body text, a primary button and an optional secondary one. The
    /// panel grows with its text. On unscaled time the dim fades in while the panel pops in
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
        [SerializeField] private float _fadeSeconds = 0.18f;
        [SerializeField] private float _paddingTop = 58f;
        [SerializeField] private float _paddingBottom = 60f;
        [SerializeField] private float _bodyGap = 12f;
        [SerializeField] private float _buttonsGap = 34f;
        [SerializeField] private float _buttonGap = 18f;

        private Canvas _canvas;
        private CanvasGroup _group;
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

        /// <summary>Stacks the title, body and buttons from the top of the panel and sizes it to fit.</summary>
        private void Layout(bool hasBody, bool hasCancel)
        {
            float y = _paddingTop;
            y = Place(_title.rectTransform, y, _title.GetPreferredValues(_title.text, _title.rectTransform.rect.width, 0f).y);
            if (hasBody)
            {
                y += _bodyGap;
                y = Place(_body.rectTransform, y, _body.GetPreferredValues(_body.text, _body.rectTransform.rect.width, 0f).y);
            }

            y += _buttonsGap;
            var confirm = (RectTransform)_confirmButton.transform;
            y = Place(confirm, y, confirm.rect.height);
            if (hasCancel)
            {
                y += _buttonGap;
                var cancel = (RectTransform)_cancelButton.transform;
                y = Place(cancel, y, cancel.rect.height);
            }

            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, y + _paddingBottom);
        }

        private static float Place(RectTransform rect, float top, float height)
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.anchoredPosition = new Vector2(0f, -top);
            return top + height;
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
