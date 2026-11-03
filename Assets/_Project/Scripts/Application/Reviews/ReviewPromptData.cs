using System;

namespace Callbreak.Application.Reviews
{
    /// <summary>review-prompt.json: how often the game has asked the store for its review card. Plain serializable data read and written by <see cref="ReviewPrompt"/>.</summary>
    [Serializable]
    public sealed class ReviewPromptData
    {
        public const int CurrentVersion = 1;

        public int schemaVersion = CurrentVersion;

        /// <summary>How many times the store was asked.</summary>
        public int asks;

        /// <summary>When it was last asked (UTC ticks); 0 while never.</summary>
        public long lastAskUtcTicks;
    }
}
