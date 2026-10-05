using System;
using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Table.Playback
{
    /// <summary>A card fading in or out (the swept trick vanishing at the winner; the hand appearing with Reduce motion).</summary>
    public sealed class CardFade : IEventAnimation
    {
        private readonly CardView _card;
        private readonly float _from;
        private readonly float _to;
        private float _elapsed;

        public CardFade(CardView card, float from, float to, float duration)
        {
            _card = card;
            _from = from;
            _to = to;
            Duration = Mathf.Max(0.0001f, duration);
        }

        public float Duration { get; }

        public void Begin()
        {
            _card.SetAlpha(_from);
        }

        public bool Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            float t = Mathf.Clamp01(_elapsed / Duration);
            _card.SetAlpha(Mathf.Lerp(_from, _to, t));
            return t >= 1f;
        }

        public void Finish()
        {
            _elapsed = Duration;
            _card.SetAlpha(_to);
        }
    }

    /// <summary>
    /// A finished trick (GDD §6, §14 "Trick won"): the four cards stay on the table for 0.9 s while
    /// the winning card glows on top of the others, then all four sweep to the winner in 0.35 s
    /// (InOutCubic), shrinking and fading as they arrive. The winning card keeps its size (ADR 024:
    /// the GDD's lift made it look larger than the rest). With Reduce motion the sweep is a 150 ms
    /// fade. No tap skips it (ADR 038). The cards are returned to the pool by the table once the
    /// event has played.
    /// </summary>
    public static class TrickSweep
    {
        public const float HoldSeconds = 0.9f;
        public const float SweepSeconds = 0.35f;
        public const float ReducedSweepSeconds = 0.15f;
        private const float ArriveScale = 0.35f;

        /// <param name="trick">The trick area holding the four cards.</param>
        /// <param name="winnerSlot">Relative seat of the winning card.</param>
        /// <param name="winnerAnchor">Where the cards go: the winner's avatar.</param>
        /// <param name="reduceMotion">Fade in place instead of sweeping.</param>
        /// <param name="onSweep">Called when the sweep starts (the collect sound).</param>
        public static IEventAnimation Create(TrickAreaView trick, int winnerSlot, RectTransform winnerAnchor, bool reduceMotion, Action onSweep)
        {
            CardView winner = trick.CardAt(winnerSlot);
            var hold = new Together().Add(new Beat(HoldSeconds));
            if (winner != null)
            {
                winner.Rect.SetAsLastSibling();
                winner.SetGlow(true);
            }

            Vector2 target = trick.Area.InverseTransformPoint(winnerAnchor.position);
            float seconds = reduceMotion ? ReducedSweepSeconds : SweepSeconds;
            var sweep = new Together();
            for (int slot = 0; slot < TrickAreaView.Slots; slot++)
            {
                CardView card = trick.CardAt(slot);
                if (card == null)
                {
                    continue;
                }

                if (!reduceMotion)
                {
                    CardPlacement at = trick.Slot(slot);
                    sweep.Add(new CardFlight(card, new CardPlacement(target, at.Rotation, at.Scale * ArriveScale, at.Flat), seconds, Ease.InOutCubic));
                }

                sweep.Add(new CardFade(card, 1f, 0f, seconds));
            }

            return new InOrder().Then(hold).Then(new Instant(onSweep)).Then(sweep);
        }
    }
}
