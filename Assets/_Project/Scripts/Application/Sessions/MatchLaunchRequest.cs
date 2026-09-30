namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// What the Table scene should start (GDD §11 screen 06: MatchLaunchRequest { Mode = Offline,
    /// Difficulty, RuleSet = NepaliStandard }). Online modes get their connection from the room or
    /// matchmaking service that produced the request.
    /// </summary>
    public sealed class MatchLaunchRequest
    {
        public MatchLaunchRequest(MatchMode mode, BotDifficulty difficulty, bool resumeSaved = false, string ruleSetId = "nepali-standard")
        {
            Mode = mode;
            Difficulty = difficulty;
            ResumeSaved = resumeSaved;
            RuleSetId = ruleSetId;
        }

        public MatchMode Mode { get; }

        public BotDifficulty Difficulty { get; }

        /// <summary>Offline only: continue the snapshot saved by an unfinished match instead of dealing a new one.</summary>
        public bool ResumeSaved { get; }

        public string RuleSetId { get; }
    }
}
