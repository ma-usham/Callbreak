# ShopUI: the shop (15 / 20 / 21), Earn Coins (22) and the coin wallet (23)

Mockups (1672 x 941): `15-card-styles.png` NEW version (from `D:\Nepali Callbreak\game-screens\`, copied to
`attach/S15-card-styles.png`; `Assets/ArtRefrences/game-screens/15-card-styles.png` was replaced by the same file during the session), `20-backgrounds.png`,
`21-table-styles.png`, `22-rewarded-coins.png`, `23-coinsection.png`. All rects below are mockup pixels
(top-left origin). Sprites are @2x (sprite px = mockup px x 2.3), ppu 200, PNG-32 sRGB, transparent outside.

ChatGPT chat (one chat for the batch): <https://chatgpt.com/c/6ac24c64-b050-83ec-ab7b-eb5085b6f94f>.
Prompts, attempts and verdicts: `ArtSource/Prompts/ShopUI_Kit.md`. Recipe from the originals to the finals:
`tools/make.sh` (re-run on 4 Oct 2026: reproduces every file in `final/` byte for byte).

## 1 · What the kit already covers (reused, nothing generated)

Checked against the five mockups (overlays in `check/reuse_*.png`; error = sRGB levels over the element,
its words and icons left out):

| Mockup element | Screens | Kit sprite (path under `Assets/_Project/Art`) | How it fits |
| --- | --- | --- | --- |
| Scenery | 15 20 21 22 23 | `Backgrounds/BG_Menu_Wide.png` (`"background": "shared"`) | the shared menu plate (owner's rule); 23 is the main menu itself |
| Back button | 15 20 21 22 | `UI/Buttons/UI_ButtonCircle_Normal.png` + `UI/Icons/Icon_Back.png` (+ `UI/Buttons/UI_Shadow_Circle.png`) | the same header button as every menu screen; disc [43.1, 35.6, 85.5, 85.2] on 15 |
| Title divider (under the subtitle) | 15 20 21 22 | `UI/Decor/Deco_TitleDivider.png`, or `_Diamond` + `_Line` as in `ArtSource/Kit/Decor.title-divider.csv` | diamond 15 px on 15/20/21, 19 px on 22 (the kit diamond is 13 px at 2.3: drawn 1.15x / 1.46x) |
| Drawer divider (line + diamond under the shop button) | 15 20 21 | `UI/Decor/Deco_TitleDivider_Diamond.png` [1553.25, 182.75, 13.5, 13.5] + `UI/Decor/Deco_TitleDivider_Line.png` left (flipped) [1477.5, 188.2, 76.0, 3.5] and right [1566.5, 188.2, 76.5, 3.5] | same drawing as the title divider, shorter; the mockup's line is 2 px with a white hairline above and below |
| Carousel panel (the wide rounded panel) | 15 20 21 | `UI/Panels/UI_Panel_Card.png`, nine 56, unit 2.065 (corner radius 24.5) at [329.0, 198.7, 977.8, 537.2] | mean 2.4, rms 3.7 (outside the tiles); plain like the mockup. `UI_Panel_Large` was not used: its textile corners and mountains are not on these panels |
| Product tile | 15 20 21 | `UI/Panels/UI_Panel_Card.png`, nine 56, unit 2.66 (radius 19-20.5); or this batch's `UI_Panel_Card_Light.png` (same sprite 3-5 levels lighter) | kit card: mean 4.5 / 3.4 levels; light variant: mean 1.5 (15) / 2.9 (21). Tiles on 15: [406.0, 224.8, 269.4, 429.1], [691.6, 226.0, 262.3, 425.4], [969.9, 226.0, 261.0, 425.3]; 21: [399.7, 224.1, 266.2, 426.9], [681.0, 222.0, 273.3, 431.9] (outline box), [971.7, 224.1, 266.3, 426.9] |
| Selected tile outline (thin crimson line) | 15 20 21 | `UI/Controls/UI_Outline_Thin_Selected.png`, nine 64, unit 2.92 (line 2.05 px, radius 20.5) | rms 4.3-4.9 with the line; on 15 at [406.15, 222.65, 271.8, 430.9] |
| Radio, filled / empty | 15 20 21 | `UI/Controls/UI_Radio_On.png`, `UI_Radio_Off.png` (made from the old 15, same drawing) | rms 10-14 (21 filled: 22): the new mockups' dot is a brighter red and the ring a darker brown |
| Carousel arrows | 15 20 21 | disc `UI/Buttons/UI_ButtonCircle_Normal.png` (fits the small 64 px disc better than this batch's disc: rms 10.4 vs 12.6) + `UI/Icons/Icon_Chevron.png` (flip for the left one) | discs about 64-66 px: 15 [334.8, 395.4, 65.4, 65.4] and [1234.8, 395.5, 65.4, 65.4]; chevron ink 16.3 x 27.1 (Icon_Chevron drawn 1.2x) |
| Scroll track + crimson thumb | 15 20 21 | `UI/Controls/UI_Slider_Track.png` (beige #D9C9BB, mockup #DECCBD) and `UI/Controls/UI_Slider_Fill.png` as the thumb | track [663, 673.5, 346, 11]; thumb [663, 673.5, 69, 11]; the thumb on the mockup is a brighter crimson (#991518 on 15, #A5070B on 21 against the fill's #7F061B) |
| "Use Selected Style / Background", "Buy for 350" | 15 20 21 | `UI/Controls/UI_Button_Crimson.png` (plain crimson pill, nine 160,0,160,0) | rms 18.7 on 15 (the gold-rimmed pill: 50.4) at [564.4, 764.2, 544.8, 92.6] |
| "Watch ad · +20 coins" | 22 | `UI/Buttons/UI_ButtonPrimary_Normal.png` (gold-rimmed pill, nine 202,0,202,0) | the right drawing (gold rim, woven ends); 22 draws it brighter red (rms 54, the plain pill 66) at [530.7, 664.2, 610.5, 103.7] |
| Main panel of Earn Coins | 22 | `UI/Panels/UI_Panel_Large.png` (nine 400,370,400,260) at [336.1, 190.7, 1000.1, 649.3] | mean 5.8: same ivory and textile accent; the mockup's textile clusters sit inside the top corners and it has no mountains at the bottom |
| Wallet capsule | 22 23 | `UI/Buttons/UI_ButtonSecondary_Normal.png` (nine 117,0,117,0) | 23 [1107.8, 64.8, 234.7, 69.0]: rms 10.2 (contents left out); 22 [1436.5, 51.8, 192.0, 71.7] (22's outline is redder, #AE6E4E) |
| Clock | 22 | `UI/Icons/Icon_Clock.png` (#2F1B0E) at about [661.9, 780.9, 37.8, 37.0] | same icon; 22 draws its ring thinner and the kit icon is drawn 1.2x its export size |
| Header buttons of the main menu | 23 | gear `UI_ButtonCircle_Normal` + `UI/Icons/Icon_Settings.png`; shop `UI_ButtonCircle_Normal` + `UI/Icons/Icon_Shop.png` | identical to 00 (the main menu's storefront button is NOT the drawer's storefront: a different drawing) |
| Everything else on 23 | 23 | as on 00: avatar, logo, fan, PLAY ONLINE, secondary buttons, footer | only the wallet is new |
| Hairlines (header separator on 23) | 23 | none: drawn rect (keep-as-drawn, as in `Decor.map.csv`) | |
| Coin shadow (on the crimson button and the ivory) | 21 22 23 | `UI/Buttons/UI_Shadow_Circle.png` | optional |

## 2 · New pieces (ChatGPT-generated, `final/`)

| File | Size (px) | 9-slice L,T,R,B | Mockup element | Placement (mockup px) | Check |
| --- | --- | --- | --- | --- | --- |
| `UI_Drawer.png` | 534 x 836 | 0,170,0,280 (stretches vertically only; top and bottom bands hold the textile) | the shop drawer (15 20 21) | [1439.8, -0.8, 232.2, 942.6] on all three (flush with the right, top and bottom screen edges) | buttons and labels left out: rms 6.9 / 7.2 / 7.0 levels (15 / 20 / 21), plain middle 2.2; left corners radius 29 (mockup), thin bright rim on the left edge |
| `UI_Drawer_Shadow.png` | 680 x 982 | 0,243,0,353 | the soft shadow left of the drawer | [1408.06, -32.54, 295.68, 1006.08] (the drawer's rect grown by 73 sprite px); moves with the drawer | blurred silhouette (sigma 10.5 mockup px, opacity 0.5, #2A1A12): 25 % darker at the drawer's edge, 13 % at 7 px, as on 15 |
| `UI_ButtonDisc_Normal.png` | 316 x 316 | - | the ivory round buttons of the drawer (shop button and the inactive categories) | shop 15 [1503.45, 47.15, 114.10, 114.10] (20: [1503.10, 47.15, 114.10, 114.10]; 21: [1503.45, 47.30, 113.60, 113.60]); 15 Backgrounds [1491.60, 427.75, 137.80, 137.80], 15 Tables [1491.93, 636.53, 136.45, 136.45]; 20 Cards [1490.93, 221.03, 138.95, 138.95], 20 Tables [1491.60, 635.80, 136.80, 136.80]; 21 Cards [1489.90, 220.80, 140.20, 140.20], 21 Backgrounds [1491.63, 428.28, 137.45, 137.45] | rms 7.5-9.9 per button on flat ground (its shadow not drawn); with the icon: rms 7.05 against 11.8 for the kit's header disc; colour +0..+3 levels. Shadow: `UI/Buttons/UI_Shadow_Circle.png`, rect = the disc's grown by 9.9 % of its diameter each side, drop 2, opacity 0.5 |
| `UI_ButtonDisc_Selected.png` | 338 x 338 | - | the crimson (selected) category button | 15 Cards [1488.13, 218.58, 144.35, 144.35]; 20 Backgrounds [1486.60, 422.25, 147.00, 147.00]; 21 Tables [1490.18, 634.73, 139.55, 139.55] | crimson to 0.922 R, white ring to 0.973 R, tan line to R. Mean colour -0.5..+5 levels, median crimson within 7; with its icon: rms 14.6 / 11.6 / 14.4, mean 5.8 / 4.4 / 6.5 (region incl. the shadow, not drawn) |
| `Icon_Storefront_Brown.png` | 140 x 144 | - | drawer's shop icon (15 20 21) | sprite [1530.37, 73.57, 60.87, 62.61] (ink [1531.52, 74.75, 58.57, 60.25]) | IoU 0.965 (15), 0.959 (20), 0.955 (21); ink #4A2C15 vs #472A13 |
| `Icon_CardsStacked_Brown.png` / `_Ivory.png` | 194 x 194 | - | Cards category icon, inactive / selected | brown 21 [1517.51, 251.89, 84.35, 84.35], brown 20 [1517.30, 251.48, 85.07, 85.17]; ivory 15 [1518.09, 251.48, 84.19, 85.37] | IoU 0.966 (21), 0.973 (20), 0.873 on the crimson (the disc's ring in the region); colour -4..0 / -1..+0 |
| `Icon_Mountains_Brown.png` / `_Ivory.png` | 212 x 116 | - | Backgrounds category icon | brown 15 [1514.72, 469.44, 92.17, 50.43], brown 21 [1514.16, 469.51, 92.68, 50.70]; ivory 20 [1514.09, 469.28, 92.63, 50.86] | IoU 0.978 / 0.952 / 0.939; colour +5..+7 (brown, 15 draws it darker) / -2..-7 (ivory) |
| `Icon_TableOval_Brown.png` / `_Ivory.png` | 218 x 144 | - | Tables category icon | brown 15 [1512.56, 680.50, 94.78, 62.61], brown 20 [1512.24, 680.14, 94.94, 62.82]; ivory 21 [1513.53, 681.26, 92.85, 61.88] | IoU 0.973 / 0.935 / 0.873; brown +8..+12 lighter than 15's (#3E230D), ivory +1..+3 |
| `Icon_Coin.png` | 132 x 132 | - | the gold spade coin, all four sizes | 22 wallet [1460.95, 57.96, 56.70, 57.29]; 23 wallet [1130.53, 73.43, 52.55, 52.55]; 21 prices [759.90, 584.85, 45.20, 45.20] and [1046.98, 584.83, 45.25, 45.25]; 21 Buy button [839.93, 786.28, 46.75, 46.75] | one sprite for 45-57 px (drawn 0.79-0.99x); mean colour within 0-8 levels on every use; rms 12.5 (22) to 18.5 (23) |
| `UI_ButtonPlus_Normal.png` | 114 x 114 | - | the crimson + of the main menu's wallet (23) | [1276.6, 75.4, 48.4, 48.4] | region rms 10.0; mean colour -2..+4; no white ring (the mockup has none: ChatGPT's ring was dropped) |
| `Icon_Play.png` | 108 x 120 | - | the ivory play triangle of "Watch ad" (22) | [628.75, 689.60, 46.30, 51.89] | rms 6.2, mean colour 0.0 / +0.2 / -0.3; its dark shadow on the crimson is not in the sprite |
| `UI_RewardBlock_Normal.png` | 140 x 140 | 62,62,62,62 | reward block with the double gold keyline (Ad 2, Ad 3) | Ad 2 [711.00, 325.85, 249.85, 254.40]; Ad 3 [1026.85, 325.85, 252.00, 254.40] | corner close-ups rms 13.1-15.5 (Ad 2), 10.9-16.8 (Ad 3) (thin lines on a soft mockup; check/UI_RewardBlock_Normal_corners*.png); outer radius 26.5, bands 1.9 gold / 0.9 light / 1.4 gold / 1.2 light, fill #FAF1E7 |
| `UI_RewardBlock_Selected.png` | 140 x 140 | 62,62,62,62 | the first block's crimson keyline (Ad 1) | [394.70, 327.25, 248.80, 251.00] | close-ups rms 12.1-14.2 (check/UI_RewardBlock_Selected_corners.png); radius 24.5, crimson 2.5 (#D41212 to #BC0A0A), light 0.8, gold 1.2, light 1.0 |
| `Deco_CoinPile_Small.png` | 312 x 206 | - | the 20-coin pile (Ad 1) | [445.78, 421.30, 135.25, 89.20] | colour within 0-2 levels on average; rms 18.7 (edges and emboss detail) |
| `Deco_CoinPile_Medium.png` | 388 x 252 | - | the 30-coin stack (Ad 2) | [752.03, 401.45, 168.15, 109.10] | within 0-2 levels; rms 13.2 |
| `Deco_CoinPile_Large.png` | 436 x 266 | - | the 50-coin group (Ad 3) | [1057.78, 396.10, 188.75, 115.60] | within 0-2 levels; rms 16.0 (fitted 3.9 % wider than drawn) |
| `Deco_CoinPile_*_Shadow.png` | 410 x 304 / 486 x 350 / 534 x 364 | - | the soft contact shadow under each pile | Small [424.54, 400.08, 177.74, 131.64]; Medium [730.79, 380.24, 210.63, 151.53]; Large [1036.57, 374.80, 231.18, 158.19] (behind the pile) | blurred silhouette, sigma 5 mockup px, 6 px down, opacity 0.8, #7A4A28: block rebuild rms 19.6 -> 14.7 |
| `Deco_CoinGlow.png` | 544 x 544 | - | the soft light rays behind the piles | 236 x 236 centred on each pile: Ad 1 [395.4, 347.9, 236, 236], Ad 2 [717, 334, 236, 236], Ad 3 [1034.2, 335.9, 236, 236] | upper part of the blocks: rms 2.2 / 5.1 / 3.2 (without it 5.6 on Ad 2) |
| `Deco_ConnectorLine.png` | 24 x 12 | 4,0,4,0 | the thin gold line between the blocks | [643.6, 443.85, 67.4, 5.2] and [960.85, 443.85, 66.0, 5.2] | rms 7.3 / 9.3 (the mockup's line is lighter near its ends) |
| `UI_Panel_Card_Light.png` | 144 x 144 | 56,56,56,56 | optional: the product tile 3-5 levels lighter than the kit card | as the tiles in section 1 (unit 2.66) | derived from the kit's ChatGPT card (no new request) |

Full rebuilds from these sprites (words left out): the drawer of 15 `check/rebuild_drawer_15.png` (rms 9.3,
mean 4.4 over the whole drawer), the three reward blocks `check/rebuild_block{1,2,3}_22.png` (rms 18.4 /
14.8 / 16.9, mean 7.2 / 7.1 / 8.2; what remains is mostly the coins' edges and emboss detail and the soft halo of the labels).
Alpha on black / white / magenta: `check/onbg/*.png` (no halos).

## 3 · Decisions and what still differs

- Generated, not cropped. Where ChatGPT drew lines that are too thin to come out exactly (the reward block
  keylines, the 3 px connector) the final is drawn at the widths and colours measured on 22, with ChatGPT's
  sheet as the reference (as the Controls kit did for its outlines and pills).
- The drawer's ivory buttons are a new disc (`UI_ButtonDisc_Normal`), not the kit's header disc: at 110-140 px
  the kit disc is enlarged 1.5x and shows a tan ring where the drawer's buttons have a bevelled white highlight
  (rms 11.8 against 7.05). The small carousel arrows keep the kit disc (it fits them better).
- One brown for all drawer icons (#4A2C15; the mockups' icons are #3E230D-#503219 and mottled); one ivory
  (#FAF2E8). Holes in the icons are transparent: the disc shows through, as on the mockups.
- One coin sprite for the four sizes (45-57 px); 22's coin is a little more orange than 21/23's: the sprite
  sits between them.
- The coin piles are fitted with a free aspect (up to 3.9 % stretch for the 50 pile) because ChatGPT drew the
  big pile slightly narrower than 22.
- Storefront: the door's arch is 1-2 px narrower than 15's. Cards icon on 20 draws 1 % larger than on 21.
- Labels (Cards, Backgrounds, Tables, Ad n, n coins, 250, Equipped/Owned/Selected, prices) are text.
- Tools: kitcontrols (`Tools/ArtPipeline/Kit/Controls`, read-only use) and `tools/` here (shopui) were built
  into the session scratchpad with `dotnet build ... --artifacts-path <scratchpad>`; `tools/make.sh` takes their
  wrappers from `KC` and `SU`.

## 4 · Swap map for the placeholders already in `ArtSource/Exports/Screens/` (4 Oct 2026)

The Shop and Earn Coins screens are being built with cut-outs of the mockups (`handoff.csv`: "cut from
mockup ... Placeholder"). These finals replace them:

| Placeholder (`ArtSource/Exports/Screens/...`) | Use instead |
| --- | --- |
| `Shop/UI_ShopDrawer_Placeholder.png` (+ `_Shadow`) | `UI_Drawer.png` (+ `UI_Drawer_Shadow.png`) |
| `Shop/UI_ShopCategoryDisc_Active_Placeholder.png` | `UI_ButtonDisc_Selected.png` |
| (the inactive category discs and the drawer's shop disc) | `UI_ButtonDisc_Normal.png` |
| `Shop/ShopButton_Shadow.png` | `UI/Buttons/UI_Shadow_Circle.png` (kit), see section 2 |
| `Shop/Icon_ShopStore_Placeholder.png` | `Icon_Storefront_Brown.png` |
| `Shop/Icon_ShopCards_Dark_Placeholder.png` / `_Light_` | `Icon_CardsStacked_Brown.png` / `_Ivory.png` |
| `Shop/Icon_ShopBackgrounds_Dark_Placeholder.png` / `_Light_` | `Icon_Mountains_Brown.png` / `_Ivory.png` |
| `Shop/Icon_ShopTables_Dark_Placeholder.png` / `_Light_` | `Icon_TableOval_Brown.png` / `_Ivory.png` |
| `Shop/Deco_ShopDrawerDivider_Placeholder.png` | kit `UI/Decor/Deco_TitleDivider_Diamond.png` + `Deco_TitleDivider_Line.png` (section 1) |
| `Shop/UI_Coin_Placeholder.png`, `EarnCoins/Icon_Coin.png` | `Icon_Coin.png` |
| `Shop/UI_ShopTrack.png`, `UI_ShopTrackThumb.png` (drawn pills) | fine as drawn; or kit `UI_Slider_Track` / `UI_Slider_Fill` (section 1) |
| `EarnCoins/UI_RewardBlock.png`, `UI_RewardBlock_Next.png` | `UI_RewardBlock_Selected.png` for the crimson-framed first block, `UI_RewardBlock_Normal.png` for the gold ones |
| `EarnCoins/Deco_CoinStack20/30/50.png` | `Deco_CoinPile_Small/Medium/Large.png` + their `_Shadow` + `Deco_CoinGlow.png` |
| `EarnCoins/Deco_RewardConnector.png` | `Deco_ConnectorLine.png` (or keep the drawn rect) |
| `EarnCoins/Icon_Play.png` | `Icon_Play.png` |
| `EarnCoins/Icon_Clock.png` | kit `UI/Icons/Icon_Clock.png` |

Not in this batch: the product pictures (card backs are the real decks; the background thumbnails and the
table previews are other agents' work).
