# Panels and controls kit

Family: `Panels`: the panels and modals, and the table, results and Game Menu pieces (name plate,
status pill, timer ring and badge, bid chips, table header, highlighted row, table body, tag pill,
box button). The small menu controls went to the Controls designer during the session.
Chat: ChatGPT project "CardGame", chat "CardImageGeneration" (first titled "Greeting response";
opened 30 Sep 2026 with `hi`; the only chat this family used):
https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcff8c-266c-83ee-a381-dc16be9a07bb

Brief: `Docs/Tasks/UI-Art-ChatGPT-Brief.md`. Prompts: Designer Handbook v1.0 §7-10 (P-0, UI-1, F-1..6),
placeholders filled in.

| Where | What |
| --- | --- |
| `ArtSource/Working/Kit/Panels/attach/` | attachments sent to ChatGPT (full screens and enlarged crops) |
| `ArtSource/Working/Kit/Panels/chatgpt/` | ChatGPT originals, `<Asset>_a1.png`, `_a2.png`, ... (every attempt kept) |
| `ArtSource/Working/Kit/Panels/check/` | side-by-side checks against the mockup, alpha on black/white, 9-slice stretch tests |
| `Tools/ArtPipeline/Kit/Panels/` | the scripts that made the attachments, cleaned the results and exported them |
| `ArtSource/Kit/Panels.json` | import settings (ppu, 9-slice) |
| `ArtSource/Kit/Panels.map.csv` | swap map for the screen engineers |

Every request attaches the full screen (`S<NN>-<name>.png`, a copy of the mockup) the first time an
element of that screen comes up, plus an enlarged crop of the element (`A<nn>_<Asset>_*.png`); the
crop is what ChatGPT copies. Attempts are numbered per asset (`_a1`, `_a2`, ...).

## 0 · Setup (P-0, sent once)

First message: `hi` (reply: "Hi! What would you like to work on today?"). Then P-0, adapted to this
family, with six screens attached (02, 06, 07, 08, 11, 13):

```
You help me extract production UI assets for "Callbreak - Play Together", a Nepali card game for phones in landscape. The reference images are the approved design; reproduce them.

Style (never change it): warm ivory parchment panels; deep crimson #861018 primary buttons with a satin gradient, a thin antique-gold #B8914F rim and restrained Nepali woven geometry ONLY at their ends; black silhouette icons #231C19; painterly Nepal scenery: Newari temple and prayer flags (left), snowy Himalaya and hillside village (right), blurred red rhododendrons (bottom corners), pale peach textile geometry (top corners).

In this chat we extract the PANELS AND CONTROLS family: the ivory panels (the large menu panel, the small card/tile, the modal panels of the match screens) and the small controls (toggles, slider, dropdown, text fields, segmented control, radio buttons, check badge, pills, bid chips, name plates, timer ring, table rows).

Rules for every image:
1. Match the reference exactly: colours, ornament, lighting, proportions. Do not redesign, restyle or "improve".
2. Never draw letters or numbers.
3. No flowers, leaves or vines on buttons or panels. No neon, glass, blue panels, green felt, coins, photorealism.
4. One asset per image (unless I send a grid and ask for the same grid back), front-on, no perspective, no mockup frame, no background scene.
5. Every asset is EMPTY: no text, no icon and no avatar inside it, and NO drop shadow or glow around it (the game adds shadows separately).
6. When I ask for transparency, give a real transparent PNG, never a drawn checkerboard. If you cannot, use a flat pure-magenta #FF00FF background instead and say so.
7. If my request is ambiguous, ask before generating.

I have attached six of the approved screens: 02-create-room, 06-offline-setup, 07-bidding, 08-gameplay, 11-settings and 13-match-menu. I will attach the other screens, and an enlarged close-up crop of each element, together with the request that needs them; the crop is what you copy. Do not generate anything yet. Reply "Ready" and list the attachments you can see.
```

Reply: "Ready", six attachments listed, but their contents were not readable ("file not found"); it
asked for them again with the first request. So every request re-attaches its screen.

## 1 · UI_Panel_Large (UI-1)

Source: `02-create-room.png`, panel rect `[449.1, 214.3, 774.8, 670.8]`.
Attachments: `S02-create-room.png`, `A01_Panel_Large_crop_02.png` (the panel, 2x),
`A01b_Panel_Large_corner_TL_02.png` (top-left corner, 6x, from attempt 2).

### Attempt 1 (UI-1)

```
Asset 1: UI_Panel_Large. Attached: S02-create-room.png (the full screen) and A01_Panel_Large_crop_02.png (the same panel enlarged 2x: this crop is what you copy).

Extract the wide ivory panel in the centre of the attached screen as an EMPTY panel: remove all text, icons, the text field, the two tiles, the segmented control and the crimson button inside it and fill with the same smooth warm ivory. Keep its rounded corners, its delicate warm outline (the thin light rim), the pale peach textile geometry in the two TOP corners and the faint mountain line-art in the two BOTTOM corners exactly as they are in the crop. The middle must be plain, even ivory so it can stretch. Front-on, landscape, the same proportions as the crop, the panel filling almost the whole image. Transparent background outside the panel, no drop shadow.
```

Result: `UI_Panel_Large_a1.png`, 1346 x 1169. Corners good (textile geometry and mountains as on
the mockup). Rejected: the background removal ate a wide half-transparent ring out of the middle,
and the edge is a thin tan line where the mockup has a bright ivory-white highlight rim.

### Attempt 2 (F-4 magenta ground + rim)

```
Second attempt at UI_Panel_Large. The previous image has two problems.

1. The background removal ate a big ring out of the middle of the panel: the ivory there became half transparent. The panel must be completely opaque ivory everywhere inside its edge. So do NOT use transparency this time: draw the panel on a flat pure-magenta #FF00FF background that fills everything outside the panel (no gradient, no shadow on the magenta).

2. The edge. You drew a thin tan line around the panel. The reference has no tan, brown or gold line: its edge is a very thin bright ivory-white highlight rim, a little lighter than the fill (see the new close-up A01b_Panel_Large_corner_TL_02.png, the top-left corner enlarged 6x). Reproduce that rim.

Keep everything else exactly as in your previous image: the same pale peach textile geometry in the two top corners, the same faint mountain line-art in the two bottom corners, plain even ivory in between, the same size and proportions, corners rounded as in the close-up. No text, no drop shadow.
```


Result: `UI_Panel_Large_a2.png`, 1346 x 1169, opaque on magenta. Panel, textile corners and rim
right. Rejected for the mountains: about a third of the panel's width each and far stronger than
the faint watermark on the mockups.

### Attempt 3 (mountains only)

Extra attachment: `A01c_Panel_Large_corner_BL_04.png` (bottom-left corner of the panel on 04, 4x).

```
Third attempt at UI_Panel_Large (asset 1, the large panel). Your second image of it (the one on magenta) is right in almost everything: keep its size, proportions, rounded corners, thin ivory-white rim, plain ivory middle and the peach textile geometry in the two top corners exactly as they are.

Change only the mountains in the two bottom corners. They are too big and too strong. In the approved design they are a small, barely visible watermark: see the new close-up A01c_Panel_Large_corner_BL_04.png (the bottom-left corner of the same panel on another screen, enlarged 4x; ignore the crimson button and the digit in it). One soft peak with a few lower ridges, tucked into the corner, about one fifth of the panel's width and one fifth of its height, only a few percent darker than the ivory, with soft edges that fade out before they reach the bottom edge. Redraw both bottom corners like that; the right corner is the mirror image of the left. Nothing else in the lower half of the panel.

Same flat pure-magenta #FF00FF background, no shadow, no text.
```

Result: `UI_Panel_Large_a3.png`, 1346 x 1169. **Accepted.**
Clean-up (`build-panels.sh UI_Panel_Large`): magenta keyed out, left half mirrored, scaled 1.35x to
@2x, colours mapped to the mockup (fill #F9F0E6 at the top to #F6EBE0 at the bottom, ornament
#F6DFCB, rim #FDFAF5; ChatGPT's fill was #FEF9EF and its ornament #F8D1B2), middle squeezed to a
plain 64 px for the 9-slice.
Export: `UI/Panels/UI_Panel_Large.png`, 864 x 694, 9-slice L400 T370 R400 B260, ppu 200.
Checks: `check/UI_Panel_Large_check_*.png` (on 02 at 3x, and placed on 01, 04, 11, 14),
`UI_Panel_Large_stretch3x.png`.
Still differs from the mockups: the top-right ornament is the mirror of the top-left (on 02 it is
drawn differently and sits about 20 px further in); the mountains are one kit drawing (each screen
has its own: larger on 01, 04 and 14, absent on 03, 05, 06 and 15; 10, 11, 12 and 16 have textile
corners at the bottom instead); corner radius about 28 mockup px (23 to 28.5 on the mockups); no
paper grain.

## 2 · UI_Panel_Card (UI-1)

Source: `06-offline-setup.png`, the "Easy" card `[440.9, 504, 252.6, 211]`.
Attachments: `S06-offline-setup.png`, `A02_Panel_Card_crop_06.png` (4x).

### Attempt 1

```
Asset 2: UI_Panel_Card. Attached: S06-offline-setup.png (the full screen) and A02_Panel_Card_crop_06.png (the "Easy" option card enlarged 4x: this crop is what you copy).

Extract the small ivory option card (the "Easy" card, the left one of the three) as an EMPTY card: remove the person icon and all the words and fill with the same smooth warm ivory. Keep its rounded corners and its edge exactly as in the crop: a very thin bright ivory-white highlight rim, slightly lighter than the fill. No tan, brown or gold outline, no ornament. The inside is plain, even ivory so it can stretch. Front-on, the same proportions as the crop, the card filling most of the image. Flat pure-magenta #FF00FF background outside the card (no transparency, no gradient), no drop shadow.
```

Result: `UI_Panel_Card_a1.png`, 1367 x 1151. **Accepted.**
Clean-up: keyed, mirrored both ways, fill #FAF0E7 -> #F6ECE1 and rim #FFFFFF -> #FDFAF6, scaled to
@2x (582 x 486), middle squeezed.
Export: `UI/Panels/UI_Panel_Card.png`, 144 x 144, 9-slice 56 on every side, ppu 200.
Checks: `check/UI_Panel_Card_check_06*.png`, `_check_14.png`, `_check_15.png`, `_stretch3x.png`.
Still differs: the rim is the one of 06 (near-white, about 1 mockup px); on 14 and 15 the mockup's
cards have a softer, barely visible rim; radius 21 (tiles on 02 and 14 are 15 to 19.5: sliceScale).

## 3 · UI_Panel_Modal_Menu (UI-1)

Source: `13-match-menu.png`, the "Game Menu" panel, outer edge of the outline
`[549.6, 217.7, 573.0, 517.9]`.
Attachments: `S13-match-menu.png`, `A04_Panel_Modal_Menu_crop_13.png` (2x).

### Attempt 1

```
Asset 3: UI_Panel_Modal_Menu. Attached: S13-match-menu.png (the full screen) and A04_Panel_Modal_Menu_crop_13.png (the "Game Menu" panel enlarged 2x: this crop is what you copy).

Extract the "Game Menu" modal panel as an EMPTY panel: remove the title, the tag pill under it, the close X, the crimson button, the two ivory rows with their icons, words and toggle, the divider line, the "Leave Match" line and the caption, and fill with the same warm ivory parchment. Keep exactly as in the crop: the rounded corners, the antique-gold outline that runs around the whole panel, the tan woven knot ornament in the TOP-LEFT corner and the matching one in the BOTTOM-RIGHT corner (the other two corners stay plain), and the faint paper tone. The middle must be plain, even parchment so it can stretch. Front-on, the same proportions as the crop, the panel filling most of the image. Flat pure-magenta #FF00FF background outside the panel (no transparency, no gradient), no drop shadow, nothing of the table, cards or avatars behind it.
```

Result: `UI_Panel_Modal_Menu_a1.png`, 1317 x 1194. **Accepted.** Outline, knots and even the
mockup's faint weave came back.
Clean-up: keyed; outermost 2 px recoloured from inside (a reddish line against the magenta); the
weave flattened to plain fill (a 9-slice middle cannot carry it); bottom half = top half turned by
180 degrees; scaled 1.04x to @2x; curve: outline's dark inner edge #87411E -> #C48C5C, fill
#FCEFE4 -> #F8EBE0, inner white line #FFFAEF -> #FFF5E8; corners feathered into the edge strips so
the outline runs through without a notch.
Export: `UI/Panels/UI_Panel_Modal_Menu.png`, 464 x 464, 9-slice 200 on every side, ppu 200.
Checks: `check/UI_Panel_Modal_Menu_check_13*.png`, `_stretch3x.png`.
Still differs: no paper weave; the outline is about 6 mockup px wide (5 on the mockup) and the
light line on its top edge is a little paler (#FDF3D1, mockup #FDE5BF).

## 4 · UI_Panel_Modal_Bid (UI-1)

Source: `07-bidding.png`, the "Make your call" panel `[482.8, 347.5, 706.3, 309.5]`.
Attachments: `S07-bidding.png`, `A03_Panel_Modal_Bid_crop_07.png` (2x).

### Attempt 1

```
Asset 4: UI_Panel_Modal_Bid. Attached: S07-bidding.png (the full screen) and A03_Panel_Modal_Bid_crop_07.png (the "Make your call" panel enlarged 2x: this crop is what you copy).

Extract the "Make your call" panel as an EMPTY panel: remove the title, the question under it, the eight round number chips and the crimson Confirm button, and fill with the same soft warm ivory. Keep exactly as in the crop: the rounded corners, the thin bright ivory-white rim (no tan or gold outline), and the pale peach textile geometry in ALL FOUR corners (a triangular woven field in each corner). The middle must be plain, even ivory so it can stretch. Front-on, landscape, the same proportions as the crop, the panel filling most of the image. Flat pure-magenta #FF00FF background outside the panel (no transparency, no gradient), no drop shadow, nothing of the table behind it.
```

Result: `UI_Panel_Modal_Bid_a1.png`, 1854 x 848. **Accepted.**
Clean-up: keyed, left half mirrored, scaled 0.91x to @2x (1626 x 712), colours mapped to the
mockup (ChatGPT's fill was a flat, lighter #FEF5E6; the mockup runs from #FBEDDF under the title to
#FBE8D5 at the bottom; ornament #FACCA5), middle squeezed.
Export: `UI/Panels/UI_Panel_Modal_Bid.png`, 512 x 484, 9-slice L224 T224 R224 B196, ppu 200.
Checks: `check/UI_Panel_Modal_Bid_check_07*.png`, `_stretch3x.png`.
Still differs: the mockup's fill also gets a little peachier towards the left and right edges and
has a soft glow behind the title; the sprite has the top-to-bottom course only. The two modals
differ materially (rim against gold outline, four textile corners against two knots, peach against
parchment), so they are two variants: `_Bid` and `_Menu`.

## 5 · Settings controls sheet (not used)

Sent before the small menu controls moved to the Controls designer. Attachments:
`S11-settings.png`, `A05_Settings_controls_sheet_11.png`.

```
Asset 5: a sheet of small controls from the Settings screen. Attached: S11-settings.png (the full screen) and A05_Settings_controls_sheet_11.png: four close-ups from it on a grey sheet, from top to bottom the ON toggle (next to "Music"), the OFF toggle (next to "Reduce motion"), the volume slider, and the language dropdown. The close-ups are what you copy.

Redraw these controls clean as SIX separate pieces in ONE image, on a flat pure-magenta #FF00FF background, well apart from each other (at least a finger's width of magenta between pieces), stacked from top to bottom in this order:
1. The ON toggle: crimson track with the white round knob at its right end.
2. The OFF toggle: grey track with the white round knob at its left end. Exactly the same size as piece 1.
3. The slider's track alone: the pale beige rounded bar at its full length, with nothing on it.
4. The slider's fill alone: the crimson rounded bar, the same length and thickness as piece 3.
5. The slider's knob alone: the white round knob.
6. The dropdown box: the rounded ivory box with its thin warm-brown outline and the small dark chevron at its right end, without the word "English".

Copy each piece exactly from the close-ups: shapes, proportions, colours and the soft shading on the knobs. Draw them large: the toggles and the dropdown about half the image wide, the two slider bars almost the full image width. No text, no labels, no numbers, no drop shadows or glows on the magenta, nothing else in the image.
```

Result: `Settings_Controls_Sheet_a1_notUsed.png`, 1201 x 1309, kept unprocessed. These six pieces
now belong to the Controls designer; nothing was exported from it.

## 6 · Table pieces sheet: UI_NamePlate, UI_StatusPill, UI_TimerRing, UI_Chip_Normal, UI_Chip_Selected

Small uniform pieces, so one request with a contact sheet (brief: "one asset per request, except
for small uniform things"). Sources: `08-gameplay.png` (seat of Bikash: plate
`[104.9, 415.8, 133.2, 33.0]`, pill `[96.5, 448.6, 150.3, 37.8]`; local seat: ring
`[18.9, 828.7, 92.6, 92.6]`) and `07-bidding.png` (chips with their edge line / rim:
`[501.9, 463.2, 72.2, 72.2]` and `[754.4, 460.75, 77.4, 77.4]`).
Attachments: `S08-gameplay.png`, `A06_Table_pieces_sheet_08_07.png` (six close-ups, 3x to 5x).

### Attempt 1

```
Asset 6: a sheet of table pieces. Attached: S08-gameplay.png (the full table screen) and A06_Table_pieces_sheet_08_07.png: six close-ups on a grey sheet. Top-left, middle-left and middle-right: three views of the same two elements, a seat's name plate (the white pill with the player's name) and, right under it, the status pill (the peach pill with "Bid 3 · Won 1"). Top-right: the player's own seat with the orange timer ring around the avatar and the small dark timer disc with the number 18. Bottom row: a bid chip from the bidding screen (S07-bidding.png, sent earlier) in its normal state (ivory, "1") and in its selected state (crimson, "4"). The close-ups are what you copy.

Redraw these clean as SIX separate pieces in ONE image, on a flat pure-magenta #FF00FF background, well apart from each other, in three rows:
Row 1: (1) the name plate alone: the plain white pill, empty. (2) the status pill alone: the peach pill with its thin darker tan outline and its soft lighter centre, empty.
Row 2: (3) the timer ring alone: the orange-gold ring that runs around the avatar, as one complete even ring with nothing inside it (magenta shows through the middle), without the avatar, the number disc or the tick mark. (4) the timer disc alone: the small dark round badge with its orange-gold rim, without the number.
Row 3: (5) the normal bid chip: the ivory disc with its soft tan edge, without the number. (6) the selected bid chip: the crimson disc, without the number.

Copy each piece exactly from the close-ups: shapes, proportions, colours, shading. Draw them large: the two pills each a little under half the image width; the ring, the disc and the two chips each about a quarter of the image width. No letters, no numbers, no avatar, no cards, no drop shadows or glows on the magenta, nothing else in the image.
```

Result: `Table_Pieces_Sheet_a1.png`, 1308 x 1202, six pieces on magenta.

| Piece | Verdict | Clean-up (`build-pieces.sh Table_Pieces`) | Export |
| --- | --- | --- | --- |
| Name plate | **Accepted.** Plain white pill, as the mockup | mirrored both ways; rows matched to the mockup (2 levels) | `UI/Table/UI_NamePlate.png`, 104 x 76, 9-slice L44 R44 |
| Status pill | **Accepted.** Peach pill with a lighter band and a light rim | fringe recoloured; left half mirrored; each row shifted to the mockup's colour at that height (ChatGPT's pill was up to 20 levels lighter) | `UI/Table/UI_StatusPill.png`, 128 x 88, 9-slice L56 R56 |
| Timer ring | **Accepted with clean-up.** Came with the avatar's white ring inside it and three tick marks | only the orange band kept; redrawn from its colour-by-radius so it is exactly round and the same all the way round (radial fill); orange #FDAD04 -> #F6960B | `UI/Table/UI_TimerRing.png`, 214 x 214 |
| Timer disc | **Rejected.** A glossy round ball; the mockup's badge is a flat oval | see asset 8 | - |
| Normal chip | **Accepted with clean-up.** Came 2% wider than tall, with a raised peach rim and a dark line around it; the mockup's chip ends in a thin tan edge line | squared; redrawn exactly round up to the end of the edge line (radius 170 of 178); body shaded like the mockup; edge line coloured as measured around the mockup's chip (#E4C9B5 at the top to #CFAB8D at the bottom) | `UI/Table/UI_Chip_Normal.png`, 166 x 166 |
| Selected chip | **Accepted with clean-up.** Crimson disc with a cream rim (right) and a dark line around it (not on the mockup); too glossy and too bright a red | squared; redrawn exactly round up to the end of the cream rim (radius 173.5 of 178); crimson brought to the mockup's #AF050F (top) to #7E0506 (bottom); rim #FBF8EE (top) to #E3CDB7 (bottom) as on the mockup | `UI/Table/UI_Chip_Selected.png`, 178 x 178 |

Checks: `check/UI_NamePlate_check_08.png`, `UI_StatusPill_check_08.png`, `UI_Seat_pieces_check_08*.png`,
`UI_TimerRing_check_08.png`, `UI_Chip_*_check_07.png`, `*_stretch3x.png`.
The chips' soft shadow is not in the sprites; `kp shadowfit` estimates what the shared circle shadow
needs under them (spread 7.2 / 7.7, drop about 2.5, opacity about 0.6 / 0.4; see the map).
Still differs: the mockup's status pill is slightly see-through over the table, the sprite is
opaque; the mockup's ivory chip has a faint light line just inside its edge, the sprite has not; the mockup's ring has three tick marks and a lighter sheen at its upper left, the sprite
is even; the mockup's selected chip has a little cloudy texture, the sprite is smooth.

## 7 · Results table sheet: UI_TableHeader, UI_RowHighlight, UI_TableBody

Source: `09-round-results.png` (body `[385.9, 322.8, 900, 311.3]`, header `[386, 322.8, 900, 51.1]`,
highlighted row `[384.9, 378.1, 902.2, 61.9]`); the same pieces are on 10 and 16.
Attachments: `S09-round-results.png`, `A07_Results_table_ends_09.png` (left and right end of the
table, 2.8x; the pieces are 900 px wide, so only their ends are shown).

### Attempt 1

```
Asset 7: the table pieces of the results screens. Attached: S09-round-results.png (the full screen) and A07_Results_table_ends_09.png: the left end and the right end of its score table, enlarged about 3x, side by side on a grey sheet (the middle of the table is left out). The table is an ivory rounded container. At its top sits a tan header band (with the words Player ... Total). The first row ("You") is highlighted by a cream-yellow band with a thin gold outline. The other rows are only separated by hairlines. The close-ups are what you copy.

Redraw THREE separate pieces in ONE image, on a flat pure-magenta #FF00FF background, stacked from top to bottom and well apart from each other:
1. The header band alone: the tan band with its two rounded TOP corners and its straight bottom edge, empty (no words).
2. The highlighted row alone: the cream-yellow band with its thin gold outline and its four rounded corners, empty (no avatar, no name, no numbers).
3. The table container alone: the ivory rounded box that holds the rows, with its soft light edge, completely empty: no header band, no highlighted row, no separator lines, no avatars, no text.

Make each piece a SHORT version of itself: join its left end and its right end from the close-ups directly, with a plain, even middle between them, so that pieces 1 and 2 are about five times as wide as they are tall and piece 3 about twice as wide as it is tall. Copy the ends exactly: corner radius, outline thickness, colours, shading. Draw them large, almost the full image width. No letters, no numbers, no drop shadows or glows on the magenta, nothing else in the image.
```

Result: `Results_Table_Sheet_a1.png`, 1774 x 887. **All three accepted.** (It drew them long, not
short; the 9-slice squeezes the middle anyway.)

| Piece | Clean-up (`build-pieces.sh Results_Table`) | Export |
| --- | --- | --- |
| Header band | a flat band: the shape is kept (rounded top corners, straight bottom), the colour set to the mockup's #E9D8C6 (ChatGPT: #EFDDCC) and the edge redrawn exactly | `UI/Table/UI_TableHeader.png`, 96 x 64, 9-slice L40 T40 R40 B8 |
| Highlighted row | mirrored both ways; trimmed to the gold outline (a white halo lay outside it); outline #F2B437 -> #E6B76F; rows matched to the mockup | `UI/Table/UI_RowHighlight.png`, 104 x 104, 9-slice 44 |
| Table body | mirrored both ways; fringe recoloured; fill #FEF7EC -> #F7EDE2 | `UI/Table/UI_TableBody.png`, 96 x 96, 9-slice 40 |

Checks: `check/UI_Table_pieces_check_09.png` (+ `_tl`, `_tr`), `_check_10.png`, `_check_16.png`,
`UI_*_alpha.png`, `UI_RowHighlight_stretch3x.png`, `UI_TableBody_stretch3x.png`.
Still differs: on 10 the mockup's highlighted row is a little lighter in its middle than at its
ends, the sprite is even; corner radius 14 mockup px for all three screens (10.5 to 13.5 on the
mockups). The row separators are hairlines and stay drawn shapes.

## 8 · Game Menu rows sheet: UI_Pill_Tag, UI_ButtonSecondary_Box_Normal, UI_TimerDisc (and UI_Pill_Elapsed)

Sources: `13-match-menu.png` (tag pill `[708.8, 304.3, 254.3, 36.9]`, Scorecard button
`[611.75, 448.65, 447.65, 74.25]` measured at the outer edge of its outline, Sound row
`[614, 539.8, 444.1, 62.3]`); `08-gameplay.png` and
`07-bidding.png` (timer badge `[43.6, 893.7, 42.3, 35.3]`).
Attachments: `A08_MenuRows_sheet_13.png` (three close-ups, 2.8x to 3.4x),
`A09_TimerBadge_crops_08_07.png` (the badge on 08 and 07, 12x). The screens were sent earlier.

### Attempt 1 (timer badge: attempt 2)

```
Asset 8: the rows of the Game Menu, plus a second attempt at the timer badge. Two attachments.

A08_MenuRows_sheet_13.png: three close-ups from the Game Menu (S13-match-menu.png, sent earlier) on a grey sheet, from top to bottom: the small tan tag pill ("Offline match · Paused"), the ivory "Scorecard" button with its thin gold outline, and the pale "Sound" row with the toggle.

A09_TimerBadge_crops_08_07.png: two close-ups (enlarged 12x) of the small dark timer badge that sits at the bottom of the player's avatar on the table screens, with the numbers 18 and 20. In your last sheet you drew this badge as a round glossy ball. It is not: it is a FLAT, matte, dark brown-black OVAL, clearly wider than it is tall (about 1.3 to 1), with only a thin orange rim. No gloss, no highlight, no bevel, no 3D shading.

Redraw FOUR separate pieces in ONE image, on a flat pure-magenta #FF00FF background, stacked from top to bottom and well apart from each other:
1. The tag pill alone: the flat tan pill, empty (no words).
2. The Scorecard button alone: the ivory rounded box with its thin gold outline and soft inner shading, empty (no clipboard icon, no word).
3. The Sound row alone: the pale ivory rounded box with its faint edge, empty (no speaker icon, no word, no toggle).
4. The timer badge alone: the flat dark oval with its thin orange rim, empty (no number), nothing of the avatar or the orange ring around it.

Copy each piece exactly from the close-ups: proportions, corner radius, outline thickness, colours. Keep pieces 2 and 3 at their full length and the same width as each other, almost the full image width; piece 1 a little over half that width; the badge about a quarter of the image width. No letters, no numbers, no icons, no drop shadows or glows on the magenta, nothing else in the image.
```

Result: `MenuRows_TimerBadge_Sheet_a1.png`, 1536 x 1024, four pieces on magenta.

| Piece | Verdict | Clean-up (`build-pieces.sh Menu_Rows`) | Export |
| --- | --- | --- | --- |
| Tag pill | **Accepted.** Flat tan pill | redrawn in exact pill geometry from its own colour; #E9D4BF -> #DFC6AF | `UI/Table/UI_Pill_Tag.png`, 108 x 86, 9-slice L46 R46 |
| Scorecard button | **Accepted.** Ivory box, gold outline with a white line inside it | fringe recoloured; left half mirrored; outline #CB7D17 -> #A86C3A, fill #FCF4E8 -> #F7EBE1; rows matched to the mockup | `UI/Table/UI_ButtonSecondary_Box_Normal.png`, 124 x 172, 9-slice L54 R54 |
| Sound row | **Not used.** It is the same element as the kit card (ivory, light rim); `UI_Panel_Card` at sliceScale 1.1 covers it (`check/UI_MenuRows_check_13.png`) | - | - |
| Timer badge | **Accepted with clean-up.** Flat dark oval with an orange rim, but 1.5 times as wide as tall | redrawn at the mockup's 42.3 x 35.3 from its own colour-by-depth; rim #FD8208 -> #F08505 | `UI/Table/UI_TimerDisc.png`, 98 x 82 |

`UI/Table/UI_Pill_Elapsed.png` (144 x 122, 9-slice L64 R64) is the tag pill's extraction at the
height of the elapsed-time pill on 05 (`[1041.8, 780.9, 122.4, 52.3]`) and in its colour #EADACB:
the same flat pill, so no request of its own.

Checks: `check/UI_MenuRows_check_13.png`, `UI_Pill_Tag_check_13.png`, `UI_Pill_Elapsed_check_05.png`,
`UI_ButtonSecondary_Box_check_13.png`, `UI_ButtonSecondary_Box_stretch3x.png`,
`UI_TimerDisc_check_08.png`, `UI_Timer_pieces_check_08.png`.
Still differs: the mockup has a faint light glow around the words on both pills, the sprites are
flat; the Scorecard button's outline is even all round (on the mockup it is lighter at the top and
darker at the bottom, which is mostly its shadow).

## Not generated

- Lobby seat cards (04): the mockup has no card behind a seat; avatar, name and status sit on the
  large panel, separated by hairlines.
- Hairlines (row separators on 09, 10, 16; the Game Menu divider; the lobby seat separators): they
  stay drawn shapes, as the brief asks.
- The small menu controls (toggles, slider, dropdown, fields, segments, radios, check badge, code
  box, rule disc, highlight pill, matchmaking seats, room-code pill, ready dot) moved to the Controls
  designer during the session.

## Totals

Ten generation requests after `hi` and the set-up message: UI_Panel_Large x3, UI_Panel_Card,
UI_Panel_Modal_Menu, UI_Panel_Modal_Bid, the settings sheet (not used), the table pieces sheet, the
results sheet, the Game Menu sheet. Sixteen sprites exported; all listed in `ArtSource/Kit/Panels.json`
and `ArtSource/Kit/Panels.map.csv`.

## How the exports were made and checked

- `Tools/ArtPipeline/Kit/Panels/build-panels.sh` and `build-pieces.sh` rebuild every sprite from the
  originals in `chatgpt/` (each block is the recipe of one asset) and write the checks. They use
  `kp` (the `kitpanels` tool in the same folder; `kp help` lists its commands).
- ChatGPT could not give real transparency for a panel (attempt 1 of the large panel came back with
  a half-transparent ring eaten out of its middle), so everything after that was asked on flat
  magenta and keyed out (`kp cut --key magenta`), and the outermost pixels recoloured from inside
  (`kp defringe`). `kp fringe` finds no magenta left in any export.
- Colours: ChatGPT returned every ivory lighter and yellower than the mockup (fill #FEF9EF for
  #F8EFE4) and every outline more saturated. Each export was measured against its mockup rectangle
  (`kp stats`, `kp line`) and mapped to the mockup's colours.
- Every sprite was placed at its mockup rectangle over the mockup (`kp check`, `kp place`), looked at
  enlarged, on black and on white, and (9-slice pieces) stretched to three times its size
  (`kp stretch`). `kp seams` reports at most 3 levels of step across any 9-slice line.
- Import settings were applied with `KitImporter.Apply()` and read back from the `.meta` files
  (ppu 200, borders as in `Panels.json`).

## Working notes for the next designer

- The download arrow in the lightbox sometimes opens a small menu ("Download image" / "Download N
  images in this series") instead of downloading; click "Download image".
- ChatGPT answers a sheet request (several pieces on magenta) as well as a single piece, and keeps
  a family consistent; it tends to add rims and gloss to discs and to draw ovals too wide.
- A check that draws a sprite over the mockup cannot show an edge line or a rim the sprite lacks:
  the mockup still shows its own around the sprite. That hid two mistakes here until the edges
  were measured (`kp line`, `kp ringprobe`): the chips first lost their edge line and rim, and the
  Scorecard button was first sized to the inner edge of its outline (what `fitrect` finds). Measure
  the outer edge of anything with an outline before choosing its rectangle.
