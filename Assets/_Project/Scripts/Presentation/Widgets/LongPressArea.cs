using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Raises <see cref="Held"/> once when a pointer stays down on it for the hold time (GDD §11
    /// screen 04: the host long-presses a seat to remove a player). Needs a raycast target on this
    /// object or a child. Releasing or sliding off before the time is up cancels it.
    /// </summary>
    public sealed class LongPressArea : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] private float _holdSeconds = 0.6f;

        private bool _pressed;
        private float _pressedAt;

        public event Action Held;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _pressedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData) => _pressed = false;

        public void OnPointerExit(PointerEventData eventData) => _pressed = false;

        private void Update()
        {
            if (_pressed && Time.unscaledTime - _pressedAt >= _holdSeconds)
            {
                _pressed = false;
                Held?.Invoke();
            }
        }

        private void OnDisable() => _pressed = false;
    }
}
