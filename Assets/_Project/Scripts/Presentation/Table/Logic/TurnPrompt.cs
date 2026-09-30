using System;
using Callbreak.Application.Ports;
using Callbreak.Core.Cards;
using Callbreak.Core.Rules;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The turn prompt's copy (GDD §11 screen 08, "TURN PROMPT COPY"): one line that tells the
    /// player what the rules ask for right now. Pure text selection; the legal cards to highlight
    /// come from the same <see cref="PlayObligation"/>.
    /// </summary>
    public static class TurnPrompt
    {
        public const string LeadAnyKey = "ui.prompt.lead_any";
        public const string BeatKey = "ui.prompt.beat";
        public const string FollowKey = "ui.prompt.follow";
        public const string TrumpKey = "ui.prompt.trump";
        public const string AnyCardKey = "ui.prompt.any_card";
        public const string WaitingForKey = "ui.prompt.waiting_for";
        public const string WinsTrickKey = "ui.prompt.wins_trick";
        public const string YouWinTrickKey = "ui.prompt.you_win_trick";
        public const string YouLeadKey = "ui.prompt.you_lead";
        public const string CallingKey = "ui.bidding.calling";

        /// <summary>Your play turn: "Your turn · Beat Q♥", "Your turn · Follow ♥", …</summary>
        public static string ForObligation(in PlayObligation obligation, ILocalization localization)
        {
            Require(localization);
            switch (obligation.Kind)
            {
                case ObligationKind.LeadAny:
                    return localization.Get(LeadAnyKey);
                case ObligationKind.BeatCard:
                    return localization.Format(BeatKey, CardLabel(obligation.CardToBeat));
                case ObligationKind.FollowSuit:
                    return localization.Format(FollowKey, SuitSymbol(obligation.Suit).ToString());
                case ObligationKind.TrumpWithSpade:
                    return localization.Get(TrumpKey);
                default:
                    return localization.Get(AnyCardKey);
            }
        }

        /// <summary>Someone else's play turn: "Waiting for Asha…".</summary>
        public static string WaitingFor(string seatName, ILocalization localization)
        {
            Require(localization);
            return localization.Format(WaitingForKey, seatName ?? string.Empty);
        }

        /// <summary>A trick was won: "Suman wins the trick", or "You win the trick" for the local seat.</summary>
        public static string TrickWon(string winnerName, bool winnerIsLocal, ILocalization localization)
        {
            Require(localization);
            return winnerIsLocal
                ? localization.Get(YouWinTrickKey)
                : localization.Format(WinsTrickKey, winnerName ?? string.Empty);
        }

        /// <summary>The beat after the fourth call when the local seat leads the first trick: "You lead".</summary>
        public static string YouLead(ILocalization localization)
        {
            Require(localization);
            return localization.Get(YouLeadKey);
        }

        /// <summary>The centre of the table while another seat is calling: "Asha is calling…".</summary>
        public static string Calling(string seatName, ILocalization localization)
        {
            Require(localization);
            return localization.Format(CallingKey, seatName ?? string.Empty);
        }

        /// <summary>"Q♥", "10♠": the rank as printed on the card's index, then the suit symbol.</summary>
        public static string CardLabel(Card card)
        {
            return RankLabel(card.Rank) + SuitSymbol(card.Suit);
        }

        public static string RankLabel(Rank rank)
        {
            switch (rank)
            {
                case Rank.Ace: return "A";
                case Rank.King: return "K";
                case Rank.Queen: return "Q";
                case Rank.Jack: return "J";
                default: return ((int)rank).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        public static char SuitSymbol(Suit suit)
        {
            switch (suit)
            {
                case Suit.Clubs: return '♣';
                case Suit.Diamonds: return '♦';
                case Suit.Hearts: return '♥';
                default: return '♠';
            }
        }

        private static void Require(ILocalization localization)
        {
            if (localization == null)
            {
                throw new ArgumentNullException(nameof(localization));
            }
        }
    }
}
