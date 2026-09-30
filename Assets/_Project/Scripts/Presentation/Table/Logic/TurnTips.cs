using System;
using Callbreak.Application.Ports;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// GDD §15 first-time experience: during a player's first three matches the prompt pill
    /// explains the rule behind the turn ("Tip: you must beat the highest heart if you can.")
    /// instead of only naming it. Pure text selection, like <see cref="TurnPrompt"/>; suits are
    /// spelled out, so a tip needs no symbol the fonts lack.
    /// </summary>
    public static class TurnTips
    {
        /// <summary>Tips switch off once this many matches are finished.</summary>
        public const int MatchesWithTips = 3;

        public const string BeatKey = "ui.tip.beat";
        public const string FollowKey = "ui.tip.follow";
        public const string TrumpKey = "ui.tip.trump";
        public const string AnyCardKey = "ui.tip.any_card";

        public static bool AreOn(int completedMatches) => completedMatches < MatchesWithTips;

        /// <summary>The tip for this turn; null when the plain prompt says it all (leading a trick).</summary>
        /// <param name="trick">The trick being played to, for the led suit.</param>
        public static string ForObligation(in PlayObligation obligation, in Trick trick, ILocalization localization)
        {
            if (localization == null)
            {
                throw new ArgumentNullException(nameof(localization));
            }

            switch (obligation.Kind)
            {
                case ObligationKind.BeatCard:
                    return localization.Format(BeatKey, localization.Get(SingularKey(obligation.Suit)));
                case ObligationKind.FollowSuit:
                    return localization.Format(FollowKey, localization.Get(SingularKey(obligation.Suit)));
                case ObligationKind.TrumpWithSpade:
                    return localization.Format(TrumpKey, localization.Get(PluralKey(trick.LedSuit)));
                case ObligationKind.AnyCard:
                    return localization.Get(AnyCardKey);
                default:
                    return null;
            }
        }

        private static string SingularKey(Suit suit) => "ui.suit." + Name(suit);

        private static string PluralKey(Suit suit) => "ui.suit." + Name(suit) + "s";

        private static string Name(Suit suit)
        {
            switch (suit)
            {
                case Suit.Clubs: return "club";
                case Suit.Diamonds: return "diamond";
                case Suit.Hearts: return "heart";
                default: return "spade";
            }
        }
    }
}
