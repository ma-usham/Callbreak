using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Callbreak.Core.Match;

namespace Callbreak.Core.Scoring
{
    /// <summary>
    /// Final (or live) standings with the GDD §4 tie-breaks, applied in order: (1) higher total; (2) more
    /// rounds where the call was met; (3) higher best single-round score; (4) still tied → the seats share
    /// the place, e.g. two players 1st and the next 3rd.
    /// </summary>
    public static class Standings
    {
        /// <summary>Ranks the seats of a score sheet. On an empty sheet everyone shares first place.</summary>
        /// <param name="sheet">The match's scored rounds.</param>
        /// <returns>Four standings sorted by rank, then seat number.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="sheet"/> is null.</exception>
        public static IReadOnlyList<Standing> Compute(ScoreSheet sheet)
        {
            if (sheet == null)
            {
                throw new ArgumentNullException(nameof(sheet));
            }

            var totals = new int[SeatExtensions.Count];
            var met = new int[SeatExtensions.Count];
            var best = new int[SeatExtensions.Count];
            for (int i = 0; i < SeatExtensions.Count; i++)
            {
                totals[i] = sheet.TotalOf((Seat)i);
                met[i] = sheet.RoundsMetBy((Seat)i);
                best[i] = sheet.BestRoundOf((Seat)i);
            }

            return Compute(totals, met, best);
        }

        /// <summary>Ranks seats from their tie-break inputs, each list indexed by seat number.</summary>
        /// <param name="totalTenths">Match totals.</param>
        /// <param name="roundsMet">Rounds in which each call was met.</param>
        /// <param name="bestRoundTenths">Best single-round scores.</param>
        /// <returns>Four standings sorted by rank, then seat number.</returns>
        /// <exception cref="ArgumentNullException">A list is null.</exception>
        /// <exception cref="ArgumentException">A list does not have four entries.</exception>
        public static IReadOnlyList<Standing> Compute(
            IReadOnlyList<int> totalTenths,
            IReadOnlyList<int> roundsMet,
            IReadOnlyList<int> bestRoundTenths)
        {
            if (totalTenths == null)
            {
                throw new ArgumentNullException(nameof(totalTenths));
            }

            if (roundsMet == null)
            {
                throw new ArgumentNullException(nameof(roundsMet));
            }

            if (bestRoundTenths == null)
            {
                throw new ArgumentNullException(nameof(bestRoundTenths));
            }

            if (totalTenths.Count != SeatExtensions.Count
                || roundsMet.Count != SeatExtensions.Count
                || bestRoundTenths.Count != SeatExtensions.Count)
            {
                throw new ArgumentException("Standings need one entry per seat.");
            }

            var unranked = new Standing[SeatExtensions.Count];
            for (int i = 0; i < unranked.Length; i++)
            {
                unranked[i] = new Standing((Seat)i, 1, false, totalTenths[i], roundsMet[i], bestRoundTenths[i]);
            }

            var ranked = new Standing[unranked.Length];
            for (int i = 0; i < unranked.Length; i++)
            {
                int better = 0;
                bool shared = false;
                for (int j = 0; j < unranked.Length; j++)
                {
                    if (j == i)
                    {
                        continue;
                    }

                    int order = Compare(unranked[j], unranked[i]);
                    if (order < 0)
                    {
                        better++;
                    }
                    else if (order == 0)
                    {
                        shared = true;
                    }
                }

                Standing s = unranked[i];
                ranked[i] = new Standing(s.Seat, better + 1, shared, s.TotalTenths, s.RoundsMet, s.BestRoundTenths);
            }

            SortByRankThenSeat(ranked);
            return new ReadOnlyCollection<Standing>(ranked);
        }

        /// <summary>
        /// Orders two seats by the tie-breaks: negative when <paramref name="a"/> places ahead of
        /// <paramref name="b"/>, positive when behind, zero when they share a place. Ignores rank and seat.
        /// </summary>
        /// <param name="a">One seat's standing.</param>
        /// <param name="b">Another seat's standing.</param>
        /// <returns>Negative, zero or positive.</returns>
        public static int Compare(in Standing a, in Standing b)
        {
            if (a.TotalTenths != b.TotalTenths)
            {
                return a.TotalTenths > b.TotalTenths ? -1 : 1;
            }

            if (a.RoundsMet != b.RoundsMet)
            {
                return a.RoundsMet > b.RoundsMet ? -1 : 1;
            }

            if (a.BestRoundTenths != b.BestRoundTenths)
            {
                return a.BestRoundTenths > b.BestRoundTenths ? -1 : 1;
            }

            return 0;
        }

        private static void SortByRankThenSeat(Standing[] standings)
        {
            // Insertion sort: four items, stable, and no comparer allocation.
            for (int i = 1; i < standings.Length; i++)
            {
                Standing current = standings[i];
                int j = i - 1;
                while (j >= 0 && IsAfter(standings[j], current))
                {
                    standings[j + 1] = standings[j];
                    j--;
                }

                standings[j + 1] = current;
            }
        }

        private static bool IsAfter(in Standing a, in Standing b)
        {
            return a.Rank != b.Rank ? a.Rank > b.Rank : a.Seat > b.Seat;
        }
    }
}
