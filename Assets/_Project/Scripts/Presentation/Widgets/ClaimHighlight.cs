using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// "Here, tap me" on a reward that is ready to collect (the owner, 2 Nov 2026; ADR 069): the main
    /// menu's Daily Rewards button and, on the Daily Rewards page, the day to collect. Its object is the
    /// glow: a warm gold halo, which breathes (it brightens and swells a little, then settles), and on the
    /// button a steady gold rim. At each breath's height the glyph beside it, the gift or the day's coins,
    /// gives a nudge: a little wiggle, a hop or a swell, as set. <see cref="On"/> shows it; off, the glow
    /// is hidden and the glyph rests where it was built. <see cref="Still"/> is Reduce motion (GDD §14):
    /// the glow stays lit at full strength and nothing moves. It runs on unscaled time, so a paused time
    /// scale doesn't stop it, and allocates nothing while it runs.
    /// </summary>
    public sealed class ClaimHighlight : MonoBehaviour
    {
        private const float FullTurn = 2f * Mathf.PI;

        [Tooltip("The halo that breathes: its opacity and its size.")]
        [SerializeField] private UnityEngine.UI.Graphic _halo;
        [Tooltip("What gives the nudge: the gift, the day's coins.")]
        [SerializeField] private RectTransform _glyph;

        [SerializeField] private float _breathSeconds = 2f;
        [Tooltip("The halo's opacity between breaths; at a breath's height, and still, it is 1.")]
        [SerializeField] private float _restingGlow = 0.35f;
        [Tooltip("How much bigger the halo grows at a breath's height.")]
        [SerializeField] private float _swell = 0.05f;

        [Tooltip("The part of a breath the nudge takes, centred on its height.")]
        [SerializeField] private float _nudgeShare = 0.36f;
        [Tooltip("The two swings' turn, degrees.")]
        [SerializeField] private float _nudgeDegrees = 10f;
        [Tooltip("The two hops' height, canvas units.")]
        [SerializeField] private float _nudgeLift;
        [Tooltip("How much bigger the glyph grows over the nudge.")]
        [SerializeField] private float _nudgeSwell = 0.08f;

        private MotionHandle _motion;
        private bool _on;
        private bool _still;

        // Where the glyph rests, read before it is first moved.
        private bool _homeKnown;
        private Vector2 _glyphHome;
        private Quaternion _glyphTurn;
        private Vector3 _glyphSize;

        /// <summary>The glow shows: the reward is ready.</summary>
        public bool On
        {
            get => _on;
            set
            {
                if (_on == value)
                {
                    return;
                }

                _on = value;
                if (!value)
                {
                    Stop();
                }

                if (gameObject.activeSelf != value)
                {
                    gameObject.SetActive(value);
                }

                if (value)
                {
                    Play();
                }
            }
        }

        /// <summary>Reduce motion: the glow stays lit at full strength and nothing moves.</summary>
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
                Play();
            }
        }

        /// <summary>The glow is breathing and the glyph nudging.</summary>
        public bool IsMoving => _motion.IsActive();

        private void OnEnable() => Play();

        private void OnDisable() => Stop();

        /// <summary>Starts breathing, or with <see cref="Still"/> lights the glow at full strength, while it shows.</summary>
        private void Play()
        {
            if (!_on || !isActiveAndEnabled)
            {
                return;
            }

            if (_still || _breathSeconds <= 0f)
            {
                Stop();
                SetGlow(1f, 1f);
                return;
            }

            if (_motion.IsActive())
            {
                return;
            }

            ReadHome();
            _motion = LMotion.Create(0f, 1f, _breathSeconds)
                .WithLoops(-1, LoopType.Restart)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .Bind(this, (phase, highlight) => highlight.Apply(phase));
        }

        /// <summary>Stops moving: the glow at full strength and its size, the glyph back where it rests.</summary>
        private void Stop()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }

            _motion = default;
            SetGlow(1f, 1f);
            if (_homeKnown && _glyph != null)
            {
                _glyph.anchoredPosition = _glyphHome;
                _glyph.localRotation = _glyphTurn;
                _glyph.localScale = _glyphSize;
            }
        }

        /// <summary>One moment of a breath, <paramref name="phase"/> 0 to 1.</summary>
        private void Apply(float phase)
        {
            // The halo: from resting up to full and back, softly.
            float breath = 0.5f - (0.5f * Mathf.Cos(phase * FullTurn));
            SetGlow(Mathf.Lerp(_restingGlow, 1f, breath), 1f + (_swell * breath));
            if (_glyph == null)
            {
                return;
            }

            // The nudge, about the breath's height: two quick swings that rise and fade, or two hops, as it swells.
            float start = 0.5f - (_nudgeShare / 2f);
            float t = _nudgeShare > 0f ? Mathf.Clamp01((phase - start) / _nudgeShare) : 0f;
            float envelope = Mathf.Sin(t * Mathf.PI);
            float swing = Mathf.Sin(t * 2f * FullTurn) * envelope;
            float hops = Mathf.Abs(Mathf.Sin(t * FullTurn));
            float size = 1f + (_nudgeSwell * envelope);
            _glyph.anchoredPosition = _glyphHome + new Vector2(0f, _nudgeLift * hops);
            _glyph.localRotation = _glyphTurn * Quaternion.Euler(0f, 0f, _nudgeDegrees * swing);
            _glyph.localScale = new Vector3(_glyphSize.x * size, _glyphSize.y * size, _glyphSize.z);
        }

        private void SetGlow(float alpha, float scale)
        {
            if (_halo == null)
            {
                return;
            }

            // The renderer's own alpha: the halo's mesh isn't rebuilt each frame.
            _halo.canvasRenderer.SetAlpha(alpha);
            _halo.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        private void ReadHome()
        {
            if (_homeKnown || _glyph == null)
            {
                return;
            }

            _homeKnown = true;
            _glyphHome = _glyph.anchoredPosition;
            _glyphTurn = _glyph.localRotation;
            _glyphSize = _glyph.localScale;
        }
    }
}
