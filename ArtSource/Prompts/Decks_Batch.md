# Card decks batch (faces and backs)

> **Since ADR 050 (26 Oct 2026) the game's faces come from `compose-decks.ps1`**
> (`Decks_Vintage.md`): the vintage pack's elements printed in these three decks' frames. This
> batch still makes the **backs**; its face cuts now go to `ArtSource/Working/Decks/sheet-cut`
> and are installed only with `-Install -InstallFaces`. What follows describes ADR 014's cut.

No image generation in this batch: every sprite is cut from the approved reference art.

| | |
| --- | --- |
| Faces | `Assets/ArtRefrences/card-designs/full-decks/{classic-crimson,himalayan-ivory,kathmandu-gold}/{spades,hearts,diamonds,clubs}.png` (12 sheets, 1642 × 958) |
| Backs | `Assets/ArtRefrences/game-screens/15-card-styles.png` (Card Collection mockup, 1672 × 941) |
| Script | `Tools/ArtPipeline/export-decks.ps1` (C# helpers in `Tools/ArtPipeline/DeckTools.cs`, C# 5, loaded with `Add-Type`) |
| Output | `ArtSource/Exports/Cards/{ClassicCrimson,HimalayanIvory,KathmanduGold}/Card_{Suit}_{Rank}.png` and `Back_{Deck}.png`, 159 files, copied once into `Assets/_Project/Art/Cards/{Deck}/` |
| Review | `ArtSource/Working/Decks/review/` (full size) and `Docs/Verification/Decks/` (half-size contact sheets, corner zooms, hand mocks, `report.txt`) |
| Numbers | `ArtSource/Working/Decks/measure-faces.csv`, `measure-backs.csv`, `report.txt` |

```powershell
& Tools\ArtPipeline\export-decks.ps1            # measure, cut, check, review sheets, handoff rows (~45 s)
& Tools\ArtPipeline\export-decks.ps1 -Install   # the same, then copy into Assets/ only if every check passed
```

The run is deterministic: a second run writes byte-identical files. It overwrites its own outputs and
never deletes anything. `handoff.csv` keeps every row that does not start with `Cards/` and gets one
row per exported file.

## Result

| | |
| --- | --- |
| Size | **228 × 358 px** for all 156 faces and the 3 backs (even numbers, aspect **0.6369**) |
| Corner radius | **14 px**, one shared alpha mask for all 159 files (checked: every file's alpha channel is identical) |
| Format | 8-bit RGBA PNG (colour type 6), sRGB chunk, straight alpha. Alpha 255 everywhere except the four anti-aliased corners, 0 outside. Fully transparent pixels carry the bled edge colour, not black, so texture filtering never pulls a dark fringe in. |
| Index box | union over the measured faces: x 23–69, y 24–119 px = **x 0.101, y 0.067, w 0.206, h 0.268** of the card. Typical card: x 28–60, y 28–108. The index never reaches past **30.3 %** of the card width. Rank glyphs are 36–48 px tall (median 40). |

## 1 · Measuring the cards on the sheets

Each sheet shows 13 cards (A–7 above, 8–K below) on parchment. Every card has a soft drop shadow
that is darkest right at the card, and a thin light rim outside its frame. On Classic Crimson and
Himalayan Ivory the rim is white or cream, 1–3 px wide. On Kathmandu Gold it is gold, 2–3 px, often
with a darker gold outer line. Edges are sharp: 1 px transitions, sometimes 2 px on the softer
sheets.

1. **Rough frame.** The manifest boxes (one layout for all 12 sheets) are only approximate. Inside
   each box, pixels well off the parchment/shadow colour line, or much darker than the parchment,
   are "strong ink" (the frame, not the rim). The first and last columns and rows with at least
   8 such pixels give the frame box. Each side's search window then reaches at most halfway to the
   neighbouring card's frame, because a fixed window ran into the next card across the 18 px gutter.
2. **Background model.** Parchment P is the brightest sample outside the frame. Shadow is P
   darkened along a warm direction K (per channel; blue darkens most), fitted on each profile. A
   pixel is on the shadow line when `1 - c/P` is proportional to K.
3. **Side profiles.** For each side, the colour profile perpendicular to it is averaged over the
   middle 60 % of that side (so over 140–200 rows or columns). The edge is found on that profile:
   - Walk inward from the brightest outer sample along the shadow ramp. The ramp only darkens
     toward the card. The card starts at the first sample that reverses the darkening by more than
     5 levels (the light rim), is strongly chromatic (residual > 20: crimson, gold), is brighter
     than the parchment, or drops by more than 40 levels in one step (a dark border line).
     Weak chroma alone does not count: the contact shadow is browner than the soft shadow
     (residual up to 13), as far off the line as Kathmandu Gold's palest rim.
   - The card reference is the brightest rim sample before the frame (light rims), or that first
     sample itself. A first sample lying on the colour line between its neighbours is itself a
     partly covered pixel, and the next one is used.
   - **Sub-pixel edge = outer side of the reference pixel − the summed coverage of the partial
     pixels outside it.** Each partial pixel is fitted as a mix of the card colour and a shadow
     at least as dark as its outer neighbour. A pixel outside the first anomaly only counts if it
     moved toward the card's colour by more than 3 levels beyond the ramp's own trend, and then at
     most half. This handles sharp edges (one partial pixel) and soft ones (two) alike.
4. **Checks on every sheet.** Each side is also measured on each half separately (tilt check), and
   top and bottom edges are compared along each row. Final state: no card's top or bottom is off
   its row by more than 0.75 px, and no side's halves differ by more than 1.5 px. The largest
   difference is 1.32 px, on Kathmandu Gold Clubs 7; only 2 faces exceed 1 px, and 151 of 156 are
   within 0.65 px. The column residuals of L and R repeat identically on all 12 sheets.
   For example, the lower-row K is about 3 px wider than the J and Q on every sheet. So the card
   widths really vary in the layout the sheets were generated from; that is not measurement noise.
5. **Corners.** Single-pixel rows through the steep half of each arc and columns through the flat
   half give edge points. A circle tangent to both straight edges is fitted with outlier
   rejection. 595 of 624 face corners fitted with rms ≤ 1 px; the other 29 take the deck's median
   radius. Face median: 13.26 sheet px.

Things that did not work, for the record: a single "brighter than parchment" test for the rim
(fails where the rim is only as bright as the parchment); taking the darkest pixel as the contact
shadow (Classic Crimson's white-to-crimson pixel is a pinkish grey darker than the shadow and sits
on the shadow line); requiring three card samples in a row (the rim is often followed by such a
pixel).

### Measured sizes (sheet px)

| Deck, row | Width median (range) | Height median (range) | Aspect |
| --- | --- | --- | --- |
| Classic Crimson A–7 | 214.00 (211.39–215.74) | 342.00 (341.48–342.01) | 0.6257 |
| Classic Crimson 8–K | 230.02 (228.45–233.29) | 356.47 (356.11–356.57) | 0.6453 |
| Himalayan Ivory A–7 | 213.90 (211.54–215.83) | 341.43 (340.60–342.19) | 0.6265 |
| Himalayan Ivory 8–K | 230.86 (227.86–233.69) | 357.22 (356.11–357.58) | 0.6463 |
| Kathmandu Gold A–7 | 213.28 (211.46–216.00) | 342.50 (341.50–343.63) | 0.6227 |
| Kathmandu Gold 8–K | 230.00 (228.00–234.40) | 358.00 (356.53–358.00) | 0.6425 |
| Back, Classic Crimson (mockup) | 184.00 | 278.00 | 0.6619 |
| Back, Himalayan Ivory (mockup) | 183.04 | 278.51 | 0.6572 |
| Back, Kathmandu Gold (mockup) | 185.00 | 278.62 | 0.6640 |

The two rows of every sheet differ. The lower-row cards are about 8 % wider and 4 % taller, so
their aspect is about 3.4 % wider than the upper row's. The task brief's "about 210 × 338" is the
upper row's frame. The GDD's "about 225 × 355" is roughly the lower row.

## 2 · Choosing the size

- One pixel size and one outline for 159 files means each card's own outline is mapped onto the
  common one. The two rows cannot both keep their aspect, so the output aspect (0.637) lies between
  the rows (upper ≈ 0.625, lower ≈ 0.645). Per card, width scale / height scale differs from 1 by
  **−2.9 % to +3.1 %**. That is the row difference plus the real ±1 % width variation of individual
  cards. No card is stretched toward 5:7 (0.714).
- **358 px tall** keeps the lower row, the court cards with the finest detail, at native resolution:
  x 0.973–1.001, y 1.000–1.005. It also matches the GDD's 358 px hand height. The upper row is
  enlarged by x 1.056–1.079, y 1.042–1.051. The largest face upscale is 1.079, well under the
  1.25× limit.
- **228 px wide** gives the balanced aspect with even numbers.
- **Corner radius 14 px:** the faces' arcs are 13.26 sheet px (median), which is about 13.9 px on
  the upper row and 13.3 px on the lower row in output pixels.

## 3 · Cutting

Per card, in sheet pixels, before any resampling:

1. **Clean region**: pixels wholly inside the card. On the straight sides this starts at the first
   fully covered pixel of each side. In the corners it is inside the fitted arc (radius + 0.25 px,
   held within 2 px of the deck median), moved inward by the side insets. Every pixel of the square
   must be inside.
2. **Replace everything else** in a 6 px margin: the rim's partial pixels, the shadow, the
   parchment. In the four corner squares they get the rim colour, taken from the clean rim pixels
   just past each arc, so the rim runs round the corner instead of the frame being smeared outward.
   Elsewhere the clean colours are grown outward (mean of filled 4-neighbours), which extends the
   rim on the straight sides. No parchment or shadow colour is left for the resampler to pick up.
   That is why there is no halo.
3. **Resample** the opaque patch so that the card's measured outline fills 228 × 358 exactly.
   Separable Catmull-Rom (Keys bicubic, a = −0.5), kernel widened when shrinking, results clamped
   to 0–255. The patch is opaque, so premultiplied and straight alpha are the same thing here.
   Alpha is not resampled at all: it comes from the analytic mask in step 4. That gives the same
   result a premultiplied resample of a clean cut-out would, and it makes every file's alpha
   identical.
4. **Alpha**: rounded rectangle 228 × 358, r = 14, 32 × 32 samples per corner pixel, quantised once
   and shared by all files. The straight edges are pixel-aligned and fully opaque.

No filter touches the artwork: no sharpening, no colour change, no clean-up. Measured against the
sheets, the interior colour shift per card is at most 0.17 levels (means −0.08 to −0.02 per
channel). The mean absolute difference is 1.6–1.7 levels (worst card 3.7), which is only the
resampling softness.

## 4 · Backs

The Card Collection mockup shows each back on a light option panel with a drop shadow. The same
measurement was used, with a wider outer window: Himalayan Ivory's first strong ink, its gold
border line, is about 9 px inside the ivory edge. The outline is: shadow, then ivory margin for
Himalayan Ivory; gold border for Classic Crimson; a dark bronze outer line for Kathmandu Gold.

| Back | On the mockup | Scale to 228 × 358 (x / y) | Aspect change |
| --- | --- | --- | --- |
| Classic Crimson | 184.00 × 278.00 | 1.239 / 1.288 | −3.8 % |
| Himalayan Ivory | 183.04 × 278.51 | 1.246 / 1.285 | −3.1 % |
| Kathmandu Gold | 185.00 × 278.62 | 1.232 / 1.285 | −4.1 % |

The backs are drawn about 3–4 % wider in proportion than the faces, so they get a slight
non-uniform resample. They are small on the mockup: the vertical upscale is 1.285–1.288, about
3 % over the 1.25× guideline. Making the faces smaller to avoid that would have cost the court
cards detail. The mockup's back corners are rounder (13.5–16.5 mockup px, about 17–21 px at this
size) than the faces' 14 px. They now have the shared 14 px corner, and the thin sliver between
the two arcs is filled with the border colour.

## 5 · QA (all run and looked at)

Automatic, in `report.txt`:

- 52 unique `Card_{Suit}_{Rank}` names per deck, plus the back: yes, for all three decks.
- All 159 files are 228 × 358, 8-bit RGBA, not interlaced; chunks `IHDR sRGB IDAT IEND`.
- The four extreme corner pixels have alpha 0 and the centre pixel alpha 255, in every file.
- No semi-transparent pixel in the interior, and every file's alpha equals the shared mask exactly.
- A saved file read back is identical to what was written (0 pixels differ).
- Installed copies are byte-identical to the exports (hash compare of all 159).
- A suspected-shadow-on-the-rim detector flags a rim pixel that sits on the sheet's shadow colour
  line and is darker than the card 2 px inside. Himalayan Ivory: 0. Classic Crimson back: 165.
  Kathmandu Gold Spades 4: 31. Kathmandu Gold back: 160. All four were inspected at 14× against
  the source. They are the art's own dark outer lines: the Classic Crimson back's darker gold edge,
  Kathmandu Gold's dark-gold outer line on the right side of the face (the same line is on the
  ace), and the Kathmandu Gold back's bronze outline. The detector cannot tell a dark outline from
  shadow, so it is only a pointer.
- Frame alignment: each face's frame profile on each side against its deck's median. Classic
  Crimson is within 1.25 px (90 % of faces within 0.7 px) and Himalayan Ivory within 1.4 px. On
  Kathmandu Gold the top and right reach 2.4 px, because on the Spades sheet the inner keyline
  sits about 2 source px further from the edge than on the other sheets. The offsets group by
  sheet and row, which is how the art was drawn.

Looked at:

- Per deck, contact sheets of all 52 faces and the back on dark (#181818) and on white. No halo,
  no shadow remnant, no clipped frame, no off-centre crop.
- Per deck, 4× zooms of all four corners of 8 faces (Spades A, Hearts 7, Diamonds 4, Clubs 8,
  Spades K, Hearts Q, Diamonds 10, Clubs J: both rows, first and last columns, number and court
  cards) and the back, each on black and on white. The rim runs continuously round every corner,
  with no dark or light fringe. In the first cut the rim was lost round the corners, because the
  corners were cut 1 px inside the arc and filled from the frame. That was fixed with the fitted
  arcs and the rim-colour fill.
- Per deck, the hand A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣ with 50 % of each card showing, on
  #E9D5BC. Every index reads and the frames line up.
- The three backs at full size on black and on white, compared with the mockup.
- Debug views of the clean region with the fitted outline drawn over it, for Classic Crimson
  Hearts K and A, Himalayan Ivory Spades A and 8, and Kathmandu Gold Clubs 7 and K.

## 6 · Differences from the reference, and open points

- **Proportions:** see section 2. One outline for all cards changes each card's aspect by −2.9 % to
  +3.1 %, and the backs' by −3.1 % to −4.1 %.
- **Backs' corners** are 14 px like the faces, less round than on the mockup (about 17–21 px at this
  size).
- **Rim at corners** is continued with the rim colour taken just past the arc, over a band of
  about 1 px. **Partly covered rim pixels** on the sides are replaced by the first fully covered rim
  pixel. This is at most 1 source px, but it is a replacement of source pixels.
- **Sub-pixel accuracy** is about ±0.3 px. It can reach 1 px where Kathmandu Gold's darkest contact
  shadow blends with its dark-gold rim: the per-half checks there disagree by up to 1.3 px.
- **Frames between sheets** differ by up to about 2 px (Kathmandu Gold Spades top). This comes from
  the art, not the cut.
- **Index**: the 44 px minimum rank height in the GDD is for a vector rebuild. Cut from the sheets,
  ranks are 36–48 px (median 40) at 358 px card height, so they reach 44 px only if the card is
  drawn at about 1.1× in the hand. On 4–10 the first pips come up beside the index, within the
  index box's width.
- **Not measured automatically**: the index boxes of 4 Kathmandu Gold faces (Spades 10, Hearts 7, 8
  and 10). Their index suit merges with the neighbouring pip's gold outline. By eye they are within
  the union box.
- **Not verified**: how the sprites look in Unity. They are not yet imported, no atlas or
  compression has been set up, and nothing has been checked on a phone. Straight edges are opaque
  to the texture border, so a rotated card in a fan may need a 1–2 px transparent padding added at
  runtime or in the atlas for smooth edges.
- `ArtSource/Working/Decks/probe/` holds scratch probes from an earlier, stopped attempt. Nothing
  uses it.
