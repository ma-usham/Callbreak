using Callbreak.Application.Ports;
using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;
using VContainer;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// GDD §12/§14 button press: scale to 0.97 in 90 ms (OutQuad); let go, it springs back past its
    /// size by 2% and settles (220 ms; ADR 023). The 8% darkening is the Button's colour tint.
    /// Lives on the button root, so the face, label and shadow scale together. All of it is relative
    /// to the size the button rests at, which its layout may set below 1 (the shop drawer's
    /// buttons, Settings' Privacy options): let go, the button settles back there, never at full
    /// size (the owner, 5 Oct 2026: a tapped drawer button stayed big).
    /// A tap also plays the button's sound cue once the audio service has been injected. It sits
    /// after the Button on its object, so a presenter that answers the press with a cue of its own
    /// is heard instead (the audio service plays one UI cue per frame, the first).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        private const float PressedScale = 0.97f;
        private const float PressSeconds = 0.09f;
        private const float ReleaseSeconds = 0.22f;
        private const float ReleaseSwell = 0.02f;

        [SerializeField] private UnityEngine.UI.Selectable _selectable;
        [SerializeField] private SoundCue _cue = SoundCue.UiTap;

        private IAudioService _audio;
        private MotionHandle _motion;
        private bool _pressed;
        private bool _armed;

        // The scale the button rests at, as its layout set it; read again when a press starts at rest.
        private Vector3 _rest = Vector3.one;

        [Inject]
        public void Construct(IAudioService audio)
        {
            _audio = audio;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Whether the press counts was settled when the pointer went down: by now the button's
            // own click may have closed its screen, and a closing screen takes no more input.
            if (_armed && _audio != null)
            {
                _audio.Play(_cue);
            }

            _armed = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _armed = _selectable == null || _selectable.IsInteractable();
            if (!_armed)
            {
                return;
            }

            if (!_pressed && !_motion.IsActive())
            {
                _rest = transform.localScale;
            }

            _pressed = true;
            Stop();
            float from = Factor();
            _motion = LMotion.Create(from, PressedScale, PressSeconds)
                .WithEase(Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(this, (factor, feedback) => feedback.Apply(factor));
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Awake()
        {
            _rest = transform.localScale;
        }

        private void Release()
        {
            if (!_pressed)
            {
                return;
            }

            _pressed = false;
            Stop();
            float from = Factor();
            _motion = LMotion.Create(0f, 1f, ReleaseSeconds)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(this, (t, feedback) =>
                {
                    float factor = Mathf.Lerp(from, 1f, EaseUtility.Evaluate(t, Ease.OutCubic)) + (ReleaseSwell * Mathf.Sin(t * Mathf.PI));
                    feedback.Apply(factor);
                });
        }

        /// <summary>The button's size now, as a share of its resting size.</summary>
        private float Factor() => Mathf.Approximately(_rest.x, 0f) ? 1f : transform.localScale.x / _rest.x;

        private void Apply(float factor)
        {
            transform.localScale = new Vector3(_rest.x * factor, _rest.y * factor, _rest.z);
        }

        private void Stop()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }
        }

        private void OnDisable()
        {
            bool moved = _pressed || _motion.IsActive();
            Stop();
            _pressed = false;
            _armed = false;
            if (moved)
            {
                // Taken away in the middle of a press: back to its own size.
                transform.localScale = _rest;
            }
        }
    }
}
