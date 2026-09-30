using System;
using Callbreak.Core.Cards;

namespace Callbreak.Core.Rules
{
    /// <summary>
    /// The obligation for one play turn plus the cards it allows, from <see cref="LegalMoves.Classify"/>.
    /// Drives the turn prompt ("Beat Q♥", "Follow ♥", …) and explains a tapped illegal card (GDD §3, §11
    /// screen 08). <see cref="Legal"/> always equals <see cref="LegalMoves.Compute"/> for the same input.
    /// </summary>
    public readonly struct PlayObligation : IEquatable<PlayObligation>
    {
        private readonly Card _cardToBeat;

        internal PlayObligation(ObligationKind kind, CardSet legal, Suit suit, bool hasCardToBeat, Card cardToBeat)
        {
            Kind = kind;
            Legal = legal;
            Suit = suit;
            HasCardToBeat = hasCardToBeat;
            _cardToBeat = cardToBeat;
        }

        /// <summary>The kind of obligation.</summary>
        public ObligationKind Kind { get; }

        /// <summary>The cards the player may play.</summary>
        public CardSet Legal { get; }

        /// <summary>
        /// The suit the obligation names: the led suit for <see cref="ObligationKind.BeatCard"/> and
        /// <see cref="ObligationKind.FollowSuit"/>, spades for <see cref="ObligationKind.TrumpWithSpade"/>.
        /// Meaningless (clubs) for the other kinds.
        /// </summary>
        public Suit Suit { get; }

        /// <summary>
        /// True when a specific card must be beaten: always for <see cref="ObligationKind.BeatCard"/>, and for
        /// <see cref="ObligationKind.TrumpWithSpade"/> when a spade must overtrump the highest spade played.
        /// </summary>
        public bool HasCardToBeat { get; }

        /// <summary>The card to beat, e.g. Q♥ for "Beat Q♥".</summary>
        /// <exception cref="InvalidOperationException"><see cref="HasCardToBeat"/> is false.</exception>
        public Card CardToBeat
        {
            get
            {
                if (!HasCardToBeat)
                {
                    throw new InvalidOperationException(Kind + " has no card to beat.");
                }

                return _cardToBeat;
            }
        }

        /// <inheritdoc />
        public bool Equals(PlayObligation other)
        {
            return Kind == other.Kind
                && Legal == other.Legal
                && Suit == other.Suit
                && HasCardToBeat == other.HasCardToBeat
                && _cardToBeat == other._cardToBeat;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is PlayObligation other && Equals(other);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return unchecked((Legal.GetHashCode() * 31) + ((int)Kind << 8) + _cardToBeat.Id);
        }

        /// <summary>E.g. "BeatCard Q♥ legal=A♥" or "FollowSuit ♥ legal=10♥ 3♥".</summary>
        /// <returns>The obligation's text.</returns>
        public override string ToString()
        {
            string detail;
            switch (Kind)
            {
                case ObligationKind.BeatCard:
                    detail = " " + _cardToBeat;
                    break;
                case ObligationKind.FollowSuit:
                    detail = " " + CardText.SuitSymbol(Suit);
                    break;
                case ObligationKind.TrumpWithSpade:
                    detail = HasCardToBeat ? " over " + _cardToBeat : string.Empty;
                    break;
                default:
                    detail = string.Empty;
                    break;
            }

            return Kind + detail + " legal=" + Legal;
        }
    }
}
