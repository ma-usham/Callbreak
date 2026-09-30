using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The middle of the table, where the current trick lies (GDD §11 T rules: "Trick cards sit
    /// toward the player who played them, rotated ±6°"). Four slots by relative seat (0 bottom,
    /// 1 right, 2 top, 3 left), measured on mockup 08; a later card lies over an earlier one.
    /// </summary>
    public sealed class TrickAreaView : MonoBehaviour
    {
        public const int Slots = 4;

        [SerializeField] private RectTransform _area;
        [SerializeField] private Vector2[] _slotPositions = new Vector2[Slots];
        [SerializeField] private float[] _slotRotations = new float[Slots];
        [SerializeField] private float _cardScale = 0.82f;

        private readonly CardView[] _cards = new CardView[Slots];

        /// <summary>Parent of the trick's cards.</summary>
        public RectTransform Area => _area;

        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Slots; i++)
                {
                    if (_cards[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Where a card played from <paramref name="relativeSeat"/> comes to rest.</summary>
        public CardPlacement Slot(int relativeSeat)
        {
            return new CardPlacement(_slotPositions[relativeSeat], _slotRotations[relativeSeat], _cardScale, 1f);
        }

        public CardView CardAt(int relativeSeat) => _cards[relativeSeat];

        /// <summary>
        /// The card now belongs to the trick: it moves under the trick area without changing its
        /// place on screen, on top of the cards already played. An animation then brings it to
        /// <see cref="Slot"/>, or <see cref="Settle"/> puts it there at once.
        /// </summary>
        public void Adopt(int relativeSeat, CardView card)
        {
            _cards[relativeSeat] = card;
            card.Rect.SetParent(_area, true);
            card.Rect.SetAsLastSibling();
            card.SetDimmed(false);
            card.SetShadow(true);
        }

        public void Settle(int relativeSeat)
        {
            CardView card = _cards[relativeSeat];
            if (card != null)
            {
                card.Placement = Slot(relativeSeat);
            }
        }

        /// <summary>Forgets the card in a slot and hands it back to the caller.</summary>
        public CardView Release(int relativeSeat)
        {
            CardView card = _cards[relativeSeat];
            _cards[relativeSeat] = null;
            return card;
        }

        /// <summary>Returns every trick card to the pool.</summary>
        public void Clear(CardViewPool pool)
        {
            for (int i = 0; i < Slots; i++)
            {
                if (_cards[i] != null)
                {
                    pool.Return(_cards[i]);
                    _cards[i] = null;
                }
            }
        }
    }
}
