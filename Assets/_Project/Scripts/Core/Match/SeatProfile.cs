using System;

namespace Callbreak.Core.Match
{
    /// <summary>
    /// Public information about whoever sits in a seat (GDD §22 MatchStarted: "names, avatars, deck ids, bot
    /// flags"; §26 PlayerViewSnapshot seats). Immutable; nothing here is secret.
    /// </summary>
    public sealed class SeatProfile : IEquatable<SeatProfile>
    {
        /// <summary>Creates a profile.</summary>
        /// <param name="name">Display name.</param>
        /// <param name="avatarId">Avatar catalogue id (may be empty).</param>
        /// <param name="deckThemeId">Deck theme id; others see this seat's card backs in it (GDD §13).</param>
        /// <param name="isBot">True for a computer opponent; bots are ready for the next round automatically.</param>
        /// <exception cref="ArgumentNullException">A string is null.</exception>
        public SeatProfile(string name, string avatarId, string deckThemeId, bool isBot)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            AvatarId = avatarId ?? throw new ArgumentNullException(nameof(avatarId));
            DeckThemeId = deckThemeId ?? throw new ArgumentNullException(nameof(deckThemeId));
            IsBot = isBot;
        }

        /// <summary>Display name.</summary>
        public string Name { get; }

        /// <summary>Avatar catalogue id.</summary>
        public string AvatarId { get; }

        /// <summary>Deck theme id used for this seat's card backs.</summary>
        public string DeckThemeId { get; }

        /// <summary>True for a bot seat. Bot seats count as ready in the round summary.</summary>
        public bool IsBot { get; }

        /// <inheritdoc />
        public bool Equals(SeatProfile other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return other != null
                && string.Equals(Name, other.Name, StringComparison.Ordinal)
                && string.Equals(AvatarId, other.AvatarId, StringComparison.Ordinal)
                && string.Equals(DeckThemeId, other.DeckThemeId, StringComparison.Ordinal)
                && IsBot == other.IsBot;
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return Equals(obj as SeatProfile);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.Ordinal.GetHashCode(Name);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(AvatarId);
                hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DeckThemeId);
                return (hash * 2) + (IsBot ? 1 : 0);
            }
        }

        /// <summary>E.g. "\"Asha\" avatar=a2 deck=crimson bot".</summary>
        /// <returns>The profile's text.</returns>
        public override string ToString()
        {
            return "\"" + Name + "\" avatar=" + AvatarId + " deck=" + DeckThemeId + (IsBot ? " bot" : " human");
        }
    }
}
