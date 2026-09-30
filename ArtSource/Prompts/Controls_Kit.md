# Controls kit (family `Controls`)

Chat: ChatGPT project "CardGame", chat "Greeting exchange" (opened 30 Sep 2026 with `hi`):
<https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abd0e07-51b0-83e8-9eea-d6cdc69cb9fa>

One chat for the whole family. Model and thinking setting left as the owner had them
(GPT-6.1 Sol, Light).

Brief: `Docs/Tasks/UI-Art-ChatGPT-Brief.md`. Prompts: Designer Handbook v1.0 §7-10 (P-0, UI-1..7,
F-1..6), placeholders filled in.

| Where | What |
| --- | --- |
| `ArtSource/Working/Kit/Controls/attach/` | what was sent: the reference screens (`S<NN>-<name>.png`, copies of the mockups), the contact sheets (`C<n>_*_sheet.png`) and each sheet's cell table (`.cells.json`) |
| `ArtSource/Working/Kit/Controls/chatgpt/` | ChatGPT originals, `C<n>_a<k>.png` = sheet n, attempt k (every attempt kept) |
| `ArtSource/Working/Kit/Controls/work/` | the sheets cut into cells (`C<n>_a<k>/<Element>.png`) and the steps from a cell to a sprite |
| `ArtSource/Working/Kit/Controls/check/` | each sprite against its mockup: mockup, sprite at mockup resolution, sprite sharp, difference x6, on black, on white |
| `Tools/ArtPipeline/Kit/Controls/` | the tool (`kc`), the sheet definitions (`sheets/C<n>.json`) and the recipe that turns the originals into the sprites (`make.sh`) |
| `ArtSource/Kit/Controls.json` | import settings (ppu, 9-slice) |
| `ArtSource/Kit/Controls.map.csv` | swap map for the screen engineers |

## How a control is made

1. The element's rectangle comes from its screen's `screen.json` and is re-measured on the mockup
   (`Tools/art profile`, `kc radial` for round things: edges to a tenth of a pixel).
2. `kc lsheet` lays enlarged crops of several controls on one 1536 x 1024 sheet: each crop fades
   into the sheet's ivory ground, so there are no tile borders to copy, and has a small grey caption.
   Long 9-sliced controls (slider, text fields, the segmented selector) are shown short: plain
   stretches of columns are taken out, which also removes their words.
3. ChatGPT redraws the sheet clean on flat magenta (prompts below). `kc cells` keys the magenta
   and cuts the answer into its cells.
4. Each cell is brought to the measured proportions (`kc warp`: only plain stretches are
   stretched, caps and knobs stay round; `kc rwarp` for ring radii), gets an exact geometric edge
   (`kc shape`), is mirrored where the control is symmetric, and its colours are put back on the
   mockup's (`kc crimson`, `kc curve`: ChatGPT returns the crimson as a purer, brighter red).
5. `kc view` and `kc fit` draw the sprite at its mockup rectangle, at mockup resolution, on a
   ground with the mockup's own element erased, and print how far it is from the mockup.

## 0 · Opening

`hi` -> "Hi! What would you like to work on today?"

## 1 · Setup (P-0 adapted to controls)

Sent with four attachments: `S11-settings.png`, `S02-create-room.png`, `S06-offline-setup.png`,
`S15-card-styles.png` (ten at once do not load; four do).

```
You help me extract production UI assets for "Callbreak - Play Together", a Nepali card game for phones in landscape. The reference images are the approved design; reproduce them.

Style (never change it): warm ivory parchment panels; deep crimson #861018 primary buttons with a satin gradient, a thin antique-gold #B8914F rim and restrained Nepali woven geometry ONLY at their ends; black silhouette icons #231C19; painterly Nepal scenery: Newari temple and prayer flags (left), snowy Himalaya and hillside village (right), blurred red rhododendrons (bottom corners), pale peach textile geometry (top corners).

In this chat we extract the CONTROLS family: the small menu controls of the approved screens (toggle switches, the volume slider, the dropdown, text fields, the segmented selector, code boxes, the check badge, selection outlines, radio buttons, pills, dots, number discs and seat circles).

Rules for every image:
1. Match the reference exactly: shape, proportions, rim thickness, corner rounding, colours, shading. Do not redesign, restyle, simplify or "improve" a control, and never swap it for a stock control with the same meaning.
2. Never draw letters or numbers. Every control is EMPTY: no text, no label and no caption inside or beside it.
3. No drop shadow and no glow around a control (the game adds shadows separately). No flowers, leaves, vines, neon, glass, blue panels, photorealism.
4. Front-on, no perspective, no rotation, crisp clean edges, perfectly symmetrical where the reference is.
5. When I send a contact sheet (a grid with one enlarged control per cell and a small grey caption under each), return ONE image with the same grid: the same controls in the same cells, in the same order, at the same size and position, redrawn clean and sharp, with the empty space between them kept, on a perfectly flat pure-magenta #FF00FF background (no gradient, no texture, no shadow on the magenta). Leave out the captions and the ivory ground of the sheet.
6. If my request is ambiguous, ask before generating.

I have attached four of the approved reference screens: 11-settings, 02-create-room, 06-offline-setup and 15-card-styles. I will attach the other screens together with the requests that use them. Do not generate anything yet. Reply "Ready" and say in one line each what you see in these four images.
```

Reply: "Ready", with a correct one-line description of each of the four screens.

## 2 · Sheet C1: Settings controls

Sheet: `attach/C1_Settings_sheet.png` (`sheets/C1.json`): toggle on `[707.2, 364.0, 81.7, 45.9]`,
toggle off `[1162.9, 502.9, 81.9, 46.0]`, slider `[428.3, 545.3, 360.6, 35.0]` (shown 144.6 px
long), dropdown `[1066.9, 362.2, 177.7, 55.1]`, all on `11-settings.png`.
Attachments: the sheet and `S11-settings.png`.

### Attempt 1

```
Sheet C1: the Settings controls. Attached: C1_Settings_sheet.png (the contact sheet: four controls enlarged from 11-settings.png; the crops are what you copy) and S11-settings.png (the full screen, for context).

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same four controls in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no ivory ground, no drop shadows, no glow.

1 toggle, on: the crimson pill-shaped switch with the round white knob at the right. Keep the satin crimson (a little lighter across the middle, darker towards the edges), the very thin ivory-white rim around the pill, and the plain soft-white knob with the same gap to the pill edge as in the crop.
2 toggle, off: the same switch in its off state: flat warm-grey pill, the white knob at the left. Same size and proportions as the on switch. Remove the soft shadow under it.
3 slider: a short piece of the volume slider: the crimson filled part with its round left end, the round white knob, and the pale beige empty part of the track with its round right end. Keep the bar height and the knob size exactly as in the sheet. Remove the shadow under the knob.
4 dropdown: the rounded ivory box with its thin warm-brown outline and the small dark chevron at the right. EMPTY: remove the word and continue the plain ivory fill where it was; keep the chevron.

Everything front-on and symmetrical top to bottom; the pill ends are exact half circles and the knobs exact circles. Do not add anything that is not in the crops.
```

Result: `chatgpt/C1_a1.png`, 1536 x 1024, flat magenta, the four controls in their cells within a
few pixels of the sheet (3 min).

## 3 · Sheet C2: frames

Sheet: `attach/C2_Frames_sheet.png` (`sheets/C2.json`): text field of 02 `[562.0, 398.9, 548.8, 63.2]`
(shown 206.8 px long, empty), text field of 14 `[508.2, 647.2, 655.4, 60.3]` (shown 217.4 px long,
empty), code box of 03 `[719.52, 482.5, 108.1, 109.0]`, segmented selector of 02
`[602.1, 634.4, 469.05, 59.6]` (shown 169 px long, without its labels).
Attachments: the sheet, `S02-create-room.png`, `S14-profile.png`, `S03-join-room.png`.

### Attempt 1

```
Sheet C2: the frames. Attached: C2_Frames_sheet.png (the contact sheet; the crops are what you copy) and the full screens they come from, for context: S02-create-room.png (the text field under "Room name" and the 15 sec | 30 sec selector), S14-profile.png (the text field under "Display name") and S03-join-room.png (the six code boxes). In the sheet the long text fields and the selector are shown shortened and without their words.

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same four controls in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no ivory ground, no drop shadows, no glow.

1 text field: the empty rounded ivory box with its thin warm-brown outline. Plain, even ivory inside.
2 text field, profile: the same kind of box from the profile screen, a little lighter inside. Plain, even fill.
3 code box: the rounded square ivory box with its thin brown outline. EMPTY: remove the character and continue the ivory where it was, keeping the very soft top-to-bottom shading of the box. Remove the shadow under the box.
4 two-part selector: the long ivory pill (the track: thin tan outline with its soft inner bevel) with the crimson pill (the selected part: satin crimson, lighter at the top, darker at the bottom) sitting in its right half, with the same narrow ivory gap around it as in the crop. Both are EMPTY: no words. Remove the shadow under the track.

Everything front-on and symmetrical (left-right and top-bottom for the boxes and for the track); corners rounded exactly as in the crops; outlines the same thin weight as in the crops, not heavier, and no second outline around them. Do not add anything that is not in the crops.
```

Result: `chatgpt/C2_a1.png`, 1536 x 1024, flat magenta, the four frames in their cells (1 min 51 s).

## 4 · Sheet C3: round controls

Sheet: `attach/C3_Round_sheet.png` (`sheets/C3.json`): check badge of 06 (circle
`[906.29, 513.69, 38.82, 38.82]`), radio selected and radio empty of 15 (`[456.72, 602.68, 39.44, 39.44]`,
`[768.19, 602.59, 39.62, 39.62]`), ready dot of 04 (`[752.29, 498.77, 31.94, 31.94]`), number disc
of 12 (`[423.57, 368.27, 58.46, 58.46]`) and the spinner ring. The spinner is not on a mockup: its
cell shows the current ring placed on the Create Room button (`attach/R_spinner_on_button_02.png`).
Attachments: the sheet, `S04-private-lobby.png`, `S12-how-to-play.png`.

### Attempt 1

```
Sheet C3: the round controls. Attached: C3_Round_sheet.png (the contact sheet; the crops are what you copy) and two more full screens for context: S04-private-lobby.png (the small green ready dot at the bottom right of a seat avatar) and S12-how-to-play.png (the crimson number discs beside the four rules). The check badge is on 06-offline-setup and the radio buttons are on 15-card-styles, which you already have.

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same six controls in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no ivory ground, no drop shadows, no glow.

1 check badge: the crimson disc with the white check mark. Keep the satin crimson, the thin ivory-white rim around the disc and the check exactly as drawn (same stroke weight, same angles, square ends). Leave out the corner of the card outline at the top right of the crop.
2 radio, selected: the warm dark-brown ring, the white gap inside it and the crimson dot in the middle, with the same ring thickness and dot size as in the crop.
3 radio, empty: the same brown ring with a plain ivory-white inside (not transparent).
4 ready dot: only the green disc with the white ring around it. Leave out the dark avatar and its white border behind it.
5 number disc: the plain satin-crimson disc. EMPTY: remove the numeral and continue the crimson where it was.
6 spinner ring: only the plain ivory-white ring, with the same thickness as in the crop; leave out the crimson button behind it (magenta inside the ring as well).

Every control is an exact circle, front-on, with concentric rings. Do not add anything that is not in the crops.
```

Result: `chatgpt/C3_a1.png`, 1536 x 1024, flat magenta, the six controls in their cells (1 min 46 s).

## 5 · Sheet C4: selection outlines and seat circles

Sheet: `attach/C4_Outlines_Seats_sheet.png` (`sheets/C4.json`). An outline is hollow and 9-sliced,
so only its corners matter: each is shown as a small rounded square made of its own top-left
corner mirrored four ways (`attach/R_outline_06_tile.png` from 06-offline-setup at 702,492 100 x 100;
`attach/R_outline_15_tile.png` from 15-card-styles at 384,212 80 x 80). Seats of 05-matchmaking:
filled `[499.11, 506.71, 119.4, 116.0]`, searching `[1050.0, 504.6, 123.3, 120.0]`.
Attachments: the sheet, `S05-matchmaking.png`.

### Attempt 1

```
Sheet C4: two selection outlines and the two seat circles. Attached: C4_Outlines_Seats_sheet.png (the contact sheet; the crops are what you copy) and S05-matchmaking.png (the four seats under "Finding players...": three filled, the last one still searching). The outlines are the crimson lines around the selected card on 06-offline-setup and on 15-card-styles, which you already have; in the sheet each is shown as a small rounded square, so that its corners are large.

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same four controls in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no ivory ground, no drop shadows, no glow.

1 selection outline: ONLY the crimson line: a hollow rounded square with the same line thickness and the same corner rounding as in the crop. Magenta inside it and outside it: no card, no fill, no white line beside the crimson.
2 selection outline, thin: the same, with the thinner line of this crop.
3 seat, filled: the round seat exactly as drawn: the thin gold-tan outer ring, the white ring inside it, the pale warm-grey disc and the black head-and-shoulders silhouette. An exact circle.
4 seat, searching: the empty seat exactly as drawn: a thin dashed warm-grey circle (keep the same number and length of dashes) and the pale grey head-and-shoulders silhouette inside it. Nothing else: magenta between the dashes and inside the circle around the silhouette.

Everything front-on and left-right symmetrical. Do not add anything that is not in the crops.
```

Result: `chatgpt/C4_a1.png`, 1536 x 1024, flat magenta, the four elements in their cells.

## 6 · Sheet C5: pills

Sheet: `attach/C5_Pills_sheet.png` (`sheets/C5.json`): room code pill of 04
`[490.33, 282.32, 375.9, 77.2]` and highlight pill of 12 `[590.8, 738.9, 490.1, 45.0]`, both with
their contents (their words run the whole length, so there is no plain stretch to cut out).
Attachments: the sheet, `S04-private-lobby.png`, `S12-how-to-play.png`.

### Attempt 1

```
Sheet C5: two pills. Attached: C5_Pills_sheet.png (the contact sheet; the crops are what you copy), S04-private-lobby.png (the pill "Room code AB12CD" at the top left of the panel) and S12-how-to-play.png (the gold-rimmed pill "Highest total after 5 rounds wins." near the bottom of the panel).

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same two pills in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no ivory ground, no drop shadows, no glow.

1 room code pill: the rounded ivory box with its thin warm outline and its soft bevel (the slightly lighter top edge, the slightly darker lower edge). EMPTY: remove the words "Room code", the code, the thin separator line and the copy icon, and continue the plain ivory where they were. Remove the shadow under the pill.
2 highlight pill: the long pale pill with its thin gold outline. EMPTY: remove the words and the two small woven diamond ornaments at its ends, and continue the plain pale fill where they were.

Both front-on and symmetrical (left-right and top-bottom); ends rounded exactly as in the crops; outlines the same thin weight as in the crops, not heavier. Do not add anything that is not in the crops.
```

Result: `chatgpt/C5_a1.png`, 1536 x 1024, flat magenta, both pills in their cells.

## 7 · Sheet C6: the toggles again

C1 attempt 1 drew both knobs as flat white discs, because the C1 prompt forbade every shadow; on
the mockup the knob casts a soft shadow on its own track. Sheet: `attach/C6_Toggles_sheet.png`
(`sheets/C6.json`): toggle on `[707.2, 364.0, 81.7, 45.9]`, toggle off `[1162.9, 502.9, 81.9, 46.0]`,
both at 7x. Attachments: the sheet and `S11-settings.png`.

### Attempt 1 (the toggles' second attempt)

```
Sheet C6: second attempt at the two toggle switches of sheet C1. Attached: C6_Toggles_sheet.png (the same two crops, larger; the crops are what you copy) and S11-settings.png (the full screen, for context).

Your first toggles were right in shape, size and colour, but the knob came out as a flat white disc: I wrongly asked you to leave out every shadow. In the crops the knob looks slightly raised, and that is part of the control. So, keeping everything else as in your first image:

1. Keep the very soft shadow that the knob casts ON THE TRACK around it: a slightly darker halo hugging the knob, a little stronger below the knob and on its side nearer the end of the pill. On the grey switch it is a slightly darker grey; on the crimson switch a slightly darker crimson.
2. The knob itself is a soft white with the faintest hint of grey towards its lower edge, as in the crops: not a drawn outline, not a gradient ball, not glossy.
3. Still NO shadow outside the pill: nothing on the magenta, no glow.

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same two switches in the same cells, at the same size and position as in the sheet, clean and sharp. No captions, no ivory ground.

1 toggle, on: the crimson pill with the satin crimson (a little lighter across the middle, darker towards the ends), the very thin ivory-white rim around the pill, the white knob at the right.
2 toggle, off: the flat warm-grey pill, no rim, the white knob at the left.

Pill ends are exact half circles, knobs exact circles, everything symmetrical top to bottom.
```

Result: `chatgpt/C6_a1.png`, 1536 x 1024, flat magenta, both switches with the knob's shadow on the
track (the off state's four times as deep as the mockup's: corrected in the colour curve).

## 8 · Sheet C7: the crimson buttons and the thin chevron

Added on the coordinator's request: the screens put the main menu's gold-rimmed PLAY ONLINE sprite
under every crimson button, the mockups draw a plainer pill there. Measured first (kc edges, kc
motif, kc vprof on every crimson button): 01, 04, 06, 07, 09, 10, 11, 12, 14, 15 and 16 draw one
plain design (no rim, the same gradient within about 15 levels, the same woven cluster at each end;
corner radius 0.32-0.48 of the height, no two groups); 02 and 03 draw the gold-rimmed PLAY ONLINE
pill (it stays); 13 draws a third one, a crimson box with a gold keyline and knots.
Sheet: `attach/C7_Buttons_sheet.png` (`sheets/C7.json`): the Start Game button of 06
`[439.95, 782.82, 792.8, 93.64]` shown 319.8 px long (words and chevron cut out), the Resume Game
box of 13 `[611.77, 358.62, 448.53, 78.2]` shown 156.5 px long, the chevron of 06 at 8x.
Attachments: the sheet, `S06-offline-setup.png`, `S13-match-menu.png`.

### Attempt 1

```
Sheet C7: the crimson buttons and their chevron. Attached: C7_Buttons_sheet.png (the contact sheet; the crops are what you copy), S06-offline-setup.png (the wide crimson "Start Game" button at the bottom of the panel, with a small white chevron after the words) and S13-match-menu.png (the crimson "Resume Game" button at the top of the Game Menu). In the sheet both buttons are shown shortened and without their words.

Return ONE image with the same grid on a perfectly flat pure-magenta #FF00FF background: the same three elements in the same cells, at the same size and position as in the sheet, redrawn clean and sharp. No captions, no ivory ground, no drop shadows, no glow.

1 crimson button: the plain crimson button exactly as drawn. It has NO gold rim and no outline of any kind. Keep the soft satin crimson, lighter at the top and darker at the bottom, and at each end the small woven ornament: the cluster of small diamonds with a cross in each and the little stepped motif beside it, in a slightly lighter and a slightly darker red than the button; the right ornament is the mirror image of the left one. Keep the ends rounded exactly as in the crop (large rounded corners, not full half circles). EMPTY: no words and no chevron; plain crimson between the two ornaments. Remove the soft shadow around the button.
2 crimson box button: the rounded crimson box exactly as drawn: the thin dark-crimson outer edge, the thin gold line just inside it all the way round, the brighter crimson face (lighter at the top, darker at the bottom) and the small line-drawn knot ornament at each end, the right one the mirror image of the left one. Corners rounded exactly as in the crop. EMPTY: no words. Remove the shadow around it.
3 chevron: ONLY the white chevron, with the same stroke thickness, the same angle and the same square-cut ends as in the crop. Leave out the crimson square behind it and its dark shadow: magenta all around the chevron.

Both buttons front-on and symmetrical (left-right and top-bottom for their outlines). Do not add anything that is not in the crops: no rim light, no bevel, no extra lines, no gold on button 1.
```

Result: `chatgpt/C7_a1.png`, 1536 x 1024, flat magenta, the three elements in their cells (1 min 51 s).
The pill's shape and cluster are right; its diamonds are drawn 2.5x too light and the lattice
between them in the button's own colour (on the mockup the cluster sits on a darker ground): the
colours are put back on the mockup's (`kc retone` with `--ground`). The box's keyline came out as
one flat orange: the edge, the two-tone keyline and its fringe are redrawn at the widths and
colours measured on all four sides (`kc ring`). The chevron's weight is the mockups' to the
hundredth (stroke 0.135 of the height); it only gets the mockups' pure white.

The two shadow sprites are not drawn by ChatGPT: each is the face's rounded rectangle blurred
(`kc mkshadow`), with the blur that fits the mockups (`kc bshadow`); drop and opacity per node are
in the map.

## 9 · Results

Seven sheets, one attempt each (the toggles took two sheets: C1 and C6). Fit = the error of the
sprite drawn at its rect on the mockup, at mockup resolution, on a ground with the mockup's own
element erased (sRGB levels rms; words and other nodes left out). All sprites have ppu 200 and are
@2x (mockup px x 2.3); the checks are in `ArtSource/Working/Kit/Controls/check/`.

| Sprite | Size | 9-slice L,T,R,B | From | Fit | What still differs from the mockup |
| --- | --- | --- | --- | --- | --- |
| UI_Toggle_On | 190x108 | - | C6 a1 (2nd attempt; C1 a1 rejected: flat knob) | 11: 9.8-10.4; 13: 18.6 | the satin highlight sits a little differently; 13 draws its switch smaller and brighter (#940813) |
| UI_Toggle_Off | 190x108 | - | C6 a1 | 4.3 | - |
| UI_Slider_Track | 64x44 | 22,0,22,0 | C1 a1 | 2.0 | - |
| UI_Slider_Fill | 64x50 | 26,0,26,0 | C1 a1 | 4.8 | - |
| UI_Slider_Knob | 76x76 | - | C1 a1 | 4.2 | - |
| UI_Dropdown | 408x126 | 30,0,98,0 | C1 a1 | 7.9 | chevron thickened 0.65 px to the mockup's weight |
| UI_Field | 160x140 | 36,36,36,36 | C2 a1 | 4.7 | - |
| UI_Field_Light | 160x140 | 36,36,36,36 | C2 a1 | 5.1 | could be UI_Field (9 levels darker inside) if one field is wanted |
| UI_CodeBox_Normal | 250x252 | - | C2 a1 | 4.3-5.0 | - |
| UI_CodeBox_Focus / _Error | 250x252 | - | derived from _Normal | no mockup | colours from GDD 12 (antique gold / Heading Burgundy): the owner's call |
| UI_Segment_Track | 160x138 | 70,0,70,0 | C2 a1 | 4.6-6.3 | - |
| UI_Segment_Selected | 132x120 | 61,0,61,0 | C2 a1 | 5.3 | - |
| UI_Ring_Spinner | 66x66 | - | C3 a1 | no mockup | - |
| UI_Check_Badge | 96x96 | - | C3 a1 | 7.7 | check thinned 0.7 px to the mockup's |
| UI_Outline_Selected | 144x144 | 60,60,60,60 | C4 a1 (shape only) | 18.2 | the error is the card's own bright inner edge beside the line |
| UI_Outline_Thin_Selected | 144x144 | 64,64,64,64 | C4 a1 (shape only) | 12.5 | two weights because 06 and 15 differ (3.2 / 2.6 px) |
| UI_Radio_On / _Off | 98x98 | - | C3 a1 (shape only) | 7.7 / 5.5-6.1 | flat colours |
| UI_Pill_RoomCode | 160x178 | 68,68,68,68 | C5 a1 | 4.8 | ChatGPT's gold outline redrawn in the mockup's beige |
| UI_ReadyDot | 74x74 | - | C3 a1 (shape only) | 7.9-9.3 | flat colours |
| UI_Seat_Filled | 274x274 | - | C4 a1 | 10.0-10.9 | round; the mockup's seats are 2% wider than high (the map's rects reproduce it) |
| UI_Seat_Searching | 272x272 | - | C4 a1 | 7.2 | 36 regular dashes (the mockup's are irregular; ChatGPT drew 32) |
| UI_Pill_Highlight | 120x104 | 53,0,53,0 | C5 a1 | 10.5 | the gold line is even; the mockup's is heavier at the ends |
| UI_Disc_Number | 142x142 | - | C3 a1 | 7.5-11.2 | - |
| UI_Button_Crimson | 336x220 | 160,0,160,0 | C7 a1 | 06: 7.7; 01: 11.9 / 12.1; 11: 13.8; 14: 13.1; 04: 15.4; 09: 16.1; 16: 17.9; 15: 20.2; 12: 20.3; 10: 24.7; 07: 28.1 | one pill for eleven drawings: corner radius 0.385 of the height (mockups 0.32-0.48: 10 is squarer, 12 and 07 rounder), 06's crimson (04 and 12 are 14-17 levels darker, 15 10 lighter, 07 purer red), 06's cluster (01 draws it smaller); no top-centre glint (09, 16, 07 have one) |
| UI_Button_Crimson_Shadow | 408x292 | 196,0,196,0 | blur (sigma 11 px) | 2.7-6.1 around the buttons | menus fit a tighter blur, results screens a wider one: one blur, drop and opacity per node |
| UI_Button_Crimson_Box | 208x180 | 96,0,96,0 | C7 a1 | 13: 11.2 | the knot is ChatGPT's clean drawing of the mockup's softer one |
| UI_Button_Crimson_Box_Shadow | 268x240 | 126,0,126,0 | blur (sigma 9.2 px) | 13: 3.3 | - |
| Icon_Chevron_Thin (UI/Icons) | 38x64 | - | C7 a1 | 11.8-17.6 (Icon_Chevron_Light: 37-43) | the mockups show a 1 px darker rim round the chevron (a sharpening halo, it does not fit as a shadow): not drawn |
