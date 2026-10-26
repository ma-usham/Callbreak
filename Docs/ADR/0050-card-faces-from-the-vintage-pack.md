# ADR 050 - Card faces from the vintage pack, in the three decks' own frames

**Status:** accepted (owner, 26 Oct 2026). It supersedes ADR 014's faces; ADR 014's backs stand.

**Context.**
- **The owner added a vintage card pack** to the project as high-quality card art and asked for it
  to replace the current cards. The pack is 56 transparent PNGs of 512 × 712: 52 faces, two jokers
  and two backs (black and red), engraved, red and black.
- **As it came, it did not fit:**
  - Its 5♠ has nine pips.
  - Its court cards are one and a half figures (a body, then a turned head with no body).
  - Its frames differ by suit, and its paper's tone from card to card.
  - Its cards are 0.72 as wide as tall. The game's are 0.637 (ADR 014), which the hand, the
    fans, the trick and the menu's fan are laid out for.
- **The owner's direction** (26 Oct 2026): use the pack's elements (the J, Q and K and the rest) to
  redesign the cards so they look like the game's three decks. A sample of six cards per deck was
  approved, with one change: Kathmandu Gold's spades and clubs black like the other decks', not
  bronze ("quite faded").

## Decision

- **Composed, not imported.** `Tools/ArtPipeline/compose-decks.ps1` (C# in `CardComposer.cs`)
  prints every face. The method and its numbers are in `ArtSource/Prompts/Decks_Vintage.md`.
  - **Frame:** each deck's frame is redrawn at 512 × 804 from ADR 014's geometry, in its own
    colours: the band, the lines, the cut corners with a Newari-style pattern, and Himalayan
    Ivory's mountains.
  - **Inks:** the pack's art is split into paper, black ink and red ink, then printed in that
    deck's inks.
  - **Index:** the pack's serif glyphs, as large as ADR 014's index and a little bolder.
  - **Pips:** one standard layout for every rank, checked to have its rank's number of pips.
  - **Aces:** the pack's ornate aces.
  - **Courts:** the pack's figures from the crown to the waist, centred and mirrored into
    double-ended cards. The large suit beside the crown goes (the index shows it), and a spear or
    a flower stops short of the index.
- **The cards keep their shape.** Faces are exported at 246 × 386 (0.637), with one rounded alpha
  for all 156. Nothing in the game's layouts changes.
- **246, not 256.** At 246 × 386 a deck's 52 faces fill one 2048 atlas page and half of another,
  as ADR 014's 228 × 358 did (ADR 048). At 256 × 402 they need two full pages: +2.7 MiB per deck,
  +8 MiB for the three.
- **Kathmandu Gold's black suits are black.** Its gold is in the frame.
- **The backs stay ADR 014's** (228 × 358, on their own textures, ADR 048).
- **The pack lives in `ArtSource/Cards/Vintage`**, outside `Assets/`. Under `Assets/` its 512 × 712
  sprites with mip maps fail `TextureBudgetTests`, and Unity would import 40 MB nobody draws. The
  jokers and the pack's two backs are kept with it, unused.
- **`export-decks.ps1`** still cuts ADR 014's sheets, but it only exports and installs the backs.
  Its face cuts stay in `ArtSource/Working/Decks/sheet-cut`; `-InstallFaces` puts them back.

## Consequences

- **Size:** the card atlases are as large as before: per deck one 2048 × 2048 and one 2048 × 1024
  ASTC 4×4 page with mip maps, 8.0 MiB, 24.0 MiB for the three decks. The measured APK is in the
  branch's report (`agent/T-vintage-cards`).
- **Sharper:** the faces are 8 % larger than ADR 014's (246 against 228 px wide), and drawn from
  512 px art instead of cut from sheets with 213 px cards: no sheet softness.
- **Court figures** are the pack's European engravings, printed in ink. ADR 014's painted Nepali
  figures are gone, and the courts read lighter at trick size than the painted ones did.
- **GDD §13** is out of date where it describes the faces: their court figures and their cut from
  the reference sheets.
- **The owner's copy in the main checkout:** `Assets/_Project/Art/Cards/Vintage` (untracked) is
  the same pack. Once this is merged it should be deleted there, or Unity imports it and
  `TextureBudgetTests` fails.
- **To undo:** run `export-decks.ps1 -Install -InstallFaces` (ADR 014's faces), then rebuild the
  atlases (`Callbreak/Build/Sprite Atlases`).
