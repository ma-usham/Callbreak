# PlayOnline: the Play Online screen (23 / 24): Quick Match and Stake Match

Mockups: `23-online-play-stake-50.png` (1671 x 941; 50 selected) and `24-online-play-stake-100.png` (1672 x 941;
100 selected), copied to `attach/`. Spec: `D:\Nepali Callbreak\game-screens\online-match-options-prompts.md`.
All rects below are **mockup pixels in the 1672 x 941 frame** (top-left origin): 24 as it is, 23 after resizing it to
1672 wide (`attach/S23n-online-play-stake-50-1672.png`; offsets grow from 0 at the left edge to 1 px at the right).
Sprites are @2x (sprite px = mockup px x 2.3), PNG-32 sRGB, transparent outside. Error numbers are sRGB levels
(rms / mean) of the piece rebuilt at its rect against the mockup, words and numbers left out
(`check/numbers.txt`, `check/reuse_numbers.txt`; pictures `check/<name>_<screen>.png` = mockup | rebuild | difference x4).

ChatGPT chat (one chat, inside the TestProject project; archived at the end):
<https://chatgpt.com/g/g-p-6abf749240f881918a0877a98eb42245-testproject/c/6ac2885c-4eb8-83ee-aa9c-e3d36e0fd417>.
Prompts, attempts and verdicts: `ArtSource/Prompts/PlayOnline.md`. Recipe from the originals to the finals:
`tools/make.sh` (re-run on 5 Oct 2026: reproduces every file in `final/` byte for byte). The whole screen rebuilt
from sprites: `check/overview_24.png`, `check/overview_23.png`.

## 1 · What the kit already covers (reused, nothing generated)

| Mockup element | Kit sprite (under `Assets/_Project/Art`) | Rect / how it fits |
| --- | --- | --- |
| Scenery | `Backgrounds/BG_Menu_Wide.png` (`"background": "shared"`) | the shared menu plate (owner's rule) |
| Back button | `UI/Buttons/UI_ButtonCircle_Normal.png` + `UI/Icons/Icon_Back.png` (+ `UI_Shadow_Circle`) | disc ring centre (86.67, 77.55), radius 45.0: sprite rect [38.42, 29.3, 96.5, 96.5]; arrow [66.52, 58.39, 39.86, 38.02], IoU 0.942 (24) / 0.879 (23). **23/24 draw a bolder arrow** than the kit's: inside the disc rms 31.1 (24) / 28.2 (23), almost all the arrow's stroke weight |
| Wallet | `UI/Buttons/UI_ButtonSecondary_Normal.png` (nine 117,0,117,0) + `UI/Icons/Icon_Coin.png` | capsule [1436.1, 54.2, 190.0, 67.9], coin [1458.30, 57.30, 58.40, 58.40]; balance left out: rms 25.9 / 26.3 (the mockup's capsule outline is a softer single tan line and its coin has a dark contact shadow) |
| Gold coin (wallet, chips, prize rows) | `UI/Icons/Icon_Coin.png` | fitted: wallet 58.4-58.8 px, chips 41.3-42.8 px, rows 34.3-34.8 px; rms 12.9-18.3; mean colour within 0.3-6.5 levels on every use. Chips: 24 [859.88, 501.38, 42.75, 42.75] and [1060.95, 501.95, 42.10, 42.10]; 23 [861.88, 502.38, 41.25, 41.25] and [1063.98, 501.83, 41.55, 41.55]. Rows (24): [1304.38, 624.13, 34.25, 34.25], [1304.13, 668.28, 34.75, 34.75], [1304.38, 713.48, 34.25, 34.25], [1304.10, 758.90, 34.60, 34.60] |
| Main panel | `UI/Panels/UI_Panel_Large.png` (nine 400,370,400,260, unit 2.3) | [305.9, 187.1, 1064.9, 650.8]: plain interior rms 3.4 / 3.2; top-left textile corner 12.5 / 11.7 (ornament placed a little differently); bottom corners 8.3 / 7.9 (the kit's faint mountains; the mockup has none) |
| Play Quick / Play for N coins | `UI/Buttons/UI_ButtonPrimary_Normal.png` (nine 202,0,202,0, unit 326/91 = 3.582) | [344.5, 692.5, 371.5, 91] and [790, 692.5, 373, 91]; labels and chevrons left out: rms 33.3 / 25.1 (24), 32.7 / 25.1 (23): the right drawing (gold rim, woven ends), the mockups' woven ends differ in detail. The plain crimson UI_Button_Crimson: 47 |
| Their chevrons | `UI/Common/Icon_Chevron_Light.png` (or `UI/Icons/Icon_Chevron_Thin.png`) | Light IoU 0.866-0.910, Thin 0.821-0.909: Light a little better overall (mean 0.887 against 0.862) |

Not reused, with the numbers that decided it (each replaced by a new piece in section 2):

| Kit candidate | Against | New piece |
| --- | --- | --- |
| `UI_ButtonDisc_Normal` (shop drawer disc) / `UI_ButtonCircle_Normal` (header disc) | the ivory badge behind the lightning and the coins: rms 12.5 / 13.2 and 7.3 / 7.4 (lightning left out) | `UI_ButtonDisc_Emblem`: 4.3 / 4.8 (3.0 / 3.5 with its shadow) |
| `UI_Outline_Thin_Selected` (crimson #800405) | the Stake Match outline, bright red with a white inner line: 42-66 along its sides | `UI_Outline_Thin_Red`: 11-22 |
| `Deco_TitleDivider` / `_Line` + `_Diamond` | the dividers under the mode titles (with gaps, taller diamond): 17.5 / 16.7 whole, 14.6 / 14.6 as pieces at the placeholder rects | `Deco_ModeTitleDivider_*`: 5.2-7.4 |
| `_Line` + `_Diamond` | the subtitle flanks (a line through a four-point star): 14.1 / 13.8 | `Deco_SubtitleFlourish_*`: 7.2-9.6 |
| drawn rects + `_Diamond` | the vertical rule: 8.6 / 10.7 | `Deco_ModeDivider_Vertical`: 6.7 / 5.8 |
| `Icon_Close` (near-black) | the popover's brown x: IoU 0.966 / 0.932 | `Icon_Close_Brown`: 0.986 / 0.965 |

## 2 · New pieces (`final/`, copied to `ArtSource/Exports/`)

| File | Size (px) | 9-slice L,T,R,B | Mockup element | Rect (mockup px) | Check (rms / mean, 24 then 23) |
| --- | --- | --- | --- | --- | --- |
| `UI_StakeChip_Selected.png` | 454 x 164 | 83,0,83,0 (unit 2.303: the canvas is the chip's 71.2 px height) | selected stake chip, crimson with a gold rim; empty | one rect per chip for both looks: 50 [816.0, 488.0, 196.7, 71.2], 100 [1025.0, 488.0, 185.9, 71.2] (fitted: 23's 50 [816.75, 487.85, 195.4, 71.2], 24's 100 [1022.3, 488.1, 188.0, 71.2]) | 19.4 / 18.6 with its shadow (mostly the 1 px rim bands); with the kit coin 18.3 (23) |
| `UI_StakeChip_Normal.png` | 454 x 164 | 83,0,83,0 | the other chip, ivory with a tan outline; empty; its pill 69 px tall inside the same canvas (as drawn) | same rects (fitted: 24's 50 [815.2, 488.1, 198.0, 71.2], 23's 100 [1027.7, 487.9, 183.8, 71.2]) | 4.4 / 5.0; with the kit coin 12.1 (24) |
| `UI_StakeChip_Shadow.png` | 530 x 240 | 121,0,121,0 | soft shadow under either chip | the chip's rect grown by 16.52 on each side | (in the chip numbers) fitted: 4.1 -> 1.3 (24), 6.1 -> 1.4 (23) |
| `UI_ButtonInfo.png` | 122 x 122 | - | the info button with its i (one sprite) | [1223.9, 490.73, 53.04, 53.04] (centre 1250.42, 517.25) | 13.6 / 13.6; the i's ink box within 0.15 px |
| `UI_ButtonDisc_Emblem.png` | 344 x 344 | - | ivory badge behind the lightning and the coins | lightning [464.39, 263.87, 149.57, 149.57]; coins [986.35, 224.36, 151.96, 151.96] | lightning badge (disc, shadow, lightning) 5.3 / 5.2 |
| `UI_ButtonDisc_Emblem_Shadow.png` | 400 x 400 | - | the badge's soft shadow | the disc's rect grown by 12.17 (lightning: [452.22, 251.70, 173.91, 173.91]) / 12.37 (coins: [973.98, 211.99, 176.70, 176.70]) | fitted 2.5 -> 1.8 |
| `Icon_Lightning.png` | 138 x 220 | - | dark-crimson lightning (Quick Match) | [509.64, 292.10, 60.0, 95.65] (2 px margin; ink box [510.51, 292.97, 58.42, 94.16]) | IoU 0.992 / 0.991; in its box 9.5 (24) |
| `Deco_StakeCoins.png` | 274 x 210 | - | stacked coins (Stake Match badge) | [1003.95, 249.13, 119.40, 91.25] | coins badge (disc, shadows, coins) 16.7 / 17.2 |
| `Deco_StakeCoins_Shadow.png` | 352 x 288 | - | the coins' contact shadow on the badge | [986.99, 232.17, 153.32, 125.17] | fitted 19.1 -> 15.7 |
| `UI_Popover.png` | 651 x 591 | 52,52,52,52 | prize popover box: fine gold edge, ivory; empty | [1175, 557, 283, 257] | whole popover (shadows, pointer, x, row lines, kit coins) 17.7 / 16.9 |
| `UI_Popover_Pointer.png` | 115 x 51 | - | its pointer toward the i, with 10 px of the box's top edge on each side; draw over the box | [1225.5, 543, 50, 22] (apex x 1250.5 = the i's centre; bottom 8 px over the box's top edge) | at the joint 15.8 (24) |
| `UI_Popover_Shadow.png` | 788 x 728 | 120,120,120,120 | the popover's soft shadow | [1145.43, 527.43, 342.14, 316.14] (box grown by 29.57) | fitted left of the box: 9.6 -> 1.7 |
| `UI_Popover_Pointer_Shadow.png` | 252 x 168 | - | the pointer's shadow (draw with the box's, under the box) | [1195.93, 513.43, 109.57, 73.04] | |
| `Icon_Close_Brown.png` | 46 x 46 | - | the popover's x, flat #573011 | [1415.76, 578.07, 20, 20] | 11.3 / 9.0 |
| `UI_PrizeRowLine.png` | 532 x 14 | 3,0,3,0 | thin line between two prize rows | [1200.35, 660.81, 231.3, 6.087], [1200.35, 705.81, ...], [1200.35, 750.46, ...] | 4.2 / 3.5 / 4.4 (24; 14 without a line) |
| `UI_Outline_Thin_Red.png` | 144 x 144 | 64,64,64,64 (unit 2.3) | the Stake Match area's selected outline; hollow | [766.45, 200.15, 594.1, 622.9] | sides 12.8-19.2 / 12.1-22.1, corners 11.4-18.8 / 15.0-17.3 (flat ground) |
| `Deco_ModeTitleDivider_Quick.png` | 776 x 48 | - | divider under Quick Match (lines, gaps, diamond) | [371.5, 500.5, 337.39, 20.87] | 7.4 / 6.6 |
| `Deco_ModeTitleDivider_Stake.png` | 826 x 46 | - | divider under Stake Match | [885.5, 423.5, 359.13, 20] | 6.0 / 5.2 |
| `Deco_ModeDivider_Vertical.png` | 36 x 1328 | - | vertical divider between the modes (diamond above the middle) | [737.82, 222.17, 15.652, 577.391] | 6.7 / 5.8 |
| `Deco_Diamond_Bronze.png` | 36 x 42 | - | the dividers' diamond alone (also inside the three dividers) | quick [531.45, 501.75, 15.55, 18.45], stake [1055.37, 424.69, 15.33, 18.23], vertical [738.58, 480.3, 14.25, 18.1] | IoU 0.953-0.985 |
| `Deco_SubtitleFlourish_Left.png` | 170 x 31 | - | flourish left of CHOOSE YOUR MATCH | [564.15, 142.69, 73.91, 13.48] | 9.6 / 8.8 |
| `Deco_SubtitleFlourish_Right.png` | 196 x 31 | - | flourish right of it | [1025.98, 142.66, 85.22, 13.48] | 7.7 / 7.2 |

Alpha on black / white / magenta: `check/onbg/*.png` (no fringes).

## 3 · Decisions and what still differs

- **Generated, not cropped.** ChatGPT drew every shape (chips, info button, badge, coins, popover look, lightning, x,
  diamond, flourish). Where the mockup's lines are 1-2 px (the chip rims, the popover's gold edge, the info ring, the
  badge rim, the dividers' lines, the row line, the red outline), they are drawn or painted at the widths and colours
  measured on 23 and 24, ChatGPT's piece giving the look (as the Controls and ShopUI kits did): ChatGPT drew them
  2-3x too heavy, or gold where the mockup is copper.
- **Chips:** both looks share one canvas and one rect per chip, so selecting only swaps the sprite. The selected
  chip's rest error (18-19) is the thin rim bands landing a fraction of a pixel off the soft mockup.
- **The two mockups disagree** by up to 5 px on the 100 chip's left edge (1021.5 on 24, 1027 on 23) and the
  chip widths (197 for 50, 184-189 for 100): the rects above are the averages.
- **Popover:** the pointer is a separate sprite placed under the i; its sprite carries a stretch of the box's top edge
  so the lines join. Its shadow is separate from the box's.
- **Badge:** one disc for both badges; the coins badge's ring is 1.6 % larger on the mockups (rect scaled).
- **Divider lines** are darker in their outer halves and fade at both ends, as measured; one sprite per divider
  (exact layout); the diamond is also delivered alone.
- **Header:** the subtitle's flanks are not the kit's title divider (a line through a small four-point star on each
  side of the subtitle, not a divider under it), so they were added (not in the brief's list).
- **Kit pieces with visible differences** (not changed, they are shared): the back arrow is bolder on 23/24; the
  wallet capsule's outline is softer; the primary buttons' woven ends differ in detail; the panel's textile corner
  sits slightly differently and the kit has faint mountains at the bottom corners.
- **Words and amounts are labels**: chip amounts, "Play for N coins", "N-coin prizes", places and prizes.
- **Tools:** `tools/po.js` (node, no packages) for the measuring and drawing kitcontrols and shopui don't have;
  kitcontrols (`Tools/ArtPipeline/Kit/Controls`) and shopui (`ArtSource/Working/ShopUI/tools`) used read-only, from
  their builds in the session scratchpad (`KC`, `SU` in make.sh). A C# helper built for this batch was blocked by
  Smart App Control on its second build, so the helpers were rewritten in node.

## 4 · Swap map for the placeholders in `ArtSource/Screens/PlayOnline/screen.json` (5 Oct 2026)

| Node(s) | Now | Use instead |
| --- | --- | --- |
| QuickIcon | `Icon_Lightning_Placeholder` | `Icon_Lightning.png` at [509.64, 292.10, 60.0, 95.65] |
| QuickDisc, StakeDisc | kit `UI_ButtonDisc_Normal` | `UI_ButtonDisc_Emblem.png` (+ `_Shadow`) at the rects in section 2 |
| StakeCoins | `Deco_CoinStack_Placeholder` | `Deco_StakeCoins.png` (+ `_Shadow`) |
| ChipNormal0/1, ChipSelected0/1 | `UI_StakeChip_*_Placeholder` | `UI_StakeChip_Normal.png` / `UI_StakeChip_Selected.png`, same rect for both looks (+ `UI_StakeChip_Shadow.png` under the chip) |
| InfoButton | `UI_InfoButton_Placeholder` | `UI_ButtonInfo.png` |
| Popover (+ shadow) | `UI_PrizePopover_Placeholder`, `UI_Panel_Card_Shadow` | `UI_Popover.png` (nine 52) + `UI_Popover_Shadow.png` (nine 120) |
| PopoverPointer | `UI_PrizePopover_Pointer_Placeholder` | `UI_Popover_Pointer.png` (+ `UI_Popover_Pointer_Shadow.png`) |
| CloseIcon | `Icon_Close_Small_Placeholder` | `Icon_Close_Brown.png` |
| PrizeRule0-2 | drawn `Deco_PrizeRule` | `UI_PrizeRowLine.png` (nine 3,0,3,0) |
| ModeRuleTop/Bottom/Diamond | drawn hairlines + kit diamond | `Deco_ModeDivider_Vertical.png` (one sprite) |
| QuickRuleLeft/Diamond/Right | kit `Deco_TitleDivider_Line` + `_Diamond` | `Deco_ModeTitleDivider_Quick.png` |
| StakeRuleLeft/Diamond/Right | kit pieces | `Deco_ModeTitleDivider_Stake.png` |
| SubtitleLine*/SubtitleDiamond* | kit pieces | `Deco_SubtitleFlourish_Left.png` / `_Right.png` |
| StakeOutline | kit `UI_Outline_Thin_Selected` recoloured by the customizer | `UI_Outline_Thin_Red.png` (nine 64, unit 2.3; no recolouring needed) |
