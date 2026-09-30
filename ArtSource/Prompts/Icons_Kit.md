# Icons kit (family `Icons`)

Chat: ChatGPT project "CardGame", chat "Greeting exchange", 30 Sep 2026
<https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcffed-33fc-83ee-9fbf-3d94b57992d2>

One chat for the whole family: 12 messages (`hi`, two setup messages, nine sheets), nine generated
images. Model and thinking setting left as the owner had them (GPT-6.1 Sol, Light).

| What | Where |
| --- | --- |
| Sources | `Assets/ArtRefrences/game-screens/NN-*.png` (1672 x 941) |
| Attachments | `ArtSource/Working/Kit/Icons/attach/` (`S1.png` ... `S8.png`, `00-approved-icons.png`, the two clipboard results sent back) |
| ChatGPT originals | `ArtSource/Working/Kit/Icons/chatgpt/S<n>_a1.png`; cut into cells in `chatgpt/cells/<Icon>_a<k>.png` (a<k> = the k-th time that icon was asked for) |
| Fitted sprites, numbers, check images | `ArtSource/Working/Kit/Icons/fitted/`, `check/`, `review/` |
| Final sprites | `Assets/_Project/Art/UI/Icons/Icon_*.png`, copies in `ArtSource/Exports/Icons/` |
| Import settings, swap map | `ArtSource/Kit/Icons.json`, `ArtSource/Kit/Icons.map.csv` |
| Scripts | `Tools/ArtPipeline/Kit/Icons/` (`icons.sh`, `icontool.cs`, `icons.json`, `sheets/`, `accepted.json`, `uses.js`, `dl.sh`) |

## How an icon is made

1. `icons.json` names each distinct icon, the mockup it is drawn largest on and a loose rectangle
   around it. `icons.sh measure` reads its ink box, ink colour and ground.
2. `icons.sh sheet --sheet S<n>` lays the enlarged crops out on a 1536 x 1024 grid
   (`sheets/S<n>.json`). Single-ink icons are shown as their ink on white, so the sheet has no tile
   edges for ChatGPT to copy; multi-coloured ones as the mockup pixels on their own ground.
3. ChatGPT redraws the sheet clean on white (prompts below). `icons.sh split` cuts it into cells.
4. `icons.sh make` turns a cell into a matte and a distance field, finds the place, size,
   width:height ratio and outline offset at which it lands on the mockup crop (compared in linear
   light, as Unity blends), and renders it at @2x (mockup x 2.3) with 2 px of clear margin: in one flat
   ink, or for a multi-coloured icon in its own colours snapped to the colours measured on the mockup.
   It writes the check image (mockup | sprite on the same ground, sharp | at mockup size | outline
   overlay; alpha on black, white copy on crimson, on white, partial alpha) and the numbers quoted
   below.
5. `accepted.json` names the attempt each icon is made from; `icons.sh build`, `export`, `map`
   and `handoff` rebuild everything from it. `icons.sh place` fits each finished sprite onto every
   screen that shows the icon and reports the rectangle for it there.

Colour. ChatGPT is asked for `#231C19` (handbook); it draws `#231914`...`#261D18`. The sprites are
re-inked `#0E0B0B`, the ink of the approved main-menu icons, so the family is one colour. The
mockups' own ink is lighter and varies per screen (`#0F1619`...`#2A2B2A`); the map gives it per use,
with the Image alpha that reads the same. Icons the mockup draws in a colour keep it: leave red
`#B60116`, clock brown `#2F1B0E`, suits red `#F90214`, the create-room badge, the follow-suit
diamonds.

Accepted when the check image reads as the same icon at 3x and the outlines run within about
0.3 mockup px of each other on average (the approved gear and shop measure 0.29 and 0.43 against
the main menu in the same test).

## Requests

### 0 · Opening

```
hi
```

Reply: "Hi! What would you like to work on today?"

### 1 · Setup (P-0 adapted to icons)

Attachments: `01-play-with-friends.png`, `02-create-room.png`, `05-matchmaking.png`,
`06-offline-setup.png`, `08-gameplay.png`, `11-settings.png`, `12-how-to-play.png`,
`13-match-menu.png`, `14-profile.png`, `00-approved-icons.png`.

```
You help me extract production UI icons for "Callbreak - Play Together", a Nepali card game for phones in landscape. The attached reference screens are the approved design; reproduce their icons exactly.

Style of the icon family (never change it): flat solid silhouette icons in near-black charcoal #231C19 with simple rounded geometry and a bold, even weight; no gradients, no shading, no outline added around a shape, no shadow, no glow, no disc, button or tile behind an icon. A few icons have a second colour in the reference (a crimson #861018 badge, a red suit symbol, a gold crown): those keep exactly the reference colours.

Rules for every image:
1. Match the reference exactly: silhouette, proportions, stroke thickness, corner rounding, gaps and counts (lines, dots, waves, teeth). Do not redesign, restyle, simplify or "improve" an icon, and never swap it for a stock icon with the same meaning.
2. Never draw letters, numbers, labels or captions. The only exceptions are marks that are part of an icon in the reference (the "i" of the info icon, the "?" on the book).
3. No flowers, scenery, panels, buttons, discs or frames. Front-on, no perspective, no rotation, crisp clean edges.
4. When I send a contact sheet (a grid with one enlarged icon per cell and a small grey caption under each), return ONE image with the same grid: the same icons in the same cells, in the same order, at the same size and position, redrawn clean and sharp on a perfectly flat pure white #FFFFFF background, with the empty space between them kept. Leave out the captions and anything that is not an icon.
5. When I ask for transparency, give a real transparent PNG, never a drawn checkerboard.
6. If my request is ambiguous, ask before generating.

I have attached nine approved reference screens (01-play-with-friends, 02-create-room, 05-matchmaking, 06-offline-setup, 08-gameplay, 11-settings, 12-how-to-play, 13-match-menu, 14-profile) and 00-approved-icons.png: five icons that are already finished for this family (settings, shop, friends, offline, chevron). Match their visual weight. Do not generate anything yet. Reply "Ready" and list the attachments you can see.
```

Reply: "Ready. I can see these 10 attachment names, but their images couldn't be loaded [...]
Please reattach the images so I can inspect the approved designs before generating." Ten images in
one message did not load; four or fewer always did.

### 2 · Setup, reattached

Attachments: `08-gameplay.png`, `11-settings.png`, `06-offline-setup.png`, `00-approved-icons.png`.

```
Reattached, fewer at a time: three of the reference screens (08-gameplay, 11-settings, 06-offline-setup) and 00-approved-icons.png (settings, shop, friends, offline, chevron: finished icons of this family, match their visual weight). I will attach the other screens together with the requests that use them. Do not generate anything yet. Reply "Ready" and say in one line each what you see in these four images.
```

Reply: "Ready." and a correct one-line description of each of the four images.

### 3 · Sheet 1: header and table icons -> `S1_a1.png`

Attachments: `S1.png`, `08-gameplay.png`, `13-match-menu.png`, `01-play-with-friends.png`.

```
Sheet 1: header and table icons. Attached: S1.png (the contact sheet) and the screens its icons come from: 08-gameplay.png, 13-match-menu.png, 01-play-with-friends.png (11-settings.png is already in this chat).

S1.png is a 4 x 2 grid of enlarged crops, one icon per cell, numbered under each cell:
1 back arrow (screen 01, top-left round button)
2 home (08, top-left)
3 scores clipboard (08, top-right, "Scores")
4 close cross (13, top-right corner of the Game Menu panel)
5 chat bubble with three dots (08, bottom-left)
6 sort: two overlapping cards (08, bottom-right)
7 sound: speaker with two waves (11, the "Sound" heading)
8 leave: door with an arrow pointing out (13, "Leave Match")

The crops are blurry because they are enlarged from the screens. Redraw this exact sheet clean: the same eight icons in the same cells, in the same order, each at the same size and position as on the sheet, as flat solid shapes with crisp edges on a perfectly flat pure white #FFFFFF background. Icons 1 to 7 are solid #231C19; icon 8 keeps its flat crimson red. Trace every silhouette: keep the proportions, stroke thickness, corner rounding, gaps and angles exactly as in the crop, and do not replace an icon with a standard icon of the same meaning. The clipboard (3) has three short lines, each with a small dot at its left, cut out in white. No captions, no numbers, no grid lines, no frames, no shadows, no gradients. Same canvas as the sheet: 1536 x 1024, landscape.
```

Result: back, home, close, chat bubble, sort, sound and leave accepted. Clipboard: body and clip
right, the three rows came out as hairlines (0.45 px at mockup size) -> asked again.

### 4 · Sheet 2: the four suit symbols -> `S2_a1.png`

Attachments: `S2.png`, `05-matchmaking.png`, `07-bidding.png`.

```
Sheet 2: the four suit symbols. Attached: S2.png (the contact sheet), 05-matchmaking.png (the spade in the search ring) and 07-bidding.png (the cards in the hand); 08-gameplay.png is already in this chat.

S2.png is a 2 x 2 grid of enlarged crops, one symbol per cell:
1 spade (screen 05, inside the search ring)
2 heart (08, the big heart on the ace of hearts in the hand)
3 diamond (08, the big diamond on the king of diamonds; ignore the faint pink line beside it, that is the edge of the next card)
4 club (07, the small club under the Q of clubs)

Redraw this sheet clean, as one matching set of suit symbols: the same four shapes in the same cells, at the same size, as flat solid shapes with crisp edges on a perfectly flat pure white #FFFFFF background. Spade and club are solid #231C19; heart and diamond keep the flat pure red of the crop. Keep each silhouette exactly as in its crop: the spade with its pointed top, full round lobes and flared stem; the heart with two round lobes, a small notch and a long pointed tip; the diamond tall, with slightly concave sides; the club with three round lobes and a flared stem. The diamond and the club are a little tilted in the crops because the cards are fanned: draw them perfectly upright. Every symbol is exactly mirror-symmetric, left to right. No captions, no numbers, no card, no frame, no shadow, no gradient. Same canvas as the sheet: 1536 x 1024, landscape.
```

Result: all four accepted. Each is mirrored about its own axis, set upright (the diamond's card
is tilted 5 degrees, the club's 8) and exported 182 px tall, so the set stands on one line.

### 5 · Sheet 3: settings, offline and room icons -> `S3_a1.png`

Attachments: `S3.png`, `02-create-room.png`, `03-join-room.png`, `14-profile.png`.

```
Sheet 3: settings, offline and room icons. Attached: S3.png (the contact sheet) and the screens its icons come from: 02-create-room.png, 03-join-room.png, 14-profile.png (11-settings, 06-offline-setup and 05-matchmaking are already in this chat).

S3.png is a 4 x 2 grid of enlarged crops, one icon per cell:
1 how to play: a closed book with a question mark on its cover (screen 11, "How to Play" button)
2 about: a ring with a lower-case i inside (11, "About" button)
3 one player: head and shoulders (06, "Easy" card)
4 lock: a closed padlock with a keyhole (02, beside "Private room")
5 cards: three playing cards fanned out (02, "Match length" tile)
6 copy: two overlapping rounded squares (03, "Paste Code" button)
7 clock: a ring with two hands (05, beside 00:12)
8 pencil (14, the small round button on the avatar)

Redraw this exact sheet clean, the same way as sheet 1: the same eight icons in the same cells and order, each at the same size and position as on the sheet, as flat solid #231C19 shapes with crisp edges on a perfectly flat pure white #FFFFFF background. Trace every silhouette: keep the proportions, stroke thickness, corner rounding, gaps and angles exactly as in the crop. Everything that is light inside an icon in the crop stays white: the question mark, the page edges of the book, the inside of the ring and of the clock, the keyhole, the gaps between the cards, the inside of the two squares. Stroke ends and corners are cleanly rounded, never chamfered or notched. No captions, no numbers, no frames, no shadows, no gradients. Same canvas as the sheet: 1536 x 1024, landscape.
```

Result: all eight accepted.

### 6 · Sheet 4: the large icons -> `S4_a1.png`

Attachments: `S4.png`, `06-offline-setup.png`.

```
Sheet 4: the large icons. Attached: S4.png (the contact sheet) and 06-offline-setup.png; 01-play-with-friends.png is already in this chat.

S4.png is a 2 x 2 grid of enlarged crops, one icon per cell:
1 monitor with a spade on its screen and a stand (screen 06, above "Choose your opponents")
2 enter room: an arrow pointing into a rounded square frame that is open on the left (01, "Join Room" card)
3 three players, normal: a large head and shoulders in front with a small player on each side (06, "Normal" card)
4 three players, hard: the same group with larger players on each side (06, "Hard" card)

Redraw this exact sheet clean, the same way as the sheets before: the same four icons in the same cells, each at the same size and position as on the sheet, as flat solid #231C19 shapes with crisp edges on a perfectly flat pure white #FFFFFF background. Trace every silhouette: keep the proportions, stroke thickness, corner rounding, gaps and angles exactly as in the crop. The monitor frame, the spade inside it, the neck and the foot keep their exact sizes; the gaps between the players stay white and keep their width; the side players in 3 are clearly smaller than in 4. Stroke ends and corners are cleanly rounded, never chamfered or notched. No captions, no numbers, no frames, no shadows, no gradients. Same canvas as the sheet: 1536 x 1024, landscape.
```

Result: all four accepted.

### 7 · Sheet 5: profile, collection and how-to-play icons -> `S5_a1.png`

Attachments: `S5.png`, `15-card-styles.png`, `12-how-to-play.png`.

```
Sheet 5: profile, collection and how-to-play icons. Attached: S5.png (the contact sheet) and the screens its icons come from: 15-card-styles.png and 12-how-to-play.png (14-profile and 08-gameplay are already in this chat).

S5.png is a 4 x 2 grid of enlarged crops, one icon per cell:
1 games played: two solid playing cards, the front one with a spade cut out in white (screen 14)
2 wins: a trophy cup with two handles (14)
3 win rate: three rising bars (14)
4 best score: a five-pointed star with rounded points (14)
5 card collection: a solid card with a white spade, in front of two more cards (15, top-right round button)
6 two cards drawn as outlines, the front one with a solid spade (12, rule 1)
7 score sheet drawn as an outline: a clipboard with a ring clip and three lines that each start with a dot (12, rule 4)
8 scores clipboard, solid: a solid clipboard with three rows cut out in white, each row a dot and a line (08, top-right, "Scores")

Redraw this exact sheet clean, the same way as the sheets before: the same eight icons in the same cells and order, each at the same size and position as on the sheet, as flat solid #231C19 shapes with crisp edges on a perfectly flat pure white #FFFFFF background. Trace every silhouette: keep the proportions, stroke thickness, corner rounding, gaps and angles exactly as in the crop; whatever is light inside an icon in the crop stays white. Stroke ends and corners are cleanly rounded, never chamfered or notched.

Icon 8 is a second attempt. On sheet 1 its three white rows came out as hairlines; in the crop they are bold. Each white line is about 13 px thick on this sheet (as thick as the clip's white outline), the dot at its left is about 20 px across, and the lines run to about 30 px from the right edge of the clipboard. Keep the clipboard body and the clip exactly as you drew them on sheet 1.

No captions, no numbers, no frames, no shadows, no gradients. Same canvas as the sheet: 1536 x 1024, landscape.
```

Result: the four profile icons accepted. Card collection, two cards with a spade and the outline
score sheet were redrawn from memory rather than traced (cards behind at other angles, thinner
outlines, overlap 0.89-0.90) -> asked again. Clipboard, second attempt: the bell-shaped clip and
its white outline were lost (a ring clip like icon 7) and the rows too thick -> asked again.

### 8 · Sheet 6: the create-room icon -> `S6_a1.png`

Attachments: `S6.png`, `01-play-with-friends.png`.

```
Sheet 6: the "create room" icon, the only one on this sheet. Attached: S6.png (one enlarged crop on its ivory tile) and 01-play-with-friends.png (the icon is on the "Create Room" card).

Redraw this icon clean, at the same size and position as on the sheet, on a perfectly flat pure white #FFFFFF background (leave out the ivory tile and the caption). It has four flat colours, and it keeps them:
- the large player in front (head and shoulders) and the shoulders of the player behind on the left: solid #231C19
- the two smaller heads behind, left and right: the flat dark grey of the crop, clearly lighter than #231C19
- the round badge that overlaps the lower right: flat crimson, as in the crop
- the bold plus on the badge: pure white, with rounded ends

Trace every silhouette: keep all sizes, positions, gaps and curves exactly as in the crop. The gaps between the players are white; a white ring of even width separates the badge from the players behind it; the right player shows only a head, because the badge covers the shoulders. Flat colours with crisp edges only: no gradient, no shading, no gloss on the badge, no outline, no shadow. Same canvas as the sheet: 1536 x 1024, landscape.
```

Result: accepted. The four drawn colours (`#261D18`, `#46433F`, `#951016`, `#FDFDFD`) are snapped to
the family ink and the colours measured on the mockup (`#0E0B0B`, `#454542`, `#8A0913`, `#FCF9F5`).

### 9 · Sheet 7: two multi-coloured icons and two second attempts -> `S7_a1.png`

Attachments: `S7.png`, `10-match-results.png`.

```
Sheet 7: two multi-coloured icons and two second attempts. Attached: S7.png (the contact sheet) and 10-match-results.png (the crown is beside "You" in the first row); 12-how-to-play, 08-gameplay and 03-join-room are already in this chat.

S7.png is a 2 x 2 grid of enlarged crops:
1 follow the suit (screen 12, rule 2): three playing cards drawn as bold outlines, the front one with a red diamond; two small red diamonds show at the lower left, between the cards. Shown on its ivory tile.
2 crown (screen 10): a small golden crown with three points, each topped by a small knob, and a separate bar under it. Shown on its pale gold tile.
3 heart (08): second attempt.
4 copy (03, "Paste Code"): second attempt.

Redraw this sheet clean: the same four icons in the same cells, each at the same size and position as on the sheet, on a perfectly flat pure white #FFFFFF background. Leave out the two tiles and the captions.
1: the outlines are solid #231C19 and as bold as in the crop; the inside of the cards is white; the diamonds keep the flat deep red of the crop. Keep the tilt and overlap of the three cards exactly.
2: keep the gold of the crop, with its soft shading from light gold at the top to deep amber at the bottom; the same three points, knobs and bar; no outline, no sparkle, no glow, no shadow.
3: flat pure red as in the crop. On sheet 2 the sides curved inwards; in the crop they do not: from its widest point each side runs in an almost straight, very slightly convex line down to the sharp tip. Two full round lobes and a small narrow notch, as in the crop. Exactly mirror-symmetric.
4: solid #231C19. Two rounded squares drawn as bold outlines of even thickness, as thick as in the crop; the front one at the lower left overlaps the back one; both have the same generously rounded corners, also the top right corner of the back square; the back outline simply ends where the front square covers it.
No gradients except the crown's own shading, no shadows, no numbers. Same canvas as the sheet: 1536 x 1024, landscape.
```

Result: follow the suit accepted (outlines in the family ink, diamonds `#B00814` as measured).
Crown: matched the crop (overlap 0.96), then withdrawn: the Decor family delivers the same crown
as `UI/Decor/Deco_Crown.png` and the match-results screen already shows that one (mine is kept in
`Working/Kit/Icons/withdrawn/`). Heart, second attempt: no closer than the first (0.38 px against
0.37 px: the pip painted on the card is not symmetric, the symbol is) -> first attempt kept. Copy,
second attempt: outlines thinner, overlap 0.87 against 0.95 -> first attempt kept.

### 10 · Sheet 8: second attempts, larger, from tightened crops -> `S8_a1.png`

Attachments: `S8.png` (cells 2-4 with the crop's edges tightened, `"crisp": 5` in `sheets/S8.json`),
`scorecard-sheet1-result.png`, `08-gameplay.png`.

```
Sheet 8: second attempts for four detailed icons of sheet 5, larger this time. Attached: S8.png (the contact sheet) and scorecard-sheet1-result.png (your own clipboard from sheet 1). The screens are already in this chat (08-gameplay, 12-how-to-play, 15-card-styles).

S8.png is a 2 x 2 grid:
1 scores clipboard, solid (08, top-right "Scores"): the enlarged crop, soft.
2 score sheet, outline (12, rule 4)
3 two cards with a spade (12, rule 1)
4 card collection (15, top-right round button)
Cells 2, 3 and 4 are the crops with their edges tightened, so they show the exact outline to trace.

Redraw this sheet clean: the same four icons in the same cells, each at the same size and position as on the sheet, as flat solid #231C19 shapes with crisp edges on a perfectly flat pure white #FFFFFF background.

1: third attempt. The body and the clip were right on sheet 1 (scorecard-sheet1-result.png): a solid rounded body and, at the top centre, a solid clip with a pointed bell-shaped top, separated from the body by a thin white outline that runs around its lower part. Keep exactly that. Only the three white rows change: each row is a round white dot at the left and a straight white line with rounded ends; the lines are about 9 px thick on this sheet and the dots about 14 px across (on sheet 1 they were hairlines, on sheet 5 too thick). Three equal rows, evenly spaced, the third like the other two. No ring, no hole in the clip.
2, 3, 4: trace precisely the silhouette shown: the same stroke thickness everywhere (the outlines are bold: do not thin them), the same corner radii, the same angles, tilts, overlaps and gaps; only make the lines straight, the curves even and the edges smooth. In 2 each of the three rows is a round dot followed by a bar with rounded ends, dot and bar separated by a small gap, and the third bar is shorter; the clip has a small round hole. In 3 and 4 the spade keeps its size and place.

No captions, no numbers, no frames, no shadows, no gradients. Same canvas as the sheet: 1536 x 1024, landscape.
```

Result: all four accepted. Card collection 0.97, two cards 0.94, score sheet 0.91 (on the mockup
each dot runs into its bar; drawn as a dot and a bar). Clipboard, third attempt: the right build;
as drawn its rows are 1.6 px thick and the white line around the clip 0.9 px. The mockups disagree
about the rows (07/08 about 0.8 px and faint, 09/10/13 about 2 px) and draw the line around the
clip about 1.4 px wide, so the sprite is cleaned up to that: the outline moved in by 0.25 px (clip
line 1.4 px) and the ink grown 0.4 px into the enclosed rows and dots only (rows 1.3 px)
(`"grow": -0.25, "holeGrow": 0.4` in `accepted.json`).

### 11 · Sheet 9: the clipboard alone -> `S9_a1.png`

Attachment: `scorecard-sheet8-result.png`.

```
Sheet 9: one icon, the solid scores clipboard again (fourth attempt). Attached: scorecard-sheet8-result.png, your own result from sheet 8. It is almost right.

Redraw exactly this icon, larger: alone, centred on a 1536 x 1024 landscape canvas, about 850 px tall, as a flat solid #231C19 shape with crisp edges on a perfectly flat pure white #FFFFFF background.

Keep everything as it is: the rounded body, the bell-shaped clip with its pointed top, and the three equal rows of a round white dot and a straight white line with rounded ends.

Change only one thing: the white outline that separates the clip from the body is too thin. Make it exactly as thick as the white row lines, the same thickness all along, following the same path around the lower part of the clip, from the notch at the left shoulder to the notch at the right shoulder.

No captions, no numbers, no frame, no shadow, no gradient.
```

Result: not used. The clip line is thicker, but the body came out 5.6 % too wide and the rows
thicker again (2.2 px). The third attempt stays.

## Accepted sprites

Fitted to the screen the icon is drawn largest on. "Outlines apart": for every point of one
outline the distance to the other, mean and 98th percentile, in mockup px (1 mockup px = 2.3
sprite px). "Width:height set" and "Outline moved" are the corrections applied to ChatGPT's drawing
to land it on the mockup. All: PNG-32, 2 px clear margin, no 9-slice, ppu 200.

| Sprite | Sheet, attempt used | Asked | Export px | Fitted to | Overlap | Outlines apart, px (mean / 98th pct) | Width:height set | Outline moved, px | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `Icon_Back` | S1 a1 | 1 | 92 x 90 | 01 | 0.989 | 0.09 / 0.40 | 0.987 | +0.04 | exported 3 % larger than on 01: 05 draws it that much larger |
| `Icon_Home` | S1 a1 | 1 | 100 x 100 | 08 | 0.983 | 0.15 / 0.43 | 0.998 | +0.20 | exported 1 % larger than on 08 (10) |
| `Icon_Scorecard` | S8 a3 | 4 | 82 x 112 | 08 | - | 0.25 / 0.81 (outer) | 1.003 | -0.25 | the three white rows are 1.3 px thick: 07/08 draw them at about 0.8 px and faint, 09/10/13 at about 2 px |
| `Icon_Close` | S1 a1 | 1 | 70 x 70 | 13 | 0.984 | 0.11 / 0.35 | 0.983 | +0.12 |  |
| `Icon_Emote` | S1 a1 | 1 | 86 x 88 | 08 | 0.945 | 0.16 / 0.56 | 0.976 | +0.17 |  |
| `Icon_Sort` | S1 a1 | 1 | 104 x 106 | 08 | 0.960 | 0.14 / 0.46 | 0.985 | +0.19 |  |
| `Icon_Sound` | S1 a1 | 1 | 126 x 114 | 11 | 0.981 | 0.15 / 0.50 | 0.999 | +0.03 |  |
| `Icon_Leave` | S1 a1 | 1 | 82 x 82 | 13 | 0.976 | 0.14 / 0.53 | 0.986 | +0.12 | in the red of the mockup (#B60116), like its label; not the family charcoal |
| `Icon_Suit_Spade` | S2 a1 | 1 | 162 x 186 | 05 | 0.991 | 0.17 / 0.53 | 0.987 | +0.24 | one of a set of four, all 182 px tall; also the trump spade of the HUD, the spade of rule 3 and of the search ring |
| `Icon_Suit_Heart` | S2 a1 | 2 | 164 x 186 | 08 | 0.978 | 0.37 / 0.85 | 0.983 | +0.13 | suit set; in the red of the mockup's cards (#F90214). The text sprites take only its outline and use their own red |
| `Icon_Suit_Diamond` | S2 a1 | 1 | 124 x 186 | 08 | 0.987 | 0.22 / 0.69 | 0.944 | +0.14 | suit set; red #F90214; drawn upright (the card on 08 is tilted 5 degrees) |
| `Icon_Suit_Club` | S2 a1 | 1 | 172 x 186 | 07 | 0.975 | 0.16 / 0.52 | 0.921 | +0.19 | suit set; drawn upright (the card on 07 is tilted 8 degrees) |
| `Icon_HowToPlay` | S3 a1 | 1 | 112 x 120 | 11 | 0.947 | 0.16 / 0.47 | 0.992 | +0.31 |  |
| `Icon_About` | S3 a1 | 1 | 122 x 122 | 11 | 0.952 | 0.20 / 0.55 | 1.004 | +0.30 |  |
| `Icon_OfflineMonitor` | S4 a1 | 1 | 360 x 286 | 06 | 0.979 | 0.12 / 0.39 | 0.995 | +0.10 |  |
| `Icon_DifficultyEasy` | S3 a1 | 1 | 128 x 144 | 06 | 0.990 | 0.13 / 0.44 | 1.000 | +0.25 |  |
| `Icon_DifficultyNormal` | S4 a1 | 1 | 212 x 150 | 06 | 0.978 | 0.18 / 0.52 | 0.988 | -0.02 |  |
| `Icon_DifficultyHard` | S4 a1 | 1 | 230 x 156 | 06 | 0.957 | 0.26 / 0.64 | 0.988 | -0.07 |  |
| `Icon_CreateRoom` | S6 a1 | 1 | 462 x 350 | 01 | 0.988 | 0.25 / 0.71 | 0.994 | -0.14 | four flat colours: front player in the family ink, the two heads behind #454542, badge #8A0913, plus #FCF9F5 |
| `Icon_EnterRoom` | S4 a1 | 1 | 338 x 320 | 01 | 0.977 | 0.12 / 0.39 | 0.997 | +0.11 | 03 draws the same icon with a lower frame: placed at its best fit there |
| `Icon_Cards` | S3 a1 | 1 | 120 x 112 | 02 | 0.971 | 0.16 / 0.48 | 1.002 | +0.17 |  |
| `Icon_Lock` | S3 a1 | 1 | 66 x 82 | 02 | 0.959 | 0.19 / 0.68 | 0.965 | +0.32 |  |
| `Icon_Copy` | S3 a1 | 2 | 88 x 94 | 03 | 0.951 | 0.20 / 1.03 | 0.984 | +0.36 | the copy icon of 04 and the paste icon of 03 are the same icon |
| `Icon_CardStack` | S8 a2 | 2 | 112 x 112 | 15 | 0.968 | 0.18 / 0.61 | 0.991 | -0.02 |  |
| `Icon_Rule_Call` | S8 a2 | 2 | 170 x 168 | 12 | 0.941 | 0.16 / 0.51 | 0.994 | -0.07 |  |
| `Icon_Rule_Follow` | S7 a1 | 1 | 180 x 162 | 12 | 0.906 | 0.26 / 0.73 | 0.989 | +0.11 | two flat colours: outlines in the family ink, diamonds #B00814 |
| `Icon_Rule_Score` | S8 a2 | 2 | 130 x 162 | 12 | 0.912 | 0.26 / 1.06 | 0.986 | +0.05 | on the mockup each dot runs into its bar; drawn as a dot and a bar |
| `Icon_Clock` | S3 a1 | 1 | 66 x 66 | 05 | 0.960 | 0.09 / 0.33 | 0.971 | +0.13 | in the dark brown of the mockup (#2F1B0E), not the family charcoal |
| `Icon_Pencil` | S3 a1 | 1 | 76 x 76 | 14 | 0.977 | 0.10 / 0.38 | 0.972 | +0.16 |  |
| `Icon_StatGames` | S5 a1 | 1 | 142 x 152 | 14 | 0.971 | 0.25 / 0.91 | 1.013 | +0.37 |  |
| `Icon_StatWins` | S5 a1 | 1 | 132 x 140 | 14 | 0.980 | 0.16 / 0.55 | 0.991 | +0.11 |  |
| `Icon_StatWinRate` | S5 a1 | 1 | 124 x 128 | 14 | 0.994 | 0.10 / 0.32 | 1.011 | +0.19 |  |
| `Icon_StatBestScore` | S5 a1 | 1 | 140 x 138 | 14 | 0.989 | 0.14 / 0.46 | 1.001 | +0.06 |  |

Checked on the exported files (`icons.sh audit`): even sizes, 2-3 px clear margin, border fully
transparent, no specks, the colour under partly transparent pixels equal to the ink (no fringe),
export copies identical, ppu 200. Every use shows its sprite at 2.3 sprite px per mockup px or more
(nothing is enlarged), and every sprite is a reduction of ChatGPT's drawing (0.26 to 0.92 sprite px
per drawn px). `icons.sh build` reproduces the exported files byte for byte from the cells.

Not made here, used as they are: the invite icon of 04 and the players icon of 02 are the approved
`Icon_Friends`; the gear of the Preferences heading on 11 is the approved `Icon_Settings`; every
chevron is the approved `Icon_Chevron` (or its white copy); the crown of 10 is the Decor family's.

## What still differs from the mockups

- One sprite serves every screen, and the mockups redraw the same icon a little differently per
  screen. Worst fits of a shared sprite (overlap, outlines apart on average): clipboard on the match
  menu 13 (0.73, 1.01 px: another clipboard drawing, with bold rows and a small clip), enter-room on
  03 (0.81, 1.31 px: a wider, lower frame), copy on 04 (0.83, 0.47 px), clipboard on 09 and 10 (0.84
  and 0.87, 0.5 px), sound on 13 (0.85, 0.62 px), trump spade of the HUD on 08 (0.91, 0.56 px at
  25 px size: the HUD spade is slimmer than the one in the search ring), home on 10 (0.94, 0.40 px).
  `Icons.map.csv` gives the best-fit rectangle for each at the sprite's own proportions and, where
  a screen draws the icon with other proportions (enter-room on 03, clipboard on 10), the stretched
  rectangle that lands closer; the full table is `ArtSource/Working/Kit/Icons/place.log`.
- Ink: the family ink `#0E0B0B` is darker than the mockups' own (`#14`...`#2A` grey). Per use the
  map gives the mockup's ink and the Image alpha that reads the same.
- The mockup's clipboard rows on 08 end in a scribble; the sprite has three equal rows.
- The back square of the copy icon has a cut top-right corner on 03 (not on 04); the sprite has
  the same rounded corner on both squares.
- The heart on 08's ace is not quite symmetric; the symbol is.
