# Play Online (screens 23 / 24): stake chips, info button, badges, prize popover, dividers

Chat: ChatGPT, chat "Greeting exchange" inside the owner's project **TestProject** (opened 4 Oct 2026 with `hi`), the
only chat of this batch:
<https://chatgpt.com/g/g-p-6abf749240f881918a0877a98eb42245-testproject/c/6ac2885c-4eb8-83ee-aa9c-e3d36e0fd417>
(archived at the end of the batch from the sidebar's Chat actions; deleting it is the owner's call). Brave "Browser 1"
(Windows, signed in to ChatGPT Pro); model and thinking setting left as they were ("Extra High").

Sources: the owner's mockups `23-online-play-stake-50.png` (1671 x 941) and `24-online-play-stake-100.png` (1672 x 941)
in `Assets/ArtRefrences/game-screens/` (= `D:\Nepali Callbreak\game-screens\`, same files); spec
`D:\Nepali Callbreak\game-screens\online-match-options-prompts.md`. 23 is measured after resizing it to the shared
1672 x 941 frame (`ArtSource/Working/PlayOnline/attach/S23n-online-play-stake-50-1672.png`), as the spec asks.

| Where | What |
| --- | --- |
| `ArtSource/Working/PlayOnline/attach/` | what was sent: the two screens, the contact sheets `P<n>_sheet.png` with their cell tables, the shortened vertical divider |
| `ArtSource/Working/PlayOnline/sheets/` | the sheet definitions (`kc lsheet`) |
| `ArtSource/Working/PlayOnline/chatgpt/` | ChatGPT originals, `P<n>_a<k>.png` = sheet n, attempt k |
| `ArtSource/Working/PlayOnline/final/` | the finished sprites (copies in `ArtSource/Exports/{UI,Icons,Decor}`) |
| `ArtSource/Working/PlayOnline/check/` | each sprite against both mockups, `numbers.txt`, `reuse_numbers.txt`, `overview_*.png` |
| `ArtSource/Working/PlayOnline/notes.md` | the inventory: kit pieces reused, new pieces, rects, 9-slice borders, swap map |
| `ArtSource/Working/PlayOnline/tools/` | `make.sh` (originals -> finals, byte for byte), `check.sh`, `reuse.sh`, `overview.sh`, `po.js` |

## How a piece is made

1. The element is measured on both mockups (`Tools/art fitrect/profile/inkbox`, `po.js ringfit/radprof/vprof/hprof2/lineprof/ray`).
2. `kc lsheet` lays enlarged crops on a 1536 x 1024 sheet fading into an ivory ground; ChatGPT redraws the sheet on flat
   magenta; `kc cells` keys the magenta and cuts the cells.
3. Each cell is fitted to its mockup element and brought to @2x (2.3 sprite px per mockup px); colours are put on the
   mockup's (`kc curve` from `shopui colorcmp` quantiles, `po.js rowmatch` row by row, `po.js ringpaint` for rings).
   Lines one or two mockup pixels wide (rims, keylines, hairlines) are drawn at the widths and colours measured on the
   mockups, ChatGPT's piece giving the look, as the Controls and ShopUI kits did.
4. Every piece is composited back at its rect on both mockups and compared (`check/numbers.txt`).

## 0 · Opening

`hi` -> "Hi! 👋 What would you like to work on today?"

## 1 · Setup (P-0 adapted)

Attachments: `S23-online-play-stake-50.png`, `S24-online-play-stake-100.png`.

```
You help me extract production UI assets for "Callbreak - Play Together", a Nepali card game for phones in landscape. The reference images are the approved design; reproduce them.

Style (never change it): warm ivory parchment panels; deep crimson buttons with a satin gradient; antique-gold accents and keylines; thin dark-brown and bronze line ornaments; polished gold coins with a spade emblem.

In this chat we extract the new pieces of the PLAY ONLINE screen: the two stake chips, the small info button, the round ivory badge and its stacked gold coins, the lightning icon, the prize popover with its pointer and its close x, and a few thin dividers.

Rules for every image:
1. Match the reference exactly: shape, proportions, outline weight, corner rounding, colours, shading and ornament. Do not redesign, restyle, simplify or "improve" anything, and never swap an element for a stock one with the same meaning.
2. Never draw letters or numbers (the only exception is the "i" symbol of the info button, when I ask for it). Every element is EMPTY unless I say otherwise: no text, no label and no caption inside or beside it.
3. No drop shadow and no glow around an element (the game adds shadows separately), unless I ask for it. No flowers, leaves, vines, neon, glass, blue, scenery.
4. Front-on, no change of perspective, no rotation, crisp clean edges, perfectly symmetrical where the reference is.
5. When I send a contact sheet (a grid with one enlarged element per cell and a small grey caption under each), return ONE image with the same grid: the same elements in the same cells, in the same order, at the same size and position, redrawn clean and sharp, with the empty space between them kept, on a perfectly flat pure magenta #FF00FF background (no gradient, no texture, no shadow on it). Leave out the captions and the pale ground of the sheet.
6. If my request is ambiguous, ask before generating.

I have attached the two approved reference screens: 23-online-play-stake-50 (the Play Online screen with the 50-coin stake selected and its prize popover open) and 24-online-play-stake-100 (the same screen with 100 selected). Do not generate anything yet. Reply "Ready" and say in one line each what you see in these two images.
```

Reply: "Ready", with a correct one-line description of each screen.

## 2 · Sheet P1: the stake chips, the info button, the ivory badge, the stacked coins

Sheet: `attach/P1_sheet.png` (`sheets/P1.json`): the crimson 50 chip of 23 `[815.5, 486.5, 197, 72]`, the ivory 50 chip
of 24 `[814.5, 488.5, 198, 69.5]`, the info button of 24 `[1226, 493, 49, 49]`, the lightning's disc of 24
`[464.8, 264.5, 148.2, 148.2]`, the coins of 24 `[1002, 247, 125, 96]`.

```
Sheet P1: the stake chips, the info button, the round ivory badge and the stacked coins. Attached: P1_sheet.png (the contact sheet; the crops are what you copy). They come from the two screens already in this chat: 1 from 23-online-play-stake-50, 2 to 5 from 24-online-play-stake-100.

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same five elements in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no pale ground of the sheet, no drop shadows, no glow.

1 stake chip, selected: the crimson pill. EMPTY: remove the gold coin and the number "50" and continue the satin crimson where they were (brighter red at the top, deeper crimson towards the bottom, with the soft sheen along the top). Keep the pill shape with fully round ends and the thin bright gold rim all around it, exactly as in the crop.
2 stake chip, not selected: the ivory pill. EMPTY: remove the coin and the number and continue the plain warm ivory. Keep its thin warm tan outline (a hair lighter along the top, a little darker along the bottom) and the fine white highlight just inside it, exactly as in the crop.
3 info button: the small round button with the thin red-brown ring (brighter red at the top, darker red-brown at the sides and bottom), its warm ivory face and the bold dark italic serif "i" in the middle, exactly as drawn: keep the "i" (the only letter allowed). Leave out the little gold point under the ring (it belongs to the popover below): magenta there.
4 round ivory badge: the ivory disc with its thin copper-tan rim and the fine white line just inside the rim. EMPTY: remove the crimson lightning and continue the plain warm ivory. An exact circle.
5 stacked coins: only the gold coins: the stack at the back, the smaller stack at the left, the big upright coin at the right with its embossed spade, and the small coin at the bottom between them, exactly as drawn (the same number of coins, the same overlaps, the same polished warm gold with bright highlights and deep amber shading, the same embossed spades). Leave out the round ivory badge behind them and its rim: magenta all around the coins, also in the gaps between them.

Perfectly symmetrical where the reference is (the two chips, the button, the badge). Do not add anything that is not in the crops.
```

Result: `chatgpt/P1_a1.png` ("Glossy Gold UI Sprite Sheet", 1536 x 1024, 1 min 40 s). **Accepted, all five.**
- Chips: the right shapes; the crimson flat (no satin gradient) and both rims too bright and too even. Kept ChatGPT's
  pill and fill; the fill matched row by row to the mockups' chips (`po.js rowmatch`, the crimson gets its top-to-bottom
  gradient); the rims drawn at the bands measured on 23 and 24 (crimson look: five 1 px bands, dark gold, gold, bright
  gold, red-brown, dark crimson; ivory look: 2 px tan, 1 px light, 1 px white). The ivory pill is drawn 69 px tall in
  the same 71.2 px canvas as the crimson one, as on the mockups.
- Info button: ring twice too thick with a gold edge the mockup does not have: rings moved to the mockup's radii
  (`kc rwarp`, the "i" kept at one scale), the ring painted with 24's ring colours angle by angle (`po.js ringpaint`,
  the darkest point of each ray), 2.3 px. The "i" lands within 0.15 px of the mockups'.
- Badge: rim gold instead of copper-tan: rim painted in 0.4-0.5 px bands with 24's colours; face colour curved.
- Coins: the same coins as the mockup; colour quantiles put on 24's and 23's.

## 3 · Sheet P2: the prize popover

Sheet: `attach/P2_sheet.png` (`sheets/P2.json`): the popover of 24 with its pointer `[1174, 543, 283, 271]`.

```
Sheet P2: the prize popover. Attached: P2_sheet.png (one enlarged crop from 24-online-play-stake-100, already in this chat; the crop is what you copy).

Return ONE image, same canvas (1536 x 1024), the popover at the same size and position as in the sheet, redrawn clean and sharp on a perfectly flat pure-magenta #FF00FF background.

Draw ONLY the popover box with its pointer, EMPTY: remove the heading "100-coin prizes", the close x, the four rows (1st, 2nd, 3rd, 4th, their gold coins and numbers) and the thin lines between the rows, and fill the whole inside with the same plain, even warm ivory. Keep exactly: the rounded-rectangle box with the same corner rounding; the fine gold edge all around it (a thin gold line with a hair of white just inside it, exactly as thin as in the crop); and the small triangular pointer on the top edge, toward the left, pointing up: its two sides carry the same fine gold edge, which runs up to the tip and back down into the top edge, and the ivory inside the pointer joins the ivory of the box with no line between them. Leave out everything around the popover (the crimson chip, the round button above the pointer, the red line, the scenery at the right): magenta there. No shadow.
```

Result: `chatgpt/P2_a1.png` ("Polished Ivory Speech Bubble on Magenta"). **Look accepted, frame redrawn.** The right
box, corner and pointer, but a bevelled gold frame about 2x as heavy as the mockup's fine edge, with a dark brown inner
line the mockup does not have. The box is drawn at the seven 1 px bands measured along each side of 23 and 24 (top >
sides > bottom colours), outer radius 21.7, fill #F9EFE5 (`po.js roundframe`); the pointer is drawn with 10 px of the
box's top edge on each side so their lines meet in mitred joints (`po.js pointer2`); the shadows are blurred
silhouettes fitted on the mockup.

## 4 · Sheet P3: the small ornaments

Sheet: `attach/P3_sheet.png` (`sheets/P3.json`): the title divider under Quick Match `[372, 499, 336, 25]`, the
flourish right of the subtitle `[1026, 139, 88, 21]`, the close x `[1411, 573, 30, 30]`, the lightning
`[506, 289, 68, 102]`, the vertical divider shown short (`attach/R_vdivider_short_24.png`, its plain middle stretches
of line taken out with `shopui splicey`), a prize row line `[1198, 659, 237, 9]`; all from 24.

```
Sheet P3: six small ornaments of 24-online-play-stake-100 (already in this chat). Attached: P3_sheet.png (the contact sheet; the crops are what you copy).

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same six elements in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no pale ground of the sheet, no shadows.

1 title divider (under "Quick Match"): two thin warm-brown lines that taper to fine points at their ends, with a small gap on each side of a bronze diamond in the middle. The diamond is a little taller than wide, filled warm bronze with a slightly darker edge.
2 flourish (beside the subtitle): one thin warm-brown line, thickest in the middle and tapering to fine points at both ends, running through a small bronze four-point star with slightly concave sides.
3 close: the dark-brown x with straight, even strokes and square ends.
4 lightning: the crimson lightning bolt with its satin shading (a little lighter towards the top, deeper crimson towards the bottom), the same outline and angles.
5 vertical divider: a thin warm-brown vertical line with rounded ends, broken by a small gap above and below a bronze diamond (the same diamond as in 1).
6 row line: one thin, even, warm-tan horizontal line with square ends. Leave out the two bits of gold coin above and below it.

Every line exactly as thin as in the crop. Remove the soft white glow around the diamonds, the star, the x and the lightning: magenta right up to their edges. Flat colours as in the crops; do not add anything.
```

Result: `chatgpt/P3_a1.png` ("Neon Magenta UI Divider Icons"). **Diamond, flourish, close and lightning accepted;
lines drawn.**
- Lightning: fitted by alpha IoU 0.992 (24) / 0.991 (23); redder and brighter than the mockups (mean #800503 against
  #700915): colour quantiles put on the mockups', the top-to-bottom gradient matched row by row inside the bolt.
- Close: IoU 0.986 / 0.965 (the kit's Icon_Close: 0.966 / 0.932), rendered in one flat ink, the mockups' brown #573011.
- Diamond (cut from cell 1): IoU 0.953-0.985 on its three uses; its outline lightened to the mockups'.
- Flourish: IoU 0.854-0.909 (right and mirrored left); colours put on the mockups'.
- Lines (cells 1, 5, 6): the right shapes, but 2-3x as heavy as the mockups' 1.4-1.6 px lines once scaled down. They are
  drawn at the opacity measured along each line on both mockups (`po.js alphaprof` + `line`): core #A88566 with light
  lines either side; the row line 1.6 px #DAC3AE.

## Not from ChatGPT

- `UI_Outline_Thin_Red.png`: the kit's UI_Outline_Thin_Selected is dark crimson (#800405); the Stake Match outline is a
  2 px bright red line between a warm halo and a white line. Drawn at the measured values with the kit outline's
  compact 9-slice geometry (no request: a 2 px line).
- Shadows (`*_Shadow.png`): blurred silhouettes (`shopui shadowsprite`), blur, offset and opacity fitted on the mockups
  (`shopui fitshadow`).
