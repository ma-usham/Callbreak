using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The art the table looks up by id at runtime: card faces and backs by deck, avatars by preset
    /// id (GDD §12 "generic monochrome silhouettes in v1, with a preset picker"). References are set
    /// on the prefab by the editor builder; nothing is loaded by name.
    /// </summary>
    public sealed class TableSkin : MonoBehaviour
    {
        [SerializeField] private CardSpriteProvider _cards;
        [SerializeField] private Sprite[] _avatars = System.Array.Empty<Sprite>();

        public CardSpriteProvider Cards => _cards;

        public Sprite Face(string deckId, Card card) => _cards.Face(deckId, card);

        public Sprite Back(string deckId) => _cards.Back(deckId);

        /// <summary>The avatar for a preset id; ids beyond the art that exists share the first one.</summary>
        public Sprite Avatar(int avatarId)
        {
            if (_avatars.Length == 0)
            {
                return null;
            }

            return _avatars[avatarId >= 0 && avatarId < _avatars.Length ? avatarId : 0];
        }
    }
}
