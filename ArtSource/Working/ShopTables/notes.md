# ShopTables: layered gameplay table + shop table previews

Status (4 Oct 2026, 20:45): **all deliverables final** (final/):

| File | Size | What |
| --- | --- | --- |
| `TableMask.png` | 2230 × 1255 | the table's silhouette, white + alpha |
| `Table_HimalayanIvory.png` | 2230 × 1255 | the default table alone (alpha = TableMask) |
| `Table_RoyalCrimson.png` | 2230 × 1255 | crimson felt, gold band/motifs, darker reddish wood; same geometry |
| `Table_HeritageWalnut.png` | 2230 × 1255 | honey-walnut top, dark band with brass key pattern, brass motifs, darker wood; same geometry |
| `BG_HimalayanVista_Back.png` | 2230 × 1255 | the scenery without the table (opaque) |
| `BG_HimalayanVista_Front.png` | 2230 × 1255 | the foreground bushes in front of the table (transparent elsewhere) |
| `Preview_HimalayanIvory.png`, `Preview_RoyalCrimson.png`, `Preview_HeritageWalnut.png` | 603 × 633 | shop product previews for mockup 21's tiles |

## The frame

Every gameplay layer is in BG_Table's frame: **2230 × 1255 px**, pixel-aligned with
`Assets/_Project/Art/Backgrounds/BG_Table.png`. That frame is the 08-gameplay mockup (1672 × 941) at
1 frame px = 1 mockup px, centred, with margins for 21:9 and 4:3:
**mockup (x, y) = frame (x − 279, y − 157)**.

Stack, back to front: `BG_HimalayanVista_Back` → `Table_*` → `BG_HimalayanVista_Front`.
All three tables share TableMask's alpha exactly, so any table stands in any scenery that leaves
the table area free.

## TableMask.png (final/TableMask.png)

White (255,255,255) with alpha = table coverage (anti-aliased, 16 sub-rows per pixel). Same frame.
Every `Table_*.png` has exactly this alpha (checked: 0 pixels differ).

| What | Frame px |
| --- | --- |
| Bounding box | x 251 – 1978, y 417 – 1254 (the drum runs out of the frame bottom) |
| Area | 1,310,493 px (≈ 47 % of the frame); 1,312,120 px with alpha > 0 |
| Far (top) outline | the lip's top edge, traced on BG_Table; nearest ellipse: centre (1113.7, 733.8), a = 850.2, b = 323.0, but the painted rim deviates from it by up to 11 px (rms 5.4), so use the polygon, not the ellipse |
| Widest point | left x ≈ 251.5 at y ≈ 780; right x ≈ 1978.5 at y ≈ 785 |
| Sides below the rim | measured on BG_Table to y ≈ 920 (left) / 950 (right): the rounded lip curves in to the carved drum |
| Bottom corners (x < 300 or x > 1930, y > 930) | hidden by flowers in BG_Table; the drum continues straight down (left side x 272 → 277, right side x 1946 → 1940 at the frame bottom). ChatGPT's reconstruction of the hidden drum put its sides at the same x within 0–2 px, so the mask stays as it is |
| Outline polygon | `ArtSource/Working/ShopTables/table_outline.json` (frame px, clockwise, closes below the frame) |

### Play area (for card and seat anchors)

The ivory playing surface is the area inside the gold key band. Its inner edge fits the ellipse
**centre (1114.3, 742.2), a = 780.2, b = 301.4** within ±1 px on the near half; on the far half the
painted band bulges out by up to ~12 px in the upper corners (about 210°–225° and 300°–315°,
angles measured with y down). In mockup px: centre (835.3, 585.2).

The rim (key band + lip) is ~29 px deep at the far centre and ~90 px wide at the left/right ends.
At the near centre (x 1115) the surface ends at y ≈ 1039, the gold key band runs 1040–1075, the lip
face 1078–1108, and the carved drum from 1110 to the frame bottom.

## Layers of BG_Table (final/)

| File | What | How |
| --- | --- | --- |
| `Table_HimalayanIvory.png` | the table alone, RGBA, alpha = TableMask | BG_Table's own pixels everywhere except under the flowers. Under them (10.9 % of the table has reconstruction weight > 0.5, 11.7 % > 0) ChatGPT's completed table (chatgpt/Table_Ivory_Complete_a1_s1.png), upscaled ×1.3337, aligned with a smooth local displacement field (measured on 407 clean 48 px tiles, max 3.6 px) and colour-matched with smooth per-channel gain fields. Faint flower haze at the fringe is unmixed instead of replaced. The anti-aliased silhouette edge is decontaminated (premultiplied against what lies behind it) |
| `BG_HimalayanVista_Back.png` | the scenery without the table, RGB | BG_Table outside the table. Inside: ChatGPT's terrace (chatgpt/BG_HimalayanVista_Fill_b2.png, made from a magenta-masked copy at 1672 × 941, so it is aligned to 0–1 px), colour-matched; the bushes inside the old table area are the solved Front colours laid over it. Edge pixels of the silhouette are solved so Back + Table reproduces BG_Table exactly |
| `BG_HimalayanVista_Front.png` | the foreground bushes in front of the table, RGBA | matte from flowers.js (colour rules for red/orange petals and green/olive leaves inside two hand-drawn corner zones, closed, small dark gaps filled, a few missed petal tips added by hand, defocus blur r 6; see Rebuilding below); colours solved as F = (BG − (1 − α)·L) / α over L = Back + Table. Transparent everywhere else (0 px outside the two corner zones) |

Checks (scripts/verify.js, composite in 8-bit straight alpha, like the files):

- Back + Ivory table + Front vs BG_Table: **mean 0.0047 levels per channel, max 0.93 levels**
  (rounding only; 0 pixels off by more than 2 levels). Outside the table: exactly 0.
- Table vs BG_Table where the table is opaque, not reconstructed and has no flowers: mean 0, max 0
  (3,467,988 channels).
- A test composite with the table recoloured cold blue (scripts in the scratchpad) showed no ivory
  remnants in the Front and clean far edges.

Known limits:

- The terrace under the old table is ChatGPT's painting; the Back alone shows a vertical seam at the
  silhouette's left edge (x ≈ 255, y 700–1000) where BG_Table's terrace has the table's shadow and the
  fill is lit. It is never visible: every table covers it.
- In the Front's thin fringe, where a bush is semi-transparent over the drum, the solved colours
  carry a little of the ivory table's drum (e.g. x 1740–1800, y 1040–1080). Over the Crimson and
  Walnut drums (same geometry, similar dark wood) this is not noticeable; over a very different colour
  it shows as a faint ghost of the carving.
- Under the densest flowers the drum is ChatGPT's re-drawn carving, which is softer than BG_Table's
  (it was generated at 1672 px and upscaled).

## How the outline was traced (scripts/)

- `rays.js`: inner edge of the key band, robust conic fit.
- `outer4.js`: the lip's outer edge = the dark groove outside the key band + a lip depth fitted as a
  smooth function of angle (Fourier, 4 harmonics) to the strongest edges; checked against
  hand-measured luminance profiles at the top centre (y 417.8), left (x 264.5 at y 713) and right
  (x 1952.5 at y 685; 1975 at y 742): all within 1 px.
- `build_mask.js`: far outline + measured side points + corners → polygon → anti-aliased mask.
- `flowers.js`, `warp_table.js`, `build_layers.js`, `verify.js`: the layer split and its checks
  (`node build_layers.js` with GATE=0.04 R0=0.05 R1=0.35 DD=22; flowers.js with DEF=6).

## Royal Crimson and Heritage Walnut (gameplay)

Built on the Ivory table's geometry (scripts/zones2.js + scripts/variant2.js, configs
scripts/cfg_RoyalCrimson.json and cfg_HeritageWalnut.json), from ChatGPT's material edits of the Ivory
table (chatgpt/Table_RoyalCrimson_c2.png, chatgpt/Table_HeritageWalnut_w1.png):

- zones of the Ivory: playing surface, key band (found geometrically: from the surface edge to the
  band's outer cream border line, width fitted as a smooth function of angle, rms 0.9 px), knot
  motifs, lip + drum;
- surface: the target colour × the Ivory's own low-frequency shading + ChatGPT's felt / grain detail;
- band and motifs: ChatGPT's pixels, aligned to the Ivory (global shift + smooth local warp), re-coloured
  with a two-colour palette map to the target ground/line colours;
- lip and drum: the Ivory's own pixels × a per-channel gain measured on mockup 21 (lip and drum
  ratios of each table to the Ivory), so the carving and the lighting are the Ivory's exactly.

Targets: mockup 21's colours relit into the game's light (the in-game Ivory surface is the mockup's
× 0.948, 0.882, 0.862 per channel). Crimson felt (151.6, 17.6, 24.9); walnut top (141.7, 78.8, 48.9);
see ArtSource/Prompts/ShopTables.md for the rest.

Checks:

| | alpha px ≠ TableMask | alpha IoU / centroid offset | ChatGPT's own silhouette IoU | ChatGPT local shifts (corrected) |
| --- | --- | --- | --- | --- |
| Royal Crimson | 0 | 1.000000 / (0.000, 0.000) | 0.9952 | rms 1.1 px, max 2.5 px; 264 of 280 tiles exact after the warp, 15 within 1 px |
| Heritage Walnut | 0 | 1.000000 / (0.000, 0.000) | 0.9968 | rms 1.2 px, max 3.6 px; 249 of 303 exact, 46 within 1 px, 8 within 1.5–2 px |

Both were also composited as Back + table + Front (no visible seams or halos at the bushes).

## Shop previews (mockup 21)

Canvas = the tile's picture area, **262 × 275 mockup px × 2.3 = 603 × 633 px**, centred on the tile
(x = tile centre − 131, y 262–537): tile 1 [401.85, 262, 262, 275], tile 2 [686.65, 262, 262, 275],
tile 3 [973.85, 262, 262, 275] (tile rects from ShopTablesLayout). It holds
the table (mockup x 408.5–656.5, y 279.5–487.5 in tile 1, the same tile-relative box in tiles 2 and 3)
and its contact shadow down to y 537, just above the name label.

- Note for the Shop builder: ArtSource/Screens/ShopTablesLayout uses TablePicture rects 256 px tall
  (y 262–518) and 266 px wide for tile 2; with these sprites use 262 × 275 for all three, or the
  shadow is cut and tile 2's table is stretched by 1.5 %.
- Made from ChatGPT's render of the mockup's Ivory preview (chatgpt/Preview_HimalayanIvory_p1.png) and
  its material edits of that render (p2 crimson, p3 walnut). Green keyed, placed so the table's bbox
  equals the mockup's (x scale 0.4785, y scale 0.4958: ChatGPT drew the table 3.6 % flatter than the
  mockup, so it is stretched by 3.6 % vertically).
- All three use the Ivory render's alpha (0 pixels differ between the three); the variants were
  aligned to it (silhouette IoU 0.9984 before, 0.9986 after; local shifts ≤ 1.4 px).
- Colour matched to each tile of mockup 21 per zone (top surface, key band ring, body): per-channel
  mean gains; on the top only pixels near the surface colour are corrected, so motifs keep their
  gold/brass. Result at mockup scale (both lightly blurred): mean signed difference ≤ 2.0 levels per
  channel; mean |difference| 12.5 (Ivory), 14.9 (Crimson), 14.3 (Walnut), which is the redrawn detail
  (the mockup is an AI image at 1×), not colour.
- Contact shadow: one soft ellipse for all three (colour 52, 36, 26; centre (299.6, 493.2), semi-axes
  (255.4, 64.9), blur σ 51, opacity 0.68, in canvas px), fitted to the darkening around tile 1's table
  (26,403 samples, rms 0.05 in alpha), faded to 0 over the canvas's outer 18 px.

## ChatGPT chats

1. https://chatgpt.com/c/6ac24bad-85e8-83e8-aaea-4a73d78c5ef2 — prompts 1–4 (hidden drum, terrace,
   Royal Crimson c1). It then would not load for about 40 minutes (HTTP 429 on every load).
2. https://chatgpt.com/c/6ac25e19-26b4-83e8-b4d6-059a271fa6bb — prompts 5–9 (Royal Crimson c2,
   Heritage Walnut w1, the three previews). Same rate limiting on reloads; it loads on the first try
   after a few minutes.

The owner's "CardGame" project no longer exists in this account (its URL says "This project can't be
found"), so both are normal chats.

## Rebuilding (all finals reproduce byte for byte; checked 4 Oct 2026, 20:43)

From `ArtSource/Working/ShopTables/scripts` (node, no packages), with
`BG=Assets/_Project/Art/Backgrounds/BG_Table.png`, `M21=Assets/ArtRefrences/game-screens/21-table-styles.png`
(full paths), `W=../work`, `F=../final`, `C=../chatgpt`:

```
node build_mask.js outer4_trace.json $W/mask corners_v1.json                 # -> TableMask.png
DEF=6 node flowers.js $BG $W/flower_alpha.bin "" $F/TableMask.png            # bush matte
node warp_table.js $BG $F/TableMask.png $C/Table_Ivory_Complete_a1_s1.png $W/flower_alpha.bin $W/Table_Ivory_a1_s1_warped.png
GATE=0.04 R0=0.05 R1=0.35 DD=22 node build_layers.js $BG $F/TableMask.png $W/Table_Ivory_a1_s1_warped.png \
    $C/BG_HimalayanVista_Fill_b2.png $F $W $W/flower_alpha.bin                # Back, Ivory table, Front
node verify.js $BG $F/BG_HimalayanVista_Back.png $F/Table_HimalayanIvory.png $F/BG_HimalayanVista_Front.png $W/recon.bin
node zones.js $F/Table_HimalayanIvory.png $W/zones && node zones2.js $F/Table_HimalayanIvory.png $W/zones
node variant2.js $F/Table_HimalayanIvory.png $W/zones $C/Table_RoyalCrimson_c2.png $F/Table_RoyalCrimson.png cfg_RoyalCrimson.json
node variant2.js $F/Table_HimalayanIvory.png $W/zones $C/Table_HeritageWalnut_w1.png $F/Table_HeritageWalnut.png cfg_HeritageWalnut.json
node preview.js $M21 $F $W/previews $C/Preview_HimalayanIvory_p1.png HimalayanIvory \
    $C/Preview_RoyalCrimson_p2.png RoyalCrimson 1 $C/Preview_HeritageWalnut_p3.png HeritageWalnut 2
```

The large intermediates (*.bin: mattes and zone weights, 11 MB each) were deleted; the commands above
recreate them. Other scripts are the measuring tools used on the way (outer*.js, rays.js, edges.js,
view.js, overlay.js, sheet.js, register.js, localshift.js, features.js, alpha_proj.js, refine_matte.js,
make_masked.js, table_clean.js, variant.js).
