namespace Callbreak.Core.Match
{
    /// <summary>
    /// The authoritative match phases (GDD §7 "Match state machine"). The engine has no clock, so the
    /// automatic phases (<see cref="Dealing"/>, <see cref="TrickResolve"/>, <see cref="RoundScoring"/>) run to
    /// completion inside <c>Start</c>/<c>Execute</c> and are never observed from outside. After any call
    /// returns, <c>MatchEngine.Phase</c> is one that waits for input: <see cref="Lobby"/>,
    /// <see cref="Calling"/>, <see cref="Playing"/>, <see cref="RoundSummary"/>, <see cref="MatchOver"/>
    /// or <see cref="Closed"/>.
    /// </summary>
    public enum Phase : byte
    {
        /// <summary>Created but not started.</summary>
        Lobby = 0,

        /// <summary>Rotating the dealer, shuffling, dealing and checking redeal rules (transient).</summary>
        Dealing = 1,

        /// <summary>A call turn is open; accepts <c>PlaceCall</c> from the current seat.</summary>
        Calling = 2,

        /// <summary>A play turn is open; accepts <c>PlayCard</c> from the current seat.</summary>
        Playing = 3,

        /// <summary>Finding the trick winner (transient).</summary>
        TrickResolve = 4,

        /// <summary>Scoring the round (transient).</summary>
        RoundScoring = 5,

        /// <summary>Round results are showing; accepts <c>ReadyNext</c> from any seat not yet ready.</summary>
        RoundSummary = 6,

        /// <summary>All rounds are scored and the standings are final; accepts nothing.</summary>
        MatchOver = 7,

        /// <summary>The table was closed by its host or server (<c>MatchEngine.Close</c>); accepts nothing.</summary>
        Closed = 8,
    }
}
