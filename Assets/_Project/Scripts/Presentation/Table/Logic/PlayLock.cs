using Callbreak.Core.Cards;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The input lock after a play (GDD §11 screen 08, §23 "Input"): the card flies at once
    /// (optimistic), and no second card can be sent until the play's own <c>CardPlayed</c> event has
    /// been played back, the session rejects it, or two seconds pass, after which the card rolls
    /// back into the hand. Taps closer together than the debounce are ignored as well.
    /// </summary>
    public sealed class PlayLock
    {
        public const double TimeoutSeconds = 2.0;
        public const double DebounceSeconds = 0.15;

        private double _lockedAt;
        private double _lastTapAt = double.NegativeInfinity;

        /// <summary>A play has been sent and not yet settled.</summary>
        public bool IsLocked { get; private set; }

        /// <summary>The card in flight while <see cref="IsLocked"/>.</summary>
        public Card Pending { get; private set; }

        /// <summary>True when a tap at <paramref name="now"/> may act: not locked, and not a bounce of the last tap.</summary>
        public bool AcceptTap(double now)
        {
            if (IsLocked || now - _lastTapAt < DebounceSeconds)
            {
                return false;
            }

            _lastTapAt = now;
            return true;
        }

        /// <summary>Locks input for <paramref name="card"/>, which has just been sent.</summary>
        public void Begin(Card card, double now)
        {
            IsLocked = true;
            Pending = card;
            _lockedAt = now;
        }

        /// <summary>The play was confirmed (its event played) or refused: input opens again.</summary>
        public void Release()
        {
            IsLocked = false;
        }

        /// <summary>
        /// True exactly once, when a locked play has waited <see cref="TimeoutSeconds"/> without an
        /// answer: the caller rolls <see cref="Pending"/> back into the hand. Releases the lock.
        /// </summary>
        public bool TryExpire(double now)
        {
            if (!IsLocked || now - _lockedAt < TimeoutSeconds)
            {
                return false;
            }

            IsLocked = false;
            return true;
        }
    }
}
