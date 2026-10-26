# ADR 048 - Card faces in sprite atlases, font atlases at 1024

**Status:** accepted (owner, 24 Oct 2026).

**Context.** The owner asked for a smaller Android build without visibly lower quality. A release
APK of main (ff62593) was 149.8 MB; Unity's build report put 118 of its 124 MiB of game data in
textures, and two causes stood out:
- **The 159 card textures were stored uncompressed** (RGBA32, 425 KB each, 66 MiB). Unity does not
  block-compress a texture whose sides are not powers of two once it has mip maps: a 228 × 358 card
  with mip maps stays RGBA32 even with an explicit ASTC override, while the same card without mip
  maps, or a 512 × 512 sprite with them, is ASTC. The cards keep mip maps on purpose (a fan card is
  46 px wide from a 228 px texture); dropping them made the trick cards visibly jagged at 720p. The
  five 384 × 384 framed portraits had the same problem.
- **The six TextMeshPro atlases were 2048 × 2048** (Alpha8, 4 MiB each) for about 110 glyphs that fill
  600 rows of a 1024 one.

## Decision

- **Sprite packer.** Editor Settings > Sprite Packer Mode is *Sprite Atlas V2 - Enabled* (it was
  Disabled; the owner approved the change for the card decks). The framed portraits use the same
  fix for the same cause; their atlas can be dropped on its own.
- **Atlases** (`Assets/_Project/Art/Atlases`, written by `Callbreak/Build/Sprite Atlases`,
  `SpriteAtlasBuilder`): one per deck with its 52 faces, and one with the framed portraits. Mip maps,
  8 px padding with alpha dilation, no rotation or tight packing, Android ASTC 4×4 on pages of at most
  2048 (OpenGL ES 3's guaranteed size; the portraits' pages at most 1024). The source sprites and every
  reference to them stay as they were; Unity draws them from the atlas pages.
- **ASTC 4×4**, not 5×5 or 6×6: at 4× zoom 4×4 can't be told from the uncompressed cards; 5×5 smooths
  the court cards' small jewels and 6×6 blurs them. 6×6 would save about 11 MB more of the APK.
- **The card backs stay out** of the atlases, on their own textures (RGBA32, 3 × 425 KB): in the
  opponents' fans a back is drawn a fifth of its size, where an atlas page's smallest mip maps blend
  its edge with the empty padding and soften the rims.
- **Font atlases at 1024 × 1024**: the six existing atlases were repacked glyph for glyph (every
  padded SDF cell copied unchanged, the glyph metrics untouched, the free and used rectangles rebuilt so
  `UiBuildKit.EnsureCharacters` can still add glyphs), and `UiBuildKit.LoadFont` creates new ones at 1024.
- `TextureBudgetTests` (EditMode) fails when a font atlas is bigger than 1024 or when a sprite with mip
  maps whose sides are not powers of two is outside an atlas (backs excepted).

## Consequences

- See the build-size report on `agent/T-build-size` for the measured sizes. Most of the font saving
  is memory, not download: the empty three quarters of each atlas zipped to almost nothing.
- At the edge of a card, bilinear filtering now reads the atlas's empty padding where its own texture
  repeated the edge, so the thin white rim is a shade dimmer under 4× zoom; at 1:1 the captures show
  no difference.
- A new deck folder under `Art/Cards` needs `Callbreak/Build/Sprite Atlases` run again (the test says so).
- To undo: delete the atlases (the cards go back to RGBA32) and set the packer back to Disabled.

## Addendum, 27 Oct 2026: reverted for quality

The owner found the game's look worse after this change: "dont change the quality the game quality
has worsen. make it like before." Everything in it that touched how the game looks is undone:

- **Sprite Packer Mode is Disabled again.** The four atlases (the three decks, the framed portraits)
  and `SpriteAtlasBuilder` are gone. Each card face and portrait is its own texture again,
  uncompressed RGBA32 with mip maps, as before. That includes ADR 050's 246 × 386 vintage faces.
- **The six font atlases are back at 2048 × 2048,** and new fonts are created at 2048 again
  (`UiBuildKit`).
- **`TextureBudgetTests` is removed:** it enforced the atlases and the 1024 font atlases that the
  owner reversed. ADR 050's notes about it no longer apply.
- **Still in force:** the 40 unused images stay deleted. None was in the build, so they never
  touched how the game looks.

**Cost:** the build is back to roughly its size before this ADR (about 20 MB more in the APK). From
now on, any size saving is the owner's call and must not lower image quality.
