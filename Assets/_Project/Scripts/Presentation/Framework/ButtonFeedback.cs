using LitMotion;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// GDD §12/§14 button press: scale to 0.97 in 90 ms (OutQuad) and back. The 8% darkening is the
    /// Button's colour tint. Lives on the button root, so the face, label and shadow scale together.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressedScale = 0.97f;
        private const float Duration = 0.09f;

        [SerializeField] private UnityEngine.UI.Selectable _selectable;

        private MotionHandle _motion;
        private bool _pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable())
            {
                return;
            }

            _pressed = true;
            ScaleTo(PressedScale);
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
            ScaleTo(1f);
        }

        private void ScaleTo(float target)
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }

            float from = transform.localScale.x;
            _motion = LMotion.Create(from, target, Duration)
                .WithEase(Ease.OutQuad)
                .Bind(transform, (v, t) => t.localScale = new Vector3(v, v, 1f));
        }

        private void OnDisable()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }

            _pressed = false;
            transform.localScale = Vector3.one;
        }
    }
}
