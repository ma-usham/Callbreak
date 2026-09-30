using System;
using Callbreak.Presentation.Framework;
using Callbreak.Presentation.Widgets;
using TMPro;
using UnityEngine;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 15: choose the deck you play with. Each option shows the deck's back (the
    /// presenter flips it to the deck's ace of spades when selected), its name and its status
    /// ("Equipped" / "Owned"); the chosen option shows the crimson outline and filled radio. A Use
    /// button that is not interactable shows its disabled look with its label dimmed.
    /// </summary>
    public sealed class CardCollectionView : UIScreen
    {
        private const float DimmedAlpha = 0.5f;

        [SerializeField] private UnityEngine.UI.Button _backButton;
        [SerializeField] private UnityEngine.UI.Button _collectionButton;
        [SerializeField] private UnityEngine.UI.Button _useButton;
        [SerializeField] private TMP_Text _useLabel;
        [SerializeField] private SelectableGroup _decks;
        [SerializeField] private TMP_Text[] _deckName;
        [SerializeField] private TMP_Text[] _deckStatus;
        [SerializeField] private UnityEngine.UI.Image[] _deckPreview;

        public event Action BackClicked;
        public event Action CollectionClicked;

        /// <summary>Raised when the player taps another deck option.</summary>
        public event Action<int> DeckSelected;

        public event Action UseClicked;

        public int DeckCount => _deckName.Length;

        /// <summary>Shows the selection without raising <see cref="DeckSelected"/>.</summary>
        public void SetSelected(int index)
        {
            _decks.SetSelectedWithoutNotify(index);
        }

        /// <summary>A deck's name ("Classic Crimson") and status ("Equipped" / "Owned").</summary>
        public void SetDeck(int index, string deckName, string status)
        {
            SetText(_deckName[index], deckName);
            SetText(_deckStatus[index], status);
        }

        /// <summary>The picture in a deck option: the deck's back, or its ace of spades. A missing sprite keeps the current picture.</summary>
        public void SetPreview(int index, Sprite sprite)
        {
            if (sprite != null)
            {
                _deckPreview[index].sprite = sprite;
            }
        }

        public void SetUseInteractable(bool interactable)
        {
            _useButton.interactable = interactable;
            SetAlpha(_useLabel, interactable ? 1f : DimmedAlpha);
        }

        private void OnEnable()
        {
            _backButton.onClick.AddListener(OnBack);
            _collectionButton.onClick.AddListener(OnCollection);
            _useButton.onClick.AddListener(OnUse);
            _decks.Changed += OnDeckSelected;
        }

        private void OnDisable()
        {
            _backButton.onClick.RemoveListener(OnBack);
            _collectionButton.onClick.RemoveListener(OnCollection);
            _useButton.onClick.RemoveListener(OnUse);
            _decks.Changed -= OnDeckSelected;
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label.text != text)
            {
                label.text = text;
            }
        }

        private static void SetAlpha(UnityEngine.UI.Graphic graphic, float alpha)
        {
            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private void OnBack() => BackClicked?.Invoke();

        private void OnCollection() => CollectionClicked?.Invoke();

        private void OnUse() => UseClicked?.Invoke();

        private void OnDeckSelected(int index) => DeckSelected?.Invoke(index);
    }
}
