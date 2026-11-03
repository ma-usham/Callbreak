namespace Callbreak.Application.Sessions
{
    /// <summary>
    /// What the Table scene should start (GDD §11 screen 06: MatchLaunchRequest { Mode = Offline,
    /// Difficulty, RuleSet = NepaliStandard }). Online modes get their connection from the room or
    /// matchmaking service that produced the request.
    /// </summary>
    public sealed class MatchLaunchRequest
    {
        public MatchLaunchRequest(MatchMode mode, BotDifficulty difficulty, bool resumeSaved = false, string ruleSetId = "nepali-standard", bool tutorial = false, bool practice = false)
        {
            Mode = mode;
            Difficulty = difficulty;
            ResumeSaved = resumeSaved;
            RuleSetId = ruleSetId;
            Tutorial = tutorial;
            Practice = practice;
        }

        public MatchMode Mode { get; }

        public BotDifficulty Difficulty { get; }

        /// <summary>Offline only: continue the snapshot saved by an unfinished match instead of dealing a new one.</summary>
        public bool ResumeSaved { get; }

        public string RuleSetId { get; }

        /// <summary>
        /// Offline only, a new match: its first hand is the guided tutorial (ADR 084), the first
        /// practice match a new player accepts.
        /// </summary>
        public bool Tutorial { get; }

        /// <summary>
        /// Offline only: the first-run practice match (ADR 084). It is never saved, so leaving it, or
        /// the app closing during it, leaves nothing to resume (the owner, 3 Nov 2026).
        /// </summary>
        public bool Practice { get; }
    }
}
