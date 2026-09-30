using System;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The art the table looks up by id at runtime: card faces and backs by deck, avatars by preset
    /// id (GDD §12 "generic monochrome silhouettes in v1, with a preset picker"). It reads the two
    /// libraries the app shares (the project scope registers both), so the table, the card
    /// collection and the profile all show the same pictures; nothing is loaded by name.
    /// </summary>
    public sealed class TableSkin
    {
        private readonly CardSpriteProvider _cards;
        private readonly AvatarLibrary _avatars;

        /// <param name="cards">Card faces and backs by deck.</param>
        /// <param name="avatars">The avatar presets; without them seats keep the picture on the prefab.</param>
        public TableSkin(CardSpriteProvider cards, AvatarLibrary avatars)
        {
            _cards = cards != null ? cards : throw new ArgumentNullException(nameof(cards));
            _avatars = avatars;
        }

        public CardSpriteProvider Cards => _cards;

        public Sprite Face(string deckId, Card card) => _cards.Face(deckId, card);

        public Sprite Back(string deckId) => _cards.Back(deckId);

        /// <summary>The avatar for a preset id; null when there is no library (the seat keeps its picture).</summary>
        public Sprite Avatar(int avatarId) => _avatars != null ? _avatars.Get(avatarId) : null;
    }
}
