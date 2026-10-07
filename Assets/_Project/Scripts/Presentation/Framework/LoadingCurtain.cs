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
    /// canvas is off and its ring stopped, so it costs nothing between loads. Its black look (ADR
    /// 042) covers the game while the store's purchase sheet is over it: black to the edges, with the
    /// ring turning round a gold spade in the middle of the screen, whichever way the sheet turned it.
    /// </summary>
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup))]
    public sealed class LoadingCurtain : MonoBehaviour, ILoadingCurtain
    {
        [SerializeField] private float _fadeSeconds = 0.25f;
        [SerializeField] private Spinner _ring;

        // The black look (ADR 042): black over the whole curtain, with its own ring round a gold spade.
        [SerializeField] private GameObject _dark;
        [SerializeField] private Spinner _darkRing;

        // The longest the black cover waits, once it may go, for the screen to be landscape again.
        [SerializeField] private float _turnBackSeconds = 1.5f;

        private Canvas _canvas;
        private CanvasGroup _group;
        private MotionHandle _motion;
        private ISettingsService _settings;
        private bool _darkLook;

        public bool IsCovering => Canvas.enabled;

        /// <summary>The curtain covers in its black look (<see cref="CoverDarkAsync"/>), not as the loading screen.</summary>
        public bool IsDark => _darkLook && IsCovering;

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
            SetDark(false);
            Canvas.enabled = true;
            Group.alpha = 1f;
            Group.blocksRaycasts = true;
            ApplyMotion();
        }

        public Task CoverAsync(CancellationToken ct)
        {
            SetDark(false);
            Canvas.enabled = true;
            Group.blocksRaycasts = true;
            ApplyMotion();
            return FadeAsync(1f, ct);
        }

        public Task CoverDarkAsync(CancellationToken ct)
        {
            if (IsCovering && !_darkLook)
            {
                return Task.CompletedTask;
            }

            SetDark(true);
            Canvas.enabled = true;
            Group.blocksRaycasts = true;
            ApplyMotion();
            return FadeAsync(1f, ct);
        }

        public async Task UncoverDarkAsync(CancellationToken ct)
        {
            if (!IsDark)
            {
                return;
            }

            // The store's sheet may have turned the game to portrait: the black stays until the
            // screen is the right way round again, so the menu never shows sideways.
            float giveUp = Time.unscaledTime + _turnBackSeconds;
            while (Screen.width < Screen.height && Time.unscaledTime < giveUp)
            {
                await Task.Yield();
                ct.ThrowIfCancellationRequested();
                if (!IsDark)
                {
                    return;
                }
            }

            await FadeAsync(0f, ct);
            if (!_darkLook)
            {
                return;
            }

            Group.blocksRaycasts = false;
            Canvas.enabled = false;
            SetDark(false);
            ApplyMotion();
        }

        public async Task UncoverAsync(CancellationToken ct)
        {
            await FadeAsync(0f, ct);
            Group.blocksRaycasts = false;
            Canvas.enabled = false;
            ApplyMotion();
        }

        /// <summary>The ring of the look that covers turns, unless Reduce motion asks for it to stand still.</summary>
        private void ApplyMotion()
        {
            bool still = _settings != null && _settings.ReduceMotion;
            Apply(_ring, still, IsCovering && !_darkLook);
            Apply(_darkRing, still, IsCovering && _darkLook);
        }

        private static void Apply(Spinner ring, bool still, bool showing)
        {
            if (ring == null)
            {
                return;
            }

            ring.Still = still;
            if (showing)
            {
                ring.Turn();
            }
            else
            {
                ring.Stop();
            }
        }

        /// <summary>The black look over the loading screen, or the loading screen itself.</summary>
        private void SetDark(bool dark)
        {
            _darkLook = dark;
            if (_dark != null && _dark.activeSelf != dark)
            {
                _dark.SetActive(dark);
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
