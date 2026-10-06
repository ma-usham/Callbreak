using Callbreak.Presentation.Framework;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// The line under the profile's name field (GDD §11 screen 14): normally the note "Your name is
    /// visible at the table." in its mockup colour, or a validation message in the error colour
    /// (Heading Burgundy from the GDD §12 palette; the mockup shows no error state). A tap on the
    /// field while the saved name has to stay (ADR 041) nudges it: the line flashes the error
    /// colour and fades back, and shakes three times (only the colour with Reduce motion).
    /// </summary>
    public sealed class ProfileNameMessage : MonoBehaviour
    {
        private const float ShakeSeconds = 0.45f;
        private const int ShakeCount = 3;
        private const float ShakeDistance = 14f;
        private const float FlashSeconds = 1.1f;

        [SerializeField] private TMP_Text _label;
        [SerializeField] private Color _noteColor = new Color(0.565f, 0.427f, 0.318f);
        [SerializeField] private Color _errorColor = new Color(0.478f, 0.063f, 0.086f);

        private MotionHandle _shake;
        private MotionHandle _flash;
        private Color _shown;
        private Vector2 _rest;
        private bool _restKnown;

        /// <summary>A nudge is under way: the line shakes, or its colour is on its way back.</summary>
        public bool IsNudging => _shake.IsActive() || _flash.IsActive();

        public void Show(string text, bool isError)
        {
            UiMotion.Stop(ref _flash);
            if (_label.text != text)
            {
                _label.text = text;
            }

            _shown = isError ? _errorColor : _noteColor;
            _label.color = _shown;
        }

        /// <summary>
        /// Draws the eye to the line: it turns the error colour and fades back to its own over
        /// 1.1 s, and, unless <paramref name="reduceMotion"/>, shakes three times over 450 ms.
        /// </summary>
        public void Nudge(bool reduceMotion)
        {
            UiMotion.Stop(ref _flash);
            StopShake();
            if (!isActiveAndEnabled)
            {
                return;
            }

            _flash = LMotion.Create(0f, 1f, FlashSeconds)
                .WithEase(Ease.InQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(this, (t, self) => self._label.color = Color.Lerp(self._errorColor, self._shown, t));
            if (reduceMotion)
            {
                return;
            }

            if (!_restKnown)
            {
                _rest = _label.rectTransform.anchoredPosition;
                _restKnown = true;
            }

            _shake = LMotion.Create(0f, 1f, ShakeSeconds)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(ReturnToRest)
                .Bind(this, (t, self) => self.ShakeOffset(t));
        }

        private void Awake()
        {
            _shown = _label.color;
        }

        private void OnDisable()
        {
            UiMotion.Stop(ref _flash);
            StopShake();
            _label.color = _shown;
        }

        private void ShakeOffset(float t)
        {
            float offset = Mathf.Sin(t * ShakeCount * 2f * Mathf.PI) * (1f - t) * ShakeDistance;
            _label.rectTransform.anchoredPosition = _rest + new Vector2(offset, 0f);
        }

        private void StopShake()
        {
            UiMotion.Stop(ref _shake);
            ReturnToRest();
        }

        private void ReturnToRest()
        {
            if (_restKnown)
            {
                _label.rectTransform.anchoredPosition = _rest;
            }
        }
    }
}
