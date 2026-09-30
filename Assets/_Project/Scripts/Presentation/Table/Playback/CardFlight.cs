using LitMotion;
using UnityEngine;

namespace Callbreak.Presentation.Table.Playback
{
    /// <summary>
    /// A card moving to a new place within its parent (GDD §14 "Card played: flies to the trick slot
    /// with a slight rotation and a soft shadow", 260 ms OutCubic; 120 ms with Reduce motion).
    /// Position, turn, size and how flat the card lies all travel together. A flight that was
    /// started before it joined the event queue (the local player's own card flies at once) simply
    /// carries on when the queue takes it over.
    /// </summary>
    public sealed class CardFlight : IEventAnimation
    {
        public const float Seconds = 0.26f;
        public const float ReducedSeconds = 0.12f;

        private readonly CardView _card;
        private readonly CardPlacement _to;
        private readonly Ease _ease;
        private CardPlacement _from;
        private float _elapsed;
        private bool _begun;

        public CardFlight(CardView card, CardPlacement to, float duration, Ease ease = Ease.OutCubic)
        {
            _card = card;
            _to = to;
            _ease = ease;
            Duration = Mathf.Max(0.0001f, duration);
        }

        public float Duration { get; }

        public CardView Card => _card;

        public bool IsDone => _begun && _elapsed >= Duration;

        public void Begin()
        {
            if (_begun)
            {
                return;
            }

            _begun = true;
            _from = _card.Placement;
        }

        public bool Tick(float deltaTime)
        {
            Begin();
            _elapsed += deltaTime;
            float t = Mathf.Clamp01(_elapsed / Duration);
            _card.Placement = t >= 1f ? _to : CardPlacement.Lerp(_from, _to, EaseUtility.Evaluate(t, _ease));
            return t >= 1f;
        }

        public void Finish()
        {
            _begun = true;
            _elapsed = Duration;
            _card.Placement = _to;
        }
    }
}
