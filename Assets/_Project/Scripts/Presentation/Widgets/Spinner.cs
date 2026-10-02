using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// Rotates its object continuously while enabled (the matchmaking arc, GDD §11 screen 05; the
    /// loading curtain's ring, mockup 17). It turns on scaled time unless <c>_unscaledTime</c> is set,
    /// which keeps it turning while the time scale is paused. <see cref="Still"/> stops it at its
    /// resting angle, a static indicator for Reduce motion (GDD §14), until it is cleared.
    /// </summary>
    public sealed class Spinner : MonoBehaviour
    {
        [SerializeField] private float _secondsPerTurn = 1.6f;
        [SerializeField] private bool _clockwise = true;
        [Tooltip("Turns on unscaled time, so a paused time scale does not stop it.")]
        [SerializeField] private bool _unscaledTime;

        private MotionHandle _motion;
        private bool _still;

        /// <summary>True while the object is turning.</summary>
        public bool IsTurning => _motion.IsActive();

        /// <summary>
        /// Reduce motion: true stops the turn and puts the object back at its resting angle; false
        /// lets it turn again while it is enabled.
        /// </summary>
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
                    Stop();
                }
                else if (isActiveAndEnabled)
                {
                    Turn();
                }
            }
        }

        /// <summary>Starts turning from the resting angle, unless it is turning already, stands still or is switched off.</summary>
        public void Turn()
        {
            if (_still)
            {
                Stop();
                return;
            }

            if (IsTurning || !enabled || !gameObject.activeInHierarchy)
            {
                return;
            }

            float direction = _clockwise ? -360f : 360f;
            var turn = LMotion.Create(0f, direction, _secondsPerTurn).WithLoops(-1, LoopType.Restart);
            if (_unscaledTime)
            {
                turn = turn.WithScheduler(MotionScheduler.UpdateIgnoreTimeScale);
            }

            _motion = turn.Bind(transform, (z, t) => t.localEulerAngles = new Vector3(0f, 0f, z));
        }

        /// <summary>Stops turning and puts the object back at its resting angle.</summary>
        public void Stop()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }

            transform.localEulerAngles = Vector3.zero;
        }

        private void OnEnable()
        {
            Turn();
        }

        private void OnDisable()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }
        }
    }
}
