using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>Rotates its object continuously while enabled (the matchmaking arc, GDD §11 screen 05).</summary>
    public sealed class Spinner : MonoBehaviour
    {
        [SerializeField] private float _secondsPerTurn = 1.6f;
        [SerializeField] private bool _clockwise = true;

        private MotionHandle _motion;

        private void OnEnable()
        {
            float direction = _clockwise ? -360f : 360f;
            _motion = LMotion.Create(0f, direction, _secondsPerTurn)
                .WithLoops(-1, LoopType.Restart)
                .Bind(transform, (z, t) => t.localEulerAngles = new Vector3(0f, 0f, z));
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
