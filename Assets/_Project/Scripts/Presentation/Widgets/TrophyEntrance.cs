using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace Callbreak.Presentation.Widgets
{
    /// <summary>
    /// GDD §14 "Victory": the trophy drops in from a little above and settles with a bounce
    /// (OutBounce); as it settles its sparkles light up one after another and twinkle once: a
    /// restrained warm sparkle, no confetti, all inside 1.5 s and on unscaled time.
    /// <see cref="Play"/> runs it. At rest, disabled or outside Play mode everything is exactly
    /// where the prefab has it, which is also what Reduce motion shows (the screen then simply
    /// does not call <see cref="Play"/>).
    /// </summary>
    public sealed class TrophyEntrance : MonoBehaviour
    {
        /// <summary>The GDD's limit for the whole entrance.</summary>
        public const float MaxSeconds = 1.5f;

        private const float DropDelay = 0.08f;
        private const float DropSeconds = 0.62f;
        private const float FadeSeconds = 0.12f;
        private const float SparkleStart = 0.62f;
        private const float SparkleStagger = 0.09f;
        private const float SparkleSeconds = 0.6f;

        // A sparkle's own time, 0-1: it pops in over the first part, then dims and brightens once.
        private const float SparkleAppear = 0.35f;
        private const float TwinkleShrink = 0.3f;
        private const float TwinkleDim = 0.55f;

        [SerializeField] private Graphic _trophy;
        [SerializeField] private Graphic[] _sparkles;
        [SerializeField] private float _dropHeight = 72f;

        private Vector2 _trophyHome;
        private float _trophyAlpha;
        private Vector3[] _sparkleScale;
        private float[] _sparkleAlpha;
        private float _stagger;
        private bool _hasHome;
        private MotionHandle _motion;

        public bool IsPlaying => _motion.IsActive();

        /// <summary>Starts the entrance from the beginning.</summary>
        public void Play()
        {
            if (!UnityEngine.Application.isPlaying || !isActiveAndEnabled || _trophy == null || _sparkles == null)
            {
                return;
            }

            Cancel();
            Remember();

            // However many sparkles there are, the last one is done inside the limit.
            int gaps = Mathf.Max(1, _sparkles.Length - 1);
            _stagger = Mathf.Min(SparkleStagger, (MaxSeconds - SparkleStart - SparkleSeconds) / gaps);
            float total = Mathf.Max(DropDelay + DropSeconds, SparkleStart + (_stagger * (_sparkles.Length - 1)) + SparkleSeconds);

            Pose(0f);
            _motion = LMotion.Create(0f, total, total)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(Finish)
                .Bind(this, (time, self) => self.Pose(time));
        }

        /// <summary>Stops an entrance under way and shows the trophy and its sparkles as the prefab has them.</summary>
        public void Rest()
        {
            Cancel();
            Settle();
        }

        private void Remember()
        {
            if (_hasHome)
            {
                return;
            }

            _trophyHome = _trophy.rectTransform.anchoredPosition;
            _trophyAlpha = _trophy.color.a;
            _sparkleScale = new Vector3[_sparkles.Length];
            _sparkleAlpha = new float[_sparkles.Length];
            for (int i = 0; i < _sparkles.Length; i++)
            {
                if (_sparkles[i] != null)
                {
                    _sparkleScale[i] = _sparkles[i].rectTransform.localScale;
                    _sparkleAlpha[i] = _sparkles[i].color.a;
                }
            }

            _hasHome = true;
        }

        /// <summary>Everything at <paramref name="time"/> seconds into the entrance.</summary>
        private void Pose(float time)
        {
            float drop = Mathf.Clamp01((time - DropDelay) / DropSeconds);
            float landed = EaseUtility.Evaluate(drop, Ease.OutBounce);
            _trophy.rectTransform.anchoredPosition = new Vector2(_trophyHome.x, _trophyHome.y + (_dropHeight * (1f - landed)));
            SetAlpha(_trophy, _trophyAlpha * Mathf.Clamp01((time - DropDelay) / FadeSeconds));

            for (int i = 0; i < _sparkles.Length; i++)
            {
                if (_sparkles[i] == null)
                {
                    continue;
                }

                float own = Mathf.Clamp01((time - SparkleStart - (i * _stagger)) / SparkleSeconds);
                float size;
                float alpha;
                if (own < SparkleAppear)
                {
                    float k = own / SparkleAppear;
                    size = own <= 0f ? 0f : EaseUtility.Evaluate(k, Ease.OutBack);
                    alpha = Mathf.Clamp01(k * 2f);
                }
                else
                {
                    float dip = Mathf.Sin(Mathf.PI * (own - SparkleAppear) / (1f - SparkleAppear));
                    size = 1f - (TwinkleShrink * dip);
                    alpha = 1f - (TwinkleDim * dip);
                }

                _sparkles[i].rectTransform.localScale = _sparkleScale[i] * size;
                SetAlpha(_sparkles[i], _sparkleAlpha[i] * alpha);
            }
        }

        private void Finish()
        {
            // This runs inside the motion's completion: forget its handle rather than cancel it.
            _motion = default;
            Settle();
        }

        private void Settle()
        {
            if (!_hasHome)
            {
                return;
            }

            _trophy.rectTransform.anchoredPosition = _trophyHome;
            SetAlpha(_trophy, _trophyAlpha);
            for (int i = 0; i < _sparkles.Length; i++)
            {
                if (_sparkles[i] != null)
                {
                    _sparkles[i].rectTransform.localScale = _sparkleScale[i];
                    SetAlpha(_sparkles[i], _sparkleAlpha[i]);
                }
            }
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
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
