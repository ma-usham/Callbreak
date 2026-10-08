# DailyRewards: the Daily Rewards page (26) and its main-menu entry

Mockup (1672 x 941): `Assets/ArtRefrences/game-screens/26-daily-rewards.png` (copied to `attach/S26-daily-rewards.png`;
byte-identical to the designer's `D:\Nepali Callbreak\game-screens\26-daily-rewards.png`). All rects below are mockup
pixels (top-left origin). Sprites are @2x (sprite px = mockup px x 2.3), ppu 200, PNG-32 sRGB, transparent outside.

ChatGPT chat (one chat for the batch, project TestProject, archived at the end):
<https://chatgpt.com/g/g-p-6abf749240f881918a0877a98eb42245-testproject/c/6ac7c198-fe68-83e8-9f5a-965039f6756b>.
Prompts, attempts and verdicts: `ArtSource/Prompts/DailyRewards_Kit.md`. Recipe from the originals to the finals:
`tools/make.sh` (re-run on 8 Oct 2026 from an empty `final/`: reproduces every file byte for byte). Checks:
`tools/check.sh` (every number in the tables below that says "rms"; 9-sliced as Unity draws it, words left out).
Screen layout: `ArtSource/Screens/DailyRewards/screen.json` -> `layout.json` (Tools/art job DailyRewards).

## 1 · What the kit already covers (reused, nothing generated)

| Mockup element | Kit sprite (path under `Assets/_Project/Art`) | How it fits on 26 |
| --- | --- | --- |
| Scenery | `Backgrounds/BG_Menu_Wide.png` (`"background": "shared"`) | the shared menu plate (owner's rule) |
| Back button | `UI/Buttons/UI_ButtonCircle_Normal.png` + `UI/Icons/Icon_Back.png` (+ `UI/Buttons/UI_Shadow_Circle.png`) | disc [42.6, 34.4, 87.0, 87.0], arrow ink [66.17, 59.06, 39.39, 37.43]; rms 21.4 / mean 8.2 with the arrow (as on 22: the same header button everywhere) |
| Wallet | `UI/Buttons/UI_ButtonSecondary_Normal.png` (nine 117,0,117,0) + `UI/Icons/Icon_Coin.png` (+ `UI_Shadow_Secondary`) | pill [1436.6, 52.6, 190.2, 71.2], coin [1460.63, 58.63, 56.45, 56.45] (fitlayer rms 15.7); together rms 18.0 / mean 8.9, "250" left out |
| Title divider | `UI/Decor/Deco_TitleDivider_Line.png` (mirrored on the left) + `_Diamond.png` | lines [606.0, 160.4, 221.0, 3.4] and [844.0, 160.4, 221.0, 3.4], diamond [826.5, 151.8, 17.5, 20.6]; rms 7.1 / mean 1.8 |
| Big panel | `UI/Panels/UI_Panel_Card.png` (nine 56), sliceScale 0.57 (its corners, drawn for radius 22.3, to 26's 36.5) | [331.2, 184.7, 1009.4, 694.3]; panel rim rms 6.10 (contents left out) against 7.16 for `UI_Panel_Large` (whose textile corners and mountains are not on 26). 26's thin inner keyline (about 4 px in) is not on the kit card |
| Claim button | `UI/Buttons/UI_ButtonPrimary_Normal.png` (nine 202,0,202,0; + `UI_Shadow_Primary`) | pill [519.8, 720.1, 634.2, 94.6] (fitrect rms 3.3); rms 38.2 / mean 17.9 (words left out): the right drawing (gold rim, woven ends) but 26 draws it brighter red with larger woven ends, as 22 did (rms 54 there) |
| Day 1's sun light | `UI/Decor/Deco_CoinGlow.png` | 180 x 180 centred on the coin: [383, 273, 180, 180]; Day 1's inside around the coin rms 4.72 (6.17 without it). 26's rays are a little sharper and longer than the kit light |
| Footer hairlines | `UI/Controls/UI_PrizeRowLine.png` (nine 3,0,3,0; tan line with white hairs above and below) | [558.5, 844.7, 91.0, 6.09] and [1022.5, 844.7, 91.0, 6.09]; rms 11.5 / mean 3.6: 26's line is a little darker (#CAA992 against #DAC3AE) |
| Tile and panel shadows | `UI/Common/UI_Panel_Card_Shadow.png` (spread 12, drop 2) | as the README gives for the card; 26's tiles have a soft shadow about 4 px deep under their bottom edge. Not verified (Tools/art does not draw `shadowUse`) |

Checked and not used:

| Kit sprite | Where | Why not |
| --- | --- | --- |
| `UI/Icons/Icon_Coin.png` | Day 1's single coin | rms 22.9 (fitlayer 26.2): 26's coin shows its thick rim at the bottom and a larger spade; `Deco_DayCoins_1` rms 9.9 |
| `UI/Decor/Deco_CoinPile_{Small,Medium,Large}` | the day piles | each day of 26 has its own pile (a different number of stacks); generated `Deco_DayCoins_2..7` |
| `UI/Decor/Deco_SubtitleFlourish_Left/Right` | "Day 1 of 7" flourishes | the star sits in the middle of the line and is 3x smaller than 26's; scaled to 26's star the line runs into the words (rms 30.6); generated `Deco_HeadingFlourish` (rms 6.7) |
| `UI/Decor/Deco_TitleDivider_Diamond` | the same | a convex diamond; 26's star has concave sides |
| `UI/Controls/UI_ReadyDot.png` | the alert dot | a green ready dot; generated a crimson `UI_Dot_Alert` of the same size |
| `UI/Panels/UI_RewardBlock_*` | the day tiles | 26's tiles have a single tan line, peach corner motifs and a different radius |

## 2 · New pieces (ChatGPT-generated, `final/`)

| File | Size (px) | 9-slice L,T,R,B | Mockup element | Placement (mockup px) | Check |
| --- | --- | --- | --- | --- | --- |
| `UI_DayTile_Normal.png` | 533 x 478 | 132,160,132,44 (the top band holds the two motifs) | a quiet day tile (Days 2-6, and Day 1 under its overlay) | Day 1 [357.705, 254.325, 231.55, 207.95]; Day 2 [601.20, 254.20, 231.55, 207.95]; Day 3 [844.50, 254.30, 231.60, 207.85]; Day 4 [1087.85, 254.20, 230.45, 207.95]; Day 5 [357.00, 474.40, 232.50, 225.15]; Day 6 [600.90, 474.40, 231.90, 224.90] | Day 2 without its words and coins rms 3.57 / mean 1.75; corner close-ups 3.0-4.8; fill re-toned on 26's (top #F8EDE3, middle #F7EDE4, bottom #F5EBE2); line tan 1.8 #E1C7B3 + white 1.0 #FBF4EB, radius 17.7; motifs land within 1-3 px (close-up rms 5.7-6.1) |
| `UI_DayTile_Bonus.png` | 1091 x 523 | 140,150,140,150 (four corner motifs) | the wide weekly-bonus tile (Day 7) | [843.85, 473.65, 474.30, 227.50] | without its decor rms 10.54 / mean 3.75; corners 8.0-11.2 (26's gold line has highlights and darker stretches that the drawn line, a top-to-bottom gradient, does not have); bands gold 0.9 #DDA866>#D2832A, 2.4 #F0BA60>#E39530, pale 1.0, white 1.0 #FDF3DF, radius 20.7; fill top #FCECD6, bottom #FAE7CC |
| `UI_DayTile_TodayFrame.png` | 542 x 485 | 138,164,138,48 | the overlay for the claimable day: Day 1's crimson keyline and its warmer inside | the tile's rect grown by 2.105 left/right and 1.425 top/bottom: Day 1 [355.6, 252.9, 235.76, 210.80] (other days in screen.json, hidden); over the bonus tile [841.745, 472.225, 478.51, 230.35] | Day 1 rebuilt (tile + overlay + glow + coin) rms 15.6 / mean 6.8 with the words left out; inside only rms 3.87 (6.52 without the overlay); corner close-ups 17-25 (a saturated 2.5 px line: 26 draws the bottom line a hair lower and heavier). Keyline 2.5 px #E01A10 (top) > #EC2008 (bottom), #B80606 on the sides, a white hair on both sides; inside a warm layer of alpha 0.3 (capped: alpha 1 would gain only 0.3 rms on Day 1 and hide the tile under it) |
| `Deco_DayCoins_1.png` | 156 x 160 | - | Day 1's single large coin | [439.18, 329.73, 67.64, 69.55] | rms 9.9 over the coin region; fitlayer 11.9; mean colour within 1.4 levels |
| `Deco_DayCoins_2.png` | 216 x 166 | - | Day 2 pile | [663.83, 330.83, 93.85, 72.34] | fitlayer rms 20.9 |
| `Deco_DayCoins_3.png` | 240 x 180 | - | Day 3 pile | [901.61, 322.17, 104.50, 77.97] | 18.9 |
| `Deco_DayCoins_4.png` | 274 x 192 | - | Day 4 pile | [1141.33, 319.09, 119.35, 83.12] | 18.9 |
| `Deco_DayCoins_5.png` | 316 x 194 | - | Day 5 pile | [404.80, 546.08, 137.40, 84.23] | 16.5 |
| `Deco_DayCoins_6.png` | 360 x 216 | - | Day 6 pile | [642.73, 541.28, 156.75, 94.15] | 32.5 (same stacks, 26's pile is softer and its back stacks sit a little differently) |
| `Deco_DayCoins_7.png` | 480 x 242 | - | Day 7 pyramid (its foot finished under the ribbon) | [975.31, 529.86, 208.40, 105.19] | 32.5 (above the ribbon, laurels left out); fitted 3.8 % narrower than drawn |
| `Deco_DayCoins_{1..7}_Shadow.png` | 266x270, 326x276, 350x290, 384x302, 426x304, 470x326, 590x352 | - | the soft contact shadow under each pile | the coins' rect grown by 23.91 on each side: [415.27, 305.82, 115.47, 117.38], [639.92, 306.92, 141.68, 120.17], [877.70, 298.26, 152.33, 125.80], [1117.42, 295.18, 167.18, 130.95], [380.89, 522.17, 185.23, 132.06], [618.82, 517.37, 204.58, 141.98], [951.40, 505.95, 256.23, 153.02] | blurred silhouette, sigma 6.5 mockup px, 1 right, 4 down, opacity 0.8, #7A4A28 (fitshadow on Day 2 and Day 5: 20.9 -> 12.6 and 19.5 -> 11.6 under the piles) |
| `Deco_Laurel.png` | 86 x 222 | - | the left laurel of Day 7 (mirror it for the right) | left [930.79, 525.98, 37.42, 96.85]; right [1191.8, 525.98, 37.42, 96.85] mirrored | rms 16.2 left, 16.2 right (mirrored); fitted 10 % narrower than drawn; leaves with the ivory outline 26 draws |
| `Deco_Ribbon_Gold.png` | 584 x 105 | 72,0,72,0 (the band stretches; its slight arch flattens if stretched much) | the empty "Weekly bonus" ribbon (the words are a label) | [952.43, 615.89, 254.15, 45.72] | rms 10.7 with the words left out; fitted 4.5 % flatter than drawn; colour put on 26's (the original was a more saturated yellow) |
| `Deco_HeadingFlourish.png` | 312 x 49 | - | the line-and-star flourish either side of "Day 1 of 7" (mirror it for the right) | left [591.65, 212.49, 135.90, 21.33]; right [945.35, 212.49, 135.90, 21.33] mirrored | rms 6.7 (both); brown #96683D as 26 |
| `Icon_DailyReward.png` | 116 x 124 | - | (no mockup) the gift box for the main menu's new button under No Ads | on the No Ads button's ivory disc; drawn at the settings gear's scale (ink 111.5 x 118.5 sprite px against the gear's 108 x 106), so it sits on a header-size disc like `Icon_Settings` | flat #0E0B0C (the header icons' ink), holes transparent, 2 px margin; checked on black/white/magenta (`check/Icon_DailyReward_onbg.png`) |
| `UI_Dot_Alert.png` | 74 x 74 | - | (no mockup) the notification dot for that button | the ready dot's size (31.9 mockup px at ppu 200); e.g. on the disc's upper right | satin crimson #CE2420 -> #8D110D (the wallet +: #C82427 -> #880E10), ivory ring #FDF9EF from 0.775 R |

Import settings: `ArtSource/Kit/DailyRewards.json`, applied with `Callbreak.Editor.KitImporter.Apply()` through
`Tools/Unity/ueval.sh` on 8 Oct 2026 ("kit import settings applied to 178 sprites"; the new metas have ppu 200, the
borders above, no mip maps, like their siblings). Copies in `ArtSource/Exports/{UI,Decor,Icons}/`; 22 rows in
`ArtSource/handoff.csv`.

Rebuilds (`tools/check.sh`, over the mockup, words left out): the whole panel with everything in it rms 19.4 / mean 7.7
(`check/rebuild_panel.png`; most of it is the claim button's red and the coins' edges); the seven tiles 16.2 / 6.4
(`check/rebuild_tiles.png`); Day 1 15.6 / 6.8 (`check/rebuild_day1.png`).

`Tools/art job DailyRewards` (screen.json -> layout.json): reconstruction error 6.44 overall as built (the hidden today
overlays and lights of Days 2-7 drawn too), 5.21 with them left out (`check/visible/`); Earn Coins, the nearest screen,
is at 4.79. The ~1.0 of the README is not reachable here: the screen draws only kit and batch sprites on the inpainted
shared plate, and Tools/art resizes every sprite whole (no 9-slicing, no `mirror`, no `shadowUse`), so it draws the
stretched kit card as an oval (Panel 13.8), the mirrored laurel, flourish and line the wrong way round (22.9, 18.1, 34.5)
and every hidden node. `tools/check.sh` gives the numbers as Unity will draw them.

## 3 · Decisions and what still differs

- Generated, not cropped. Where ChatGPT drew lines that do not come out exactly (the tiles' outlines, the crimson
  keyline) the frame is drawn at the widths and colours measured on 26 and ChatGPT's inside (fill texture and the peach
  motifs) is kept, re-toned onto 26's fill, as the ShopUI batch did for its reward blocks.
- Today's look is one overlay over the normal tile (as asked), not a third tile: a 2.5 px crimson keyline with white hairs
  and a warm layer of alpha 0.3 that makes the normal tile's inside Day 1's (rms 3.87 inside). On the bonus tile it keeps
  the gold-cream face and its motifs and swaps the gold border for the crimson keyline (`check/today_over_bonus.png`;
  there is no mockup of Day 7 as today). The light behind the coin (`CoinGlow{i}`) is a separate node, as asked.
- The day tiles are one sprite for Days 1-6: 26 draws Days 5-6 17 px taller, which the 9-slice middle takes.
- The ribbon, laurel and coin piles are fitted with a free aspect where 26 asked for it (ribbon 4.5 % flatter, laurel
  10 % narrower, Day 7 pile 3.8 % narrower, Day 1 coin 1.8 % narrower than drawn); the piles' mean colour is within
  0-2 levels in red and green and 1-5 levels low in blue after the curves.
- Labels (all words) are text; DayLabel0 and DayLabel6 are crimson on 26, the other day names dark brown: the builder
  takes each label's measured colour, so a presenter that moves "today" may want to recolour them.
- The gift icon and the dot have no mockup: their size is a proposal (the gear's scale; the ready dot's size).
- Not verified: anything in Unity (no `Tools/Unity/screen.sh` run: the view class comes from the main agent), the tile and
  panel shadows (`shadowUse`), the ScreenExtras fields (capFit, xFit, dilate, lineGradient, mirror) which the
  DailyRewards customizer has to apply.
- Tools: kitcontrols and shopui (read-only use) and `tools/` here (drt) were built into the session scratchpad;
  `tools/make.sh` and `tools/check.sh` take their wrappers from `KC`, `SU`, `DRT`.
