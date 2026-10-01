namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The client's copy of the emote limit (GDD §8: one every 3 s and ten per round; the server
    /// enforces the same). Lets the picker refuse politely instead of sending what will be dropped.
    /// </summary>
    public sealed class EmoteLimiter
    {
        public const double MinSecondsApart = 3.0;
        public const int MaxPerRound = 10;

        private double _lastSentAt = double.NegativeInfinity;
        private int _sentThisRound;

        public int SentThisRound => _sentThisRound;

        /// <summary>Whether an emote may be sent at <paramref name="now"/>.</summary>
        public bool CanSend(double now)
        {
            return _sentThisRound < MaxPerRound && now - _lastSentAt >= MinSecondsApart;
        }

        /// <summary>
        /// How much of the wait for the next emote is left at <paramref name="now"/> (ADR 024): 1 just
        /// after one is sent, falling evenly to 0 when another may go; 1 while the round's ten are
        /// used up.
        /// </summary>
        public float Cooldown(double now)
        {
            if (_sentThisRound >= MaxPerRound)
            {
                return 1f;
            }

            double left = MinSecondsApart - (now - _lastSentAt);
            return left <= 0.0 ? 0f : (float)System.Math.Min(1.0, left / MinSecondsApart);
        }

        /// <summary>Counts an emote sent at <paramref name="now"/>; false (and nothing counted) when over the limit.</summary>
        public bool TrySend(double now)
        {
            if (!CanSend(now))
            {
                return false;
            }

            _lastSentAt = now;
            _sentThisRound++;
            return true;
        }

        /// <summary>A new round starts a new allowance.</summary>
        public void ResetRound()
        {
            _sentThisRound = 0;
        }
    }
}
