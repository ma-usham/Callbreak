using System;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The name question when the game starts (ADR 022): a dimmed backdrop and the dialog's panel
    /// with a title, the name field, a line under it (the note, or why a name cannot be used) and
    /// Continue. It cannot be dismissed: the backdrop only keeps taps from reaching the menu. It
    /// pops in and fades out like the dialog (<see cref="UiMotion"/>), on its own canvas above it;
    /// its labels come from the UI table, and <see cref="NamePromptPresenter"/> decides when it
    /// shows and what the line says.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public sealed class NamePromptView : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_InputField _field;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Color _noteColor = new Color(0.478f, 0.349f, 0.247f, 1f);
        [SerializeField] private Color _errorColor = new Color(0.478f, 0.063f, 0.086f, 1f);
        [SerializeField] private float _fadeSeconds = 0.18f;

        // The panel stacks its parts from the top and grows with its text, like the dialog.
        [SerializeField] private float _paddingTop = 58f;
        [SerializeField] private float _paddingBottom = 60f;
        [SerializeField] private float _titleGap = 30f;
        [SerializeField] private float _messageGap = 12f;
        [SerializeField] private float _buttonGap = 30f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private MotionHandle _motion;
        private MotionHandle _panelMotion;

        /// <summary>The text in the field changed (typed, pasted or cleared).</summary>
        public event Action<string> NameChanged;

        /// <summary>Continue, or Done / Enter on the keyboard.</summary>
        public event Action Submitted;

        public bool IsShowing { get; private set; }

        /// <summary>With Reduce motion the panel only fades (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        public string Text => _field.text;

        private Canvas Canvas => _canvas != null ? _canvas : _canvas = GetComponent<Canvas>();

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        private void Awake()
        {
            Canvas.enabled = false;
            Group.alpha = 0f;
        }

        private void OnEnable()
        {
            _field.onValueChanged.AddListener(OnValueChanged);
            _field.onSubmit.AddListener(OnSubmit);
            _continueButton.onClick.AddListener(OnContinue);
        }

        private void OnDisable()
        {
            _field.onValueChanged.RemoveListener(OnValueChanged);
            _field.onSubmit.RemoveListener(OnSubmit);
            _continueButton.onClick.RemoveListener(OnContinue);
        }

        /// <summary>Opens the panel with <paramref name="name"/> in the field and the field focused (the keyboard comes up on a phone).</summary>
        public void Show(string name)
        {
            _field.SetTextWithoutNotify(name ?? string.Empty);
            Canvas.enabled = true;
            Layout();
            IsShowing = true;
            Group.interactable = true;
            Group.blocksRaycasts = true;
            Animate(1f, deactivate: false);
            _field.ActivateInputField();
        }

        public void Hide()
        {
            if (!IsShowing)
            {
                return;
            }

            IsShowing = false;
            _field.DeactivateInputField();
            Group.interactable = false;
            Group.blocksRaycasts = false;
            Animate(0f, deactivate: true);
        }

        /// <summary>The line under the field: a note, or (in the error colour) why the name cannot be used.</summary>
        public void SetMessage(string text, bool isError)
        {
            text ??= string.Empty;
            _message.color = isError ? _errorColor : _noteColor;
            if (_message.text != text)
            {
                _message.text = text;
                Layout();
            }
        }

        /// <summary>Title, field, line and Continue from the top of the panel; the panel fits them.</summary>
        private void Layout()
        {
            float y = _paddingTop;
            y = Place(_title.rectTransform, y, _title.GetPreferredValues(_title.text, _title.rectTransform.rect.width, 0f).y);
            y += _titleGap;
            var field = (RectTransform)_field.transform;
            y = Place(field, y, field.rect.height);
            y += _messageGap;
            y = Place(_message.rectTransform, y, _message.GetPreferredValues(_message.text, _message.rectTransform.rect.width, 0f).y);
            y += _buttonGap;
            var button = (RectTransform)_continueButton.transform;
            y = Place(button, y, button.rect.height);
            _panel.sizeDelta = new Vector2(_panel.sizeDelta.x, y + _paddingBottom);
        }

        private static float Place(RectTransform rect, float top, float height)
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
            return top + height;
        }

        public void SetContinueInteractable(bool interactable)
        {
            _continueButton.interactable = interactable;
        }

        private void Animate(float target, bool deactivate)
        {
            UiMotion.Stop(ref _motion);
            UiMotion.Stop(ref _panelMotion);
            _panel.anchoredPosition = Vector2.zero;
            float from = Group.alpha;
            _motion = LMotion.Create(from, target, deactivate ? UiMotion.Short : _fadeSeconds)
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

        private void OnValueChanged(string text)
        {
            if (IsShowing)
            {
                NameChanged?.Invoke(text);
            }
        }

        private void OnSubmit(string text)
        {
            if (IsShowing)
            {
                Submitted?.Invoke();
            }
        }

        private void OnContinue()
        {
            if (IsShowing)
            {
                Submitted?.Invoke();
            }
        }

        private void OnDestroy()
        {
            UiMotion.Stop(ref _motion);
            UiMotion.Stop(ref _panelMotion);
        }
    }
}
