Accepted: P1 a1 (composition), P2 a1 (plate centre), strips L a1 / R a1 / T a1, F1 a1, T1 a1
Finals: `ArtSource/Working/ShopBackgroundsB/final/BG_MonasteryGarden_Back.png`, `_Front.png`, `Thumb_MonasteryGarden.png`
(copies in `ArtSource/Exports/Backgrounds/` and `ArtSource/Exports/Screens/Shop/`)

# Shop background 6: Monastery Garden (gameplay plate, foreground layer, shop tile)

Agent: ShopBackgroundsB, 4 Oct 2026. Working files: `ArtSource/Working/ShopBackgroundsB/`.
Chat (the same single chat as Pokhara Lakeside): https://chatgpt.com/c/6ac24827-71e4-83ee-85c7-ba5e04d26741
Method, frame and scripts: see `Shop_PokharaLakeside.md` (same steps).

Spec: `D:\Nepali Callbreak\game-screens\shop-backgrounds-prompts.md` ("Monastery Garden: calm sunlit
garden with a white Buddhist stupa, colorful prayer flags, marigolds and mountain foothills").

Placement: the stupa stands right of centre (view x ~1150-1320) so that in gameplay it is visible
between the top seat (view x 715-960) and the Scores/Settings buttons (x 1420+), below the round
text; the monastery building is at the left, below the logo. Its platform ends at view y ~255, above
the table's far rim there (y 275-306), so nothing important is cut by the table.

## P1: the scene around the table (`chatgpt/Monastery_WithTable_a1.png`, 1672 x 941)

Attachment: `attach/BG_Table_view.jpg`.

```
Thank you, Pokhara Lakeside is done. Now a second background for the same table: "Monastery Garden".

Attached again: the current Himalayan Vista background (BG_Table_view.jpg, 1672 x 941), the reference for the table, the camera and the style.

Make a NEW background for the same table in exactly the same painterly style, finish, detail and colour richness.

Keep exactly as they are:
- The table: same position, size, oval shape, perspective, ivory top, gold rim and carved wood. It marks where the table stands.
- The camera: same viewpoint, same horizon height, same perspective and scale, same 16:9 framing, same image size (1672 x 941).
- The light: warm late-morning sunlight from the upper left.

Replace everything around the table with a calm, sunlit Buddhist monastery garden in the Himalayan foothills:
- The table stands on a flat, open, sunlit garden lawn of short green grass; the lawn is level and continues under and around the table.
- Right of centre (where the highest peak is in the attached image), a gleaming white Buddhist stupa stands in the middle distance on a low terrace behind the garden: a whitewashed dome on a stepped square base, a gilded square tower painted with the Buddha's eyes, and a tall golden spire with a small golden parasol on top. It is fully visible, its base above the table's far edge, never behind the table.
- Strings of colourful prayer flags (blue, white, red, green, yellow) run down from the top of the stupa's spire to the garden on both sides, and a string of prayer flags crosses the upper left as in the attached image.
- Beds of bright orange and yellow marigolds along the garden's edges on the left and right, with a few small flowering trees and shrubs.
- On the left in the middle distance: a small Tibetan-style monastery building (whitewashed walls, dark red window frames, a golden roof ornament), partly behind trees.
- Behind the garden: green rolling mountain foothills with terraced slopes, and distant snowy Himalayan peaks under a clear blue sky with a few soft white clouds.
- In front of the table's two bottom corners, soft out-of-focus foreground marigolds (orange and yellow, with green leaves) frame the table the way the red flowers do now. Keep the bottom centre of the image clear of them.

Calm, sunlit and peaceful. No people, no monks, no animals, no text, no letters or writing anywhere (none on the flags either), no logos, no UI, no playing cards.
```

Result: 1672 x 941. The scene is right; the table itself came back slightly squeezed (tile shifts on
the rim: far edge dy +19, sides dy +4..+5, near edge dy +1), which does not matter because the table
is removed in the next step and the real table layer is placed by TableMask.

## P2: the same picture without the table (`chatgpt/Monastery_NoTable_a1.png`, 1672 x 941)

```
Very good, keep this scene. Next step: the same picture WITHOUT the table and WITHOUT the foreground marigolds.

Start from your last image (not from the attached Himalayan Vista image):
- Remove the card table completely: its ivory top, gold rim and carved wooden body.
- Where the table stood, continue the flat, level green garden lawn naturally, in the same perspective: the lawn runs back to the low stone terrace wall under the stupa and to the marigold beds at the left and right, and fills the whole lower part of the picture down to the bottom edge. Keep the lawn open and plain: no paths, no objects, no flower beds in the middle, no table shadow.
- Also remove the out-of-focus orange and yellow marigolds in front of the table's bottom corners (bottom left and bottom right); continue the lawn there too. Keep the marigold beds further back along the garden's edges.
- Keep everything else exactly as it is, at the same pixel positions: the stupa, its spire and prayer flags, the monastery building, the trees and blossoms, the foothills, the snowy peaks, the sky and clouds. Same camera, horizon and light, same image size 1672 x 941.

No text, no people, no UI.
```

Result: scenery in place (tile shifts dx 0/-1, dy -1/0 on six tiles: sky, peaks, stupa, monastery).
While it was generating the conversation answered HTTP 429 (rate limit) for a few minutes; the
request was not repeated. Used 1:1 as the plate centre at (279, 157).

## Margins (strip edits, `scripts/strip-canvas.ps1`, `scripts/assemble-back.ps1`)

| Attempt | Input | Fit to its canvas | Verdict |
| --- | --- | --- | --- |
| `Monastery_Left_a1.png` (1671 x 941) | `attach/Monastery_Left_input.png` | s 1.000, shift 0.5, 16.5 px (came back 16 px lower), NCC 0.47; only the strip and a seam band are used | Accepted |
| `Monastery_Right_a1.png` | `attach/Monastery_Right_input.png` | s 1.000, shift 0, 0, NCC 0.94 | Accepted |
| `Monastery_Top_a1.png` | `attach/Monastery_Top_input.png` | s 1.000, shift -0.5, 0, NCC 0.94 | Accepted |

Left prompt:

```
Very good. Now the margins for wider and taller phone screens, one side at a time, as edits. LEFT side first.

The attached image (Monastery_Left_input.png, 1672 x 941) is the monastery garden picture without the table at its original size, shifted 279 px to the right. The strip along the left edge (x 0 to 278) is only a blurred placeholder.

Edit this image: repaint ONLY that blurred strip as finished painting in the same style and detail, continuing the scene to the left: more of the leafy tree at the left edge (trunk and foliage up to the top edge), the string of prayer flags continuing up and to the left, pink flowering trees, the marigold bed and the sunlit green lawn below.

Keep everything right of x 279 exactly as it is, pixel for pixel: same position and size, nothing moved, scaled, zoomed or repainted (the monastery building stays at about x 410 to 640, y 120 to 280; the tip of the stupa's spire at about x 1514, y 12). Same 1672 x 941 image. No new landmarks, no text, no people, no frame.
```

Right: "Thank you. Now the RIGHT side, the same way. ... shifted 279 px to the left. The strip along the
right edge (x 1393 to 1671) ... continuing the scene to the right: the prayer flags from the stupa's
spire continuing down to the right, more pink flowering trees and evergreens, the marigold beds and
the terrace wall, the foothills and snowy peaks under the blue sky, and the sunlit green lawn below.
Keep everything left of x 1393 exactly as it is ... (the tip of the stupa's spire stays at about
x 956, y 12; the stupa's white dome at about x 885 to 1025, y 160 to 225)."
Top: "Thank you. Last one for this scene: the TOP, the same way. ... shifted 157 px down. The strip along
the top edge (y 0 to 156) ... continuing the scene upward: more of the same clear blue sky with a few
soft white clouds, the upper foliage of the tree at the left, and the string of prayer flags at the
top left continuing upward. The stupa's golden spire and the prayer-flag lines from its top stay as
they are. Keep everything below y 157 exactly as it is ... (the tip of the stupa's spire stays at
about x 1235, y 169; the monastery building at about x 130 to 360, y 277 to 437)."

Assembly: colour fits on the overlap (low-frequency gain/offset) left R 0.84 / G 0.90 / B 0.91 (the
left output came back lighter), right 1.00 / 1.02 / 0.97, top 0.95 / 0.90 / 0.91. Seams at plate
x 363-511 (left), 1711-1800 (right), y 157-216 (top). Bottom margin and top corners filled from the
plate (never visible: under the table and Front, or outside every screen shape).

## F1: the foreground layer (`chatgpt/Monastery_Front_a1.png`, 1672 x 941, real alpha)

Attachment `attach/Monastery_Front_guide.jpg` (plate + BG_Table's table, 75%).

```
Very good, that is exactly right. Now the same foreground layer for Monastery Garden.

The attached image (Monastery_Front_guide.jpg, 1672 x 941) is the whole Monastery Garden plate with the table in place, scaled to 75%. Ignore the red flowers at the table's bottom corners in this guide; they belong to another background.

Make a PNG with a real transparent background, exactly the same size and framing (1672 x 941), containing ONLY soft, out-of-focus foreground marigolds very close to the camera that frame the table's two bottom corners, like the foreground marigolds in your first Monastery Garden picture with the table:
- Bottom left: a lush cluster of bright orange and yellow marigold blooms with green leaves, rising from the canvas's bottom-left corner: about 500 px wide along the bottom edge and about 450 px tall at the left edge, overlapping the table's lower-left rim.
- Bottom right: a similar but not mirrored cluster rising from the bottom-right corner: about 480 px wide and about 430 px tall at the right edge, overlapping the table's lower-right rim.
- Keep the bottom centre (x 520 to 1150) and the whole table top completely clear.

Everything else fully transparent: real alpha, no background colour, no checkerboard pattern, no ground, no table, no sky. Soft blurred edges, the same warm light from the upper left as the plate.
```

`scripts/fit-front.ps1` as for Pokhara (levels, x1.3337, clusters x0.82 / x0.85 toward their corners):
left box 0,644 706x611, right 1594,754 636x501; 0% of the playing surface and of the bottom centre covered.

## T1: the shop tile picture (`chatgpt/Thumb_Monastery_a1.png`, 1173 x 1341)

Attachments: `attach/20-backgrounds_tiles.png`, `attach/Monastery_Back_view.jpg`.

```
That worked well, thank you. Now the shop tile picture for "Monastery Garden", the same way as the Pokhara Lakeside tile.

Image 1 (20-backgrounds_tiles.png) shows three tiles of the game's shop; their pictures show how a background is previewed. Image 2 (Monastery_Back_view.jpg) is the Monastery Garden background itself, without the table.

Paint the tile picture for "Monastery Garden" so it sits naturally next to the three tile pictures in image 1: the same finish, detail, brightness, saturation and painterly style, and the same kind of framing: a close, eye-level postcard view of the place that fills the whole picture, with sky in the upper part, the landmark large in the middle and its ground in the foreground. Show the same place as image 2: the gleaming white Buddhist stupa with the gilded tower painted with the Buddha's eyes and its golden spire, strings of colourful prayer flags running down from the spire, bright orange and yellow marigold beds and a few pink flowering trees, the green garden lawn in front, green foothills and snowy peaks under a blue sky with soft white clouds, warm daylight from the upper left.

One full-bleed rectangular painting in portrait 7:8 (for example 1176 x 1344 px). No table, no cards, no people, no text or writing anywhere (none on the flags), no caption, no frame, no border, no rounded corners.
```

`scripts/make-thumb.ps1` (centre crop to 590:670, Lanczos-3 to 590 x 670), then luma-preserving
saturation x0.70: sat 0.44, luma 148 (raw 0.59; the plate's scenery is 0.42).

## Checks

`check/MonasteryGarden_check_{full,16x9,21x9,4x3}.png` (Back + `ShopTables/final/Table_HimalayanIvory.png` + Front).
- View (16:9): 72% of its pixels are ChatGPT's no-table picture 1:1; 63% of the scenery visible
  around the table.
- Plate scenery outside the table: luma 151.6, sat 0.423 (BG_Table 156.6, 0.415). Not graded.
- HUD boxes: 16:9 ROUND 185, Scores/Settings 148 (0% dark); 4:3 ROUND 159, Scores/Settings 205;
  21:9 Scores/Settings 134 with 26% dark pixels (trees in the right margin), where BG_Table itself has
  99 and 58%.
- Table on the ground: the terrace wall under the stupa meets the lawn at plate y ~425, far behind the
  floor line under the table's far lip (~720), with open lawn between; nothing stands inside the
  table's outline.
