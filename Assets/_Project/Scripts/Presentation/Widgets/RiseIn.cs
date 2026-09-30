using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// GDD §14 "Results: panel rises": <see cref="Play"/> puts the object a little below its place
    /// and lets it rise into it (400 ms, OutCubic, unscaled time). It moves only its own object,
    /// so it does not disturb a screen transition, which slides the screen's content root. At
    /// rest, disabled or outside Play mode the object is exactly where the prefab has it. With
    /// Reduce motion the screen simply does not call <see cref="Play"/>.
    /// </summary>
    public sealed class RiseIn : MonoBehaviour
    {
        [SerializeField] private float _distance = 30f;
        [SerializeField] private float _seconds = 0.4f;

        private RectTransform _rect;
        private Vector2 _home;
        private bool _hasHome;
        private MotionHandle _motion;

        public bool IsPlaying => _motion.IsActive();

        /// <summary>Starts the rise from the beginning; a rise already under way starts again.</summary>
        public void Play()
        {
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled || _seconds <= 0f)
            {
                return;
            }

            Cancel();
            if (!_hasHome)
            {
                _rect = (RectTransform)transform;
                _home = _rect.anchoredPosition;
                _hasHome = true;
            }

            Offset(_distance);
            _motion = LMotion.Create(_distance, 0f, _seconds)
                .WithEase(Ease.OutCubic)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(this, (below, self) => self.Offset(below));
        }

        /// <summary>Stops a rise under way and puts the object in its place.</summary>
        public void Rest()
        {
            Cancel();
            if (_hasHome)
            {
                Offset(0f);
            }
        }

        private void Offset(float below)
        {
            _rect.anchoredPosition = new Vector2(_home.x, _home.y - below);
        }

        private void Cancel()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }
        }

        private void OnDisable() => Rest();
    }
}
