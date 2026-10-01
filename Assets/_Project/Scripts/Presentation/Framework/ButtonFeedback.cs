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
    /// Lives on the button root, so the face, label and shadow scale together.
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

            _pressed = true;
            Stop();
            float from = transform.localScale.x;
            _motion = LMotion.Create(from, PressedScale, PressSeconds)
                .WithEase(Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(transform, (v, t) => t.localScale = new Vector3(v, v, 1f));
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Release()
        {
            if (!_pressed)
            {
                return;
            }

            _pressed = false;
            Stop();
            float from = transform.localScale.x;
            _motion = LMotion.Create(0f, 1f, ReleaseSeconds)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(transform, (t, target) =>
                {
                    float s = Mathf.Lerp(from, 1f, EaseUtility.Evaluate(t, Ease.OutCubic)) + (ReleaseSwell * Mathf.Sin(t * Mathf.PI));
                    target.localScale = new Vector3(s, s, 1f);
                });
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
            Stop();
            _pressed = false;
            _armed = false;
            transform.localScale = Vector3.one;
        }
    }
}
