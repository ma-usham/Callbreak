using System;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;

namespace Callbreak.Core.Scoring
{
    /// <summary>
    /// Round scores in integer tenths (GDD §4 "Round score"; "Scores MUST be stored as integer tenths"):
    /// a met call (won ≥ call) scores <c>call × 10 + (won − call) × OvertrickTenths</c>; a missed call scores
    /// <c>−call × 10</c>. With <see cref="RuleSet.BigCallBonus"/>, a made call of 8 or more scores 130.
    /// Never uses floating point.
    /// </summary>
    public static class ScoreCalculator
    {
        /// <summary>Smallest call that earns the big-call bonus when made (GDD §5 BigCallBonus "8+").</summary>
        public const int BigCallThreshold = 8;

        /// <summary>The flat score of a made big call: 13.0 (GDD §5 edge case 7).</summary>
        public const int BigCallTenths = 130;

        /// <summary>The round score of one seat, in tenths.</summary>
        /// <param name="call">The seat's call, 1–13.</param>
        /// <param name="won">Tricks the seat won this round, 0–13.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <returns>The round delta in tenths, e.g. 41 for call 4, won 5.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The call or trick count is out of range.</exception>
        public static int RoundScore(int call, int won, RuleSet rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (call < 1 || call > RuleSet.CallLimit)
            {
                throw new ArgumentOutOfRangeException(nameof(call), call, "A call is 1-13.");
            }

            if (won < 0 || won > Deck.HandSize)
            {
                throw new ArgumentOutOfRangeException(nameof(won), won, "Tricks won are 0-13.");
            }

            if (!IsMet(call, won))
            {
                return -call * 10;
            }

            if (rules.BigCallBonus && call >= BigCallThreshold)
            {
                return BigCallTenths;
            }

            return (call * 10) + ((won - call) * rules.OvertrickTenths);
        }

        /// <summary>Whether a call was met: at least as many tricks won as called (GDD §4 "won ≥ call").</summary>
        /// <param name="call">The call.</param>
        /// <param name="won">Tricks won.</param>
        /// <returns>True when met.</returns>
        public static bool IsMet(int call, int won)
        {
            return won >= call;
        }
    }
}
