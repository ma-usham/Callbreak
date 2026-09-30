using System;
using Callbreak.Application.Ports;
using Callbreak.Core.Match;
using Callbreak.Core.Scoring;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The copy and the row order of the result screens (GDD §11 screens 09, 10 and 16). Scores are
    /// formatted by <see cref="ScoreTenths"/> (one decimal, a true minus sign); this adds what
    /// depends on the player's place and on how many rounds were played.
    /// </summary>
    public static class ResultsText
    {
        /// <summary>What a round that has not been played shows on the scorecard (an en dash).</summary>
        public const string Unplayed = "–";

        public const string HeadlineWinKey = "ui.headline.win";
        public const string HeadlineSharedKey = "ui.headline.shared";
        public const string HeadlineSecondKey = "ui.headline.second";
        public const string HeadlineThirdKey = "ui.headline.third";
        public const string HeadlineFourthKey = "ui.headline.fourth";
        public const string RoundsCompletedKey = "ui.match_results.footer";
        public const string OneRoundCompletedKey = "ui.match_results.footer_one";
        public const string YouKey = "ui.common.you";

        /// <summary>The match results headline for the local player's place: a win, a shared win, or 2nd to 4th.</summary>
        public static string HeadlineKey(in Standing local)
        {
            switch (local.Rank)
            {
                case 1:
                    return local.IsShared ? HeadlineSharedKey : HeadlineWinKey;
                case 2:
                    return HeadlineSecondKey;
                case 3:
                    return HeadlineThirdKey;
                default:
                    return HeadlineFourthKey;
            }
        }

        /// <summary>"5 rounds completed" / "1 round completed".</summary>
        public static string RoundsCompleted(int rounds, ILocalization localization)
        {
            if (localization == null)
            {
                throw new ArgumentNullException(nameof(localization));
            }

            return rounds == 1 ? localization.Get(OneRoundCompletedKey) : localization.Format(RoundsCompletedKey, rounds);
        }

        /// <summary>
        /// The rows of the round results, top to bottom, as mockup 09 lists the table: the local
        /// player first, then the seats as the eye finds them on the table: top, left, right.
        /// </summary>
        public static void RoundRowOrder(Seat local, Seat[] rows)
        {
            if (rows == null || rows.Length < SeatExtensions.Count)
            {
                throw new ArgumentException("Four rows are needed.", nameof(rows));
            }

            rows[0] = local;
            rows[1] = Relative(local, 2);
            rows[2] = Relative(local, 3);
            rows[3] = Relative(local, 1);
        }

        private static Seat Relative(Seat local, int relativeSeat)
        {
            return (Seat)(((int)local + relativeSeat) % SeatExtensions.Count);
        }
    }
}
