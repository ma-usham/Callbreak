using System;
using Callbreak.Core.Cards;
using UnityEngine;

namespace Callbreak.Presentation.Feedback
{
    /// <summary>
    /// The card art of every deck (GDD §13, §23 "CardSpriteProvider resolves faces from your deck's
    /// atlas and each seat's back by its DeckThemeId"): per deck, 52 faces indexed by
    /// <see cref="Card.Id"/>, a back, and a base drawn under faces that are see-through around the
    /// card's marks (ADR 053). Since ADR 068 every deck has its own faces, each a whole card, and no
    /// base. The asset is filled by the editor builder (Callbreak/Build/Table Card Assets), so nothing
    /// is looked up by name at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Callbreak/Card Sprite Provider", fileName = "CardSprites")]
    public sealed class CardSpriteProvider : ScriptableObject
    {
        /// <summary>One deck: its stable id ("classic"), its back, its base and its 52 faces.</summary>
        [Serializable]
        public sealed class DeckSprites
        {
            [SerializeField] private string _id;
            [SerializeField] private Sprite _back;
            [SerializeField] private Sprite _base;
            [SerializeField] private Sprite[] _faces = new Sprite[Card.Count];

            public DeckSprites()
            {
            }

            /// <param name="cardBase">The deck's border and paper, drawn under every face; null for faces that carry their own.</param>
            public DeckSprites(string id, Sprite back, Sprite cardBase, Sprite[] faces)
            {
                _id = id;
                _back = back;
                _base = cardBase;
                _faces = faces;
            }

            public string Id => _id;

            public Sprite Back => _back;

            /// <summary>The deck's border and paper under its faces; null when the faces are whole cards.</summary>
            public Sprite Base => _base;

            /// <summary>The face for a card id 0-51, or null when the art is missing.</summary>
            public Sprite Face(int cardId)
            {
                return _faces != null && (uint)cardId < (uint)_faces.Length ? _faces[cardId] : null;
            }
        }

        [SerializeField] private DeckSprites[] _decks = Array.Empty<DeckSprites>();
        [SerializeField] private string _defaultDeckId = "classic";

        public int DeckCount => _decks.Length;

        public string DefaultDeckId => _defaultDeckId;

        public DeckSprites DeckAt(int index) => _decks[index];

        public bool HasDeck(string deckId) => Find(deckId) != null;

        /// <summary>The face of <paramref name="card"/> in a deck; an unknown deck falls back to the default one.</summary>
        public Sprite Face(string deckId, Card card)
        {
            DeckSprites deck = Find(deckId) ?? Find(_defaultDeckId);
            return deck?.Face(card.Id);
        }

        /// <summary>A deck's base, the border and paper under its faces; an unknown deck falls back to the default one.</summary>
        public Sprite Base(string deckId)
        {
            DeckSprites deck = Find(deckId) ?? Find(_defaultDeckId);
            return deck?.Base;
        }

        /// <summary>A deck's back; an unknown deck falls back to the default one.</summary>
        public Sprite Back(string deckId)
        {
            DeckSprites deck = Find(deckId) ?? Find(_defaultDeckId);
            return deck?.Back;
        }

        /// <summary>Replaces the library (editor builder only).</summary>
        public void SetDecks(DeckSprites[] decks, string defaultDeckId)
        {
            _decks = decks ?? Array.Empty<DeckSprites>();
            _defaultDeckId = defaultDeckId;
        }

        private DeckSprites Find(string deckId)
        {
            if (string.IsNullOrEmpty(deckId))
            {
                return null;
            }

            for (int i = 0; i < _decks.Length; i++)
            {
                if (_decks[i] != null && string.Equals(_decks[i].Id, deckId, StringComparison.Ordinal))
                {
                    return _decks[i];
                }
            }

            return null;
        }
    }
}
