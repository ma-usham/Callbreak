namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// The timing of a deck picture's look at its ace of spades on the Card Collection screen (GDD
    /// §11 screen 15: "Selecting a deck flips its preview to the Ace of Spades face and back"). The
    /// card narrows to its edge showing its back, opens on its face, stays, and turns back the same
    /// way; with Reduce motion it never narrows and the face is simply there for the hold.
    /// Engine-free, so it is tested outside Unity; <c>CardCollectionView</c> plays it.
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

        private static float EaseOutQuad(float progress)
        {
            float clamped = progress < 0f ? 0f : progress > 1f ? 1f : progress;
            float rest = 1f - clamped;
            return 1f - rest * rest;
        }
    }
}
