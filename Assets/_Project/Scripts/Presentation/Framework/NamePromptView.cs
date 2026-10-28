using System;
using Callbreak.Presentation.Widgets;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The name screen when the game starts (ADR 022), as the owner's mockup 22 draws it (ADR 043):
    /// the Profile screen's look over the menu background, with the name field at the top of the
    /// panel (the owner: the keyboard must not hide it), the line under it (the note, or why a name
    /// cannot be used), the player's portrait with the name under it, the six portraits to choose
    /// from, and Save Changes. It cannot be dismissed. The keyboard comes only when the player taps
    /// the field, and its Done (or Enter) only puts it away: Save Changes answers the question. The
    /// screen fades in and out on a canvas of its own above the menu and its dialogs;
    /// <see cref="NamePromptPresenter"/> decides when it shows and what it says. Should a keyboard
    /// ever reach the field, the content rises so the field stays just above it
    /// (<see cref="KeyboardLift"/>), and settles back when it goes.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public sealed class NamePromptView : MonoBehaviour
    {
        // How quickly the content follows the keyboard: about the time it takes to close most of the way.
        private const float LiftSmoothing = 0.08f;

        // A Save that cannot be used yet shows its label dimmed, as on the Profile screen.
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private RectTransform _content;
        [SerializeField] private Image _avatar;
        [SerializeField] private TMP_Text _playerName;
        [SerializeField] private AvatarPicker _avatars;
        [SerializeField] private TMP_InputField _field;
        [SerializeField] private TMP_Text _message;
        [SerializeField] private Button _saveButton;
        [SerializeField] private TMP_Text _saveLabel;
        [SerializeField] private Color _errorColor = new Color(0.478f, 0.063f, 0.086f, 1f);
        [SerializeField] private float _fadeSeconds = 0.18f;

        private readonly Vector3[] _corners = new Vector3[4];
        private Canvas _canvas;
        private CanvasGroup _group;
        private MotionHandle _motion;
        private Color _noteColor;
        private bool _noteColorKnown;
        private float _lift;
        private float _liftSpeed;

        /// <summary>The text in the field changed (typed, pasted or cleared).</summary>
        public event Action<string> NameChanged;

        /// <summary>Save Changes.</summary>
        public event Action Submitted;

        /// <summary>A portrait in the grid was tapped (ADR 041).</summary>
        public event Action<int> AvatarPicked;

        public bool IsShowing { get; private set; }

        /// <summary>With Reduce motion the content moves for the keyboard at once (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        /// <summary>
        /// The on-screen keyboard's height in screen pixels, 0 while it is down (the presenter
        /// gives the device's; without one the content never moves).
        /// </summary>
        public Func<float> KeyboardHeight { get; set; }

        /// <summary>How far the content is lifted to keep the field above the keyboard, in canvas units.</summary>
        public float Lift => _lift;

        public string Text => _field.text;

        /// <summary>The portraits to choose from.</summary>
        public AvatarPicker Avatars => _avatars;

        private Canvas Canvas => _canvas != null ? _canvas : _canvas = GetComponent<Canvas>();

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        private void Awake()
        {
            Canvas.enabled = false;
            Group.alpha = 0f;
            RememberNoteColor();
        }

        private void OnEnable()
        {
            _field.onValueChanged.AddListener(OnValueChanged);
            _field.onSubmit.AddListener(OnKeyboardDone);
            _saveButton.onClick.AddListener(OnSave);
            _avatars.Picked += OnAvatarPicked;
        }

        private void OnDisable()
        {
            _field.onValueChanged.RemoveListener(OnValueChanged);
            _field.onSubmit.RemoveListener(OnKeyboardDone);
            _saveButton.onClick.RemoveListener(OnSave);
            _avatars.Picked -= OnAvatarPicked;
        }

        /// <summary>Opens the screen with <paramref name="name"/> in the field. The keyboard waits for a tap on the field.</summary>
        public void Show(string name)
        {
            _field.SetTextWithoutNotify(name ?? string.Empty);
            Canvas.enabled = true;
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
            _field.DeactivateInputField();
            Group.interactable = false;
            Group.blocksRaycasts = false;
            Animate(0f, deactivate: true);
        }

        /// <summary>The portraits by preset index in the grid, and the one ringed as chosen.</summary>
        public void SetAvatars(Func<int, Sprite> pictureOf, int chosen)
        {
            _avatars.SetPictures(pictureOf);
            _avatars.Choose(chosen);
        }

        /// <summary>The player's portrait above the name; a null keeps the one it shows.</summary>
        public void SetAvatar(Sprite avatar)
        {
            if (avatar != null && _avatar.sprite != avatar)
            {
                _avatar.sprite = avatar;
            }
        }

        /// <summary>The name under the portrait.</summary>
        public void SetPlayerName(string playerName)
        {
            playerName ??= string.Empty;
            if (_playerName.text != playerName)
            {
                _playerName.text = playerName;
            }
        }

        /// <summary>The line under the field: a note, or (in the error colour) why the name cannot be used.</summary>
        public void SetMessage(string text, bool isError)
        {
            RememberNoteColor();
            text ??= string.Empty;
            _message.color = isError ? _errorColor : _noteColor;
            if (_message.text != text)
            {
                _message.text = text;
            }
        }

        /// <summary>Save Changes can be used once the name can; until then its label is dimmed.</summary>
        public void SetSaveInteractable(bool interactable)
        {
            _saveButton.interactable = interactable;
            Color color = _saveLabel.color;
            color.a = interactable ? 1f : DimmedAlpha;
            _saveLabel.color = color;
        }

        /// <summary>The note's colour is the one the line was built in.</summary>
        private void RememberNoteColor()
        {
            if (!_noteColorKnown)
            {
                _noteColor = _message.color;
                _noteColorKnown = true;
            }
        }

        /// <summary>The content follows the keyboard: up while it covers the field, back down once it has gone.</summary>
        private void Update()
        {
            float target = IsShowing ? TargetLift() : 0f;
            if (_lift == target && _liftSpeed == 0f)
            {
                return;
            }

            _lift = ReduceMotion
                ? target
                : Mathf.SmoothDamp(_lift, target, ref _liftSpeed, LiftSmoothing, float.PositiveInfinity, Time.unscaledDeltaTime);
            if (Mathf.Abs(_lift - target) < 0.5f)
            {
                _lift = target;
                _liftSpeed = 0f;
            }

            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, _lift);
        }

        /// <summary>The lift that keeps the field above the keyboard, measured with the content at rest.</summary>
        private float TargetLift()
        {
            float pixels = KeyboardHeight != null ? KeyboardHeight() : 0f;
            if (pixels <= 0f)
            {
                return 0f;
            }

            Canvas root = Canvas.rootCanvas;
            var screen = (RectTransform)root.transform;
            ((RectTransform)_field.transform).GetWorldCorners(_corners);
            float floor = screen.rect.yMin;
            float bottom = screen.InverseTransformPoint(_corners[0]).y - floor - _lift;
            float top = screen.InverseTransformPoint(_corners[1]).y - floor - _lift;
            return KeyboardLift.For(bottom, top, pixels / root.scaleFactor, screen.rect.height);
        }

        private void Animate(float target, bool deactivate)
        {
            UiMotion.Stop(ref _motion);
            float from = Group.alpha;
            _motion = LMotion.Create(from, target, deactivate ? UiMotion.Short : _fadeSeconds)
                .WithEase(deactivate ? Ease.InCubic : Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() =>
                {
                    if (deactivate)
                    {
                        Canvas.enabled = false;
                    }
                })
                .Bind(this, (alpha, view) => view.Group.alpha = alpha);
        }

        private void OnValueChanged(string text)
        {
            if (IsShowing)
            {
                NameChanged?.Invoke(text);
            }
        }

        /// <summary>The keyboard's Done (or Enter) puts the keyboard away; the player then taps Save Changes (ADR 043).</summary>
        private void OnKeyboardDone(string text)
        {
            if (IsShowing)
            {
                _field.DeactivateInputField();
            }
        }

        private void OnSave()
        {
            if (IsShowing)
            {
                Submitted?.Invoke();
            }
        }

        private void OnAvatarPicked(int index)
        {
            if (IsShowing)
            {
                AvatarPicked?.Invoke(index);
            }
        }

        private void OnDestroy()
        {
            UiMotion.Stop(ref _motion);
        }
    }
}
