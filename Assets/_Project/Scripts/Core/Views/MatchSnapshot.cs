using System;

namespace Callbreak.Core.Views
{
    /// <summary>
    /// The complete state of a match as plain data (GDD §22 "Snapshots"; §29 offline_match.json), for
    /// offline save and resume and for server-side recovery. It holds every hand, so it must never be sent
    /// to a client. Public fields of primitives, strings, arrays and nested [Serializable] classes only, so
    /// <c>UnityEngine.JsonUtility</c> or any JSON library can round-trip it. Seats, phases and cards are
    /// stored as numbers: seat 0–3, <c>Phase</c> value, card id 0–51. A snapshot that fails to restore is
    /// discarded, never fixed up.
    /// </summary>
    [Serializable]
    public sealed class MatchSnapshot
    {
        /// <summary>The schema this build writes and accepts.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Schema version; <c>MatchEngine.Restore</c> rejects any other value.</summary>
        public int schemaVersion;

        /// <summary>Id of the rule set the match runs under; must match the rule set passed to Restore.</summary>
        public string ruleSetId;

        /// <summary>The match id.</summary>
        public string matchId;

        /// <summary>The engine phase as its numeric <c>Phase</c> value; always a waiting phase.</summary>
        public int phase;

        /// <summary>The round number (0 before the match starts).</summary>
        public int round;

        /// <summary>True when the setup fixed the round-1 dealer (only matters before the match starts).</summary>
        public bool hasFixedFirstDealer;

        /// <summary>The round-1 dealer seat.</summary>
        public int firstDealer;

        /// <summary>This round's dealer seat.</summary>
        public int dealer;

        /// <summary>The seat whose turn is open (or the last trick's winner in the round summary).</summary>
        public int currentSeat;

        /// <summary>The open turn id; 0 when no turn is open.</summary>
        public int currentTurn;

        /// <summary>The last turn id issued in the match; the next turn is this plus one.</summary>
        public int turnCounter;

        /// <summary>Four seats in seat order: profile, hand and round state.</summary>
        public SeatSnapshot[] seats;

        /// <summary>The trick in progress (its leader even when empty).</summary>
        public TrickSnapshot currentTrick;

        /// <summary>This round's finished tricks in order.</summary>
        public TrickSnapshot[] completedTricks;

        /// <summary>Every scored round in order.</summary>
        public RoundSnapshot[] rounds;
    }
}
