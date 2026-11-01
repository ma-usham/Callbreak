using System;
using System.Collections.Generic;

namespace Callbreak.Application.Cosmetics
{
    /// <summary>
    /// The deck the player plays with (GDD §13, screen 15). Ids are the stable strings of
    /// <see cref="CosmeticCatalog.Decks"/>: "classic" (the default), "mithila-ink" and
    /// "kathmandu-royal" (ADR 068).
    /// </summary>
    public interface ICosmeticsService
    {
        event Action Changed;

        IReadOnlyList<string> DeckIds { get; }

        string SelectedDeckId { get; }

        void SelectDeck(string deckId);
    }
}
