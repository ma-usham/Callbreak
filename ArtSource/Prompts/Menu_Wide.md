Accepted: BG_Menu_Wide_a4.png (assembled: ArtSource/Working/Menu/fit/BG_Menu_Wide_a4.png -> Assets/_Project/Art/Backgrounds/BG_Menu_Wide.png, 2560 x 1440)

# Main-menu background, wide extension (handbook BG-2)

Source: `Assets/_Project/Art/Backgrounds/BG_Menu_Clean.png` (1920 × 1080, UI-free, opaque)
Input: `ArtSource/Working/Menu/attach/BG_Menu_Extend_input.png`, 2560 × 1440: flat #FF00FF with
the source pasted 1:1 (no resampling) at 320, 180, so 320 px of magenta left and right and 180 px
top and bottom. Checked: centre pixels identical to the source, border pure magenta.
ChatGPT originals: `ArtSource/Working/Menu/chatgpt/BG_Menu_Wide_a1.png` … `_a4.png`
Assembly: `Tools/ArtPipeline/Kit/Decor/wide-menu.ps1 -Attempt a4 -Install`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abce176-12b8-83e8-a20a-bbc387de83f5
(ChatGPT project "CardGame", 30 Sep 2026; the Decor designer's single chat, auto-titled "Image
outpainting correction". Model setting left as the owner had it. After the first prompt the chat
answered HTTP 429 for about an hour; it opened again later and the result was waiting.)

## How the final file is made

ChatGPT never returns 2560 × 1440: every attempt came back 1672 × 941 (the canvas scaled by
0.653) with the centre re-drawn. So, as the handbook says, the original goes back on top:

1. the attempt is upscaled to 2560 × 1440 (bicubic, light unsharp mask) and, if needed, moved by
   whole pixels so its re-drawn scenery sits on the original's;
2. its colour is shifted onto the plate's, measured on the plain haze only;
3. `BG_Menu_Clean.png` is pasted 1:1 at (320, 180), fading out over its outer 48 px so the two
   pictures cross-fade at the seam. Inside that band the centre is the plate, pixel for pixel
   (checked: difference 0.0).

The margins are therefore 1.53× upscaled ChatGPT painting (softer than the centre); the centre
1920 × 1080 is unchanged.

## Prompt 1 (a1)

```
Background plates for the game "Callbreak - Play Together". This request: extend the main-menu background for wider screens.

The attached image (BG_Menu_Extend_input.png, 2560 x 1440) is a finished painting, the misty main-menu background of the game: a Newari temple with prayer flags on the left, snowy Himalaya and a hillside village by a lake on the right, blurred red rhododendrons in the bottom corners, pale peach textile patterns in the top corners, and soft pale ivory mist across the middle. It sits in the middle of a flat pure-magenta (#FF00FF) border: 320 px of magenta on the left and right, 180 px at the top and bottom. The painting itself is 1920 x 1080.

Outpaint: fill ONLY the magenta border by continuing the scene outward, so the whole canvas becomes one seamless painting:
- Top: more of the same pale hazy ivory sky, with the pale peach textile pattern continuing outward from both top corners.
- Bottom: more of the blurred red rhododendrons and their leaves in the lower corners, and more of the pale misty ground between them.
- Left: the rest of the same temple where the left edge cuts it off (its roofs, walls, base and steps), more of the same string of prayer flags, and more trees.
- Right: more of the snowy mountains, the forested slopes, the hillside village and the lake.

Do not change the central painting: keep it exactly where it is, at the same size and with the same colours, and keep the soft ivory mist in the middle empty. Match its brushwork, lighting, colours and atmospheric haze in the new areas so there is no visible seam. No new landmarks (no extra temples, pagodas, peaks, lakes or large buildings), no text, no people, no frames, and no magenta left anywhere. Output the full 2560 x 1440 canvas, 16:9.
```

## Prompt 2 (a2)

```
The centre moved, so this one cannot be used. In your result the painting was enlarged by about 1.2x to fill the canvas: the temple and the mountains are bigger and sit further out than on the canvas I attached, so the original painting cannot be laid back over the middle.

Please redo it from the canvas I attached above (not from your result). This is an outpaint (a zoom-out), not a resize:
- The central painting keeps its exact size and position. It occupies exactly the middle 75% of the canvas width and height; the magenta border is the outer 12.5% on each side.
- Every landmark stays where it is on the 2560 x 1440 canvas: the tip of the temple's golden spire at about x 535, y 450; the stone guardian lion at about x 730, y 960; the top of the highest snow peak at about x 1930, y 525; the pale peach textile patterns in the painting's own top corners at (320, 180) and (2240, 180).
- Only the magenta border gets new painting, continuing what touches the painting's edge: pale hazy ivory sky at the top; blurred red rhododendrons, leaves and pale misty ground at the bottom; the rest of the temple's roofs and base, the prayer-flag string and trees on the left; more mountains, forested slopes, village and lake on the right.
- The soft ivory mist in the middle stays empty. No new landmarks, no text, no people, no frames, no magenta left anywhere. Same 16:9 canvas.
```

## Prompt 3 (a3)

```
Better: the painting is now at the right place and size. Keep that layout exactly as in your last result. Three things in the NEW border areas still do not match the painting, and they show as a visible frame when the original is laid back over the middle. Please redo it once more from the canvas I attached, changing only these:

1. Sky. The painting's own sky is a flat, pale, hazy ivory with no clouds and no blue. The new sky along the top and in the upper corners must be exactly that same plain ivory haze: no cloud shapes, no blue.

2. Textile corners. The pale peach textile pattern belongs in the top-left and top-right corners of the whole 2560 x 1440 canvas. Continue the painting's own corner pattern outward to those canvas corners with the same motif size and the same very pale peach (not pink, not red), so it reads as one soft corner ornament on each side.

3. Flowers. The rhododendrons are an out-of-focus foreground: large, soft and blurred, like the ones in the painting's own bottom corners. Keep them in the two bottom corners and along the lower part of the side borders. The middle of the bottom edge stays pale misty ground: no rocks, shrubs or flowers there.

Everything else stays as in your last result: the temple, flags, trees, mountains, slopes and village exactly where they are, the mist in the middle empty. No text, no magenta left, same 16:9 canvas.
```

## Prompt 4 (a4)

```
Very close now. The sky, the flowers and the layout are right: keep all of that exactly as in your last result. Two small corrections in the border, then it is done. Redo it once more from the canvas I attached:

1. Prayer flags on the left. In the original painting one string of flags leaves the painting's left edge at canvas point (320, 440) and rises towards the upper left. Your border must continue that same string: from (320, 440) up to about (0, 230) at the left edge of the canvas, same flag size and spacing. In your last result the string in the border runs about 130 px too low, so it does not meet the painting's string at the edge. Only one string on that side.

2. Textile corners. Make the new textile pattern in the two top canvas corners as pale as the painting's own corner pattern: very pale peach, low contrast, no pink or red tiles. Same motif size as before.

Nothing else changes: same plain ivory haze at the top, same soft blurred rhododendrons in the bottom corners, empty misty ground in the bottom middle, temple, trees, mountains and village where they are. No text, no magenta, same 16:9 canvas.
```

## Attempts

Checked each time: size; leftover magenta (none in any attempt); where the re-drawn scenery sits
against the input canvas (best local shift on a 6 × 4 grid of tiles over the centre); colour of
the haze against the plate; then the assembled 2560 × 1440 by eye, whole and in eight crops along
the seams, plus a high-pass view for seam lines.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_Menu_Wide_a1.png` | 1672 × 941 | Not an outpaint: the painting was enlarged about 1.2× to fill the canvas (temple and peaks bigger and further out, no tile of the centre lines up), so the plate cannot go back on top. | Rejected |
| `BG_Menu_Wide_a2.png` | 1672 × 941 | Layout right (scenery within 1–2 px of the input). But the new top is a blue sky with cumulus clouds against the plate's plain ivory haze, the new flowers are sharp and small with rocks and shrubs along the whole bottom, and the textile corners are strong pink. Shows as a frame around the centre. | Rejected |
| `BG_Menu_Wide_a3.png` | 1672 × 941 | Sky, flowers and layout right (scenery at 0–1 px). The prayer-flag string in the left border ran about 130 px lower than the plate's string, so two strings met at the seam (one starting in mid-air); textile corners still pinker than the plate's. | Rejected |
| `BG_Menu_Wide_a4.png` | 1672 × 941 | Scenery at 0–1 px of the input; haze within 1–2 levels of the plate before correction; one flag string running from the upper-left canvas corner into the plate's own; pale peach textile corners that join the plate's; soft blurred flowers in both bottom corners, empty misty ground between them. No new landmark, no text. | Accepted |

## What still differs / not verified

- The margins are softer than the centre (1.53× upscale). Visible only if compared closely.
- At the bottom-left seam (canvas x ≈ 320, y ≈ 1110–1200) a slightly paler column shows between
  the plate's flowers and the new ones; on the right a faint 60 px dash at y ≈ 1245. Both read as
  gaps in blurred foliage.
- The faint ghost ring near (460, 290) and the faint arcs in the misty ground near y ≈ 1050 are in
  `BG_Menu_Clean.png` itself (left from the main-menu UI removal), not from this step.
- Not verified in Unity: no capture at 21:9 or 4:3 was taken after the swap (no Editor commands
  were run for it).
- The previous placeholder (mirror-blur margins) is kept at
  `ArtSource/Working/Menu/fit/BG_Menu_Wide_placeholder_before.png`.
