using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// A slow, small rocking about the object's pivot: the main menu's card fan idles with it
    /// (GDD §11 screen 00). <see cref="Still"/> stops it and sets the object upright, which is what
    /// Reduce motion asks for (GDD §14). It runs on unscaled time and allocates nothing.
    /// </summary>
    public sealed class IdleSway : MonoBehaviour
    {
        private const float FullTurn = 2f * Mathf.PI;

        [SerializeField] private float _degrees = 1.2f;
        [SerializeField] private float _periodSeconds = 5f;

        private bool _still;
        private float _phase;

        public bool Still
        {
            get => _still;
            set
            {
                if (_still == value)
                {
                    return;
                }

                _still = value;
                if (value)
                {
                    Rest();
                }
            }
        }

        private void Update()
        {
            if (_still || _periodSeconds <= 0f)
            {
                return;
            }

            _phase = Mathf.Repeat(_phase + Time.unscaledDeltaTime / _periodSeconds, 1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_phase * FullTurn) * _degrees);
        }

        private void OnDisable() => Rest();

        private void Rest()
        {
            _phase = 0f;
            transform.localRotation = Quaternion.identity;
        }
    }
}
