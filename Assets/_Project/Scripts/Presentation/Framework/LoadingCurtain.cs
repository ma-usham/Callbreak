using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The loading curtain (GDD §23 curtain layer): the scenic background and the logo over
    /// everything while a scene loads. It starts covered, so the first frame of the app already
    /// shows it, and it fades on unscaled time. Its canvas is switched off while it is open, so it
    /// costs nothing between loads.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public sealed class LoadingCurtain : MonoBehaviour, ILoadingCurtain
    {
        [SerializeField] private float _fadeSeconds = 0.25f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private MotionHandle _motion;

        public bool IsCovering => Canvas.enabled;

        private Canvas Canvas => _canvas != null ? _canvas : _canvas = GetComponent<Canvas>();

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        private void Awake()
        {
            Canvas.enabled = true;
            Group.alpha = 1f;
            Group.blocksRaycasts = true;
        }

        public Task CoverAsync(CancellationToken ct)
        {
            Canvas.enabled = true;
            Group.blocksRaycasts = true;
            return FadeAsync(1f, ct);
        }

        public async Task UncoverAsync(CancellationToken ct)
        {
            await FadeAsync(0f, ct);
            Group.blocksRaycasts = false;
            Canvas.enabled = false;
        }

        private async Task FadeAsync(float target, CancellationToken ct)
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }

            float from = Group.alpha;
            if (Mathf.Approximately(from, target) || _fadeSeconds <= 0f)
            {
                Group.alpha = target;
                return;
            }

            _motion = LMotion.Create(from, target, _fadeSeconds * Mathf.Abs(target - from))
                .WithEase(Ease.OutQuad)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(Group, (alpha, group) => group.alpha = alpha);
            await _motion.ToAwaitable(ct);
        }

        private void OnDestroy()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }
        }
    }
}
