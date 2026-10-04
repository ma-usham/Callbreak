# Shop tables: layered gameplay table, scenery layers, table previews

Source: `Assets/_Project/Art/Backgrounds/BG_Table.png` (2230 × 1255) and the shop mockups
`Assets/ArtRefrences/game-screens/20-backgrounds.png`, `21-table-styles.png` (1672 × 941).
Spec: `D:\Nepali Callbreak\game-screens\shop-drawer-and-tables-prompts.md`.
Files: `ArtSource/Working/ShopTables/` (attach/ = what was sent, chatgpt/ = raw downloads, final/).

ChatGPT, 4 Oct 2026. Two chats (both archived at the end):
1. https://chatgpt.com/c/6ac24bad-85e8-83e8-aaea-4a73d78c5ef2 — prompts 1–4. Opened with "hi"
   (reply: "Hi! What can I help you with today?"). After prompt 4 every load of it returned HTTP 429
   for about 40 minutes, so a second chat was opened.
2. https://chatgpt.com/c/6ac25e19-26b4-83e8-b4d6-059a271fa6bb — prompts 5–9. Opened with "hi" (reply:
   "Hi! 👋 How can I help you today?").
The owner's "CardGame" project no longer exists in this account ("This project can't be found"), so
both are normal chats. Model and thinking setting left as they were ("Extra High").

| Asset | Prompt | Attempt used |
| --- | --- | --- |
| Table_HimalayanIvory (hidden drum) | 1 | a1 s1 |
| BG_HimalayanVista_Back (terrace) | 3 | b2 (b1 not usable) |
| BG_HimalayanVista_Front | none (solved from BG_Table) | — |
| Table_RoyalCrimson | 5 | c2 (c1 not used) |
| Table_HeritageWalnut | 6 | w1 |
| Preview_HimalayanIvory / RoyalCrimson / HeritageWalnut | 7 / 8 / 9 | p1 / p2 / p3 |

## Prompt 1 (a1): complete the hidden apron, attachment `attach/Table_Ivory_cutout_green.png`

The table cut out of BG_Table with TableMask v1, on flat #00FF00, 2230 × 1255 (ChatGPT shows the
attachment as 2048 × 1152).

```
The attached image (Table_Ivory_cutout_green.png, 2230 x 1255) is a cut-out of the carved card table from my game's background painting, placed on a flat pure-green (#00FF00) background. At the bottom-left and bottom-right of the table, blurred out-of-focus red flowers and dark green leaves from the foreground cover the table's carved wooden apron and the lower part of its rounded wooden lip.

Remove those flowers and leaves completely and repaint the table underneath them, so the whole table is visible and sharp:
- Continue the carved walnut apron exactly as it looks in the middle: the plain polished band under the rim, the band of scroll carving, and the plain bands below it, running around the drum to its left and right sides and down to the bottom edge of the image.
- Continue the rounded wooden lip and its highlights around the lower left and lower right ends of the table, where it curves in to the apron.
- Keep the table's outline exactly where it is: its sides go straight down to the bottom edge of the image. Do not change the shape, size or position of the table.

Keep every part of the table that is not covered by flowers exactly as it is: the ivory playing surface, the gold key-pattern band, the gold motifs, the wood colour, the lighting and the carving. Same image size (2230 x 1255) and the same framing. Keep the background flat pure green #00FF00 everywhere outside the table: no shadows, no flowers, no leaves, no scenery, no text.
```

ChatGPT worked 2 min 38 s and returned a series of three 1672 × 941 images (0.75 of the frame).
Downloaded: `chatgpt/Table_Ivory_Complete_a1_s1.png` ("Ornate Ivory and Walnut Table on Green-1"),
`chatgpt/Table_Ivory_Complete_a1_s2.png` ("Ornate Carved Drum Table on Green Screen").
**s1 used** (only under the bushes, 11.7 % of the table): it kept the outline (sides within 0–2 px of
TableMask) and the carving bands continue cleanly; aligned to BG_Table by a smooth local warp (407 clean
tiles, shifts up to 3.6 px) and colour-matched. s2 not used.

## Prompt 2 (b1): the scenery without the table, attachment `attach/BG_Table.png`

```
Thank you, that works. Next request, a new edit of a different image.

The attached image (BG_Table.png, 2230 x 1255) is my game's background painting: a sunny stone terrace above a lake town with snowy Himalayan peaks, and a big round carved card table in the middle of the terrace.

Make a version of this exact painting WITHOUT the table:
- Remove the round carved table completely (its ivory top, gold rim and the whole carved wooden drum down to the bottom edge).
- Also remove the blurred, out-of-focus red flowers and green leaves in the bottom-left and bottom-right foreground corners.
- Where the table and those foreground flowers were, continue the pale stone-paved terrace floor naturally: the same stone slabs and paving pattern in the same perspective, the slabs getting larger toward the bottom edge, the same warm late-afternoon light and soft shadows. Behind where the far edge of the table was, continue the low stone walls, stone posts and flowering shrubs exactly as they continue at the sides.

Keep everything else exactly as it is, in the same place and the same colours: the sky and clouds, the mountains, the lake and town, the pagoda, the trees, the prayer flags, the stone walls and the flowering shrubs at the sides. Do not re-frame, crop, zoom or shift anything. Same 16:9 framing and the same image size (2230 x 1255). No table, no new objects, no people, no text.
```

ChatGPT made the terrace as a re-framed, zoomed-out repaint (1672 × 941, walls moved, scale ≈ 0.94):
`chatgpt/BG_HimalayanVista_NoTable_b1.png` ("Himalayan Terrace Overlooking Lake and Peaks"). Not usable
for layering (tile registration found no consistent offset), so the next prompt masks the area instead.

## Prompt 3 (b2): fill only the masked area, attachment `attach/BG_HimalayanVista_masked_1672.png`

BG_Table scaled to 1672 × 941 with the table (TableMask dilated 3 px) and the two foreground bushes
painted flat #FF00FF.

```
Thank you. That result re-drew and re-framed the whole scene (it is zoomed out and the walls moved), so I can't layer it with the original. Let's do it differently.

The attached image (BG_HimalayanVista_masked_1672.png, 1672 x 941) is the same background painting, already at your output size, with the table and the blurred foreground flowers painted over in flat magenta (#FF00FF).

Fill ONLY the magenta area with what is behind the table, continuing the scene seamlessly from the pixels around it:
- the pale stone-paved terrace floor, with the same stone slabs and joints as the visible paving at the left and right, in the same perspective, the slabs getting larger toward the bottom edge, in the same warm late-afternoon light and soft shadows;
- along the top of the magenta area: the bases of the low stone walls, stone posts and flowering shrubs that are cut off there, continued naturally; in the middle, where only distant trees and the lake are visible above the magenta, the far edge of the terrace with a low stone parapet whose top stays just below those trees.

Do not change any pixel outside the magenta area: keep the sky, mountains, lake, town, pagoda, trees, prayer flags, walls and shrubs exactly as they are, in the same place, at the same size and in the same colours. Same 1672 x 941 size and framing: no zoom, crop or shift. No table, no flowers in the foreground, no new objects, no people, no text, and no magenta left anywhere.
```

Result `chatgpt/BG_HimalayanVista_Fill_b2.png` ("Himalayan Terrace Mountain Vista"): aligned with BG_Table
to 0–1 px on every scenery tile (it kept the unmasked pixels in place), terrace and parapet continued,
no magenta left. **Accepted** (only its pixels inside the table area are used).

## Prompt 4 (c1): Royal Crimson, attachment `attach/Table_HimalayanIvory_green.png` (2230 × 1255)

```
Perfect, thank you. Now a material variant of the table.

The attached image (Table_HimalayanIvory_green.png, 2230 x 1255) is the finished game table alone on flat pure green (#00FF00). Make the "Royal Crimson" version of this exact table by changing ONLY its materials:
- The large oval playing surface: premium deep-crimson felt (rich, deep red, fine soft felt texture, calm and uncluttered, evenly lit like the ivory top is now).
- The gold key-pattern band around the playing surface and the small gold knot motifs near its edge: keep them as restrained gold geometric detail, exactly where they are and the same size.
- The rounded rim lip and the whole carved drum below it: dark polished carved wood (deep brown, almost black-brown, with soft polished highlights), with the same carving.

Keep the geometry identical: the same silhouette and outline, the same perspective and size, the rim, lip, bands and carving in exactly the same places, the same lighting direction and highlights. Do not move, resize, re-frame or redesign anything; do not add or remove ornaments. Same 2230 x 1255 canvas and framing, the same flat pure green (#00FF00) everywhere outside the table, no shadows, no text.
```

ChatGPT generated an image, but the chat then stopped loading: every load of the conversation
returned HTTP 429 (rate limited) for more than 30 minutes, so the image could not be downloaded.

## Second chat (the first one would not load any more)

https://chatgpt.com/c/6ac25e19-26b4-83e8-b4d6-059a271fa6bb — opened with "hi" (reply: "Hi! 👋 How
can I help you today?"). Model and thinking setting left as they were ("Extra High").

### Prompt 5 (c2): Royal Crimson, attachments `attach/Table_HimalayanIvory_green_1672.png` (the table on green at 1672 × 941) and `attach/Mockup21_tables_x2.png` (mockup 21's three table previews, ×2)

```
I'm making material variants of a card table for my game. Image 1 (Table_HimalayanIvory_green_1672.png, 1672 x 941) is the finished table alone on flat pure green (#00FF00). Image 2 is the shop's product reference for three versions of this table; the middle one is "Royal Crimson".

Edit Image 1 into the "Royal Crimson" version of this exact table by changing ONLY its materials, matching the middle table of Image 2:
- The large oval playing surface: premium deep-crimson felt (rich deep red, fine soft felt texture, calm and uncluttered, evenly lit in the same warm light as Image 1).
- The key-pattern band around the playing surface and the small knot motifs near its edge: restrained gold geometric detail on the crimson, like Image 2, exactly where the band and motifs are now and the same size.
- The rounded rim lip and the whole carved drum below it: dark polished carved wood (deep brown, almost black-brown, soft polished highlights), with exactly the same carving.

Keep the geometry identical to Image 1: the same silhouette and outline, the same perspective and size, the rim, lip, bands, motifs and carving in exactly the same places, the same lighting direction and highlights. Do not move, resize, re-frame or redesign anything, and do not copy Image 2's camera angle or legs. Same 1672 x 941 canvas and framing as Image 1, the same flat pure green (#00FF00) everywhere outside the table, no shadows, no text.
```

Result `chatgpt/Table_RoyalCrimson_c2.png` ("Royal Crimson Ornate Tabletop"): silhouette IoU with
TableMask 0.9952 after alignment; band, knot motifs and drum carving in the same places (local shifts
rms 1.1 px, max 2.5 px, corrected by the warp). **Used** for the crimson felt texture and the band and
motif colours (see "Building the variants" below).

When the first chat could be loaded again, its result of Prompt 4 was also downloaded:
`chatgpt/Table_RoyalCrimson_c1.png` ("Ornate Crimson Velvet Pedestal on Green"). Not used.

### Prompt 6 (w1): Heritage Walnut, same two attachments as Prompt 5

```
Excellent, that is exactly right. Now the third version, from the same two images again (Image 1 = the ivory table on green, Image 2 = the shop reference; this time match its RIGHT table, "Heritage Walnut").

Edit Image 1 into the "Heritage Walnut" version of this exact table by changing ONLY its materials:
- The large oval playing surface: a rich honey-walnut wooden tabletop with a fine natural walnut grain, calm matte finish, evenly lit in the same warm light as Image 1, uncluttered.
- The key-pattern band around the playing surface: a dark carved Newari-inspired rim band with a subtle brass inlay of the same key pattern, exactly where the band is now and the same size. The small knot motifs near the edge of the top: subtle brass inlay, exactly where they are now and the same size. Keep the brass confined to the band and these motifs.
- The rounded rim lip and the whole carved drum below it: dark walnut wood with the same carving, like the right table of Image 2.

Keep the geometry identical to Image 1: the same silhouette and outline, the same perspective and size, the rim, lip, bands, motifs and carving in exactly the same places, the same lighting direction and highlights. Do not move, resize, re-frame or redesign anything, and do not copy Image 2's camera angle or legs. Same 1672 x 941 canvas and framing as Image 1, the same flat pure green (#00FF00) everywhere outside the table, no shadows, no text.
```

Result `chatgpt/Table_HeritageWalnut_w1.png` ("Heritage Walnut Ornate Tabletop on Green Screen"): silhouette
IoU 0.9968; local shifts rms 1.2 px, max 3.6 px (corrected). **Used** like c2.

## Building the variants (scripts/zones2.js, scripts/variant2.js)

ChatGPT's variants are re-drawings, so they are not used as they are. Each final table is built on the
Ivory table's geometry, per material zone of the Ivory (zones2.js: the key band found geometrically
between the surface edge and its outer cream border line; knot motifs; surface; lip + drum):

- surface: the target colour × the Ivory's own low-frequency shading (60 px) + the variant's texture
  (variant minus its 40 px low-pass; 90 px for the walnut grain), scaled to the target's luminance;
- band and knot motifs: the variant's aligned pixels, re-coloured by a two-colour palette map from the
  variant's own ground/line colours to the target ground/line colours (shading kept);
- lip and drum: the Ivory's own pixels × a per-channel gain, so the carving is identical.

Alpha = TableMask for all three. Target colours are mockup 21's, relit into the gameplay light by the
ratio in-game Ivory surface / mockup Ivory surface (0.948, 0.882, 0.862):

| | surface | band ground | band lines / motifs | wood gain (lip+drum, from mockup 21) |
| --- | --- | --- | --- | --- |
| Royal Crimson | 151.6, 17.6, 24.9 | 138, 16, 19 | 223, 146, 93 | 0.985, 0.872, 0.865 |
| Heritage Walnut | 141.7, 78.8, 48.9 | 98, 50, 28 | 206, 146, 89 | 0.866, 0.881, 0.874 |

ChatGPT's raw colours, for the record: crimson felt mean (169, 4.8, 15.5) (the usual over-saturation),
walnut top (151, 82, 46).

## Shop previews (second chat)

### Prompt 7 (p1): Himalayan Ivory preview, attachment `attach/Mockup21_Ivory_preview_x4.png`

Mockup 21's tile-1 picture area [402, 268, 262, 272] enlarged ×4 (reference only; the art is generated).

```
Thank you, both are exactly right. Now a different kind of image for the same game: the shop's product previews of these tables.

The attached image (Mockup21_Ivory_preview_x4.png) is the shop screen's current preview of the default table "Himalayan Ivory", cropped from the shop mockup and enlarged, so it is small and soft. Recreate this exact product as a clean, sharp, high-resolution product image:
- the same table design: a broad oval top with a warm-ivory playing surface, a gold key-pattern band around it, four gold diamond knot motifs at its edge (front, back, left, right), a polished carved walnut rim, a carved apron with geometric diamond panels, and four short carved square legs;
- the same slightly elevated 3/4 front camera, the same proportions and the same soft warm studio light from the upper left;
- isolated on a flat pure green (#00FF00) background: no floor, no shadow, no reflection, no text, nothing else;
- a square canvas, the table centred and filling about 90% of the width.
It will be shown small in a shop tile, so keep the silhouette and the carving clear and the playing surface calm and uncluttered.
```

Result `chatgpt/Preview_HimalayanIvory_p1.png` ("Ornate Ivory Inlay Game Table", 1254 × 1254): the mockup's
design, table 1192 × 965 px (3.6 % flatter than the mockup's 248 × 208). **Used** (master geometry).

### Prompt 8 (p2): Royal Crimson preview, attachments `chatgpt/Preview_HimalayanIvory_p1.png` and `attach/Mockup21_tables_x2.png`

```
Beautiful, that is exactly right. Now the two other previews, made from this same image so all three match. Image 1 is the preview you just made (Preview_HimalayanIvory_p1.png). Image 2 is the shop reference with the three tables.

Edit Image 1 into the "Royal Crimson" version of this exact table, matching the MIDDLE table of Image 2, by changing ONLY its materials:
- the oval playing surface: premium deep-crimson felt, calm and evenly lit;
- the key-pattern band and the four diamond knot motifs: restrained gold geometric detail on the crimson, exactly where they are now and the same size;
- the rim, the carved apron and the legs: dark polished carved wood, with exactly the same carving.
Keep the geometry identical: the same silhouette, size, position and camera, the same carving and lighting. Same square canvas and the same flat pure green (#00FF00) background, no shadow, no text.
```

Result `chatgpt/Preview_RoyalCrimson_p2.png` ("Royal Crimson Ornate Game Table"): silhouette IoU with p1
0.9984. **Used.**

### Prompt 9 (p3): Heritage Walnut preview, same attachments as Prompt 8

```
Perfect. Now the third preview from the same Image 1 (the ivory preview, Preview_HimalayanIvory_p1.png), matching the RIGHT table of Image 2, "Heritage Walnut", by changing ONLY its materials:
- the oval playing surface: a rich honey-walnut wooden top with a fine natural grain, calm matte finish;
- the key-pattern band: a dark carved Newari-inspired band with a subtle brass inlay of the key pattern; the four diamond knot motifs: subtle brass inlay; brass only on the band and these motifs;
- the rim, the carved apron and the legs: dark walnut wood with exactly the same carving.
Keep the geometry identical: the same silhouette, size, position and camera, the same carving and lighting. Same square canvas and the same flat pure green (#00FF00) background, no shadow, no text.
```

Result `chatgpt/Preview_HeritageWalnut_p3.png` ("Ornate Walnut Table with Gold Inlay"): silhouette IoU with
p1 0.9984. **Used.**

Post-processing (scripts/preview.js): green key, placement on the tile's picture area (603 × 633),
the master's alpha for all three, per-zone colour match to each tile of mockup 21, one fitted soft
contact shadow. Details and numbers in ArtSource/Working/ShopTables/notes.md.
