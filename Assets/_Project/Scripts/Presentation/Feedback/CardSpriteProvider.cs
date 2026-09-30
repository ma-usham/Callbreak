using System;
using Callbreak.Core.Cards;
using UnityEngine;

namespace Callbreak.Presentation.Feedback
{
    /// <summary>
    /// The card art of every deck (GDD §13, §23 "CardSpriteProvider resolves faces from your deck's
    /// atlas and each seat's back by its DeckThemeId"): 52 faces indexed by <see cref="Card.Id"/> and
    /// one back per deck. The asset is filled by the editor builder
    /// (Callbreak/Table/Build Card Sprite Library), so nothing is looked up by name at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Callbreak/Card Sprite Provider", fileName = "CardSprites")]
    public sealed class CardSpriteProvider : ScriptableObject
    {
        /// <summary>One deck: its stable id ("classic-crimson"), its back and its 52 faces.</summary>
        [Serializable]
        public sealed class DeckSprites
        {
            [SerializeField] private string _id;
            [SerializeField] private Sprite _back;
            [SerializeField] private Sprite[] _faces = new Sprite[Card.Count];

            public DeckSprites()
            {
            }

            public DeckSprites(string id, Sprite back, Sprite[] faces)
            {
                _id = id;
                _back = back;
                _faces = faces;
            }

            public string Id => _id;

            public Sprite Back => _back;

            /// <summary>The face for a card id 0-51, or null when the art is missing.</summary>
            public Sprite Face(int cardId)
            {
                return _faces != null && (uint)cardId < (uint)_faces.Length ? _faces[cardId] : null;
            }

            public int FaceCount
            {
                get
                {
                    int count = 0;
                    if (_faces != null)
                    {
                        for (int i = 0; i < _faces.Length; i++)
                        {
                            if (_faces[i] != null)
                            {
                                count++;
                            }
                        }
                    }

                    return count;
                }
            }
        }

        [SerializeField] private DeckSprites[] _decks = Array.Empty<DeckSprites>();
        [SerializeField] private string _defaultDeckId = "classic-crimson";

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
