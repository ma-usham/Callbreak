using System;
using System.Collections.Generic;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The art the table looks up by id at runtime: card faces and backs by deck, avatars by preset
    /// id (GDD §12 "generic monochrome silhouettes in v1, with a preset picker"), and the backdrop by
    /// the player's equipped background and table (the owner, 4 Oct 2026). It reads the two libraries
    /// the app shares (the project scope registers both), so the table, the card collection and the
    /// profile all show the same pictures, and the Table prefab's backdrop library; nothing is loaded
    /// by name.
    /// </summary>
    public sealed class TableSkin
    {
        private readonly CardSpriteProvider _cards;
        private readonly AvatarLibrary _avatars;
        private readonly TableBackdropLibrary _backdrops;
        private readonly HashSet<string> _toldMissing = new HashSet<string>(StringComparer.Ordinal);

        /// <param name="cards">Card faces and backs by deck.</param>
        /// <param name="avatars">The avatar presets; without them seats keep the picture on the prefab.</param>
        /// <param name="backdrops">Every background and table; without it the table keeps the picture on the prefab.</param>
        public TableSkin(CardSpriteProvider cards, AvatarLibrary avatars, TableBackdropLibrary backdrops = null)
        {
            _cards = cards != null ? cards : throw new ArgumentNullException(nameof(cards));
            _avatars = avatars;
            _backdrops = backdrops;
        }

        public CardSpriteProvider Cards => _cards;

        /// <summary>Whether there is a backdrop library to show the equipped background and table from.</summary>
        public bool HasBackdrops => _backdrops != null;

        public Sprite Face(string deckId, Card card) => _cards.Face(deckId, card);

        public Sprite Back(string deckId) => _cards.Back(deckId);

        /// <summary>The avatar for a preset id; null when there is no library (the seat keeps its picture).</summary>
        public Sprite Avatar(int avatarId) => _avatars != null ? _avatars.Get(avatarId) : null;

        /// <summary>
        /// The backdrop for the player's equipped background and table (<see cref="HasBackdrops"/>
        /// first). One that this build has no art for (an unknown id, such as one the game no longer
        /// has, or art not made yet) shows the default one instead, and the log says so once.
        /// </summary>
        public TableBackdrop Backdrop(string backgroundId, string tableId)
        {
            if (_backdrops == null)
            {
                throw new InvalidOperationException("This table has no backdrop library.");
            }

            TableBackdrop backdrop = _backdrops.Backdrop(backgroundId, tableId);
            TellIfMissing("background", backgroundId, backdrop.BackgroundId);
            TellIfMissing("table", tableId, backdrop.TableId);
            return backdrop;
        }

        private void TellIfMissing(string kind, string asked, string shown)
        {
            if (string.Equals(asked, shown, StringComparison.Ordinal) || !_toldMissing.Add(kind + "/" + asked))
            {
                return;
            }

            string name = string.IsNullOrEmpty(asked) ? "(none)" : "\"" + asked + "\"";
            Debug.LogWarning($"[TableSkin] The {kind} {name} has no art in this build; the table shows \"{shown}\" instead.");
        }
    }
}
