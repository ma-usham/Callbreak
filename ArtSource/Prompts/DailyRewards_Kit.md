# DailyRewards kit (screen 26: day tiles, coins of each day, Day 7 decor, main-menu gift icon)

Chat: ChatGPT, project **TestProject**, chat "Greeting response" (opened 8 Oct 2026 with `hi`), the only chat of
this batch: <https://chatgpt.com/g/g-p-6abf749240f881918a0877a98eb42245-testproject/c/6ac7c198-fe68-83e8-9f5a-965039f6756b>.
Archived at the end of the batch from the sidebar's Chat actions; deleting it is the owner's call.

Browser: the owner's Brave on Windows (listed as "Browser 5", `navigator.brave` true, ChatGPT signed in).
Model and thinking setting left as they were ("Instant").

Source: the owner's new mockup `Assets/ArtRefrences/game-screens/26-daily-rewards.png` (1672 x 941; byte-identical
to `D:\Nepali Callbreak\game-screens\26-daily-rewards.png`). Spec: `D:\Nepali Callbreak\game-screens\26-daily-rewards-prompt.md`.
Style reference for the coins: 22-rewarded-coins (copied from the ShopUI batch).

| Where | What |
| --- | --- |
| `ArtSource/Working/DailyRewards/attach/` | what was sent: the screens (`S<NN>-<name>.png`), the contact sheets (`D<n>_sheet.png`, `.cells.json`), the setup prompt (`P0_setup.txt`), reference images (`R_*.png`) |
| `ArtSource/Working/DailyRewards/sheets/` | the sheet definitions (`kc lsheet`); D4 and D5 were made with `drt refsheet` (commands below) |
| `ArtSource/Working/DailyRewards/chatgpt/` | ChatGPT originals, `D<n>_a<k>.png` = request n, attempt k (every attempt kept) |
| `ArtSource/Working/DailyRewards/work/` | cells and the steps from a cell to a sprite |
| `ArtSource/Working/DailyRewards/check/` | each sprite against mockup 26 (and the reuse checks of kit sprites) |
| `ArtSource/Working/DailyRewards/final/` | the finished sprites (copies in `ArtSource/Exports/` and `Assets/_Project/Art/UI/...`) |
| `ArtSource/Working/DailyRewards/notes.md` | the inventory: what the kit covers, what is new, every final file and its numbers |
| `ArtSource/Working/DailyRewards/tools/` | `drt` (small C# helpers), `make.sh` (originals -> finals, reproduces `final/` byte for byte) and `check.sh` |

Tools: `kitcontrols` (`Tools/ArtPipeline/Kit/Controls`), `shopui` (`ArtSource/Working/ShopUI/tools`), `drt` (this batch),
`Tools/art`, all built into the session scratchpad with `dotnet build ... --artifacts-path <scratchpad>`.

## 0 · Opening

`hi` -> "Hi! How can I help you today?"

## 1 · Setup (the ShopUI setup, adapted)

Attachments: `S26-daily-rewards.png`, `S22-rewarded-coins.png`. Prompt: `attach/P0_setup.txt` (the ShopUI setup's rules 1-6
word for word, the style line, and this batch's list of pieces; "flat dark silhouette icons"; the laurel named as the one
exception to "no leaves").

Reply: "Ready", with a correct one-line description of each screen.

## How a piece is made

1. Measured on 26 (`Tools/art fitrect/profile/inkbox/probe/bounds`, `kc line/hprof/radial`, `shopui fitframe`).
2. `kc lsheet` lays enlarged crops on a 1536 x 1024 sheet fading into an ivory ground; ChatGPT redraws it on flat
   magenta (white for the icon); each element is cut from the original by a box clear of its neighbours and keyed
   (`kc crop` + `kc cut --key magenta`).
3. Each cell is fitted to 26 (`shopui fitlayer`), its colours put on 26's (`shopui colorcmp` -> `kc curve`), and
   brought to @2x (2.3 sprite px per mockup px). Tiles: ChatGPT's inside (fill texture and peach motifs) is re-toned onto
   26's smooth fill (`drt retone`), ChatGPT's own outline replaced (`drt overframe --clean`) and the frame drawn at the
   widths and colours measured on 26 (`drt overframe --bands`), as the ShopUI batch did for its reward blocks.
4. Every piece composited back at its rect, 9-sliced as Unity draws it (`shopui scene/nineview`, `tools/check.sh`).

## 2 · D1: the three day-tile looks

Sheet `attach/D1_sheet.png` (`sheets/D1.json`, scale 1.95): Day 2 tile `[600.6, 253.6, 232.0, 208.2]`, Day 1 tile
`[356.6, 253.6, 233.0, 208.2]`, Day 7 tile `[843.8, 473.3, 474.5, 228.0]`.

```
Sheet D1: the three day-tile looks of 26-daily-rewards (already in this chat). Attached: D1_sheet.png (the contact sheet; the crops are what you copy).

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same three tiles in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no pale ground of the sheet, no drop shadows.

1 day tile (Day 2): the rounded rectangle tile with its warm-ivory inside, its thin soft tan outline with a hair of white just inside it, and the two pale peach woven diamond motifs in its top-left and top-right corners, exactly as in the crop (soft, low contrast, the same size and the same distance from the corners). EMPTY: remove "Day 2", the coin pile with its shadow and "+15 coins", and fill the whole inside with the same plain, even warm ivory. Same corner rounding as in the crop.

2 today's tile (Day 1): the same tile with the thin crimson keyline (a hair of white just inside and just outside it), its warmer, slightly peach-cream inside and the same two peach corner motifs, exactly as in the crop. EMPTY in the same way: remove "Day 1", the coin, the light rays behind it and "+10 coins"; the inside plain, even warm cream.

3 weekly bonus tile (Day 7): the wide tile with its richer gold border (a hair of white just inside it), its warm golden-cream inside and the four pale peach woven motifs in its four corners, exactly as in the crop. EMPTY: remove "Day 7", the two laurel branches, the coin pile, the ribbon with "Weekly bonus" and "+75 coins", and continue the plain, even warm golden cream there.

All three front-on and exactly symmetrical left to right. Do not add anything that is not in the crops.
```

Result: `chatgpt/D1_a1.png` ("Ornate Blank Reward Panels"). **Accepted** (insides and motifs).
- The motifs are the same woven diamonds at the same places (within 1-3 px), but stronger and more orange than 26's:
  their contrast is scaled to 26's (gain 0.56-0.62 on green and blue) over 26's own smooth fill (`drt retone`).
- The outlines came back heavier and redder (a 4-5 mockup px band of line + white): replaced, and the frames drawn at
  26's measured bands (normal: tan 1.8 #E1C7B3 + white 1.0, radius 17.7; bonus: gold 0.9 + 2.4 with a top-to-bottom
  gradient, pale 1.0, white 1.0, radius 20.7).
- The today tile (cell 2) gave the look only. The overlay is solved from the normal tile and 26's Day 1 (see notes):
  solving against ChatGPT's own today tile made a noisy layer of alpha 0.7 that hid the bonus tile under it.
- The keyed cells carry a faint fringe of 1-5 px from the magenta key: the tiles' true boxes are cropped before sizing.

## 3 · D2: the coins of each day

Sheet `attach/D2_sheet.png` (`sheets/D2.json`, scale 2.65): Day 1 `[434, 328, 80, 82]`, Day 2 `[655, 324, 110, 86]`,
Day 3 `[893, 318, 118, 88]`, Day 4 `[1134, 316, 134, 88]`, Day 5 `[400, 543, 148, 90]`, Day 6 `[635, 534, 165, 102]`,
Day 7 `[968, 527, 222, 100]`.

```
Sheet D2: the seven coin illustrations of 26-daily-rewards (already in this chat), one per day; they grow with the amount. Attached: D2_sheet.png (the contact sheet; the crops are what you copy). 22-rewarded-coins shows the same coins.

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same seven coin illustrations in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no pale ground of the sheet.

Keep each exactly as drawn: the same number of coins, the same stacks and their heights, the same overlaps, the same large front coin with its embossed spade, the same polished warm gold (bright highlights, deep amber shading), the thin rims and the soft embossed spades on the stacked coins.
1 is a single large coin, front-on, with its thick rim edge showing a little at the bottom.
7 is the big pyramid of stacks of Day 7. Its bottom is hidden behind a gold ribbon in the crop: remove the ribbon and the words on it, and finish the lowest coins of the stacks plainly, resting on the same baseline as the bottom of the front coin, so the pile ends just below where the ribbon's top edge was.
All seven in the same gold and at the same coin size as in the crops. Remove the pale light rays and the glow behind the coin of 1 and the soft shadow under every pile: magenta right up to the coins, also in the gaps between them. Leave out the laurel branches beside 7. No outline around the coins, no sparkles, no text, no numbers.
```

Result: `chatgpt/D2_a1.png` ("Golden Spade Coin Collection"). **All seven accepted**: the same stacks and front coins as 26,
clean keying, drawn at 2.6-2.9x (so the @2x sprites are downscaled). Colour quantiles put on 26's (mean within 0-2
levels in red and green and 1-5 in blue after the curve; blue was 7-15 levels low); fitted rms 11.9 (Day 1) to 32.5 (Day 7, against 26's soft drawing);
Day 7's foot was finished under the ribbon as asked. Day 1's coin replaces the kit's `Icon_Coin` there (rms 9.9 against 22.9).

## 4 · D3: the laurel and the ribbon

Sheet `attach/D3_sheet.png` (`sheets/D3.json`, scale 4.4): laurel `[928, 524, 52, 102]`, ribbon `[950, 610, 262, 56]`.

```
Sheet D3: the two decorations of the Day 7 tile of 26-daily-rewards (already in this chat). Attached: D3_sheet.png (the contact sheet; the crops are what you copy).

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same two elements in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no pale ground of the sheet, no drop shadows, no glow.

1 laurel branch (the left one): the gold laurel branch, exactly as in the crop: a slender stem curving up from the lower right to the upper left, the pointed leaves in pairs along it and one leaf at the tip, in soft matte antique gold (lighter warm gold leaves with a slightly darker gold midrib, the stem a little darker). Keep the number of leaves, their shapes, angles and spacing. Leave out the coins at its right and the ribbon below it: magenta there, and magenta between the leaves.

2 ribbon: the gold ribbon banner, exactly as in the crop: the long straight front band with its gentle satin shading (a lighter highlight along the top edge, a deeper gold towards the bottom edge), and the two folded tails at the left and right ends that tuck behind the band, with their V-cut ends and the darker gold of their inner folds. EMPTY: remove "Weekly bonus" and continue the band's plain satin gold there. Leave out the coins above it, the laurel tips and "+75 coins" below it: magenta all around the ribbon.

Both front-on; the ribbon exactly symmetrical left to right. Do not add anything that is not in the crops.
```

Result: `chatgpt/D3_a1.png` ("Golden Laurel and Ribbon Emblem").
- Ribbon **accepted**: the same band, tails and V-cuts; drawn a little taller, fitted 4.5 % flatter, colour put on 26's
  (it was more saturated yellow); rms 10.7 around it with the words left out.
- Laurel **not used**: a narrower S-shaped branch without 26's thin ivory outline (fitted rms 13-20 only when squashed).
  Asked again in D5.

## 5 · D4: the gift-box icon for the main menu (no mockup)

Attachments: `attach/D4_sheet.png` (`drt refsheet`: the header icons `Icon_NoAds`, `Icon_Settings`, `Icon_Shop` as ink on
white at 2x sprite size, and an empty dashed cell) and `attach/R_mainmenu_header.png` (the main-menu header of
`ArtSource/Screens/MainMenu/check-noads/capture-1920x1080.png`, x 1130..1850, y 40..185, at 2x).

```
Sheet D4: a new icon for the main menu, in the style of its header icons. Attached: D4_sheet.png (cells 1-3 are the game's existing header icons as dark ink on white: no ads, settings, shop; cell 4 is empty) and R_mainmenu_header.png (the main menu's header: the same icons on their round ivory buttons, beside the coin wallet).

The new icon opens the Daily Rewards page (26-daily-rewards, already in this chat). It sits on the same round ivory button, just below the no-ads button.

Return ONE image, the same 1536 x 1024 canvas, on a perfectly flat pure white #FFFFFF background: cells 1, 2 and 3 exactly as they are in the sheet (unchanged, same size and position), and in cell 4, centred in the dashed box, a GIFT BOX icon: a square gift box seen front-on with a slightly wider lid, a vertical ribbon band down the middle of the lid and the box, and a bow of two loops on top; a small spade shape on the front of the box is welcome. Draw it exactly in the style of icons 1-3: one flat solid colour #0E0B0C, crisp edges, the same stroke weight and the same thin white gaps between parts (as between the gear's teeth and ring, or the awning's scallops), the same chunky rounded-corner geometry, the same visual size and weight as the settings gear (about as tall as the gear). Everything that is light inside the icon is pure white background showing through. Flat colour only: no outline of another colour, no gradient, no shading, no texture, no shadow, no button disc, no text. Leave out the dashed box and the captions.
```

`drt refsheet --out attach/D4_sheet.png --size 1536,1024 --bg "#FFFFFF" --items "Icon_NoAds.png@58,378,268,268;Icon_Settings.png@468,404,216,216;Icon_Shop.png@852,404,216,216" --boxes "1214,394,260,260" --captions ...`

Result: `chatgpt/D4_a1.png` ("Minimalist Icon Strip: No Ads, Settings, Shop, Gift"). **Accepted**: cells 1-3 came back
unchanged at the same scale, so the gift's size is read against the gear on the same image: ink 223 x 237 sheet px =
111.5 x 118.5 sprite px (the gear is 216 x 212 sheet px = its 108 x 106 sprite). Lid with a ribbon band, two-loop bow, a
white spade on the box joined to the ribbon line by a thin white gap. Matte on white, rendered in #0E0B0C (the header
icons' ink).

## 6 · D5: the laurel again, and the notification dot

Attachment: `attach/D5_sheet.png` (`drt refsheet`, ivory ground): the laurel of 26 `[924, 520, 52, 108]` at 7.7x, the kit's
`UI_ReadyDot` (shape) and the main menu's crimson + button (colour, from `R_mainmenu_header.png`).

```
Sheet D5: the laurel branch once more, and a small notification dot. Attached: D5_sheet.png.

Return ONE image, the same 1536 x 1024 canvas, on a perfectly flat pure-magenta #FF00FF background, with two elements at the same size and position as cells 1 and 2 of the sheet. No captions, no pale ground of the sheet, no drop shadows, no glow.

1 laurel branch: cell 1 is the left laurel of the Day 7 tile, enlarged. Your laurel in the previous image was close but not the same drawing: trace THIS crop exactly. The stem starts at the lower right, runs up and to the left, and in its upper half bends back to the right, so the branch is a gentle open curve; count and copy the leaves exactly: the leaves on the left side of the stem are long and lie almost flat, pointing out to the left and up; the leaves on the right are shorter; one leaf at the tip. Copy the colours of the crop: soft muted honey gold leaves (lighter towards their tips), a slightly darker thin midrib, and a thin even ivory-white outline around every leaf and around the stem, exactly as in the crop. Matte, no shine. Leave out the coins at the right edge of the crop.

2 notification dot: the same shape as cell 2 (the game's ready dot: a round disc inside an even, thin white ring), but the disc in the satin crimson of cell 3 (the wallet's + button: bright red at the top, deep crimson at the bottom), and the ring a warm ivory-white. No plus, no symbol, no number. An exact circle, front-on. Leave cell 3 empty (magenta).
```

Result: `chatgpt/D5_a1.png` ("Golden Laurel Branch and Red Button").
- Laurel **accepted**: 26's open C-curve, leaf layout and ivory outline; still a little wide (fitted 10 % narrower,
  rms 16.2 around it, against 13.0 for D3's branch squashed 17 %, which has the wrong curve and no outline).
- Dot **accepted**: disc #CE2420 -> #8D110D (the + button is #C82427 -> #880E10), ivory ring #FDF9EF, disc to 0.775 R;
  brought to the ready dot's 74 px.

## 7 · D6: the heading flourish

Added after checking the kit: `Deco_SubtitleFlourish_*` has its star in the middle of the line and 3x smaller than 26's
(rms 30.6 at best), `Deco_TitleDivider_Diamond` is a convex diamond. Sheet `attach/D6_sheet.png` (`sheets/D6.json`): the
left flourish of 26 `[604, 210, 126, 26]` at 8x.

```
Sheet D6: the small flourish beside the "Day 1 of 7" heading of 26-daily-rewards (already in this chat). Attached: D6_sheet.png (one enlarged crop: the flourish left of the heading; the one on the right is the same drawing mirrored).

Return ONE image, the same 1536 x 1024 canvas, on a perfectly flat pure-magenta #FF00FF background, the flourish at the same size and position as in the sheet, redrawn clean and sharp: a fine warm-brown line that is thickest at its right end and thins out and fades to nothing towards the left, ending on the right in a small four-pointed star with gently concave sides (taller than it is wide), the line joining the star's left point. The same warm brown as in the crop (a little lighter in the middle of the star), flat, no shine. Leave out the pale halo around the star and the line: magenta right up to them. No caption, no text, no other ornament.
```

Result: `chatgpt/D6_a1.png` ("Golden Flourish on Magenta"). **Accepted**: fitted at the drawn aspect, brown put on 26's
(#8C632C -> #96683D); rms 6.7 for both flourishes (the right one mirrored).

## 8 · Results

Seven requests after the setup (D1-D6, D5 being D3's laurel again plus the dot), one attempt each, one piece rejected
(D3's laurel, replaced by D5's). No rate limit was hit. Files, sizes, 9-slice borders, placements and every check number:
`ArtSource/Working/DailyRewards/notes.md`; rows in `ArtSource/handoff.csv`; import settings `ArtSource/Kit/DailyRewards.json`.

| Sprite | Size | 9-slice L,T,R,B | From |
| --- | --- | --- | --- |
| UI_DayTile_Normal | 533x478 | 132,160,132,44 | D1 a1 cell 1 (inside), frame drawn at measured values |
| UI_DayTile_Bonus | 1091x523 | 140,150,140,150 | D1 a1 cell 3 (inside), frame drawn at measured values |
| UI_DayTile_TodayFrame | 542x485 | 138,164,138,48 | D1 a1 cell 2 (look); solved overlay + keyline at measured values |
| Deco_DayCoins_1..7 | 156x160 .. 480x242 | - | D2 a1 |
| Deco_DayCoins_1..7_Shadow | 266x270 .. 590x352 | - | blurred silhouettes |
| Deco_Laurel | 86x222 | - | D5 a1 cell 1 |
| Deco_Ribbon_Gold | 584x105 | 72,0,72,0 | D3 a1 cell 2 |
| Deco_HeadingFlourish | 312x49 | - | D6 a1 |
| Icon_DailyReward | 116x124 | - | D4 a1 cell 4 |
| UI_Dot_Alert | 74x74 | - | D5 a1 cell 2 |
