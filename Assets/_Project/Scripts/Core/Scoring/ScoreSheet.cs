using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Callbreak.Core.Match;
using Callbreak.Core.Rules;

namespace Callbreak.Core.Scoring
{
    /// <summary>
    /// Every scored round of a match so far (GDD §11 screen 16 "a read-only view of the session's
    /// ScoreSheet"). Immutable: <see cref="Append(IReadOnlyList{int}, IReadOnlyList{int}, RuleSet)"/> returns
    /// a new sheet, so one instance can be shared by the engine, views and worker threads. Each total is by
    /// construction the sum of that seat's round deltas (GDD §22 invariant).
    /// </summary>
    public sealed class ScoreSheet : IEquatable<ScoreSheet>
    {
        private readonly RoundResult[] _rounds;
        private readonly ReadOnlyCollection<RoundResult> _view;

        private ScoreSheet(RoundResult[] rounds)
        {
            _rounds = rounds;
            _view = new ReadOnlyCollection<RoundResult>(rounds);
        }

        /// <summary>A sheet with no rounds.</summary>
        public static ScoreSheet Empty { get; } = new ScoreSheet(new RoundResult[0]);

        /// <summary>Scored rounds in order; <c>Rounds[0]</c> is round 1.</summary>
        public IReadOnlyList<RoundResult> Rounds => _view;

        /// <summary>
        /// Scores a finished round and returns the sheet with it appended.
        /// </summary>
        /// <param name="calls">Each seat's call, indexed by seat number.</param>
        /// <param name="won">Tricks each seat won, indexed by seat number; must total 13.</param>
        /// <param name="rules">The match's rule set.</param>
        /// <returns>A new sheet one round longer.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">Not four entries, or tricks do not total 13.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A call or trick count is out of range.</exception>
        public ScoreSheet Append(IReadOnlyList<int> calls, IReadOnlyList<int> won, RuleSet rules)
        {
            if (calls == null)
            {
                throw new ArgumentNullException(nameof(calls));
            }

            if (won == null)
            {
                throw new ArgumentNullException(nameof(won));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (calls.Count != SeatExtensions.Count || won.Count != SeatExtensions.Count)
            {
                throw new ArgumentException("Calls and tricks won need one entry per seat.");
            }

            var lines = new SeatRoundResult[SeatExtensions.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                var seat = (Seat)i;
                int delta = ScoreCalculator.RoundScore(calls[i], won[i], rules);
                lines[i] = new SeatRoundResult(seat, calls[i], won[i], delta, TotalOf(seat) + delta);
            }

            return Append(new RoundResult(_rounds.Length + 1, lines));
        }

        /// <summary>Appends an already scored round, e.g. one rebuilt from the wire.</summary>
        /// <param name="round">The next round's result.</param>
        /// <returns>A new sheet one round longer.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="round"/> is null.</exception>
        /// <exception cref="ArgumentException">The round number or a total does not follow on from this sheet.</exception>
        public ScoreSheet Append(RoundResult round)
        {
            if (round == null)
            {
                throw new ArgumentNullException(nameof(round));
            }

            if (round.Round != _rounds.Length + 1)
            {
                throw new ArgumentException("Expected round " + (_rounds.Length + 1) + ", got " + round.Round + ".", nameof(round));
            }

            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                SeatRoundResult line = round[(Seat)i];
                if (line.TotalTenths != TotalOf((Seat)i) + line.DeltaTenths)
                {
                    throw new ArgumentException("Total of " + line.Seat + " is not previous total plus delta.", nameof(round));
                }
            }

            var rounds = new RoundResult[_rounds.Length + 1];
            Array.Copy(_rounds, rounds, _rounds.Length);
            rounds[_rounds.Length] = round;
            return new ScoreSheet(rounds);
        }

        /// <summary>The seat's match total in tenths (0 before any round is scored).</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The running total.</returns>
        public int TotalOf(Seat seat)
        {
            return _rounds.Length == 0 ? 0 : _rounds[_rounds.Length - 1][seat].TotalTenths;
        }

        /// <summary>Rounds in which the seat met its call: tie-break 2 (GDD §4 "Ties").</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The count of met calls.</returns>
        public int RoundsMetBy(Seat seat)
        {
            int met = 0;
            for (int i = 0; i < _rounds.Length; i++)
            {
                if (_rounds[i][seat].Met)
                {
                    met++;
                }
            }

            return met;
        }

        /// <summary>The seat's best single-round score in tenths: tie-break 3 (GDD §4). 0 with no rounds.</summary>
        /// <param name="seat">Any seat.</param>
        /// <returns>The highest round delta.</returns>
        public int BestRoundOf(Seat seat)
        {
            if (_rounds.Length == 0)
            {
                return 0;
            }

            int best = int.MinValue;
            for (int i = 0; i < _rounds.Length; i++)
            {
                best = Math.Max(best, _rounds[i][seat].DeltaTenths);
            }

            return best;
        }

        /// <inheritdoc />
        public bool Equals(ScoreSheet other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (other == null || other._rounds.Length != _rounds.Length)
            {
                return false;
            }

            for (int i = 0; i < _rounds.Length; i++)
            {
                if (!_rounds[i].Equals(other._rounds[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as ScoreSheet);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _rounds.Length;
                for (int i = 0; i < _rounds.Length; i++)
                {
                    hash = (hash * 397) ^ _rounds[i].GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>One line per round, as <see cref="RoundResult.ToString"/>.</summary>
        /// <returns>The sheet's text.</returns>
        public override string ToString()
        {
            return _rounds.Length == 0 ? "(no rounds)" : string.Join(" | ", (object[])_rounds);
        }
    }
}
