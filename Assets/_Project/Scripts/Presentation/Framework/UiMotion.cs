using System;
using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// The UI's shared motion (GDD §14), so panels, pills and buttons all move alike: things that
    /// open pop in (a little smaller and transparent, growing with a soft overshoot as they fade
    /// in), things that close shrink a touch as they fade out, and lists rise in one after another.
    /// With Reduce motion only the fades are left. Everything runs on unscaled time, so a paused
    /// match does not stop the UI.
    /// </summary>
    public static class UiMotion
    {
        /// <summary>Closing, small fades.</summary>
        public const float Quick = 0.14f;

        /// <summary>Fades and small pops.</summary>
        public const float Short = 0.22f;

        /// <summary>Panels and screens.</summary>
        public const float Medium = 0.3f;

        /// <summary>Between two items rising in one after another.</summary>
        public const float Stagger = 0.06f;

        /// <summary>
        /// Opens <paramref name="target"/>: from <paramref name="fromScale"/> to its size with a soft
        /// overshoot, while <paramref name="group"/> (if any) fades in. With Reduce motion it only fades.
        /// </summary>
        public static MotionHandle PopIn(Transform target, CanvasGroup group, bool reduceMotion, float fromScale = 0.92f, float seconds = Medium, float delay = 0f)
        {
            var state = new PopState(target, group, reduceMotion ? 1f : fromScale, 1f, 0f, 1f, Ease.OutBack, Ease.OutCubic);
            state.Apply(0f);
            return Run(state, seconds, delay, null);
        }

        /// <summary>
        /// Closes <paramref name="target"/>: it shrinks to <paramref name="toScale"/> as
        /// <paramref name="group"/> fades out; <paramref name="onComplete"/> runs at the end (hide it there).
        /// </summary>
        public static MotionHandle PopOut(Transform target, CanvasGroup group, bool reduceMotion, Action onComplete, float toScale = 0.96f, float seconds = Quick)
        {
            float from = target != null ? target.localScale.x : 1f;
            float alpha = group != null ? group.alpha : 1f;
            var state = new PopState(target, group, from, reduceMotion ? from : toScale, alpha, 0f, Ease.InCubic, Ease.InCubic);
            return Run(state, seconds, 0f, onComplete);
        }

        /// <summary>A short "look here": the target swells by <paramref name="amount"/> and settles back.</summary>
        public static MotionHandle Pulse(Transform target, bool reduceMotion, float amount = 0.05f, float seconds = Short)
        {
            if (reduceMotion || target == null)
            {
                return default;
            }

            return LMotion.Create(0f, 1f, seconds)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(() =>
                {
                    if (target != null)
                    {
                        target.localScale = Vector3.one;
                    }
                })
                .Bind(target, (t, tr) =>
                {
                    float s = 1f + (amount * Mathf.Sin(t * Mathf.PI));
                    tr.localScale = new Vector3(s, s, 1f);
                });
        }

        /// <summary>Fades <paramref name="group"/> to <paramref name="to"/>.</summary>
        public static MotionHandle Fade(CanvasGroup group, float to, float seconds = Short, Action onComplete = null, float delay = 0f)
        {
            float from = group.alpha;
            MotionBuilder<float, NoOptions, LitMotion.Adapters.FloatMotionAdapter> builder = LMotion.Create(from, to, seconds)
                .WithEase(to > from ? Ease.OutCubic : Ease.InCubic)
                .WithDelay(delay)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale);
            if (onComplete != null)
            {
                builder = builder.WithOnComplete(onComplete);
            }

            return builder.Bind(group, (alpha, g) => g.alpha = alpha);
        }

        /// <summary>
        /// The target rises <paramref name="distance"/> into its place from below and grows into
        /// its size, after <paramref name="delay"/> (one of a list rising in turn). With Reduce
        /// motion it stays in place. Its place is wherever it is when this is called.
        /// </summary>
        public static MotionHandle RiseIn(RectTransform target, bool reduceMotion, float distance = 36f, float delay = 0f, float seconds = Medium)
        {
            if (reduceMotion || target == null)
            {
                return default;
            }

            Vector2 home = target.anchoredPosition;
            var state = new RiseState(target, home, distance);
            state.Apply(0f);
            return LMotion.Create(0f, 1f, seconds)
                .WithDelay(delay)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnCancel(() => state.Apply(1f))
                .Bind(state, (t, s) => s.Apply(t));
        }

        /// <summary>The object's CanvasGroup, added the first time it is needed.</summary>
        public static CanvasGroup GroupOf(GameObject target)
        {
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.AddComponent<CanvasGroup>();
        }

        /// <summary>Cancels a motion that is still running and clears the handle.</summary>
        public static void Stop(ref MotionHandle handle)
        {
            if (handle.IsActive())
            {
                handle.Cancel();
            }

            handle = default;
        }

        /// <summary>Jumps a motion that is still running to its end (its completion runs) and clears the handle.</summary>
        public static void Finish(ref MotionHandle handle)
        {
            if (handle.IsActive())
            {
                handle.TryComplete();
            }

            handle = default;
        }

        private static MotionHandle Run(PopState state, float seconds, float delay, Action onComplete)
        {
            MotionBuilder<float, NoOptions, LitMotion.Adapters.FloatMotionAdapter> builder = LMotion.Create(0f, 1f, seconds)
                .WithDelay(delay)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale);
            if (onComplete != null)
            {
                builder = builder.WithOnComplete(onComplete);
            }

            return builder.Bind(state, (t, s) => s.Apply(t));
        }

        private sealed class PopState
        {
            private readonly Transform _target;
            private readonly CanvasGroup _group;
            private readonly float _fromScale;
            private readonly float _toScale;
            private readonly float _fromAlpha;
            private readonly float _toAlpha;
            private readonly Ease _scaleEase;
            private readonly Ease _alphaEase;

            public PopState(Transform target, CanvasGroup group, float fromScale, float toScale, float fromAlpha, float toAlpha, Ease scaleEase, Ease alphaEase)
            {
                _target = target;
                _group = group;
                _fromScale = fromScale;
                _toScale = toScale;
                _fromAlpha = fromAlpha;
                _toAlpha = toAlpha;
                _scaleEase = scaleEase;
                _alphaEase = alphaEase;
            }

            public void Apply(float t)
            {
                if (_target != null)
                {
                    float s = Mathf.LerpUnclamped(_fromScale, _toScale, EaseUtility.Evaluate(t, _scaleEase));
                    _target.localScale = new Vector3(s, s, 1f);
                }

                if (_group != null)
                {
                    // Fades finish a little before the scale, so the overshoot happens fully visible.
                    _group.alpha = Mathf.Lerp(_fromAlpha, _toAlpha, EaseUtility.Evaluate(Mathf.Clamp01(t * 1.6f), _alphaEase));
                }
            }
        }

        private sealed class RiseState
        {
            private readonly RectTransform _target;
            private readonly Vector2 _home;
            private readonly float _distance;

            public RiseState(RectTransform target, Vector2 home, float distance)
            {
                _target = target;
                _home = home;
                _distance = distance;
            }

            public void Apply(float t)
            {
                if (_target == null)
                {
                    return;
                }

                float rise = EaseUtility.Evaluate(t, Ease.OutCubic);
                float s = Mathf.LerpUnclamped(0.94f, 1f, EaseUtility.Evaluate(t, Ease.OutBack));
                _target.anchoredPosition = new Vector2(_home.x, _home.y - (_distance * (1f - rise)));
                _target.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
