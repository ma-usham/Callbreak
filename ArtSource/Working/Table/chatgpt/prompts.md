# UI_ChatBubble: the table's speech bubble

Chat: ChatGPT project "CardGame", one new chat opened 2 Oct 2026 with `hi` (reply: "Hi! 👋 How can
I help you today?"); the only chat used for this asset:
https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abec435-6bdc-83e8-acb7-7d91b7ecc22a

Reference: the owner's example (213 x 121 px): an ivory bubble with "Well played!" over the table.
Attached as `UI_ChatBubble_reference_x4.png` (the same image enlarged 4x). Measured on it: fill
#FDF5ED, rim amber-orange (#FB9B13 to #FCBC34) with a light line inside it, box about 187 x 57 px
with corners of about 13 px, tail out of the bottom edge from 16 to 39 px right of the left edge,
its tip about 41 px below the box near its left edge; soft shadow straight down (about 28%
darkening just below the rim, gone 9 px lower).

## Attempt 1

Attachment: `UI_ChatBubble_reference_x4.png`.

```
I'm making UI art for a mobile card game (Callbreak, played in landscape). The attached image is our reference: a speech bubble over the game table with the words "Well played!" in it.

Please draw this speech bubble as a clean, empty game UI sprite, in exactly the reference's style:
- Fill: smooth, even warm ivory/cream (about #FDF5ED), completely flat: no texture, no gradient, no pattern.
- Edge: the same thin warm amber-orange rim as the reference (golden orange, about #F6A21E), with the faint light highlight line just inside it. Same thickness all the way round.
- Shape: a wide rounded rectangle with the same corner roundness as the reference, about 3.5 times as wide as it is tall, with long, perfectly straight top and bottom edges and straight left and right sides. Fill and rim must look exactly the same all along the straight parts, so the middle can be stretched horizontally in the game.
- Tail: the same small tapering tail as the reference, coming out of the bottom edge just to the right of the bottom-left corner and pointing down and to the left; its tip ends roughly below the bubble's left side. The rim runs around the tail without a break and the fill flows into it without a seam. No other tail, bump or notch anywhere.
- Shadow: a gentle, soft drop shadow beneath the bubble and its tail, as in the reference: slightly lower than the bubble, softly blurred, light, a semi-transparent warm dark grey that fades out to nothing.

Leave out everything else: NO text or letters, no table, no cards, no background scene, no glow. Flat front view, no perspective, centred, the bubble about 1200 px wide with empty margin around it and its shadow. The background must be truly transparent: a real PNG with an alpha channel (not a drawn checkerboard, not white). The inside of the bubble must be fully opaque.
```

Result: `UI_ChatBubble_a1.png`, 1448 x 1086, real alpha (inside 254/255). Box 1328 x 408, corner
radius 0.37 of its height, tail base 0.28h-0.66h from the left edge, tip 0.51h below the box and
0.09h right of its left edge. Rejected: a flat, heavy, saturated orange rim (#FB9D03) where the
reference has a soft golden one; a short dark shadow (alpha up to 0.5) under the bottom edge only.
(The download was named after the image's title, "Blank Golden Speech Bubble UI Asset.png", not
"ChatGPT Image ...".)

## Attempt 2

```
Thank you, this is close, and the transparency is right: keep a real transparent background exactly like this. Please make a second version with these changes, everything else as it is (same size, same proportions, same corner roundness, same straight edges, no text):

1. Rim: in the reference the rim is golden amber, not orange, and it looks soft. Make it a golden amber band (about #FFB51C) with only its very outer edge a little deeper orange (about #EC9524), blending on the inside through pale gold (about #FEE299) into a thin white highlight line, then the ivory fill. A bit thinner than now: the golden band about 3% of the bubble's height.
2. Fill: a touch lighter, about #FDF5ED, still completely flat.
3. Tail: longer and more slender, like the reference. Its base starts right where the bottom-left corner's curve ends and is about 40% of the bubble's height wide; it reaches down about 70% of the bubble's height below the bottom edge; its tip, slightly rounded, ends directly below the bubble's left edge (not to the right of it). Its two sides are nearly straight.
4. Shadow: softer and a little wider: shifted straight down by about 4% of the bubble's height and blurred over about 10% of it, about 30% dark right under the rim and fading out smoothly, under the bubble and the tail and faintly along the sides.
```

Result: `UI_ChatBubble_a2.png` ("Golden Amber Speech Bubble.png"), 1448 x 1086, real alpha. Box
1381 x 385. Rim right: orange outer line #EC8808 (6 px), golden band #F8AC1E-#FEDA6E (12 px), pale
gold, white line (7 px): 7% of the height, as on the reference. Fill #F9F0E8 (asked #FDF5ED).
Still off: corners rounder than the reference (radius 0.39 of the height, reference 0.29); tail
shorter (tip 0.45h below the box, reference 0.67h) and ending 0.17h right of the left edge; a
faint golden haze (alpha up to 0.28) around the top and sides; the shadow under the bottom edge
too dark (alpha 0.72 right under the rim).

## Attempt 3

```
This one is much better. Keep this rim exactly as it is (the orange outer line, the golden band, the pale gold and the white line), the fill, the size and the real transparency. Three corrections, everything else unchanged:

1. Corners: a little less round. Now the corner radius is about 40% of the bubble's height; in the reference it is about 30%. The straight parts of the top, bottom and side edges get correspondingly longer.
2. Tail: longer, like the reference. Its tip reaches down about two thirds of the bubble's height below the bottom edge and ends straight below the bubble's left edge (now it is shorter and ends further right). Its base stays where it is, just after the bottom-left corner's curve.
3. No glow: there is a faint golden haze around the rim on the top and on the sides. Remove it: outside the rim everything is fully transparent, except the soft shadow under the bottom edge and under the tail. Make that shadow a little lighter than now: about 30% dark right under the rim, fading out smoothly over about a tenth of the bubble's height.
```

Result: `UI_ChatBubble_a3.png` ("Clean Layered Chat Bubble.png"), 1448 x 1086. Corners right (radius
0.31 of the height), haze gone (alpha at most 0.07), shadow lighter (alpha about 0.36 under the
rim). Rim kept, its outer line a deeper orange (#E27200). Tail unchanged in effect: tip 0.51h below
the box and 0.16h right of its left edge, base 0.26h-0.57h.

## Attempt 4 (tail only)

Attachment: `UI_ChatBubble_reference_tail_x6.png` (the reference's left end, 6x).

```
Very good: the corners, the rim and the clean transparency are right now. Keep all of that exactly. One last change, the tail only.

The attached close-up is the reference's left end enlarged 6x (ignore the words and the table). Its tail is a wider and longer wedge than yours:
- the tail's left side starts right where the bottom-left corner's curve ends and runs down steeply (almost straight down, leaning a little to the left);
- its right side starts on the bottom edge about 40% of the bubble's height further right and runs down-left at about 45 degrees;
- the two sides meet in a slightly rounded tip about two thirds of the bubble's height below the bottom edge, straight below the bubble's left edge.

The rim runs around the tail as it does now. Same soft shadow under the bubble and the tail; nothing else changes.
```

Result: `UI_ChatBubble_a4.png` ("Glossy Cream Chat Bubble with Tail.png"), 1448 x 1086, real
alpha. **Used.** Box 1380 x 365, corner radius 0.32 of the height, rim as attempt 3; the tail is now
a wedge (steep left side, right side at about 45 degrees, base 0.24h-0.75h) but still short (tip
0.49h below the box, 0.23h right of its left edge). The faint haze came back (alpha up to 0.29);
it is dropped in the clean-up anyway.

The chat was archived after this (not deleted).

## Clean-up and export

Done with a throwaway .NET tool in the session scratchpad (`bubble`, built on ArtTool's `Img`,
`Matte` and `Inpaint` classes) and `Tools/ArtPipeline/Kit/Panels/kp`. Recipe:

```
bubble pair --in UI_ChatBubble_a4.png --outbox box.png --outtail tail.png --outfull full.png
  --minr 170 --boxh 144 --fill "#FDF5ED" --flatfill 34 --tailk 1.37 --tailtip 0.03
  --dy 12.9 --sigma 9.8 --opacity 0.62 --scolor "#521A10"
  --rimmap "100:#CF7A22;125:#DE8A22;150:#E99A24;170:#F4AA20" --rimmax 190
kp nine --in box.png --out box9.png --border 72,58,72,84 --mid 16,48 --feather 12
bubble bleed (box9.png, and tail.png padded to 152 px wide)
```

1. Body only: pixels with alpha >= 0.5 and red >= 170 (sRGB), largest piece, holes filled. That
   drops ChatGPT's haze and its own shadow (dark brown, alpha up to 0.9 right under the rim);
   edge pixels darkened by them take the rim colour just inside.
2. Colours: fill #F9F0E7 -> #FDF5ED (the reference's), and everything deeper than 34 px inside
   the outline made exactly that fill (ChatGPT's faint paper noise goes). The rim's outer orange
   softened towards the reference by its green value (deep orange #D56A03 -> about #CF7A22,
   amber -> #E99A24); gold, pale gold and the white line kept.
3. Tail: below the box's bottom edge stretched 1.37x and sheared so its tip ends 0.03h right of
   the box's left edge and 0.67h below it, as on the reference (ChatGPT's: 0.49h and 0.23h).
4. Scaled to a box (outer rim) 144 px tall: @2x for a 72-unit box at ppu 200.
5. Shadow made from the silhouette, fitted to the reference's shadow on the white card (in linear
   light, as uGUI blends): shifted 12.9 px down, Gaussian sigma 9.8 px, opacity 0.62, #521A10. On
   white it gives 27% darker right under the rim, about 1% 28 px lower; 12% beside the side rim
   (the reference: 29% and 11%).
6. Split for the table's layout (the tail goes bottom-left, mirrored bottom-right on the right
   seat): the box is the right half mirrored (symmetric, no tail); the tail is an overlay solved so
   that tail-over-box reproduces the whole bubble (largest difference 4 levels); it covers the
   box's bottom rim where the tail joins, so the rim runs around the tail without a line across
   its base.
7. Box: middle squeezed to 16 px wide (mean column), corners feathered 12 px into the edges;
   `kp seams` at most 1 level across every slice line.

| Export | Size | 9-slice (Unity L, B, R, T) | Notes |
| --- | --- | --- | --- |
| `Assets/_Project/Art/UI/Table/UI_ChatBubble.png` | 160 x 190 | 72, 84, 72, 58 | box with its shadow; outer rim 23.16 px in from left and right, 10.16 from the top, 35.58 from the bottom |
| `Assets/_Project/Art/UI/Table/UI_ChatBubble_Tail.png` | 152 x 144 | none | drawn over the box; its top-left 1 px right of the box image's left edge and 51 px above the box image's bottom edge |

Import (via `Tools/Unity/ueval.sh`, read back): Sprite, Single, ppu 200, Full Rect, no mip maps,
alpha is transparency, clamp, bilinear, compressed (as `UI_NamePlate`), max size 4096.

Check: `UI_ChatBubble_check.png` (reference at 2.46x; box + tail at 230 x 95 units on the table's
colour; mirrored; a shorter box).

Still differs from the reference: the tail's base is a little wider (0.51h against 0.38h); the
rim is crisper (the reference is a soft, slightly blurred small image); no inner glow along the
white line.
