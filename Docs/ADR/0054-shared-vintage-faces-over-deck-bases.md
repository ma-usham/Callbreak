# ADR 054 - The vintage faces shared by every deck, over a base per deck

**Status:** proposed (28 Oct 2026). The owner asked for a smaller game "without compromising the
game quality", with the decks made from their borders instead of 52 faces each. It brings back
ADR 053's drawing (a deck's base under one shared set of faces) with ADR 050's vintage art, which the
owner kept when ADR 053's redrawn cards were reverted.

**Context.**
- **The cards were most of the game's data.** ADR 050 baked each deck's frame into its own
  52 faces: 159 textures of 246 × 386, each uncompressed RGBA32 with mip maps (0.5 MiB loaded).
  - They came to 75 of the 135 MiB of game data in a release build.
  - The card library references every deck, so all 159 were in memory at once.
  - Compressing them is off the table: ADR 048's ASTC atlases were reverted for quality.
- **The three decks share their art.** `compose-decks.ps1` prints them from the same index glyphs,
  pips, aces and court figures. The decks differ in frame and paper (Himalayan Ivory's mountains
  included), plus a few ink tones: the pips' red, Himalayan Ivory's court black, and the gold line
  across a court card's waist.
- **The six font atlases** were 2048 × 2048 for about 110 glyphs each (4 MiB each, three quarters
  empty). ADR 048 repacked them glyph for glyph into 1024 × 1024, then reverted that with the
  atlases; the fonts have not changed since.

## Decision

- **A base per deck:** `Art/Cards/<Deck>/Base_<Deck>.png`, the deck's frame on its paper. It is
  the script's `Frame`, exported like the faces.
- **One face per card for every deck:** `Art/Cards/Faces/Card_<Suit>_<Rank>.png`.
  - Each face is Classic Crimson's whole card less its base (`Layers.Lift` in `CardComposer.cs`;
    the method is in `ArtSource/Prompts/Decks_Vintage.md`).
  - At each pixel it stores the coverage of the ink that explains it best, solved in linear light,
    and the colour, premultiplied.
  - Over Classic Crimson's base the GPU's blend gives that deck's cards back exactly. In the mip
    maps they come back at PSNR 59 dB or better.
- **Premultiplied alpha, its own UI shader.**
  - Unity blends UI in linear light and averages an sRGB texture's mip maps in linear light. With
    straight alpha, a face's mip maps average fringe colour without its coverage, and trick-size
    cards drift by up to 30 levels (PSNR 37–44 dB). Premultiplied, they don't.
  - Premultiplied colour can also add light: where the card was a shade lighter than its paper (the
    faint ring the export's sharpening leaves beside dark ink), the ring lightens whichever base is
    under it.
  - The shader is `Callbreak/UI/Premultiplied` (`Art/Shaders/UI_Premultiplied.shader`, material
    `Art/Cards/CardFace.mat`, made by `TableCardBuilder.EnsureFaceMaterial`). It is Unity's UI
    shader with `Blend One OneMinusSrcAlpha` for a premultiplied texture. Tint, CanvasGroup fades and
    RectMask2D clipping work as on any Image.
  - The faces import with *Alpha Is Transparency* off, since its colour dilation would spoil
    premultiplied colour.
  - `CardFaceMaterial.Apply` gives an image that material while it shows a face over a base, and
    the default UI material for a back or a face shown on its own. It is used by:
    - `CardView`'s face;
    - the shop's deck peek (`ShopItemTile.FaceMaterial`).
  - The main menu's fan only shows faces, so its faces keep the material.
- **Drawing and code: ADR 053's.**
  - `CardSpriteProvider.Base(deckId)`.
  - `CardView.ShowFace(card, face, cardBase)`, with the base image under the face.
  - The base images in the menu's fan and the shop's peek.
  - `TableCardBuilder.BuildDeckBases` builds it all. Its new steps are
    `MainMenuBuilder.UseSharedFanFaces` and `ShopCustomizer.UseDeckFaceMaterial`.
- **The backs are unchanged:** ADR 014's, on their own textures.
- **The script installs the layers.**
  - `compose-decks.ps1 -Install` copies the shared faces and the bases.
  - Its QA fails unless the faces give back Classic Crimson's cards to within one level.
  - The whole cards of every deck stay in `ArtSource/Exports/Cards`, for review and as that
    check's reference.
  - The 156 per-deck faces leave `Assets/`.
- **The font atlases are 1024 × 1024 again:** ADR 048's repack, restored as committed (every glyph's
  padded cell copied unchanged). `UiBuildKit.LoadFont` creates new fonts at 1024, with a second page
  allowed if a builder adds more glyphs than fit. The fonts are static, so nothing is added at run time.

## Consequences

- **Smaller.** A build carries 58 card textures instead of 159: 52 faces, 3 bases and 3 backs.
  Sizes are from release builds of main and of this change, measured with
  `Tools/Unity/android-size.sh`:

  | Release build (signed with the debug key) | main (67f7702) | this change |
  | --- | ---: | ---: |
  | Download from Play, ARM64 phone (bundletool, from the .aab) | 100.12 MB | **83.91 MB** (−16.2 MB, −16 %) |
  | Download from Play, 32-bit phone | 88.98 MB | 72.78 MB |
  | Universal APK | 135.87 MB | 119.59 MB |
  | Game data in the APK, zipped | 45.17 MiB | 29.65 MiB |
  | Game data, raw (Unity's build report; about what the textures take in memory) | 134.6 MiB | 67.9 MiB |
  | of it: card textures | 76.6 MiB | 27.9 MiB |
  | of it: font atlases | 25.5 MiB | 7.5 MiB |

  The Android build profile already builds an App Bundle, so Play sends each phone only its own
  native libraries; the universal APK carries both.

- **Classic Crimson is unchanged.** Captured at 720p, 1080p and 1440p, the table differs from main by
  PSNR 63 dB, and the main menu (its fan and all its text) by 67 dB.
- **Himalayan Ivory and Kathmandu Gold** keep their frames, paper and mountains exactly, but print
  in Classic Crimson's inks (CIEDE2000):
  - pip red: 1.3 (Ivory), 2.5 (Gold);
  - Ivory's court black: 4.8;
  - the 1.4 px gold line across a court's waist: 8–9.
  - Their tables differ from main by PSNR 52–54 dB.
  - Giving those two decks their own tones back would take a tint per deck on the red cards and a
    per-deck copy of the 12 courts.
- **A new deck** is a base and a back.
- **Effects:** the glow and silhouette are still drawn from the Classic Crimson back's outline.
- **Tests:**
  - `SharedCardFaceTests`: every face over Classic Crimson's base gives back its whole card; the
    faces import premultiplied.
  - `FontAtlasTests`: no font atlas above 1024.
  - `CardDeckBaseTests`, `MenuStateTests` and `ShopDeckPeekFlowTests` check the face material.
- **Supersedes** ADR 050's per-deck faces; its size, shape and rounded alpha stand. ADR 048's font
  atlases are back in force.
- **To undo:** revert this change. The whole cards are in `ArtSource/Exports/Cards`.
