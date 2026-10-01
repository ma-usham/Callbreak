using System;
using System.Collections.Generic;
using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Framework
{
    /// <summary>
    /// Navigation between the screens of one scene (GDD §23): a stack with Show, Push, Replace and
    /// Pop, the screen-change transition and Android back, which goes to the top screen first.
    /// Plain C#; the scene's lifetime scope creates it with the scene's screens. The transition
    /// (280 ms, OutQuart): the arriving screen fades in, slides 36 px in the direction of travel
    /// and grows from 98.5% to its size; the leaving one slides on and fades out in the first part
    /// of that time, so the two never blur together. A screen that does not slide
    /// (<see cref="UIScreen.Slides"/>) only fades. With Reduce motion screens only fade.
    /// </summary>
    public sealed class ScreenRouter : IDisposable
    {
        private const float Duration = 0.28f;
        private const float Slide = 36f;
        private const float ArrivingScale = 0.985f;

        // The leaving screen is gone by this share of the transition.
        private const float LeavingFade = 0.6f;

        private readonly Dictionary<ScreenId, UIScreen> _screens = new Dictionary<ScreenId, UIScreen>();
        private readonly List<ScreenId> _stack = new List<ScreenId>();
        private readonly Dictionary<UIScreen, MotionHandle> _motions = new Dictionary<UIScreen, MotionHandle>();

        public ScreenRouter(IReadOnlyList<UIScreen> screens)
        {
            foreach (UIScreen screen in screens)
            {
                if (screen == null || screen.Id == ScreenId.None)
                {
                    continue;
                }

                _screens[screen.Id] = screen;
                screen.gameObject.SetActive(false);
            }
        }

        /// <summary>Raised after the top screen changes, with the new top screen.</summary>
        public event Action<ScreenId> Changed;

        public ScreenId Current => _stack.Count > 0 ? _stack[_stack.Count - 1] : ScreenId.None;

        public int Depth => _stack.Count;

        /// <summary>With Reduce motion on, screens only fade (GDD §14).</summary>
        public bool ReduceMotion { get; set; }

        public bool Has(ScreenId id) => _screens.ContainsKey(id);

        public T Get<T>(ScreenId id) where T : UIScreen => _screens.TryGetValue(id, out UIScreen s) ? s as T : null;

        /// <summary>Makes <paramref name="id"/> the only screen on the stack.</summary>
        public void Show(ScreenId id)
        {
            UIScreen from = Top();
            _stack.Clear();
            _stack.Add(id);
            Transition(from, _screens[id], 1, instant: from == null);
        }

        /// <summary>
        /// Replaces the whole stack at once, showing only its top without a transition: how the Menu
        /// scene opens straight onto a screen deeper than the main menu (behind the curtain).
        /// </summary>
        public void ShowStack(IReadOnlyList<ScreenId> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                throw new ArgumentException("The stack needs at least one screen.", nameof(ids));
            }

            UIScreen from = Top();
            _stack.Clear();
            foreach (ScreenId id in ids)
            {
                _stack.Add(id);
            }

            Transition(from, _screens[ids[ids.Count - 1]], 1, instant: true);
        }

        public void Push(ScreenId id)
        {
            if (Current == id)
            {
                return;
            }

            UIScreen from = Top();
            _stack.Add(id);
            Transition(from, _screens[id], 1, instant: false);
        }

        public void Replace(ScreenId id)
        {
            UIScreen from = Top();
            if (_stack.Count > 0)
            {
                _stack.RemoveAt(_stack.Count - 1);
            }

            _stack.Add(id);
            Transition(from, _screens[id], 1, instant: false);
        }

        /// <summary>Returns to the previous screen; false when already at the root.</summary>
        public bool Pop()
        {
            if (_stack.Count <= 1)
            {
                return false;
            }

            UIScreen from = Top();
            _stack.RemoveAt(_stack.Count - 1);
            Transition(from, Top(), -1, instant: false);
            return true;
        }

        /// <summary>Android back / Escape: the top screen may handle it, otherwise the stack pops.</summary>
        public bool Back()
        {
            UIScreen top = Top();
            if (top != null && top.HandleBack())
            {
                return true;
            }

            return Pop();
        }

        private UIScreen Top() => _stack.Count > 0 && _screens.TryGetValue(_stack[_stack.Count - 1], out UIScreen s) ? s : null;

        private void Transition(UIScreen from, UIScreen to, int direction, bool instant)
        {
            if (from == to)
            {
                return;
            }

            if (from != null)
            {
                from.CanvasGroup.interactable = false;
                from.CanvasGroup.blocksRaycasts = false;
                from.OnHidden();
                Animate(from, 1f, 0f, 0f, -Slide * direction, instant, deactivate: true);
            }

            to.gameObject.SetActive(true);
            to.transform.SetAsLastSibling();
            to.CanvasGroup.interactable = true;
            to.CanvasGroup.blocksRaycasts = true;
            Animate(to, 0f, 1f, Slide * direction, 0f, instant, deactivate: false);
            to.OnShown();
            Changed?.Invoke(to.Id);
        }

        private void Animate(UIScreen screen, float fromAlpha, float toAlpha, float fromX, float toX, bool instant, bool deactivate)
        {
            if (_motions.TryGetValue(screen, out MotionHandle running) && running.IsActive())
            {
                running.Cancel();
            }

            bool arriving = !deactivate;
            bool slides = screen.Slides && !ReduceMotion;
            float fromScale = arriving && slides ? ArrivingScale : 1f;
            if (!slides)
            {
                fromX = 0f;
                toX = 0f;
            }

            if (instant)
            {
                Apply(screen, toAlpha, toX, 1f);
                if (deactivate)
                {
                    screen.gameObject.SetActive(false);
                }

                return;
            }

            Apply(screen, fromAlpha, fromX, fromScale);
            var state = new AnimState(screen, fromAlpha, toAlpha, fromX, toX, fromScale, arriving);

            // Linear time; Step gives each part its own curve.
            MotionBuilder<float, LitMotion.NoOptions, LitMotion.Adapters.FloatMotionAdapter> builder =
                LMotion.Create(0f, 1f, Duration);
            if (deactivate)
            {
                builder = builder.WithOnComplete(() => screen.gameObject.SetActive(false));
            }

            _motions[screen] = builder.Bind(state, (t, s) => Step(s, t));
        }

        private static void Step(AnimState s, float t)
        {
            float move = EaseUtility.Evaluate(t, Ease.OutQuart);
            float fade = s.Arriving ? EaseUtility.Evaluate(t, Ease.OutCubic) : EaseUtility.Evaluate(Mathf.Clamp01(t / LeavingFade), Ease.OutQuad);
            Apply(
                s.Screen,
                Mathf.Lerp(s.FromAlpha, s.ToAlpha, fade),
                Mathf.Lerp(s.FromX, s.ToX, move),
                Mathf.LerpUnclamped(s.FromScale, 1f, move));
        }

        private static void Apply(UIScreen screen, float alpha, float x, float scale)
        {
            screen.CanvasGroup.alpha = alpha;
            screen.SlideTarget.anchoredPosition = new Vector2(x, 0f);
            screen.SlideTarget.localScale = new Vector3(scale, scale, 1f);
        }

        public void Dispose()
        {
            foreach (MotionHandle handle in _motions.Values)
            {
                if (handle.IsActive())
                {
                    handle.Cancel();
                }
            }

            _motions.Clear();
        }

        private sealed class AnimState
        {
            public readonly UIScreen Screen;
            public readonly float FromAlpha;
            public readonly float ToAlpha;
            public readonly float FromX;
            public readonly float ToX;
            public readonly float FromScale;
            public readonly bool Arriving;

            public AnimState(UIScreen screen, float fromAlpha, float toAlpha, float fromX, float toX, float fromScale, bool arriving)
            {
                Screen = screen;
                FromAlpha = fromAlpha;
                ToAlpha = toAlpha;
                FromX = fromX;
                ToX = toX;
                FromScale = fromScale;
                Arriving = arriving;
            }
        }
    }
}
