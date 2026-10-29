# ADR 061 - The V1 rebuild's cards: one face set for every deck, Classic Crimson's back from Figma

**Status:** proposed (30 Oct 2026). The owner: "for now use the cards from the figma that we created in
new ui v1". Asked how three decks should share Figma's single card design, they chose "same faces, own
backs".

## Context

The Figma V1 Rebuild ("V1 Rebuild · Cards") has:
- one set of 52 vector cards (`Card` 179:2678, 240 × 376): cream paper, a crimson outline and gold frame
  lines, the court figures under a temple arch;
- one card back (`Card Back` 177:15).

Its Card shop showed that back for Classic Crimson and the old painted backs for the other two decks.
The game draws a card as ADR 054 lays it out: a shared, premultiplied face (the ink) over a base per
deck (its paper and frame), with a back per deck.

## Decision

- **The 52 shared faces are the Figma cards' ink:** the indices, pips, aces' rings and court figures,
  without the paper, outline and frame lines.
  - As in ADR 054, each face is lifted from its whole card: the whole card less the base, premultiplied
    in linear light.
  - Its alpha is the ink's own coverage (from a render of the ink alone), raised only where the whole
    card needs it.
  - Over the base it gives Figma's whole card back exactly (0 levels; `SharedCardFaceTests` allows 1).
- **Every deck's base is the Figma blank card** (paper, outline, frame lines). So the three decks look
  the same face up.
- **Classic Crimson's back is Figma's Card Back.** Himalayan Ivory and Kathmandu Gold keep their own backs,
  so the decks differ by their backs.
- **The game's card sizes stay:** 246 × 386 faces and bases, 228 × 358 backs. Figma's 240 × 376 card is
  the same shape (1.567 against 1.569), so the hand, the trick (ADR 059 upright, ADR 060's scale and
  slots), the fans, the menu's fan and the shop keep their layout.
- **The card shadow and glow** (`FX_CardSilhouette`, `FX_CardGlow`) are drawn again by
  `TableCardBuilder.BuildEffects` from the new back's outline: Figma's corners are rounder.
- **Tools/ArtPipeline/NewUI/cut_cards.py** cuts them from Figma's four export sheets ("Unity Export ·
  Cards" 1 and 2: the ink alone, the blank card and the back; "Unity Export · Whole Cards" 1 and 2),
  drawn at twice the size and shot on black and on white. It scales them down in premultiplied linear
  light. Each file keeps its `.meta`: the GUID and ADR 048's import settings (uncompressed, mip maps,
  no atlas).

## Consequences

- The same number of card textures (52 faces, 3 bases, 3 backs) at the same sizes, so the build size is
  unchanged. The three bases are now one picture three times.
- `ArtSource/Exports/Cards` holds Figma's whole cards, the base and the faces: the review copies and
  `SharedCardFaceTests`' reference. `compose-decks.ps1` still makes the vintage cards (ADR 050, 054) and
  would overwrite them.
- The owner means to redesign the cards later. Re-exporting is the Figma sheets plus `cut_cards.py`.
