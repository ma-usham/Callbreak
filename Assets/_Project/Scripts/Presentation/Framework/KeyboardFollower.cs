using System;
using UnityEngine;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Keeps a text field above the on-screen keyboard (<see cref="KeyboardLift"/>) by lifting the
    /// content it sits in as one piece: up while the field has focus and the keyboard would cover
    /// it, back down once the keyboard goes or the field lets go. It follows the keyboard as the
    /// name panel does, or moves at once with Reduce motion (GDD §14). The view that owns it ticks
    /// it from LateUpdate, after a screen transition has placed the content for the frame (the
    /// router slides a screen's content sideways and keeps it at rest in height).
    /// </summary>
    public sealed class KeyboardFollower
    {
        // How quickly the content follows the keyboard: about the time it takes to close most of the way.
        private const float Smoothing = 0.08f;

        private readonly RectTransform _content;
        private readonly RectTransform _field;
        private readonly Func<bool> _focused;
        private readonly Vector3[] _corners = new Vector3[4];
        private Canvas _canvas;
        private float _lift;
        private float _speed;

        /// <param name="content">What rises: a screen's content, everything but its background.</param>
        /// <param name="field">The rect to keep above the keyboard.</param>
        /// <param name="focused">Whether the field has the keyboard now.</param>
        public KeyboardFollower(RectTransform content, RectTransform field, Func<bool> focused)
        {
            _content = content;
            _field = field;
            _focused = focused;
        }

        /// <summary>
        /// The on-screen keyboard's height in screen pixels, 0 while it is down (the presenter gives
        /// the device's; without one the content never moves).
        /// </summary>
        public Func<float> KeyboardHeight { get; set; }

        public bool ReduceMotion { get; set; }

        /// <summary>How far the content is lifted, in its own anchored units.</summary>
        public float Lift => _lift;

        /// <summary>Moves the content one frame towards where the keyboard wants it.</summary>
        public void Tick(float deltaTime)
        {
            float target = _focused() ? TargetLift() : 0f;
            if (_lift != target || _speed != 0f)
            {
                _lift = ReduceMotion
                    ? target
                    : Mathf.SmoothDamp(_lift, target, ref _speed, Smoothing, float.PositiveInfinity, deltaTime);
                if (Mathf.Abs(_lift - target) < 0.5f)
                {
                    _lift = target;
                    _speed = 0f;
                }
            }

            Apply();
        }

        /// <summary>Back to rest at once: the screen went away, and comes back with its content in place.</summary>
        public void Settle()
        {
            _lift = 0f;
            _speed = 0f;
            Apply();
        }

        private void Apply()
        {
            Vector2 position = _content.anchoredPosition;
            if (position.y != _lift)
            {
                _content.anchoredPosition = new Vector2(position.x, _lift);
            }
        }

        /// <summary>The lift that keeps the field above the keyboard, measured with the content at rest.</summary>
        private float TargetLift()
        {
            float pixels = KeyboardHeight != null ? KeyboardHeight() : 0f;
            if (pixels <= 0f || _content.parent == null)
            {
                return 0f;
            }

            if (_canvas == null)
            {
                _canvas = _content.GetComponentInParent<Canvas>();
                if (_canvas == null)
                {
                    return 0f;
                }
            }

            Canvas root = _canvas.rootCanvas;
            var screen = (RectTransform)root.transform;

            // Canvas units per anchored unit of the content (1 for a screen on its canvas), and how
            // far the content stands lifted right now (a screen transition may have put it at rest).
            float scale = screen.InverseTransformVector(_content.parent.TransformVector(Vector3.up)).y;
            if (scale <= 0f)
            {
                return 0f;
            }

            float lifted = _content.anchoredPosition.y * scale;
            _field.GetWorldCorners(_corners);
            float floor = screen.rect.yMin;
            float bottom = screen.InverseTransformPoint(_corners[0]).y - floor - lifted;
            float top = screen.InverseTransformPoint(_corners[1]).y - floor - lifted;
            return KeyboardLift.For(bottom, top, pixels / root.scaleFactor, screen.rect.height) / scale;
        }
    }
}
