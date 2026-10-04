# ShopUI kit (shop drawer, coins, Earn Coins)

Chat: ChatGPT, chat "Greeting exchange" (opened 4 Oct 2026 with `hi`), the only chat of this batch:
<https://chatgpt.com/c/6ac24c64-b050-83ec-ab7b-eb5085b6f94f> (archived at the end of the batch from the
sidebar's Chat actions; deleting it is the owner's call).

Not in the "CardGame" project: in the Brave signed in to ChatGPT on 4 Oct 2026 ("Browser 1"),
`https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/project` answers "This
project can't be found" and the sidebar lists no CardGame project (the other agents' chats of the
day are outside any project too). The chat was started as a plain chat instead. Model and thinking
setting left as they were ("Extra High").

Sources: the owner's new mockups (1672 x 941): `15-card-styles.png` (NEW version, from
`D:\Nepali Callbreak\game-screens\`; the copy under `Assets/ArtRefrences` was the old one until it was replaced with the same file during the session),
`20-backgrounds.png`, `21-table-styles.png`, `22-rewarded-coins.png`, `23-coinsection.png`.
Spec: `D:\Nepali Callbreak\game-screens\shop-drawer-and-tables-prompts.md`, `rewarded-coins-prompts.md`.

| Where | What |
| --- | --- |
| `ArtSource/Working/ShopUI/attach/` | what was sent: the screens (`S<NN>-<name>.png`), the contact sheets (`S<n>_sheet.png`) and each sheet's cell table (`.cells.json`), reference images (`R_*.png`) |
| `ArtSource/Working/ShopUI/sheets/` | the sheet definitions (`kc lsheet`) |
| `ArtSource/Working/ShopUI/chatgpt/` | ChatGPT originals, `S<n>_a<k>.png` = sheet n, attempt k (every attempt kept) |
| `ArtSource/Working/ShopUI/work/` | cells, steps from a cell to a sprite, measurement crops |
| `ArtSource/Working/ShopUI/check/` | each sprite against its mockup (and the reuse checks of kit sprites) |
| `ArtSource/Working/ShopUI/final/` | the finished sprites (copies in `ArtSource/Exports/`) |
| `ArtSource/Working/ShopUI/notes.md` | the inventory: what the kit covers, what is new, every final file |
| `ArtSource/Working/ShopUI/tools/` | `shopui` (small C# helpers) and `make.sh` (the recipe from the originals to the finals) |

Tools: `kitcontrols` (`Tools/ArtPipeline/Kit/Controls`, built into the scratchpad, not into
`Temp/`), `Tools/art` (measuring), `shopui` (this folder).

## 0 · Opening

`hi` -> "Hi! How can I help you today?"

## 1 · Setup (P-0 adapted)

Attachments: `S15-card-styles.png`, `S21-table-styles.png`, `S22-rewarded-coins.png`, `S23-coinsection.png`.

```
You help me extract production UI assets for "Callbreak - Play Together", a Nepali card game for phones in landscape. The reference images are the approved design; reproduce them.

Style (never change it): warm ivory parchment panels; deep crimson #861018 buttons with a satin gradient; antique-gold #B8914F accents and keylines; flat dark-brown silhouette icons; pale peach textile geometry; polished gold coins with a spade emblem.

In this chat we extract the new pieces of the SHOP and the COIN screens: the shop's right-hand drawer, its round category button and icons, the gold coin, the coin piles, the reward blocks of the Earn Coins page and a few small buttons.

Rules for every image:
1. Match the reference exactly: shape, proportions, outline weight, corner rounding, colours, shading and ornament. Do not redesign, restyle, simplify or "improve" anything, and never swap an element for a stock one with the same meaning.
2. Never draw letters or numbers. Every element is EMPTY unless I say otherwise: no text, no label and no caption inside or beside it.
3. No drop shadow and no glow around an element (the game adds shadows separately), unless I ask for it. No flowers, leaves, vines, neon, glass, blue, scenery.
4. Front-on, no change of perspective, no rotation, crisp clean edges, perfectly symmetrical where the reference is.
5. When I send a contact sheet (a grid with one enlarged element per cell and a small grey caption under each), return ONE image with the same grid: the same elements in the same cells, in the same order, at the same size and position, redrawn clean and sharp, with the empty space between them kept, on the background I name (perfectly flat pure magenta #FF00FF or pure white #FFFFFF: no gradient, no texture, no shadow on it). Leave out the captions and the pale ground of the sheet.
6. If my request is ambiguous, ask before generating.

I have attached four of the approved reference screens: 15-card-styles (the shop, Cards tab, with the drawer on the right), 21-table-styles (the shop, Tables tab, with gold coin prices), 22-rewarded-coins (the Earn Coins page) and 23-coinsection (the main menu with the coin wallet). Do not generate anything yet. Reply "Ready" and say in one line each what you see in these four images.
```

Reply: "Ready", with a correct one-line description of each screen. (The first upload went to a
hidden duplicate file input and attached nothing; the second form's "Attach files" input worked.)

## How a piece is made

1. The element is measured on its mockup (`Tools/art fitrect/profile/inkbox/probe`, `kc radial/rays/line`).
2. `kc lsheet` (`ArtSource/Working/ShopUI/sheets/S<n>.json`) lays enlarged crops on a 1536 x 1024 sheet that
   fades into an ivory ground (the icon sheet is turned into dark ink on white with `kc curve`). The drawer is
   shown short: rows 176..822 of 15 taken out (`shopui splicey`, `attach/R_drawer_short_15.png`).
3. ChatGPT redraws the sheet on flat magenta (white for the icons, black for the glow); `kc cells` keys it
   and cuts the cells.
4. Each cell is fitted to the mockup and brought to @2x (2.3 sprite px per mockup px): icons by alpha
   against the mockup's own drawing (`shopui fiticon`), round things by their ring radii (`kc rwarp`), the
   rest by `kc fit` / `shopui fitlayer`; colours are put on the mockup's (`kc curve`, `kc crimson`,
   `kc gradmap`, measured with `shopui colorcmp`).
5. Every piece is composited back at its mockup rect and compared (`shopui scene`, `nineview`), alone and
   rebuilt with its neighbours; the numbers are in `ArtSource/Working/ShopUI/notes.md`.

## 2 · Sheet S1: the drawer, the selected category button, the + button

Sheet: `attach/S1_sheet.png` (`sheets/S1.json`): the drawer of 15 shown short (x 1420..1672), the Cards button
of 15 `[1488.5, 218.4, 144, 144]`, the + of 23 `[1276.3, 75.4, 48.8, 48.8]`. Attachment: the sheet.

```
Sheet S1: the shop drawer and two round buttons. Attached: S1_sheet.png (the contact sheet; the crops are what you copy). The screens are already in this chat: 15-card-styles for 1 and 2, 23-coinsection for 3.

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same three elements in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no pale ground of the sheet, no drop shadows, no glow.

1 drawer panel: the tall warm-ivory drawer at the right edge of 15-card-styles, shown SHORT here: its plain middle has been taken out, so you see its top part and its bottom part joined. Draw ONLY the panel, EMPTY: remove the round shop button and its icon and continue the panel there (the pale peach textile pattern where the pattern is, plain ivory elsewhere). Keep the rounded top-left and bottom-left corners, the straight left edge with its thin bright ivory-white rim, the pale peach woven diamond pattern across the top and across the bottom exactly as in the crop (soft, low contrast, partly faded), and the plain even ivory between the two patterned bands. The drawer is flush with the right, top and bottom edges of the screen: its right side, its top edge and its bottom edge are straight and run to the edges of the panel area; do not round those corners. No scenery and no shadow to the left of the panel: magenta there.

2 selected category button: the crimson disc with its white ring. EMPTY: remove the cards icon and continue the satin crimson where it was (a little lighter towards the top, darker towards the bottom). Keep the even white ring around the crimson and the very thin warm tan line just outside the ring. Remove the soft shadow under it. An exact circle.

3 plus button: the crimson disc with the bold white plus, exactly as drawn: satin crimson, brighter red at the top and darker at the bottom, the white plus with square ends in the centre, and the thin light rim around the disc. Leave out the ivory wallet behind it and the shadow: magenta all around the disc. An exact circle.

Do not add anything that is not in the crops.
```

Result: `chatgpt/S1_a1.png` ("Magenta UI Card and Circular Buttons", 1536 x 1024, 36 s). **Accepted.**
- Drawer: the right look (rounded left corners, peach bands top and bottom, plain middle); drawn taller than
  its cell (cut from the full image), yellower than 15 and with rounder corners (radius 36 against 29 mockup
  px): edges cropped, colours mapped on the blue channel to 15's fill and ornament, the left corners redrawn at
  29 with a 1 mockup px rim.
- Selected button: ring proportions as on 15/20 (crimson to 0.924 of the radius; mockups 0.918-0.930); its
  crimson purer and brighter (#CE191B at the top against #9C1719): put on the mockups' (`kc crimson`).
- + button: right, but with a white ring and gold edge that the mockup does not have (my prompt asked for "the
  thin light rim"): cut at the crimson's edge; the plus kept as drawn (thickening it 0.4 px left a visible box
  in the crimson and was dropped).

## 3 · Sheet S2: the drawer icons

Sheet: `attach/S2_sheet.png` (`sheets/S2.json`, then `kc curve` to dark ink on white): storefront (15)
`[1532.2, 75.2, 57.2, 59.4]`, stacked cards (21) `[1520.1, 253.4, 79.7, 80.8]`, mountains (15)
`[1516.7, 471.3, 88.5, 46.6]`, oval table (15) `[1515.1, 682.3, 96.1, 62]`.

```
Sheet S2: the four drawer icons. Attached: S2_sheet.png (the contact sheet; the crops are what you copy). They come from the drawer of 15-card-styles and 21-table-styles, already in this chat; in the sheet they are shown as their dark ink on white.

S2_sheet.png is a 2 x 2 grid of enlarged crops, one icon per cell:
1 storefront (15, the round button at the top of the drawer): a small shop with a flat awning whose lower edge is scalloped, a thin light line under the awning, and an arched door
2 stacked cards (21, the Cards button): the front card upright with a spade, two cards behind it tilted left and right, and the top edge of one more card above them
3 mountains (15, the Backgrounds button): a large peak with snow streaks and two small peaks
4 oval table (15, the Tables button): an oval tabletop with a thick rim and a light inner ring, and two short legs

Redraw this exact sheet clean: the same four icons in the same cells, in the same order, each at the same size and position as on the sheet, as flat solid dark-brown #4A2C15 shapes with crisp edges on a perfectly flat pure white #FFFFFF background. Trace every silhouette: keep the proportions, stroke thickness, corner rounding, gaps and angles exactly as in the crop, and do not replace an icon with a standard icon of the same meaning. Everything that is light inside an icon in the crop stays white and keeps its width: the line under the awning, the scallops and the door; the light outline that separates the front card from the cards behind it, and the spade; the snow streaks; the light ring on the tabletop. Flat colour only: no texture, no wood grain, no shading. No captions, no numbers, no frames, no shadows, no gradients. Same canvas as the sheet: 1536 x 1024, landscape.
```

Result: `chatgpt/S2_a1.png` ("Minimalist Brown Icon Sheet"). **All four accepted.** Fitted on the mockups
(alpha against alpha, `shopui fiticon`): storefront IoU 0.965, cards 0.966, mountains 0.978, table 0.973; 0.87
to 0.98 on the other screens and in the selected (ivory on crimson) look. The selected look is the same
silhouette in ivory, so one drawing gives both colours. Left as drawn: the storefront's door arch is 1-2 px
narrower than 15's.

## 4 · Sheet S3: the coin piles

Sheet: `attach/S3_sheet.png` (`sheets/S3.json`): the piles of 22 at 3.2x: 20 `[440.8, 422.6, 140.3, 87.5]`,
30 `[747.8, 401.6, 172.3, 110.5]`, 50 `[1053.8, 397.7, 193.4, 115.0]`.

```
Sheet S3: the three coin piles of the Earn Coins page. Attached: S3_sheet.png (the contact sheet; the crops are what you copy). They come from 22-rewarded-coins, already in this chat: 1 is the pile of Ad 1 (20 coins), 2 the stack of Ad 2 (30 coins), 3 the group of Ad 3 (50 coins).

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same three coin illustrations in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no pale ground of the sheet.

Keep each pile exactly as drawn: the same number of coins, the same stacks and their heights, the same overlaps, the same angled front coin with its embossed spade, the same polished warm gold (bright highlights, deep amber shading), the small notches on the coin rims, and the soft embossed spade on the coin faces. All three in the same gold and at the same coin size. Remove the pale light rays and the glow behind the coins and the soft shadow under them: magenta right up to the coins, also in the gaps between them. No outline around the coins, no sparkles, no text, no numbers.
```

Result: `chatgpt/S3_a1.png` ("Golden Spade Coin Piles"). **Accepted**: the same coins, stacks and front coin
as 22, clean keying. Colour quantiles put on 22's (mean within 2 levels); fitted rms 13.2-18.7. The contact
shadows are blurred silhouettes (`_Shadow` sprites).

## 5 · Sheet S4: reward blocks, coin, play triangle, connector

Sheet: `attach/S4_sheet.png` (`sheets/S4.json`): Ad 2 block `[713.0, 329.0, 246.1, 246.9]`, Ad 1 block
`[394.1, 330.0, 247.0, 245.9]`, wallet coin `[1460.5, 63.0, 57.7, 58.3]`, play triangle
`[628.6, 688.8, 46.2, 54.0]`, connector `[643, 443, 68, 7]`, all on 22.

```
Sheet S4: the reward blocks, the coin, the play triangle and the connector line of 22-rewarded-coins (already in this chat). Attached: S4_sheet.png (the contact sheet; the crops are what you copy).

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same five elements in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no pale ground of the sheet, no drop shadows.

1 reward block (Ad 2): the rounded square block with its warm-ivory inside and its fine double gold keyline: a thin gold line at the edge and a thinner gold line just inside it, with a hair of ivory between them, exactly as in the crop. EMPTY: remove "Ad 2", the coin pile, "30 coins" and the soft light rays, and fill the whole inside with the same plain, even warm ivory. Same corner rounding as in the crop.
2 reward block, first (Ad 1): the same block with a crimson keyline at the edge and the thin gold line just inside it, exactly as in the crop. EMPTY in the same way: plain even ivory inside.
3 coin: the round gold coin of the wallet: polished warm gold, the raised outer rim, the thin groove ring inside it and the embossed spade in the middle, exactly as drawn. Front-on, an exact circle. Leave out the ivory wallet behind it: magenta all around the coin.
4 play triangle: only the ivory play triangle with its rounded corners and its soft shading (lighter at the upper left, slightly darker cream towards the lower right). Leave out the crimson button behind it and the dark shadow around it: magenta all around.
5 connector line: only the thin warm-gold horizontal line between the two blocks, with its soft light-to-deeper gold shading across its thickness. Leave out the ends of the two blocks (the crimson line on the left, the gold lines on the right): magenta there.

Both blocks front-on and exactly symmetrical. Do not add anything that is not in the crops.
```

Result: `chatgpt/S4_a1.png` ("Glossy Casino UI Asset Sheet"). Coin and play triangle **accepted** (coin: its
saturation brought down to the mockups', mean within 0-8 levels on all four uses; triangle: mean within 0.3
levels, magenta fringe removed). The two blocks gave the right look but lines of the wrong weight (the inner
gold line drawn twice as heavy and darker than the outer; the first block's inner line red, not gold) and
rounder corners: both frames are drawn at the widths, radii and colours measured on 22 (`shopui fitframe`), as
the Controls kit did for its outlines. The connector came back as a 7 px bar with white edges: the 3 px
hairline is drawn in 22's colours (this cell as the reference).

## 6 · G1: the glow behind the coin piles

Attachment: `attach/G1_glow_crop_22.png` (the inside of the Ad 2 block of 22, 3x).

```
Request G1: the soft light behind the coin piles. Attached: G1_glow_crop_22.png, the inside of the Ad 2 block of 22-rewarded-coins enlarged 3x. Behind the coin pile there is a very soft warm light: a faint glow with pale rays fanning out from behind the coins.

Draw ONLY that light, on a perfectly flat pure BLACK #000000 background, at the same size and position as in the crop: the same soft glow and the same faint rays in the same directions and lengths, as light on black (white to pale warm yellow where it is brightest, fading smoothly to black). Continue the light where the coins, the words "Ad 2" and "30 coins" and the block's frame are now: no coins, no text, no frame, no ivory. The glow is round and centred where the middle of the coin pile is; keep its rays soft and subtle, not sharp. Square image.
```

Result: `chatgpt/G1_a1.png` ("Golden Radial Light Burst", 1254 x 1254). **Accepted**: a starburst, stronger
than the mockup's; alpha = 1.6 x brightness^0.6 in warm white (#FFFCF2), 236 mockup px, chosen by fitting the
upper half of the three blocks (rms 2.2 / 5.1 / 3.2; 5.6 without a glow on Ad 2).

## 7 · Sheet S5: the ivory round button

Added after checking the kit: `UI_ButtonCircle_Normal` (the header disc) at the drawer's 110-140 px is enlarged
1.5x and shows a tan ring where the drawer's buttons have a bevelled white highlight (rms 11.8).
Sheet: `attach/S5_sheet.png` (`sheets/S5.json`): the Backgrounds button of 15 `[1490.5, 427.4, 140, 140]` at 5.4x.

```
Sheet S5: the ivory round button of the shop drawer. Attached: S5_sheet.png (one enlarged crop: the Backgrounds button of 15-card-styles, already in this chat; the crop is what you copy). The same button, smaller, is used for the shop icon at the top of the drawer and for the left and right arrows of the card carousel.

Return ONE image, same canvas (1536 x 1024), the button at the same size and position as in the sheet, redrawn clean and sharp on a perfectly flat pure-magenta #FF00FF background. EMPTY: remove the mountains icon and continue the plain warm ivory where it was. Keep exactly: the warm ivory face, its very soft shading (a hair lighter at the upper left, a hair darker towards the lower right), the thin bright white highlight just inside the edge along the upper left, and the thin warm tan edge line around the disc. An exact circle, front-on. Remove the soft shadow around the disc: magenta right up to the edge line. No caption, no icon, no text.
```

Result: `chatgpt/S5_a1.png` ("Centered Ivory UI Button"). **Accepted**: the rings set to 15's radii
(highlight 0.956-0.985 R, tan line to R) in 15's colours; with the mountains icon on top: rms 7.05 (kit disc
11.8). The carousel's small arrow discs keep the kit disc (it fits them better: rms 10.4 against 12.6).

## 8 · Results

Six requests after the setup, one attempt each, nothing rejected outright (the reward block frames and the
connector were redrawn at measured values, see 5). Files, sizes, 9-slice borders, placements and every check
number: `ArtSource/Working/ShopUI/notes.md`; sprites in `ArtSource/Working/ShopUI/final/` and
`ArtSource/Exports/{UI,Icons,Decor}/`; rows in `ArtSource/handoff.csv`; recipe `ArtSource/Working/ShopUI/tools/make.sh`.

| Sprite | Size | 9-slice L,T,R,B | From |
| --- | --- | --- | --- |
| UI_Drawer | 534x836 | 0,170,0,280 | S1 a1 |
| UI_Drawer_Shadow | 680x982 | 0,243,0,353 | blurred silhouette of UI_Drawer |
| UI_ButtonDisc_Normal | 316x316 | - | S5 a1 |
| UI_ButtonDisc_Selected | 338x338 | - | S1 a1 |
| UI_ButtonPlus_Normal | 114x114 | - | S1 a1 |
| Icon_Storefront_Brown | 140x144 | - | S2 a1 |
| Icon_CardsStacked_Brown / _Ivory | 194x194 | - | S2 a1 |
| Icon_Mountains_Brown / _Ivory | 212x116 | - | S2 a1 |
| Icon_TableOval_Brown / _Ivory | 218x144 | - | S2 a1 |
| Icon_Coin | 132x132 | - | S4 a1 |
| Icon_Play | 108x120 | - | S4 a1 |
| UI_RewardBlock_Normal / _Selected | 140x140 | 62,62,62,62 | S4 a1 (look), lines at measured values |
| Deco_CoinPile_Small / _Medium / _Large | 312x206 / 388x252 / 436x266 | - | S3 a1 |
| Deco_CoinPile_*_Shadow | 410x304 / 486x350 / 534x364 | - | blurred silhouettes |
| Deco_CoinGlow | 544x544 | - | G1 a1 |
| Deco_ConnectorLine | 24x12 | 4,0,4,0 | drawn (hairline) |
| UI_Panel_Card_Light | 144x144 | 56,56,56,56 | the kit's UI_Panel_Card, 3-5 levels lighter |
