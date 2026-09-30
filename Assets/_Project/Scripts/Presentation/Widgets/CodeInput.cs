using System;
using Callbreak.Application.Rooms;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Six-box room-code entry (GDD §11 screen 03). One hidden <see cref="TMP_InputField"/> takes
    /// typing, backspace and paste; each box shows one character. Input is uppercased and anything
    /// outside the <see cref="RoomCode"/> alphabet is dropped. Typing always continues at the end,
    /// so the boxes fill left to right and backspace steps back. The next box to fill shows a
    /// stronger rim while the field has focus. An error gives every box its error look
    /// (<see cref="SetError"/>) and shakes the row (<see cref="Shake"/>, unless
    /// <see cref="ReduceMotion"/> is on).
    /// </summary>
    public sealed class CodeInput : MonoBehaviour
    {
        public const int Length = RoomCode.Length;

        private const float ShakeSeconds = 0.4f;
        private const int ShakeCount = 3;

        [SerializeField] private TMP_InputField _input;
        [SerializeField] private TMP_Text[] _letters;
        [SerializeField] private GameObject[] _focusLooks;
        [SerializeField] private GameObject[] _errorLooks;
        [SerializeField] private RectTransform _shakeTarget;
        [SerializeField] private float _shakeDistance = 12f;

        private MotionHandle _shake;
        private Vector2 _restPosition;
        private bool _restKnown;
        private bool _focused;
        private bool _error;

        /// <summary>Raised when the user types, deletes or pastes; carries the normalised code.</summary>
        public event Action<string> Changed;

        public string Code => _input.text;

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
            Render();
        }

        /// <summary>Gives the field keyboard focus (opens the on-screen keyboard on phones).</summary>
        public void Focus() => _input.ActivateInputField();

        /// <summary>
        /// The boxes' error look on or off: a rim in the error colour on all six. While it is on
        /// it takes the focus rim's place.
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
        }

        private void LateUpdate()
        {
            bool focused = _input.isFocused;
            int end = _input.text.Length;
            if (focused && (_input.selectionStringAnchorPosition != end || _input.selectionStringFocusPosition != end))
            {
                // A tap or an arrow key moved the (invisible) caret: typing continues at the end.
                _input.stringPosition = end;
            }

            if (focused != _focused)
            {
                _focused = focused;
                Render();
            }
        }

        private static char Validate(string text, int charIndex, char addedChar)
        {
            char upper = char.ToUpperInvariant(addedChar);
            return RoomCode.Alphabet.IndexOf(upper) >= 0 ? upper : '\0';
        }

        private void OnValueChanged(string value)
        {
            Render();
            Changed?.Invoke(value);
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
            for (int i = 0; i < _focusLooks.Length; i++)
            {
                if (_focusLooks[i] != null)
                {
                    _focusLooks[i].SetActive(_focused && !_error && i == active);
                }
            }

            for (int i = 0; i < _errorLooks.Length; i++)
            {
                if (_errorLooks[i] != null)
                {
                    _errorLooks[i].SetActive(_error);
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
