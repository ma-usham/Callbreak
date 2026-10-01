using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The middle of the table, where the current trick lies (GDD §11 T rules: "Trick cards sit
    /// toward the player who played them"). Four slots by relative seat (0 bottom, 1 right, 2 top,
    /// 3 left), measured on mockup 08, every card the same size and lying straight (ADR 024; the
    /// GDD turns them ±6°); a later card lies over an earlier one.
    /// It can also show the previous trick in place of the current one for a moment (the peek of
    /// GDD screen 08: "tap the trick area to peek at the previous trick").
    /// </summary>
    public sealed class TrickAreaView : MonoBehaviour
    {
        public const int Slots = 4;

        [SerializeField] private RectTransform _area;
        [SerializeField] private Vector2[] _slotPositions = new Vector2[Slots];
        [SerializeField] private float[] _slotRotations = new float[Slots];
        [SerializeField] private float _cardScale = 0.82f;

        private readonly CardView[] _cards = new CardView[Slots];
        private readonly CardView[] _peek = new CardView[Slots];

        /// <summary>Parent of the trick's cards.</summary>
        public RectTransform Area => _area;

        /// <summary>The previous trick is lying in the slots; the current trick's cards are hidden meanwhile.</summary>
        public bool IsPeeking { get; private set; }

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

        /// <summary>Starts the peek: the current trick's cards step aside for the previous trick's.</summary>
        public void BeginPeek()
        {
            IsPeeking = true;
            for (int i = 0; i < Slots; i++)
            {
                if (_cards[i] != null)
                {
                    _cards[i].SetAlpha(0f);
                }
            }
        }

        /// <summary>A card of the previous trick, face up, laid in the slot of the seat that played it.</summary>
        public void AdoptPeek(int relativeSeat, CardView card)
        {
            _peek[relativeSeat] = card;
            card.Rect.SetParent(_area, false);
            card.Rect.SetAsLastSibling();
            card.SetShadow(true);
            card.Placement = Slot(relativeSeat);
        }

        /// <summary>Ends the peek: the previous trick's cards go back to the pool and the current trick shows again.</summary>
        public void EndPeek(CardViewPool pool)
        {
            if (!IsPeeking)
            {
                return;
            }

            IsPeeking = false;
            for (int i = 0; i < Slots; i++)
            {
                if (_peek[i] != null)
                {
                    pool.Return(_peek[i]);
                    _peek[i] = null;
                }

                if (_cards[i] != null)
                {
                    _cards[i].SetAlpha(1f);
                }
            }
        }

        /// <summary>Returns every trick card to the pool.</summary>
        public void Clear(CardViewPool pool)
        {
            EndPeek(pool);
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
