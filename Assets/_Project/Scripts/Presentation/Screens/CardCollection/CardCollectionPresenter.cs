using System;
using System.Collections.Generic;
using Callbreak.Application.Cosmetics;
using Callbreak.Application.Ports;
using Callbreak.Core.Cards;
using Callbreak.Presentation.Feedback;
using Callbreak.Presentation.Framework;
using VContainer.Unity;

namespace Callbreak.Presentation.Screens
{
    /// <summary>
    /// GDD §11 screen 15: choose the deck you play with. Every deck shows its back; the one being
    /// looked at flips to its ace of spades so the faces can be seen before choosing. "Use
    /// Selected Deck" equips it from the next match; all three decks are owned, nothing costs money.
    /// </summary>
    public sealed class CardCollectionPresenter : IStartable, IDisposable
    {
        private static readonly Card AceOfSpades = new Card(Suit.Spades, Rank.Ace);

        private readonly CardCollectionView _view;
        private readonly ScreenRouter _router;
        private readonly ICosmeticsService _cosmetics;
        private readonly ILocalization _localization;
        private readonly CardSpriteProvider _sprites;

        private int _selected;

        public CardCollectionPresenter(
            CardCollectionView view,
            ScreenRouter router,
            ICosmeticsService cosmetics,
            ILocalization localization,
            CardSpriteProvider sprites)
        {
            _view = view;
            _router = router;
            _cosmetics = cosmetics;
            _localization = localization;
            _sprites = sprites;
        }

        public void Start()
        {
            _view.BackClicked += OnBack;
            _view.DeckSelected += OnDeckSelected;
            _view.UseClicked += OnUse;
            _router.Changed += OnScreenChanged;
            _cosmetics.Changed += Render;
            _localization.LanguageChanged += Render;
            _selected = EquippedIndex();
            Render();
        }

        public void Dispose()
        {
            _view.BackClicked -= OnBack;
            _view.DeckSelected -= OnDeckSelected;
            _view.UseClicked -= OnUse;
            _router.Changed -= OnScreenChanged;
            _cosmetics.Changed -= Render;
            _localization.LanguageChanged -= Render;
        }

        private void OnScreenChanged(ScreenId id)
        {
            if (id == ScreenId.CardCollection)
            {
                _selected = EquippedIndex();
                Render();
            }
        }

        private void Render()
        {
            IReadOnlyList<string> decks = _cosmetics.DeckIds;
            int equipped = EquippedIndex();
            int count = Math.Min(_view.DeckCount, decks.Count);
            for (int i = 0; i < count; i++)
            {
                string deck = decks[i];
                _view.SetDeck(
                    i,
                    _localization.Get("ui.deck." + deck),
                    _localization.Get(i == equipped ? "ui.card_collection.equipped" : "ui.card_collection.owned"));
                _view.SetPreview(i, i == _selected ? _sprites.Face(deck, AceOfSpades) : _sprites.Back(deck));
            }

            _view.SetSelected(_selected);
            _view.SetUseInteractable(_selected != equipped);
        }

        private int EquippedIndex()
        {
            IReadOnlyList<string> decks = _cosmetics.DeckIds;
            for (int i = 0; i < decks.Count; i++)
            {
                if (decks[i] == _cosmetics.SelectedDeckId)
                {
                    return i;
                }
            }

            return 0;
        }

        private void OnBack() => _router.Pop();

        private void OnDeckSelected(int index)
        {
            if (index < 0 || index >= _cosmetics.DeckIds.Count)
            {
                return;
            }

            _selected = index;
            Render();
        }

        private void OnUse()
        {
            IReadOnlyList<string> decks = _cosmetics.DeckIds;
            if (_selected >= 0 && _selected < decks.Count)
            {
                _cosmetics.SelectDeck(decks[_selected]);
            }

            Render();
        }
    }
}
