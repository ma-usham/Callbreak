# ADR 053 - Shared card faces with a base and a back per deck

**Status:** accepted (owner, 28 Oct 2026).

**Context.** ADR 050 baked each deck's frame and inks into its own 52 faces, so the three decks
were 159 textures of 246 x 386 (156 faces and 3 backs). The faces of the three decks differed only in
their frame: the kings, pips and indices were the same. The owner had the cards redesigned in Figma
(the "Callbreak Game UI" file, page "V2 · Cards"). Their court cards are classic double-ended figures
with a Nepali touch, generated with ChatGPT. The owner then chose the code change that draws a deck's
own art under one shared set of faces, rather than exporting 52 faces per deck. They asked for the same
border on every deck, with the decks told apart by other means ("all types of card will have same border
but make them feel like they are three different types").

## Decision

- **Faces, once:** `Art/Cards/Faces/Card_<Suit>_<Rank>.png`, 52 sprites of 246 x 386, see-through
  around the card's marks (indices, pips, court art). Every deck uses the same ones.
- **A base per deck:** `Art/Cards/<Deck>/Base_<Deck>.png`, the card's paper and border, opaque inside
  the shared rounded outline. Every deck has the same sand edge and fine inner line. The decks differ by
  paper tone and a quiet motif:
  - Classic Crimson: warm white paper with crimson lattice corners.
  - Himalayan Ivory: cream paper with faint mountain lines.
  - Kathmandu Gold: champagne paper with a faint gold lattice and gold corners.
- **A back per deck:** `Back_<Deck>.png`, redrawn in vector (lattice, frame, crimson seal). The backs
  stay the strong difference between decks.
- **`CardSpriteProvider`:** `DeckSprites` gains `Base`, and the provider gains `Base(deckId)` with the
  same fallback to the default deck. Each deck lists the shared faces, so `Face(deckId, card)` is
  unchanged for callers.
- **Drawing:** the Card prefab has a `Base` image between Glow and Face (with its `CardPerspective`).
  - `CardView.ShowFace(card, face, cardBase)` shows the base under the face.
  - `ShowBack` hides it.
  - The table passes the local deck's base everywhere a face is shown (ADR 031): the hand, the trick,
    the deal, and an opponent's card turning over.
  - The main menu's fan and the shop's deck peek have a base image under their faces too.
- **Builders:**
  - `TableCardBuilder.BuildLibrary` loads the shared faces and each deck's base and back.
  - `MainMenuBuilder` and `ShopCustomizer` build the base images. `AddFanBases` and `AddDeckBases`
    add them to the saved prefabs.
  - `TableCardBuilder.BuildDeckBases` runs all three.

## Consequences

- **Fewer textures:** cards in a build drop from 159 textures to 58 (52 faces, 3 bases, 3 backs). The
  old per-deck faces of ADR 050 stay in the repository, unreferenced, until a cleanup removes them.
- **Adding a deck:** a new deck is one base and one back, not 53 pictures.
- **Effects:** the silhouette and glow are still drawn from the Classic Crimson back's outline, which
  keeps the shared card shape.
- **Superseded:** ADR 050's per-deck faces. Its size and rounded alpha still hold.
