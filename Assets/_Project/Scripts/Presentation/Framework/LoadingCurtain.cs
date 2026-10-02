using System.Threading;
using System.Threading.Tasks;
using Callbreak.Application.Ports;
using Callbreak.Application.Settings;
using Callbreak.Presentation.Widgets;
using LitMotion;
using UnityEngine;
using VContainer;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The loading curtain (GDD §23 curtain layer, mockup 17): over everything while a scene loads,
    /// the shared menu background with the logo, the Play Online emblem (a ring turning around the
    /// still spade), "Loading..." and the tagline. It starts covered, so the first frame of the app
    /// already shows it, fades on unscaled time and opens as soon as the next scene is ready, with no
    /// minimum time. The ring turns on unscaled time too, and stands still with Reduce motion (a
    /// static indicator, GDD §14). The curtain learns that setting when the app scope injects it,
    /// which can be after it first covers, and follows it while the game runs. While it is open its
    /// canvas is off and its ring stopped, so it costs nothing between loads.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public sealed class LoadingCurtain : MonoBehaviour, ILoadingCurtain
    {
        [SerializeField] private float _fadeSeconds = 0.25f;
        [SerializeField] private Spinner _ring;

        private Canvas _canvas;
        private CanvasGroup _group;
        private MotionHandle _motion;
        private ISettingsService _settings;

        public bool IsCovering => Canvas.enabled;

        private Canvas Canvas => _canvas != null ? _canvas : _canvas = GetComponent<Canvas>();

        private CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();

        /// <summary>
        /// The player's settings, for Reduce motion. Injected by the app scope (more than once does no
        /// harm); until then the ring turns.
        /// </summary>
        [Inject]
        public void Construct(ISettingsService settings)
        {
            if (_settings != null)
            {
                _settings.Changed -= ApplyMotion;
            }

            _settings = settings;
            if (_settings != null)
            {
                _settings.Changed += ApplyMotion;
            }

            ApplyMotion();
        }

        private void Awake()
        {
            Canvas.enabled = true;
            Group.alpha = 1f;
            Group.blocksRaycasts = true;
            ApplyMotion();
        }

        public Task CoverAsync(CancellationToken ct)
        {
            Canvas.enabled = true;
            Group.blocksRaycasts = true;
            ApplyMotion();
            return FadeAsync(1f, ct);
        }

        public async Task UncoverAsync(CancellationToken ct)
        {
            await FadeAsync(0f, ct);
            Group.blocksRaycasts = false;
            Canvas.enabled = false;
            ApplyMotion();
        }

        /// <summary>The ring turns while the curtain covers, unless Reduce motion asks for it to stand still.</summary>
        private void ApplyMotion()
        {
            if (_ring == null)
            {
                return;
            }

            _ring.Still = _settings != null && _settings.ReduceMotion;
            if (IsCovering)
            {
                _ring.Turn();
            }
            else
            {
                _ring.Stop();
            }
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
            if (_settings != null)
            {
                _settings.Changed -= ApplyMotion;
            }

            if (_motion.IsActive())
            {
                _motion.Cancel();
            }
        }
    }
}
