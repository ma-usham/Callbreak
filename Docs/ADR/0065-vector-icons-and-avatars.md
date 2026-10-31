# ADR 065 - The icons and the avatars as vector art

**Status:** proposed (31 Oct 2026). The owner, after the V1 rebuild's plates, scenes, cloths, logos and
cards were in the game: "ok then implement what are remained in the game. except cards for now."

## Context

The V1 rebuild (ADR 058) redrew the kit as vector art but kept two sets of V1 images, in Figma and in
the game:
- **the icons**: flat glyphs, black and a few brown or ivory, and four paintings (the coin, the gold spade,
  the Quick Match bolt, Play), the V1 kit's image components 4:76 to 4:123;
- **the five player portraits** (Avatar_Default and Avatar_01 to 04, and the same five framed for the
  name prompt), paintings the owner has called AI-looking.

## Decision

- **Every icon the game shows is drawn again as vector art**, keeping its meaning and silhouette so
  players still know it, in the rebuild's palette (Figma's "Callbreak Colors"):
  - the glyphs in ink #131211;
  - the brown and ivory variants in brown/800 #572E12 and surface ivory #F9F0E6;
  - Leave, the Create Room badge and the Follow rule's diamonds in crimson #9A0815;
  - the people behind on Friends and Create Room in brown/400 and /600 instead of grey;
  - the coin, the gold spade, the bolt and Play as crafted vector art with the logo's and the Dhaka
    Ring's golds and crimsons.
- **The five portraits are drawn again as flat vector portraits** of the same people, in the same
  clothes and framing, and framed the same way for the name prompt.
- **Each sprite keeps its file, size, GUID and import settings.** Each icon is fitted into the box the
  old icon's ink filled, so every screen shows it where, and as large as, it showed the old one; no
  prefab changes.
- **Not redrawn:**
  - the suit icons (card pieces; the owner: "except cards");
  - the cloth tab icon (Icon_TableOval_*, vector since ADR 058);
  - three icons nothing shows (Icon_CardStack, Icon_Sort, Icon_Storefront_Brown);
  - the avatar ring (a white disc) and the chosen ring.
- **Figma:** the page "V1 Rebuild · Icons & Avatars" shows each V1 icon and portrait beside its redraw,
  as editable vectors uploaded from the scripts' SVGs.

## Consequences

- `Tools/ArtPipeline/NewUI/icons.py` and `avatars.py` are the icons' and portraits' source, as
  `scenes.py` is the scenes'. They render with headless Edge on black and on white and recover the
  alpha, as the other NewUI art does.
- The build's size does not change: every texture keeps its size and import settings (ASTC 6×6 on
  Android, a fixed size per pixel). The source PNGs shrink from 3.24 MB to 0.89 MB (55 files).
- The portraits face almost to the front (each head turned a little), with simpler faces than the
  paintings: one shared face kit, told apart by face shape, hair, hats and clothes.
