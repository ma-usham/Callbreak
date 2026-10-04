Accepted: P1 a1 (composition), P2 a1 (plate centre), strips L a2 / R a1 / T a1, TopRight a2, F1 a1, T1 a1
Finals: `ArtSource/Working/ShopBackgroundsB/final/BG_PokharaLakeside_Back.png`, `_Front.png`, `Thumb_PokharaLakeside.png`
(copies in `ArtSource/Exports/Backgrounds/` and `ArtSource/Exports/Screens/Shop/`)

# Shop background 5: Pokhara Lakeside (gameplay plate, foreground layer, shop tile)

Agent: ShopBackgroundsB, 4 Oct 2026. Working files: `ArtSource/Working/ShopBackgroundsB/`
(`chatgpt/` raw downloads, `attach/` what was attached, `scripts/` assembly, `check/` composites,
`notes.md` decisions).
Chat (one chat for all of this agent's work, also Monastery Garden and the Himalayan Vista tile; the
owner's "CardGame" project no longer exists in the account, so it is a plain chat):
https://chatgpt.com/c/6ac24827-71e4-83ee-85c7-ba5e04d26741
Model and thinking setting left as the owner had them ("Extra High").

Spec: `D:\Nepali Callbreak\game-screens\shop-backgrounds-prompts.md` ("Pokhara Lakeside: tranquil lake
with a few small wooden boats, green shore, reflected light and a snowy triangular Himalayan peak
beyond, airy and peaceful"), carousel per `shop-drawer-and-tables-prompts.md`.

## Frame

The plate is BG_Table's frame, 2230 x 1255. The 16:9 gameplay view is the mockup (1672 x 941) at
(279, 157), 1 frame px = 1 mockup px. `attach/BG_Table_view.jpg` is BG_Table cropped there (equal to
08-gameplay.png outside the UI).

## P1: the scene around the table (`chatgpt/Pokhara_WithTable_a1.png`, 1672 x 941)

Attachment: `attach/BG_Table_view.jpg`.

```
Background plates for the game "Callbreak - Play Together", a Nepali card game for phones in landscape.

The attached image (BG_Table_view.jpg, 1672 x 941) is the game's current table background, "Himalayan Vista": a painted daytime scene seen from a raised camera, with a large ornate ivory card table filling the lower two-thirds, a stone terrace around it, red flowers in front of the table's two bottom corners, a temple and prayer flags on the left, a lake town and the snowy Himalaya behind.

Make a NEW background for the same table, called "Pokhara Lakeside", in exactly the same painterly style, finish, detail and colour richness.

Keep exactly as they are:
- The table: same position, size, oval shape, perspective, ivory top, gold rim and carved wood. It marks where the table stands.
- The camera: same viewpoint, same horizon height, same perspective and scale, same 16:9 framing, same image size (1672 x 941).
- The light: warm late-morning sunlight from the upper left.

Replace everything around the table with Pokhara Lakeside:
- The table stands on a flat, open, sunlit green lakeside lawn of fresh short grass; the lawn is level and continues under and around the table.
- Behind the table a tranquil lake (Phewa Lake) stretches into the distance, calm water with soft reflections of the mountain and the sky and glints of reflected sunlight.
- A few small colourful wooden rowing boats (Pokhara doongas: red, blue, yellow) float on the lake or rest at the shore, to the LEFT and to the RIGHT of the table, fully visible, never behind or under the table.
- Green forested hills along the far shore, with a few small houses half hidden in the trees.
- Beyond them one dominant snowy triangular Himalayan peak, Machhapuchhre (the fishtail mountain), rising into a clear blue sky with a few soft white clouds, right of centre where the highest peak is in the attached image, with a lower snowy range beside it.
- At the far left and far right edges: lakeside trees and flowering shrubs framing the view.
- In front of the table's two bottom corners, soft out-of-focus foreground plants frame the table the way the red flowers do now: pink bougainvillea with green leaves and some tall lakeside grass. Keep the bottom centre of the image clear of them.

Airy, peaceful and bright. No people, no text, no letters, no logos, no UI, no playing cards.
```

Result: 1672 x 941. The table stayed where it was: best tile shift against BG_Table_view on the rim
dx -1, dy 0 (NCC 0.955-0.974 on four rim tiles). Accepted as the composition.

## P2: the same picture without the table (`chatgpt/Pokhara_NoTable_a1.png`, 1672 x 941)

```
Very good, keep this scene. Next step: the same picture WITHOUT the table and WITHOUT the foreground flowers.

Start from your last image (not from the attached Himalayan Vista image):
- Remove the card table completely: its ivory top, gold rim and carved wooden body.
- Where the table stood, continue the flat, level green lakeside lawn naturally, in the same perspective. The lake's near shoreline runs across behind where the table was (a natural grassy shore with a few stones at the water's edge, the same line as at the left and right), and the lawn fills the whole lower part of the picture down to the bottom edge. Keep the lawn open and plain: no paths, no objects, no flower beds, no boats on the grass, no table shadow.
- Also remove the out-of-focus pink bougainvillea and tall grass in front of the table's bottom corners (bottom left and bottom right); continue the lawn there too. Keep the bougainvillea bushes and trees higher up at the far left and right edges.
- Keep everything else exactly as it is, at the same pixel positions: the lake, the boats, the far shore and houses, the hills, the fishtail peak and the snowy range, the sky and clouds, the trees and bushes at the sides. Same camera, horizon and light, same image size 1672 x 941.

No text, no people, no UI.
```

Result: the scenery stayed in place (tile shifts dx 0/-1, dy -1 on eight tiles: sky, peak, far shore,
side trees, boats). This is the centre of the Back plate, used 1:1 (no resampling) at (279, 157).

## Margins for 21:9 and 4:3

| Attempt | Input | Result | Verdict |
| --- | --- | --- | --- |
| `Pokhara_Wide_a1.png` | `attach/Pokhara_Extend_input.png`: 2230 x 1255, the picture 1:1 in the middle of a flat magenta border (279 / 157 px) | 1672 x 941; the picture came back about 0.87 of the canvas instead of 0.75 and lower (peak tip at 1065, 181 instead of 1049, 131): a re-framing, not an outpaint | Rejected |
| `Pokhara_Wide_a2.png` | `attach/Pokhara_Extend_input_1672.png`: the same canvas at ChatGPT's own size (picture 1254 x 706 at 209, 118), landmark coordinates in the prompt | again about 0.88 and lower (peak tip 1065, 165; far shore y 360 instead of 289) | Rejected |
| `Pokhara_Left_a1.png` | `attach/Pokhara_Left_input.png`: 1672 x 941, the picture 1:1 shifted 279 px right, magenta strip on the left | re-framed again, scene about 10% larger | Rejected |
| `Pokhara_Left_a2.png` | `attach/Pokhara_Left_input2.png`: as above but the strip is a blurred mirror of the picture's edge (`scripts/strip-canvas.ps1`), asked as an edit ("repaint only the blurred strip") | picture kept in place: similarity fit s 1.0002, shift -0.5, -0.5 px, NCC 0.945; the tree near the seam was redrawn larger | Accepted |
| `Pokhara_Right_a1.png` | `attach/Pokhara_Right_input2.png` (strip on the right) | s 1.0125, shift 0.0, 2.0 px, NCC 0.804 | Accepted |
| `Pokhara_Top_a1.png` | `attach/Pokhara_Top_input2.png` (strip along the top, 157 px) | s 0.978, shift 8, 18.5 px, NCC 0.43: slightly smaller and lower, but only its new sky is used (seam in the top 55 rows of the view, through sky) | Accepted |

Magenta borders make ChatGPT re-frame the whole picture; a blurred placeholder in the same place,
asked as an edit of that strip, keeps the picture where it is. The prompts for the magenta attempts
asked for an outpaint "of the border only, picture unchanged" with landmark coordinates; they are in
the chat.

### Strip prompts (left shown; right and top differ only in side, size and landmarks)

```
That re-framed the picture again (everything came out about 10% larger), so let's try a different way.

The attached image (Pokhara_Left_input2.png, 1672 x 941) is the lakeside picture at its original size, shifted 279 px to the right. The strip along the left edge (x 0 to 278) is only a blurred placeholder.

Edit this image: repaint ONLY that blurred strip as finished painting in the same style and detail, continuing the scene to the left: the rest of the big leafy tree (its trunk, branches and foliage up to the top edge), more pink bougainvillea, a few stones along the shore and the sunlit green lawn below.

Keep everything right of x 279 exactly as it is, pixel for pixel: same position and size, nothing moved, scaled, zoomed or repainted (the tree trunk stays at about x 320, the left blue boat at about x 400 to 590, y 290 to 340, the tip of the fishtail peak at about x 1400, y 18). Same 1672 x 941 image. No new landmarks, no text, no people, no frame.
```

Right: "...shifted 279 px to the left. The strip along the right edge (x 1393 to 1671) ... continuing the
scene to the right: more of the drooping willow tree (trunk and hanging branches up to the top edge),
more pink bougainvillea, the stony lake shore and the sunlit green lawn below. Keep everything left of
x 1393 exactly as it is ... (the tip of the fishtail peak stays at about x 842, y 18; the right blue
boat at about x 1140 to 1300, y 300 to 355)."
Top: "...shifted 157 px down. The strip along the top edge (y 0 to 156) ... continuing the scene
upward: more of the same clear blue sky with a few soft white clouds, the upper foliage of the big
leafy tree at the left and of the willow at the right. Keep everything below y 157 exactly as it is
... (the tip of the fishtail peak stays at about x 1121, y 172; the far shore of the lake at about
y 385)."

## Upper-right fix for the HUD (`chatgpt/Pokhara_TopRight_a1.png`, `_a2.png`)

The game draws dark "Scores" / "Settings" labels without a backing at plate x 1714-1908, y 266-288
(16:9), x 1977-2195 (21:9) and y 108-130 (4:3). On the first assembly the willow and dark hills sat
behind them (16:9 box luma 137, 36% of pixels darker than 120; BG_Table: 230, 0%).

a1, attachment `attach/Pokhara_TopRight_window.png` (plate x 558..2230, y 0..941):

```
Lovely, thank you. Before the other tile pictures, one fix to the Pokhara Lakeside plate.

The attached image (Pokhara_TopRight_window.png, 1672 x 941) is the upper right part of the finished wide Pokhara Lakeside plate. In the game, small dark text labels ("Scores", "Settings") sit in the upper right of this area, around x 1150 to 1650 and y 100 to 300, and the round counter sits around x 380 to 740, y 20 to 210. Dark text needs a bright, calm background behind it, but here the willow tree and the dark green hills sit right behind the labels.

Edit this image:
1. Remove the drooping willow tree at the upper right completely: its trunk, branches and hanging foliage. Keep the pink bougainvillea bushes and the stones at the right edge where they are.
2. In the upper right (about x 1080 to 1672, y 40 to 330), continue the sky with a bank of large, bright, soft white clouds that covers the tops of the hills on the right, so the area behind the labels is light. Below the clouds the lower forested slopes, the far shore and the lake continue naturally; where the willow stood, continue the lake, the far shore, the bougainvillea and the lawn.
3. Keep everything else exactly as it is, pixel for pixel: the fishtail peak and the snowy range, the hills in the middle and on the left, the lake, the boats, the shore stones, the lawn, the leafy branches at the top left and the sky left of x 1050. Same 1672 x 941 image, no zoom, no shift, no new objects, no text.
```

a1 was right in content but re-framed (scale 1.085). a2, attachment `attach/Pokhara_TopRight_input2.png`
(the window with only its upper right replaced by a blurred copy of a1, aligned):

```
The clouds and the open upper right are exactly what I need, but the picture came back about 8% larger and lower, so it no longer lines up with the rest of the plate. Let's do it as a strip edit again, like the margins.

The attached image (Pokhara_TopRight_input2.png, 1672 x 941) is the original window at its original size. Only its upper right part (x 1080 to 1672 above y 330, and x 1250 to 1672 above y 440) is a blurred placeholder showing what goes there: a bank of large bright soft white clouds over the tops of the right hills, the lower forested slopes, the far shore and the lake below them, and the top of the pink bougainvillea bush at the right.

Edit this image: repaint ONLY that blurred area as finished painting in the same style and detail, following the placeholder: bright white clouds in the upper right, no willow tree, no branches hanging into the sky. Keep everything else exactly as it is, pixel for pixel: same position and size, nothing moved, scaled, zoomed or repainted (the tip of the fishtail peak stays at about x 837, y 175; the blue boat at the right shore at about x 1140 to 1300, y 455 to 510). Same 1672 x 941 image. No new landmarks, no text, no people, no frame.
```

a2 kept the scale (1.001) but had local shifts up to 10 px, so `scripts/pokhara-topright.ps1` aligns it
with a local displacement field (96 px tiles every 48 px, 355 valid, median NCC 0.77; afterwards every
check tile matches at 0, 0), matches its colour on the unedited part and joins it along two
minimum-difference seams (below the edit at window y 600-697, left of it at window x 880-1069).

## F1: the foreground layer (`chatgpt/Pokhara_Front_a1.png`, 1672 x 941, real alpha)

Attachment `attach/Pokhara_Front_guide.jpg`: the assembled plate with BG_Table's table cut by TableMask,
scaled to 1672 x 941.

```
Thank you, the plates are done. Now the foreground layer for Pokhara Lakeside, as a separate transparent image.

The attached image (Pokhara_Front_guide.jpg, 1672 x 941) is the whole Pokhara Lakeside plate with the table in place, scaled to 75%: the phone screen shows its middle part, the outer margins only show on wider or taller phones. Ignore the red flowers at the table's bottom corners in this guide; they belong to another background.

Make a PNG with a real transparent background, exactly the same size and framing (1672 x 941), containing ONLY soft, out-of-focus foreground plants very close to the camera that frame the table's two bottom corners, like the pink bougainvillea and tall grass in your first Pokhara picture with the table:
- Bottom left: a lush cluster of pink bougainvillea with green leaves and a few blades of tall lakeside grass, rising from the canvas's bottom-left corner: about 500 px wide along the bottom edge and about 450 px tall at the left edge, overlapping the table's lower-left rim.
- Bottom right: a similar but not mirrored cluster rising from the bottom-right corner: about 480 px wide and about 430 px tall at the right edge, overlapping the table's lower-right rim.
- Keep the bottom centre (x 520 to 1150) and the whole table top completely clear.

Everything else fully transparent: real alpha, no background colour, no checkerboard pattern, no ground, no table, no sky. Soft blurred edges, the same warm light from the upper left as the plate.
```

Result: real alpha (77% fully clear, no checkerboard, no haze after levelling). `scripts/fit-front.ps1`:
alpha levels 12 -> 0 and 250 -> 255, x1.3337 Lanczos-3 (premultiplied) to 2230 x 1255, then the left
cluster scaled x0.82 toward the bottom-left corner and the right one x0.85 toward the bottom-right, so
their reach matches BG_Table's own flowers (ShopTables' BG_HimalayanVista_Front: left box 0,641 693x614,
right 1519,800 711x455; ours: left 0,680 694x575, right 1606,735 624x520). Before scaling they covered
0.8% of the playing surface; after, 0%.

## T1: the shop tile picture (`chatgpt/Thumb_Pokhara_a1.png`, 1173 x 1341)

Attachments: `attach/20-backgrounds_tiles.png` (mockup 20's three tiles, crop 375,225 890x430, as a
style reference only) and `attach/Pokhara_Back_view.jpg` (the plate's 16:9 view).

```
Thank you, both foreground layers are done. Last part: the pictures for the shop tiles.

Image 1 (20-backgrounds_tiles.png) shows three background tiles of the game's shop. Their pictures (Kathmandu Courtyard, Village Terrace, Vintage Home) show how a background is previewed in the shop. Image 2 (Pokhara_Back_view.jpg) is the Pokhara Lakeside background itself, without the table.

Paint the tile picture for "Pokhara Lakeside" so it sits naturally next to the three tile pictures in image 1: the same finish, detail, brightness, saturation and painterly style, and the same kind of framing: a close, eye-level postcard view of the place that fills the whole picture, with sky in the upper part, the landmark large in the middle and its ground in the foreground. Show the same place as image 2: the calm lake with a few small colourful wooden rowing boats near the shore, the green lakeside lawn with pink bougainvillea, the forested hills and the snowy triangular fishtail peak (Machhapuchhre) under a blue sky with soft white clouds, warm daylight from the upper left.

One full-bleed rectangular painting in portrait 7:8 (for example 1176 x 1344 px). No table, no cards, no people, no text, no caption, no frame, no border, no rounded corners.
```

`scripts/make-thumb.ps1`: centre crop 1173 x 1332 (y 4) to the tile aspect, Lanczos-3 to 590 x 670
(mockup 20's tile pictures are 252-262 x 292 mockup px, x2.3; same size as ShopBackgroundsA's).

## Colour

ChatGPT's Pokhara sky and lake came back far more saturated than BG_Table's (sky 107,180,246, sat 0.56,
against 165,207,245, sat 0.33). Blue hues (195-225 deg, 25 deg fade) are desaturated x0.70 with the
HSV value kept, on the Back plate and the tile picture; the tile then gets a luma-preserving
saturation x0.75. Plate scenery outside the table (16:9): luma 151.2, sat 0.383 (BG_Table 156.6, 0.415).
Tile: sat 0.41, luma 147 (mockup 20 tiles 1-2: 0.39-0.41, luma 130-137).

## Checks

`scripts/final-checks.ps1` -> `check/PokharaLakeside_check_{full,16x9,21x9,4x3}.png` (Back + ShopTables'
`final/Table_HimalayanIvory.png` + Front).
- View (16:9): 68% of its pixels are ChatGPT's no-table picture 1:1 (after the blue grade); the rest
  is strip content near the edges (seams at plate x 279-503 and 1711-1776, y 157-211) and the
  upper-right edit. Of the scenery visible around the table, 60% is the centre picture.
- HUD boxes (dark text, no backing): 16:9 ROUND luma 187, Scores/Settings 199; 21:9 Scores/Settings 209;
  4:3 ROUND 163, Scores/Settings 212; 0% dark pixels in all (BG_Table: 196 / 230 / 99 (58% dark) / 162 / 198).
- Table on the ground: the lawn runs under the table; the shore line meets the lawn at plate y ~510
  beside the table, well above the floor line under the far lip (~720), so the shore reads as behind it.
  The two front boats' near hull ends are slightly overlapped by the table's far rim (natural occlusion;
  both boats stay readable).
- Front: 0% of the playing surface and 0% of the bottom centre (x 799-1429, y 900+) covered.
