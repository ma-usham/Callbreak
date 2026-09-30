using System.Collections.Generic;
using UnityEngine;

namespace Callbreak.Presentation.Table
{
    /// <summary>
    /// The scene's 52 card views (GDD §23): created once when the table loads and reused for every
    /// round. A card is one view for its whole life in a round: dealt into a hand or a fan, played
    /// to the trick, swept away and returned here.
    /// </summary>
    public sealed class CardViewPool : MonoBehaviour
    {
        public const int DeckSize = 52;

        [SerializeField] private CardView _prefab;
        [SerializeField] private RectTransform _storage;

        private readonly List<CardView> _free = new List<CardView>(DeckSize);
        private int _created;

        /// <summary>Views created so far; stays at 52 in a normal match.</summary>
        public int Created => _created;

        public int Free => _free.Count;

        private void Awake()
        {
            Prewarm(DeckSize);
        }

        /// <summary>Makes sure <paramref name="count"/> views exist (they wait, inactive, under the storage object).</summary>
        public void Prewarm(int count)
        {
            while (_created < count)
            {
                CardView card = Create();
                card.gameObject.SetActive(false);
                _free.Add(card);
            }
        }

        /// <summary>A plain, upright card under <paramref name="parent"/>, on top of its siblings.</summary>
        public CardView Rent(Transform parent)
        {
            CardView card;
            if (_free.Count > 0)
            {
                card = _free[_free.Count - 1];
                _free.RemoveAt(_free.Count - 1);
            }
            else
            {
                card = Create();
            }

            card.Rect.SetParent(parent, false);
            card.Rect.SetAsLastSibling();
            card.ResetState();
            card.gameObject.SetActive(true);
            return card;
        }

        public void Return(CardView card)
        {
            if (card == null || _free.Contains(card))
            {
                return;
            }

            card.gameObject.SetActive(false);
            card.Rect.SetParent(_storage, false);
            card.ResetState();
            _free.Add(card);
        }

        private CardView Create()
        {
            CardView card = Instantiate(_prefab, _storage);
            card.name = "Card" + _created.ToString("00");
            _created++;
            return card;
        }
    }
}
