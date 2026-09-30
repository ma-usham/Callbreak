using System;

namespace Callbreak.Core.Rules
{
    /// <summary>
    /// Call (bid) validity (GDD §3 "The call"): each player calls once, from <see cref="RuleSet.MinCall"/> to
    /// <see cref="RuleSet.MaxCall"/>, with no passing and no limit on the total of all calls.
    /// </summary>
    public static class CallRules
    {
        /// <summary>The value used for a seat that has not called yet in the current round.</summary>
        public const int NotCalled = -1;

        /// <summary>Whether <paramref name="call"/> is within the rule set's range.</summary>
        /// <param name="call">The proposed call.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <returns>True for MinCall ≤ call ≤ MaxCall.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static bool IsValid(int call, RuleSet rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return call >= rules.MinCall && call <= rules.MaxCall;
        }

        /// <summary>
        /// Clamps an estimate into the legal range, as auto-calls must be (GDD §5 edge case 8: "Estimator
        /// call, clamped to MinCall…MaxCall").
        /// </summary>
        /// <param name="call">Any integer estimate.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <returns>The nearest legal call.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="rules"/> is null.</exception>
        public static int Clamp(int call, RuleSet rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (call < rules.MinCall)
            {
                return rules.MinCall;
            }

            return call > rules.MaxCall ? rules.MaxCall : call;
        }
    }
}
