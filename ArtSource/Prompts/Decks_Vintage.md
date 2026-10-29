# Card faces from the vintage pack (ADR 050)

No image generation: every face is composed from the owner's vintage card pack and the three
decks' own look (ADR 014). The backs are still ADR 014's (`export-decks.ps1`, `Decks_Batch.md`).

| | |
| --- | --- |
| Source | `ArtSource/Cards/Vintage/`: the owner's pack, 56 transparent PNGs of 512 × 712 (52 faces, `Joker_Black`, `Joker_Red`, `CardBack_01` black, `CardBack_02` red). Named `Spade_A`, `Heart_07`, `Club_10`, `Diamond_K`. The jokers and the pack's backs are not used. |
| Mountains | `ArtSource/Cards/HimalayanIvory/Mountains_{Top,Bottom}.png`: Himalayan Ivory's faint mountain wash, lifted once from its ADR 014 ace (228 px card scale) |
| Script | `Tools/ArtPipeline/compose-decks.ps1` (C# in `CardComposer.cs` and `DeckTools.cs`, C# 5, `Add-Type`; run it in a fresh PowerShell) |
| Output | `ArtSource/Exports/Cards/{ClassicCrimson,HimalayanIvory,KathmanduGold}/Card_{Suit}_{Rank}.png`, the 156 whole cards (review and the layers' check); the layers the game draws (ADR 054): `ArtSource/Exports/Cards/Faces/Card_{Suit}_{Rank}.png` (52 shared faces) and `ArtSource/Exports/Cards/{Deck}/Base_{Deck}.png`, copied into `Assets/_Project/Art/Cards/` by `-Install` |
| Review | `ArtSource/Working/Decks/review/` (full size) and `Docs/Verification/Decks/` (half-size contact sheets, corner zooms, hand mocks, `compose-report.txt`) |

```powershell
powershell -File Tools\ArtPipeline\compose-decks.ps1 -Sample    # A♠ 5♠ 10♥ J♣ Q♦ K♥ of each deck (~7 s)
powershell -File Tools\ArtPipeline\compose-decks.ps1             # all 156 faces, checks, review sheets, handoff rows (~40 s)
powershell -File Tools\ArtPipeline\compose-decks.ps1 -Install    # the same, then copy into Assets/ if every check passed
```

The run is deterministic (the paper grain has a fixed seed). After `-Install`, re-run
`Callbreak/Build/Sprite Atlases` only if a deck folder was added; the atlases repack by themselves.

## What was wrong with the pack as it came

- **5♠ has nine pips** (the 9's layout). Every other number card is right.
- **The court cards are one and a half figures**: a full upper body, then a turned head with no
  body. Real cards are mirrored at the waist.
- **The frames differ by suit** (clubs boxed, spades and hearts in an L, diamonds with rosettes),
  and the paper's tone differs from card to card.
- **Its cards are 0.72 as wide as tall**, the game's 0.637 (ADR 014). Squeezing them 11 % or
  changing the table's layout were the other choices (owner, 26 Oct 2026: redesign instead).

## Method

1. **Split the pack into inks.** Each pixel is paper + k (black − paper) + r (red − paper), least
   squares, k, r ≥ 0, k + r ≤ 1. Paper: the median of the lighter half of the card's middle, per
   card. Black (16, 13, 12) and red (135, 8, 24), measured on the pips. Coverage under 0.06 is the
   paper's grain and stains and becomes 0. The result is two coverage maps, which are linear: they
   are re-sampled (Catmull-Rom, widened when shrinking) and turned without fringes, and printed in
   any deck's inks.
2. **Lift the elements** (pack pixels; pieces are 8-connected at coverage 0.3, grown 2 px):
   - **Pip**: the middle pip of each suit's 3, box (150, 250)–(362, 462).
   - **Index glyphs**: the spades' ranks, box (10, 18)–(100, 128), pieces under 80 px dropped; one
     scale for all (the K sets it).
   - **Aces**: box (60, 100)–(452, 620); a piece belongs to the ornament when its centre lies in
     x 100–412, y 110–600 (the frame's bars, its corner rosettes and the pack's index centre outside).
   - **Courts**: box (80, 62)–(436, 400), from the crown to the waist (above the pack's turned
     head). The figure is the one piece of 20000 px or more; small pieces are kept only inside its
     box; the large suit beside the crown (2000 px or more, within x 90–205, y 72–217) is dropped,
     since the index shows the suit. Ink deepened (coverage s → 1 − (1 − s)^1.5) so the hatching
     holds at trick size; scaled 0.92.
3. **Draw each deck's frame** at the master size, 512 × 804 (twice the export), from ADR 014's
   geometry scaled by 512 / 228: the coloured band (16.8 px), lines inside it, an octagonal panel
   whose corners are cut at x + y = 120.5, and in each cut corner a pattern after the decks' Newari
   lattice (a turned tile with an eight-pointed star, a four-petal flower and a dot along each
   edge). Paper grain: value noise on a 6 px lattice, ±1.2 %.
4. **Print** the index (top left and turned bottom right), then the pips, the ace or the court:
   - **Index**: rank centred at (0.195 W, 0.137 H), 0.108 H tall, strokes 1 master px bolder;
     suit at (0.195 W, 0.262 H), 0.12 W wide. As large as ADR 014's (it is all a hand card shows
     of itself).
   - **Pips**: columns 0.355 / 0.5 / 0.645 W, rows 0.305 / 0.695 H (four rows for 9 and 10,
     evenly between); 0.17 W wide for 2–6, 0.145 W for 7–10 (their middle pips sit between the
     outer ones). Pips below the middle are turned. The script checks every layout has its rank's
     number of pips.
   - **Aces**: the ornament, 0.56 W wide, centred.
   - **Courts**: the figure centred on its waist and turned about the card's centre, so the halves
     meet; a line in the deck's gold across the waist; the ink cleared from the index's corner
     (a spear or a flower stops short of it).
5. **Export** at 246 × 386 (Catmull-Rom from the master) with one shared alpha: the rounded
   corner, 15.1 px (14 px of a 228 px card, scaled). 246 × 386 keeps the cards' 0.637, and a
   deck's 52 faces still fill one 2048 atlas page and half of another, as ADR 014's did; at
   256 × 402 they need two full pages (+8 MiB for the three decks).

## Decks

| | Classic Crimson | Himalayan Ivory | Kathmandu Gold |
| --- | --- | --- | --- |
| Band | `#6A0507` | `#CC9C68` | `#100E0B`, gold rim `#E3B254` 3.6 px |
| Lines (outermost first, px) | `#FBF1D6` 2.7, `#A8854E` 5.2, `#F3E6CB` 2.2 | `#F8F0DE` 4.4, `#CDA478` 4.6, `#F4E9D8` 2.2 | `#5C4520` 2.2, `#F2CB6A` 4.0, `#B07A2A` 2.4, `#E9D9AE` 2.2 |
| Cut corners | `#A50E10`, pattern `#E9A266` | `#EAD3B0`, pattern `#B98450` | `#14110C`, pattern `#EBC05A` |
| Paper | `#FBF5EA` | `#FCF6EC`, mountains `#E2CFB4` | `#FBF3E4` |
| Black / red ink | `#141111` / `#B0101B` | `#141111` / `#AA0F18` | `#141111` / `#BC101B` |
| Courts | `#171212` / `#A50F18` | `#211913` / `#A3121A` | `#171212` / `#A8121B` |

Kathmandu Gold's spades and clubs were bronze on ADR 014's sheets; the owner had them black like
the other decks' (26 Oct 2026): its gold is in the frame.

## Checks (`compose-report.txt`)

- 52 faces per deck, all 246 × 386, 8-bit RGBA, corners clear, centre opaque, every alpha
  channel identical to the shared mask.
- Pip layouts 2–10: one pip per rank point.
- The layers (ADR 054): each deck's base under the shared faces, blended as the GPU does (linear
  light, premultiplied), against that deck's whole cards. Classic Crimson must come back to within
  one level (it comes back exactly); the other decks show its inks over their own frames and paper.

## Layers (ADR 054)

The game draws a card as its deck's base (the deck's `Frame`: band, lines, cut corners, paper,
Himalayan Ivory's mountains) under one face shared by every deck. `Layers.Lift` (in
`CardComposer.cs`) takes each face off Classic Crimson's whole card:

1. A pixel that equals the base stays clear.
2. Otherwise its coverage is the least-squares fit, in linear light and weighted by luminance, of the
   ink that explains it best (Classic Crimson's black, red, court black, court red and seam).
3. The coverage is raised if a darker channel needs more to stay above zero. A channel lighter than
   the paper (the faint ring the export's Catmull-Rom leaves beside dark ink) doesn't raise it.
4. The colour is stored premultiplied: the card less the base's share. It may exceed the coverage,
   adding light where the ring was, so the ring lightens whichever base is under it.

Premultiplied because Unity blends UI and averages an sRGB texture's mip maps in linear light.
With straight alpha the mip maps average a fringe's colour without its coverage, and trick-size
cards drift by up to 30 levels. The faces draw with `Callbreak/UI/Premultiplied` and import with
*Alpha Is Transparency* off.
