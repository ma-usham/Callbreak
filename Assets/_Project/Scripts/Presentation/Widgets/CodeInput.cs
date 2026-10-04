using System;
using Callbreak.Application.Rooms;
using LitMotion;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Six-box room-code entry (GDD §11 screen 03). One hidden <see cref="TMP_InputField"/> takes
    /// typing, backspace and paste; each box shows one character. Input is uppercased and anything
    /// outside the <see cref="RoomCode"/> alphabet is dropped. Typing always continues at the end,
    /// so the boxes fill left to right and backspace steps back. A box is drawn in one of three
    /// looks of the same size: the next box to fill has the focus look while the field has focus
    /// (GDD §12: focus = gold outline), all six have the error look while an error shows
    /// (<see cref="SetError"/>), and the normal look otherwise. An error also shakes the row
    /// (<see cref="Shake"/>, unless <see cref="ReduceMotion"/> is on). While the field has focus a
    /// caret blinks in the next box to fill (the owner, 3 Oct 2026), at the field's blink rate; the
    /// hidden field's own caret stays invisible.
    /// </summary>
    public sealed class CodeInput : MonoBehaviour
    {
        public const int Length = RoomCode.Length;

        private const float ShakeSeconds = 0.4f;
        private const int ShakeCount = 3;

        [SerializeField] private TMP_InputField _input;
        [SerializeField] private TMP_Text[] _letters;
        [SerializeField] private Image[] _boxes;
        [SerializeField] private Sprite _normalBox;
        [SerializeField] private Sprite _focusBox;
        [SerializeField] private Sprite _errorBox;
        [SerializeField] private RectTransform _shakeTarget;
        [SerializeField] private float _shakeDistance = 12f;

        // The caret drawn over the boxes; null in a JoinRoom prefab built before it existed.
        [SerializeField] private Graphic _caret;

        private MotionHandle _shake;
        private Vector2 _restPosition;
        private bool _restKnown;
        private bool _focused;
        private bool _error;
        private float _blinkFrom;

        /// <summary>Raised when the user types, deletes or pastes; carries the normalised code.</summary>
        public event Action<string> Changed;

        public string Code => _input.text;

        /// <summary>The hidden field has the keyboard (the on-screen keyboard is up on a phone).</summary>
        public bool IsFocused => _input.isFocused;

        /// <summary>
        /// The box the caret stands in, blinking or not: the next box to fill while the field has
        /// focus; -1 while there is none (no focus, all six filled, or no caret in this prefab).
        /// </summary>
        public int CaretBox => _caret != null && _focused && Code.Length < Length ? Code.Length : -1;

        /// <summary>The caret's bar, for a check of where it is drawn; null in an older prefab.</summary>
        public Graphic Caret => _caret;

        public bool Interactable
        {
            get => _input.interactable;
            set => _input.interactable = value;
        }

        /// <summary>
        /// Reduce motion (GDD §13): <see cref="Shake"/> leaves the boxes where they are, so an
        /// error shows by their error look and the message alone.
        /// </summary>
        public bool ReduceMotion { get; set; }

        /// <summary>Shows a code (normalised first) without raising <see cref="Changed"/>.</summary>
        public void SetCodeWithoutNotify(string code)
        {
            _input.SetTextWithoutNotify(RoomCode.Normalize(code));
            _input.stringPosition = _input.text.Length;
            _blinkFrom = Time.unscaledTime;
            Render();
            UpdateCaret();
        }

        /// <summary>Gives the field keyboard focus (opens the on-screen keyboard on phones).</summary>
        public void Focus() => _input.ActivateInputField();

        /// <summary>
        /// The boxes' error look on or off: all six at once. While it is on, no box shows the
        /// focus look.
        /// </summary>
        public void SetError(bool on)
        {
            if (_error != on)
            {
                _error = on;
                Render();
            }
        }

        /// <summary>
        /// Three horizontal shakes over 400 ms (GDD §11: the boxes shake on a join error). Does
        /// nothing while <see cref="ReduceMotion"/> is on.
        /// </summary>
        public void Shake()
        {
            StopShake();
            if (ReduceMotion || _shakeTarget == null || !isActiveAndEnabled)
            {
                return;
            }

            RememberRest();
            _shake = LMotion.Create(0f, 1f, ShakeSeconds)
                .WithOnComplete(ReturnToRest)
                .Bind(this, (t, self) => self.ShakeOffset(t));
        }

        private void OnEnable()
        {
            _input.characterLimit = Length;
            _input.onValidateInput += Validate;
            _input.onValueChanged.AddListener(OnValueChanged);
            Render();
        }

        private void OnDisable()
        {
            _input.onValidateInput -= Validate;
            _input.onValueChanged.RemoveListener(OnValueChanged);
            StopShake();

            // Switched off, the field loses the keyboard; LateUpdate looks again once it is back,
            // so the boxes never come back with a focus they no longer have.
            _focused = false;
            if (_caret != null)
            {
                _caret.enabled = false;
            }
        }

        private void LateUpdate()
        {
            bool focused = _input.isFocused;
            int end = _input.text.Length;
            if (focused && (_input.selectionStringAnchorPosition != end || _input.selectionStringFocusPosition != end))
            {
                // A tap or an arrow key moved the hidden field's caret: typing continues at the end.
                _input.stringPosition = end;
            }

            if (focused != _focused)
            {
                _focused = focused;
                _blinkFrom = Time.unscaledTime;
                Render();
            }

            UpdateCaret();
        }

        private static char Validate(string text, int charIndex, char addedChar)
        {
            char upper = char.ToUpperInvariant(addedChar);
            return RoomCode.Alphabet.IndexOf(upper) >= 0 ? upper : '\0';
        }

        private void OnValueChanged(string value)
        {
            // Typing shows the caret at once, in its new box, as a text field does.
            _blinkFrom = Time.unscaledTime;
            Render();
            UpdateCaret();
            Changed?.Invoke(value);
        }

        /// <summary>
        /// The caret in the next box to fill, centred on it, on for the first half of each blink;
        /// hidden while <see cref="CaretBox"/> is -1.
        /// </summary>
        private void UpdateCaret()
        {
            if (_caret == null)
            {
                return;
            }

            int box = CaretBox;
            bool on = box >= 0 && box < _boxes.Length && BlinkOn();
            if (on)
            {
                // Placed in its parent's space: the bar and the boxes move as one (the row's shake,
                // a screen sliding in), so it only changes when the caret goes to another box and
                // the canvas is not rebuilt every frame.
                RectTransform target = _boxes[box].rectTransform;
                RectTransform bar = _caret.rectTransform;
                Vector3 centre = bar.parent.InverseTransformPoint(target.TransformPoint(target.rect.center));
                Vector3 position = new Vector3(centre.x - bar.rect.center.x, centre.y - bar.rect.center.y, bar.localPosition.z);
                if (bar.localPosition != position)
                {
                    bar.localPosition = position;
                }
            }

            if (_caret.enabled != on)
            {
                _caret.enabled = on;
            }
        }

        /// <summary>The field's own blink: <see cref="TMP_InputField.caretBlinkRate"/> blinks a second, on for half of each.</summary>
        private bool BlinkOn()
        {
            float rate = _input.caretBlinkRate;
            if (rate <= 0f)
            {
                return true;
            }

            float period = 1f / rate;
            return (Time.unscaledTime - _blinkFrom) % period < period * 0.5f;
        }

        private void Render()
        {
            string code = _input.text ?? string.Empty;
            for (int i = 0; i < _letters.Length; i++)
            {
                string letter = i < code.Length ? code[i].ToString() : string.Empty;
                if (_letters[i].text != letter)
                {
                    _letters[i].text = letter;
                }
            }

            int active = Mathf.Min(code.Length, Length - 1);
            for (int i = 0; i < _boxes.Length; i++)
            {
                Sprite look = _error ? _errorBox : _focused && i == active ? _focusBox : _normalBox;
                if (_boxes[i].sprite != look)
                {
                    _boxes[i].sprite = look;
                }
            }
        }

        private void RememberRest()
        {
            if (!_restKnown)
            {
                _restPosition = _shakeTarget.anchoredPosition;
                _restKnown = true;
            }
        }

        private void ShakeOffset(float t)
        {
            float offset = Mathf.Sin(t * ShakeCount * 2f * Mathf.PI) * (1f - t) * _shakeDistance;
            _shakeTarget.anchoredPosition = _restPosition + new Vector2(offset, 0f);
        }

        private void StopShake()
        {
            if (_shake.IsActive())
            {
                _shake.Cancel();
            }

            ReturnToRest();
        }

        private void ReturnToRest()
        {
            if (_restKnown)
            {
                _shakeTarget.anchoredPosition = _restPosition;
            }
        }
    }
}
