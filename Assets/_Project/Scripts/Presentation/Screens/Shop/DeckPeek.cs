using System;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The timing of a deck picture's look at its ace of spades in the shop's Cards category (GDD
    /// §11 screen 15: "Selecting a deck flips its preview to the Ace of Spades face and back"). The
    /// card narrows to its edge showing its back, opens on its face, stays, and turns back the same
    /// way; with Reduce motion it never narrows and the face is simply there for the hold. A tap on
    /// the deck while it peeks keeps the face up for another hold (<see cref="TimeAfterTapAgain"/>).
    /// Engine-free, so it is tested outside Unity; <c>ShopView</c> plays it.
    /// </summary>
    public static class DeckPeek
    {
        /// <summary>One half of a turn: the card narrows to its edge, or opens again (OutQuad).</summary>
        public const float FlipLegSeconds = 0.12f;

        /// <summary>How long a selected deck shows its face before it turns back.</summary>
        public const float FaceHoldSeconds = 1.2f;

        /// <summary>How long a peek lasts: the hold, plus the two turns unless motion is reduced.</summary>
        public static float Seconds(bool reduceMotion)
        {
            return reduceMotion ? FaceHoldSeconds : FaceHoldSeconds + 4f * FlipLegSeconds;
        }

        /// <summary>
        /// A peeking card <paramref name="time"/> seconds after it was selected: the side it shows
        /// and how wide it is drawn (1 = flat, 0 = edge-on). After <see cref="Seconds"/> it is flat
        /// on its back again.
        /// </summary>
        public static void Pose(float time, bool reduceMotion, out bool showsFace, out float width)
        {
            if (reduceMotion)
            {
                showsFace = time < FaceHoldSeconds;
                width = 1f;
                return;
            }

            float faceUp = 2f * FlipLegSeconds;
            float turnsBack = faceUp + FaceHoldSeconds;
            float edgeOnAgain = turnsBack + FlipLegSeconds;
            if (time < FlipLegSeconds)
            {
                showsFace = false;
                width = 1f - EaseOutQuad(time / FlipLegSeconds);
            }
            else if (time < faceUp)
            {
                showsFace = true;
                width = EaseOutQuad((time - FlipLegSeconds) / FlipLegSeconds);
            }
            else if (time < turnsBack)
            {
                showsFace = true;
                width = 1f;
            }
            else if (time < edgeOnAgain)
            {
                showsFace = true;
                width = 1f - EaseOutQuad((time - turnsBack) / FlipLegSeconds);
            }
            else
            {
                showsFace = false;
                width = EaseOutQuad((time - edgeOnAgain) / FlipLegSeconds);
            }
        }

        /// <summary>
        /// The deck that is peeking is tapped again, <paramref name="time"/> seconds into its peek:
        /// the time the peek carries on from. The card keeps the side and the width it has at that
        /// moment, so nothing jumps, and its face is then up for a whole hold again. A card still
        /// turning to its face carries on; a held face starts its hold again; a card turning back
        /// turns round from where it is. With Reduce motion the hold simply starts again. A peek
        /// that has ended starts from the beginning.
        /// </summary>
        public static float TimeAfterTapAgain(float time, bool reduceMotion)
        {
            if (reduceMotion || time <= 0f)
            {
                return 0f;
            }

            float faceUp = 2f * FlipLegSeconds;
            float turnsBack = faceUp + FaceHoldSeconds;
            float edgeOnAgain = turnsBack + FlipLegSeconds;
            if (time < faceUp)
            {
                return time;
            }

            if (time < turnsBack)
            {
                return faceUp;
            }

            if (time < edgeOnAgain)
            {
                // The face is narrowing: it opens again from this width (the second half-turn).
                float faceWidth = 1f - EaseOutQuad((time - turnsBack) / FlipLegSeconds);
                return FlipLegSeconds + FlipLegSeconds * EaseOutQuadInverse(faceWidth);
            }

            // The back is opening: it narrows again from this width (the first half-turn).
            float backWidth = EaseOutQuad((time - edgeOnAgain) / FlipLegSeconds);
            return FlipLegSeconds * EaseOutQuadInverse(1f - backWidth);
        }

        private static float EaseOutQuad(float progress)
        {
            float clamped = Clamp01(progress);
            float rest = 1f - clamped;
            return 1f - rest * rest;
        }

        /// <summary>The progress at which <see cref="EaseOutQuad"/> reaches <paramref name="eased"/>.</summary>
        private static float EaseOutQuadInverse(float eased)
        {
            return 1f - (float)Math.Sqrt(1f - Clamp01(eased));
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }
    }
}
