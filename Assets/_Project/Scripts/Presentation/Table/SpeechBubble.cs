using Callbreak.Presentation.Framework;
using LitMotion;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// A speech bubble at the table: a call as it is placed, or a quick phrase. The pill hugs its
    /// text, pops in, stays for a while, then shrinks a little as it fades away (ADR 023); with
    /// Reduce motion it is there at once and only fades. Plain C#: the view that owns the pill
    /// keeps one and stops it when it is disabled.
    /// </summary>
    public sealed class SpeechBubble
    {
        private readonly RectTransform _pill;
        private readonly TMP_Text _text;
        private readonly float _padding;
        private readonly float _minWidth;
        private MotionHandle _motion;
        private bool _still;

        public SpeechBubble(RectTransform pill, TMP_Text text, float padding, float minWidth)
        {
            _pill = pill;
            _text = text;
            _padding = padding;
            _minWidth = minWidth;
        }

        /// <summary>Whether the bubble is on screen (also while it fades away).</summary>
        public bool IsShowing => _pill != null && _pill.gameObject.activeSelf;

        public string Text => _text.text;

        /// <summary>Pops the bubble in with <paramref name="text"/>; it goes by itself after <paramref name="seconds"/>.</summary>
        public void Show(string text, float seconds, bool reduceMotion)
        {
            Stop();
            _still = reduceMotion;
            text ??= string.Empty;
            if (_text.text != text)
            {
                _text.text = text;
            }

            float width = Mathf.Max(_minWidth, Mathf.Ceil(_text.GetPreferredValues(text).x) + _padding);
            _pill.sizeDelta = new Vector2(width, _pill.sizeDelta.y);
            UiMotion.GroupOf(_pill.gameObject).alpha = 1f;
            _pill.gameObject.SetActive(true);
            _pill.localScale = reduceMotion ? Vector3.one : new Vector3(0.6f, 0.6f, 1f);
            _motion = LMotion.Create(reduceMotion ? 1f : 0.6f, 1f, 0.3f)
                .WithEase(Ease.OutBack)
                .WithOnComplete(() => Hold(seconds))
                .Bind(_pill, (scale, pill) => pill.localScale = new Vector3(scale, scale, 1f));
        }

        /// <summary>Takes the bubble away at once.</summary>
        public void Hide()
        {
            Stop();
            HideNow();
        }

        /// <summary>Stops the bubble where it is (its owner is being disabled).</summary>
        public void Stop()
        {
            if (_motion.IsActive())
            {
                _motion.Cancel();
            }

            _motion = default;
        }

        private void Hold(float seconds)
        {
            _motion = LMotion.Create(0f, 1f, Mathf.Max(0.1f, seconds)).WithOnComplete(FadeAway).RunWithoutBinding();
        }

        /// <summary>Its time is up: it shrinks a little as it fades (it only fades with Reduce motion).</summary>
        private void FadeAway()
        {
            _motion = UiMotion.PopOut(_pill, UiMotion.GroupOf(_pill.gameObject), _still, HideNow, 0.85f, UiMotion.Short);
        }

        private void HideNow()
        {
            _motion = default;

            // When the scene unloads the pill may already be gone.
            if (_pill != null)
            {
                _pill.gameObject.SetActive(false);
                _pill.localScale = Vector3.one;
                UiMotion.GroupOf(_pill.gameObject).alpha = 1f;
            }
        }
    }
}
