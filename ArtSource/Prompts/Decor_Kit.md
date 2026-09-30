# Decor kit (decorations, avatars, backgrounds)

Designer: Decor. Brief: `Docs/Tasks/UI-Art-ChatGPT-Brief.md`. Handbook prompts BG-2, UI-6, UI-7, IC-2.
Chat (the Decor designer's only chat, ChatGPT project "CardGame", auto-titled "Image outpainting
correction"; model setting left as the owner had it):
https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abce176-12b8-83e8-a20a-bbc387de83f5
The same chat holds the wide menu background (`ArtSource/Prompts/Menu_Wide.md`). It was already
in use before the "first message is `hi`" rule, so no `hi` was sent; the Decor work starts at the
setup message below.

Files: attachments `ArtSource/Working/Kit/Decor/attach/`, ChatGPT originals
`ArtSource/Working/Kit/Decor/chatgpt/<Asset>_aN.png`, fitted sprites and check sheets
`ArtSource/Working/Kit/Decor/fit/`, scripts `Tools/ArtPipeline/Kit/Decor/`, finals
`Assets/_Project/Art/UI/Decor/` (copies in `ArtSource/Exports/Decor/`), import settings
`ArtSource/Kit/Decor.json`, swap map `ArtSource/Kit/Decor.map.csv`.

How every asset is checked (`Tools/ArtPipeline/Kit/Decor/fit-decor.ps1`): the cut-out is laid on
its mockup at the measured rectangle over the panel ivory (or over a UI-free plate for pieces on
scenery), and `fit/<Asset>_check.png` shows mockup | sprite | difference ×4 at up to 6×;
`fit/<Asset>_alpha.png` shows it on black and on white. Colour is matched to the mockup on the
sprite's opaque pixels (per-channel gain and offset) before export. Export size = mockup pixels
× 2.3, rounded up to even; ppu 200.

## Setup (P-0, sent once with nine screens attached)

Attached: 09-round-results, 10-match-results, 16-scorecard, 12-how-to-play, 05-matchmaking,
08-gameplay, 04-private-lobby, 14-profile, 01-play-with-friends (ChatGPT renamed them with a
`(1)`… suffix because the project already holds files with those names). Reply: "Ready".

```
New job in this same chat; the background work above is finished and approved. From here on you help me extract the small decorative art of "Callbreak - Play Together", a Nepali card game for phones in landscape. The reference images are the approved design; reproduce them.

Style (never change it): warm ivory parchment panels; deep crimson #861018 with a satin gradient and a thin antique-gold #B8914F rim; warm brown line ornaments with small diamonds; painted gold for the trophy and the crown; black silhouette icons #231C19; painterly Nepal scenery behind everything.

Rules for every image:
1. Match the reference exactly: shape, colours, ornament, lighting, proportions. Do not redesign, restyle or "improve".
2. Never draw letters or numbers unless I ask for the logo.
3. One asset per image unless I send a sheet. Front-on, no perspective, no mockup frame, no background scene, no drop shadow and no glow unless I ask for one.
4. Give a real transparent PNG. If you cannot, use a flat pure-magenta (#FF00FF) background instead; never a drawn checkerboard and never white.
5. Fill the image with the asset, leaving a small margin, as large and sharp as you can.
6. If my request is ambiguous, ask before generating.

I have attached nine approved screens for context: 09 round results, 10 match results, 16 scorecard, 12 how to play, 05 matchmaking, 08 gameplay, 04 private lobby, 14 profile and 01 play with friends. With each request I will attach an enlarged close-up of the one element I want; the close-up is what you copy. Do not generate anything yet. Reply "Ready" and list the attachments you can see.
```

## 1 · Deco_SpadeMedal (UI-7) — also the seal of 16

Attachment: `crop_09_medal.png` (09, rect 772,186,128,112 at 6×).

```
Asset 1: Deco_SpadeMedal.

Extract only the small round crimson medal with the ivory spade from the attached close-up (crop_09_medal.png). It sits under the heading on the Round Results screen (09), and the same medal is on the Scorecard screen (16).

Output it alone, centred, front-on, as large as fits, on a transparent background. Keep exactly: the thin gold rim with its soft highlight, the satin crimson disc with its gentle top-to-bottom shading, and the cream spade with its soft shading, all in the same proportions. It must be a perfect circle.

Add nothing: no lines or diamonds beside it, no text, no sparkles, no glow and no shadow.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Deco_SpadeMedal_a1.png` | 1254 × 1254, real alpha | Accepted. Disc, rim and spade as in the mockup; came back 0.6% out of round (1110 × 1103) and was made exactly round. Crimson a little too red and bright; corrected against the mockup (r ×1.04 −15, g ×0.96 +3, b ×1.07 −1). |

Export: `UI/Decor/Deco_SpadeMedal.png`, 180 × 180, no 9-slice. Mockup rect 09: 796.6, 203, 78.2 × 78
(edges measured on the row through its centre: 796.9 and 875.0). On 16 the same sprite is the
seal at 796.0, 217.0, 79.2 × 78.7 (checked: `fit/zz_seal16_with_medal_check.png`, error 10.0), so
`Deco_Seal_Spade` is not a separate file.
Still differs: the mockup's gold rim is 4.2 px wide at the sides and about 5.5 px at the top on
09 (3.9 px at the sides on 16); ChatGPT's is 3.7 px all round.
The soft shadow under it stays the shared `UI/Buttons/UI_Shadow_Circle.png`.

## 2 · Deco_Trophy (UI-7)

Attachment: `crop_10_trophy.png` (10, rect 650,0,400,146 at 4×).

```
Approved: the medal is right. Match this exact style for the next assets in this chat.

Asset 2: Deco_Trophy.

Extract the gold trophy cup with its two laurel branches from the attached close-up (crop_10_trophy.png; top centre of the Match Results screen, 10). Output the cup and the two branches alone, arranged exactly as in the close-up (one branch on each side of the foot, curving up and outward, not touching the cup), centred, front-on, as large as fits, on a transparent background.

Keep exactly: the polished gold with its bright vertical highlight, the dark bronze spade on the front of the cup, the two curled handles, the stepped foot, and the olive-gold leaves on their thin darker stems, all in the same proportions.

Do not include the small four-point sparkles, the soft glow or the faint light rays behind the cup. No shadow, no text.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Deco_Trophy_a1.png` | 2075 × 758, real alpha | Accepted. Cup, handles, spade, foot and both branches as in the mockup, clean alpha on black and white. Gold slightly dark and saturated; corrected against the mockup (r ×0.90 +23, g ×0.93 +15, b ×0.98 +9). |

Export: `UI/Decor/Deco_Trophy.png`, 500 × 278, no 9-slice. Mockup rect 10 (cup and branches, fitted
over the UI-free plate): 728.1, 18.0, 217.0 × 120.2.
Still differs: ChatGPT's laurel branches carry more leaves (about eleven each against eight or
nine), and the outlines of cup and leaves sit a pixel or two off the mockup's in places (whole-rect
error 20.6 of 255, most of it along the outlines). The three sparkles are the separate
`Deco_TrophySparkle`. The very faint glow and the pale yellow rays behind the cup (2–10 levels
over the haze) are not in this sprite and not rebuilt: see the map.

## 3 · Ornament sheet: Deco_MedalLine, Deco_FooterArrow, Deco_RoundOrnament, Deco_PillOrnament, Deco_Crown (UI-7, one request)

Attachment: `sheet_ornaments.png` (1536 × 1024, five crops at 5× on the panel ivory: 09 rect
568,226,212,36; 09 rect 596,840,126,34; 08 rect 638,22,96,33; 12 rect 589,733,56,56; 10 rect
652,396,70,54). Made by `Tools/ArtPipeline/Kit/Decor/make-attachments.ps1`.

```
Approved: the trophy is right.

Asset 3: a sheet of five small ornaments (sheet_ornaments.png, attached). The sheet shows five close-ups cut from the screens, each still on its own patch of background:
- top left: a long hair-thin dark-brown line that ends in a solid diamond at its right end and fades out to nothing at its left end;
- middle left: a shorter, lighter brown line of the same kind, with a small diamond at its right end;
- bottom left: a short near-black line of the same kind with a diamond at its right end (it sits on blue sky);
- top right: a woven gold diamond ornament built from small squares (ignore the gold pill outline around it);
- right, below that: a small gold crown with a bar under it.

Redraw this sheet clean: the same five ornaments, each in the same place and at the same size as on the sheet, in their own colours, on ONE transparent background. Remove the ivory, blue-sky and yellow background patches and the gold pill outline. Keep the lines perfectly straight, horizontal and hair-thin with the same soft fade at their open end, the diamonds sharp and solid, the little squares of the woven ornament crisp, and the crown with its gold shading. No text, no shadows, no glow, nothing added.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Sheet_Ornaments_a1.png` | 1536 × 1024, real alpha | Accepted. All five ornaments in place, clean alpha. ChatGPT's first tries added a glow (it said so) and it settled on this one itself. Its lines are thinner than the mockup's and its fades are only roughly right, so the line ornaments are finished from mockup measurements (below). |

Line ornaments (`fit-decor.ps1 -Diamond … -LineInk -LineThickness …`): ChatGPT's diamond keeps its
shape at the mockup's size; its line is stretched to the mockup's length and thickened to the
mockup's thickness; ink colour and the line's strength along its length are measured on the mockup.

| Export | Size | Mockup rect | Still differs |
| --- | --- | --- | --- |
| `UI/Decor/Deco_MedalLine.png` | 424 × 36 | 09 left 584.0, 236.4, 183.8 × 15.4; right = flipped at 903.9, 236.4; 16 left 581.8, 250.3; 16 right flipped 903.5, 250.4 | Error 3.4 of 255 on 09. No soft halo round the diamond (the mockup has a 1 px light edge). |
| `UI/Decor/Deco_FooterArrow.png` | 250 × 18 | 09 left 602.0, 854.1, 108.4 × 7.8; right = flipped at 960.9, 854.1 | Error 2.5. |
| `UI/Decor/Deco_RoundOrnament.png` | 182 × 22 | 08 left 643.0, 34.2, 78.3 × 9.2; right flipped 950.6, 34.0; 07 left 643.6, 34.1; 07 right 950.9, 34.0 | The mockup's line thins to a point at its open end; this one keeps its thickness and fades. Near-black #180C0D. |
| `UI/Decor/Deco_PillOrnament.png` | 60 × 62 | 12 left 602.6, 748.3, 26.0 × 26.6; right 1043.4, 748.3 | The mockup's little squares are a regular 4 × 4 lattice; ChatGPT's are about thirteen squares of two sizes. Reads the same at 26 px. Colour matched (offset only). |
| `UI/Decor/Deco_Crown.png` | 76 × 62 | 10 row 0: 671.0, 410.0, 32.9 × 26.5; rows 1–3 + 62.0 each | ChatGPT's three finials are round balls; on the mockup the middle one is a small diamond. Gold matched (r ×0.91 +20, g ×0.96 +7, b ×0.99 +3). |

## 4 · Divider sheet: Deco_TitleDivider, Deco_LobbyDivider, Deco_ProfileDivider, Deco_ProfileDividerVertical (UI-7, one request)

Attachment: `sheet_dividers.png` (1536 × 1024, four rows, each 1400 px wide on the panel ivory: 01
rect 662,196,351,30; 04 rect 452,632,768,30; 14 rect 444,579,784,30; 14 rect 738,253,30,302 turned
to lie flat).

```
Asset 4: a sheet of four long divider ornaments (sheet_dividers.png, attached). Each row of the sheet is one thin horizontal line with a small solid diamond, cut from a screen and still on its ivory background:
- row 1: a warm brown line with a diamond in the middle; the line is strongest next to the diamond and fades out to nothing at both ends;
- row 2: a pale, even hair-thin line with a small diamond in the middle, touching the line;
- row 3: a pale, even hair-thin line with a small diamond in the middle and a small gap on each side of the diamond;
- row 4: a pale line with a larger pale diamond left of the middle and a gap on each side of it (this one is used upright in the game).

Redraw this sheet clean: the same four ornaments, each in the same place and at the same size as on the sheet, in their own colours, on a transparent background (or the flat magenta fallback). Keep every line perfectly straight, horizontal and hair-thin, with the same fades and gaps; keep the diamonds sharp, solid and the same size as on the sheet. No ivory patches, no text, no shadows, no glow, nothing added.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Sheet_Dividers_a1.png` | 1536 × 1024, flat magenta ground | Accepted. Four clean dividers with the right gaps; keyed from the magenta (the line colours came back pinkish from the key, so the ink is taken from the mockup). Rows 2 and 3 are drawn small (diamond 25–33 px), so their diamond is the sharper one of row 4 at the measured size. |

| Export | Size | Mockup rect | Still differs |
| --- | --- | --- | --- |
| `UI/Decor/Deco_TitleDivider.png` | 760 × 30 | 01: 672.0, 204.3, 330.0 × 13.0 | Error 2.7. Proportions of screen 01 only: see "Title divider on the other screens". |
| `UI/Decor/Deco_TitleDivider_Diamond.png`, `Deco_TitleDivider_Line.png` | 30 × 30, 366 × 8 | per screen: `ArtSource/Kit/Decor.title-divider.csv` | Parts of the sprite above (no extra request). |
| `UI/Decor/Deco_LobbyDivider.png` | 1720 × 32 | 04: 462.5, 641.4, 747.1 × 13.3 | Error 1.9. The line is 1.02× ChatGPT's pixels (within the 2× limit). |
| `UI/Decor/Deco_ProfileDivider.png` | 1768 × 38 | 14: 451.8, 586.6, 767.9 × 16.1 | Error 2.3. Line 1.12× ChatGPT's pixels. |
| `UI/Decor/Deco_ProfileDividerVertical.png` | 32 × 670 | 14: 746.6, 260.8, 13.3 × 290.8 | Error 1.8. |

### Title divider on the other screens

The mockups draw this divider differently on every screen: the diamond is 0.89–1.21× the size it
has on 01 and the line is 177–317 px long (276 on 01). One sprite stretched into each box squashes
the diamond, so `ArtSource/Kit/Decor.title-divider.csv` gives, for each of the twelve uses, the
measured diamond centre and size and the line ends, and two ways to place the art: the composed
sprite at its own proportions (`composed_rect`; right for 01, 04, 10 and 15) or the diamond plus two
line sprites (`diamond_rect`, `line_left_rect_flipped`, `line_right_rect`; right everywhere).

## 5 · Deco_AceOverKing (as drawn, like the menu fan)

Attachment: `crop_12_cards.png` (12, rect 996,368,318,336 at 3.5×).

```
Approved: both sheets are right.

Asset 5: Deco_AceOverKing.

Extract the two overlapping playing cards from the attached close-up (crop_12_cards.png; right side of the How to Play panel, screen 12) exactly as they are drawn: the Ace of Spades in front, tilted to the left, and the King of Hearts behind it, tilted to the right, with the same positions, tilts, overlap and sizes.

Keep every detail as it is: the ivory card faces with their faint paper pattern and rounded corners, the black A with the small spade under it at the top-left and again upside-down at the bottom-right, the large black spade in the middle, the red K with the small red heart in both visible corners, and the king's picture in its thin frame, all in the same colours.

Output only the two cards, as large as fits, on a transparent background (or the magenta fallback): remove the panel, its text and the scenery. Keep the soft shadow that the front card casts on the back card; no shadow on the background. No letters other than the A and K on the cards.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Deco_AceOverKing_a1.png` | 1220 × 1289, real alpha | Accepted. Both cards, tilts, overlap, indices and the king as drawn; clean alpha. Colour within 2–5 levels (r ×0.98 +4, g ×0.98 +4, b ×0.99 +5). |

Export: `UI/Decor/Deco_AceOverKing.png`, 640 × 650. Mockup rect 12: 1007.0, 390.5, 277.5 × 282.3.
The mockup's drop shadow under the cards is rebuilt as its own sprite (`shadow-decor.ps1`: the
cut-out's blurred silhouette, values searched against the mockup): `UI/Decor/Deco_AceOverKing_Shadow.png`,
808 × 818, spread 36.4, drop 11, opacity 0.40 (`fit/Deco_AceOverKing_Shadow_check.png`).
Still differs: the king is re-drawn in finer detail, the paper pattern is a little stronger, and
the card edges sit a pixel or two off the mockup's in places (whole-rect error 18.7 of 255 without
the shadow, 14.9 with it; most of what remains is the king's drawing).

## 6 · Deco_SearchArc (UI-7)

Attachment: `crop_05_arc.png` (05, rect 728,208,214,190 at 5×).

```
Asset 6: Deco_SearchArc.

Extract only the ring from the attached close-up (crop_05_arc.png; the search indicator in the Matchmaking panel, screen 05), WITHOUT the black spade in its centre.

Output the ring alone, centred, as large as fits, on a transparent background (or the magenta fallback): a perfect circle of even thickness with a completely empty centre.

Keep it exactly as drawn: the right half, from the top round to the bottom, is solid satin crimson; from the bottom the ring continues up the left side as a soft brown that fades lighter and lighter; at the left (about 9 o'clock) sits the small bright white glowing dot; from there up to the top the ring is only a very pale, barely visible track. Same ring thickness relative to its diameter as in the close-up.

No spade, no text, no shadow, nothing else.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Deco_SearchArc_a1.png` | 1254 × 1254, real alpha | Accepted. Crimson half, fading brown quarter, glow dot and pale track as drawn. Crimson matched (r ×1.04 −18, g ×1.00 +5, b ×1.03 +1). |

Export: `UI/Decor/Deco_SearchArc.png`, 372 × 372: padded to a square with the ring's centre at the
sprite's centre, because the ring rotates. Mockup rect 05: 755.4, 222.3, 161.0 × 161.0 (ring
diameter 143.7; measured on the mockup: 142.8 wide, 143.8 high, centre 835.4, 302.7).
Still differs: the mockup's glow dot has a wider, softer halo; the mockup's ring is 9.1 px thick at
the top and 10.4–10.6 px at the bottom and right, this one is even.

## 7 · Logo_Callbreak_NoFlag (UI-6)

Attachment: `crop_08_logo.png` (08, rect 170,22,360,104 at 4×).

```
Asset 7: Logo_Callbreak_NoFlag (the only asset with letters).

Extract the small logo from the attached close-up (crop_08_logo.png; top-left of the gameplay screen, 08) exactly as it is: the crimson CALLBREAK lettering on its gentle arch, with the darker shading across the letters and the small ivory spade inside each of the two A letters, and under it the words PLAY TOGETHER in dark letters with the thin grey rule and its small diamond on each side.

Keep every letter, colour, bevel, spacing and the arch identical; do not redesign, restyle or respell anything. Make the edges clean and sharp.

Output the logo alone, as large as fits, on a transparent background (or the magenta fallback): no sky, no clouds, no roof, no flag, no glow and no shadow.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Logo_Callbreak_NoFlag_a1.png` | 2172 × 724, real alpha | Accepted. Every letter right, same arch, dark bands across the letters in the same places, dark subtitle, grey rules with diamonds. Colour nearly as is (r ×1.01 +6, g ×1.01 +5, b ×1.17 +9). No tilt against the mockup (checked −1.2° to +0.4°; 0° fits best). |

Export: `UI/Decor/Logo_Callbreak_NoFlag.png`, 768 × 190. Mockup rects: 07 183.4, 33.2, 333.3 × 82.4;
08 183.4, 33.2, 333.0 × 82.4; 09 184.6, 33.0, 331.5 × 82.0; 10 184.9, 32.9; 16 184.4, 33.1.
Still differs: the two spades inside the A letters are clean ivory spades (on the mockup they are
blurred to star-like blobs); the letters are glossier, with a thin bright rim; the mockup's soft
white halo round the logo on 07/08 is not part of the sprite. Screen 03 shows the main-menu logo
(gold PLAY TOGETHER): keep `UI/Common/Logo_Callbreak_NoFlag.png` there.

## 8 · Deco_MountainFooter (UI-7)

Attachment: `crop_01_mountains.png` (01, rect 368,600,460,100 at 3×).

```
Asset 8: Deco_MountainFooter.

The attached close-up (crop_01_mountains.png) is the lower part of the left option card on the Play with Friends screen (01). Behind the text there is a very faint drawing of snowy mountains in pale peach-tan: one range rising in the bottom-left corner and one in the bottom-right corner, both fading away towards the middle and towards the top.

Redraw only that faint mountain drawing, the same width and in the same position as in the close-up, on the card's plain flat ivory (#F8EFE5): the same pale tan ridges and soft shading, the same very low contrast and the same fade-out. Remove the words "invite your friends" and the crimson button along the bottom edge, and continue the faint mountains and the plain ivory where they were.

Landscape, the full width of the close-up. No text, no button, no frame, no shadow. For this one asset keep the flat ivory background: do not make it transparent and do not use magenta.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Deco_MountainFooter_a1.png` | 2172 × 724, on flat ivory | Accepted. Both ranges where the mockup has them, same ridges, text and button gone. |

Export: `UI/Decor/Deco_MountainFooter.png`, 1058 × 216, a transparent overlay: matted against the
flat ivory (darker than the ground becomes the drawing's tan at the alpha that reproduces it,
lighter becomes white; paper grain below 12% dropped). Mockup rect, left card: 368.0, 622.4,
460.0 × 93.2; right card the same sprite at 848.0 (checked: best at +480 px).
Still differs: over the card ivory it is within 3 levels of the mockup in both mountain areas; on a
ground other than the card's ivory the white snow highlights would show more.

## 9 · Avatar sheet: Avatar_01 … Avatar_04 (IC-2 variants, one request)

Attachment: `ref_avatar_default.png` (the approved `UI/Avatars/Avatar_Default.png` at 4× on ivory).

```
Asset 9: four avatar colour variants on one sheet (Avatar_01 to Avatar_04).

The attached image (ref_avatar_default.png) is the game's approved default avatar: a plain head-and-shoulders silhouette in warm light grey inside a dark charcoal circle. Perfect circle, front-on, no rim, no face details, no shadow.

Make a 2 x 2 sheet of four colour variants of this exact avatar for the profile picker. In all four keep the same circle and exactly the same silhouette shape, size and position; only the colours change:
- top left, warm grey: a mid warm-grey circle with a pale warm-grey silhouette;
- top right, bronze: a deep bronze-brown circle with a light golden-tan silhouette;
- bottom left, crimson: a circle in the game's deep crimson (#861018) with a pale rose-ivory silhouette;
- bottom right, ivory: a warm ivory circle (#F3E7D8) with a thin warm-brown outline so it shows on ivory panels, and a soft tan-brown silhouette.

Flat colours with the same very soft shading as the original. Four equal circles, evenly spaced, each as large as fits in its quarter, on a transparent background (or the magenta fallback). No text, no labels, no shadows, no glow.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Sheet_Avatars_a1.png` | 1254 × 1254, real alpha | Accepted. Four discs with the default's silhouette. The cut-out edges are jagged for 4–6 px, so each disc gets an exact circular edge (`avatars.ps1`), and the ivory disc's thin outline (#693F1B) is re-drawn at that edge. |

Exports: `UI/Avatars/Avatar_01.png` (warm grey, disc #60564E), `Avatar_02.png` (bronze, #543210),
`Avatar_03.png` (crimson, #840D15), `Avatar_04.png` (ivory, #F4E9D9 with outline), each 222 × 222
like `Avatar_Default.png`. The silhouette is scaled by 0.965–0.972 and moved under 2 px so it has
the default's height and centre (`fit/Avatars_check.png`).
Still differs: the silhouette is 3% wider than the default's. Nothing on the mockups to compare
the colours with (screen 14 shows only the default).

## 10 · Deco_TrophySparkle (UI-7)

Attachment: `crop_10_sparkle.png` (10, rect 662,60,54,54 at 10×).

```
Asset 10: Deco_TrophySparkle.

Extract the small four-point sparkle from the attached close-up (crop_10_sparkle.png; it sits beside the trophy on the Match Results screen, 10): a slim four-pointed star, pale gold with a near-white centre, its vertical points a little longer than its horizontal ones, with its own faint soft glow.

Output the one sparkle alone, centred, upright, as large as fits with a clear margin, on a transparent background (or the magenta fallback). Keep its exact colours, its slim pointed shape and the soft glow right around it. Nothing else: no second sparkle, no rays, no text, no shadow.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Deco_TrophySparkle_a1.png` | 1254 × 1254, real alpha | Accepted. White core, pale gold points, vertical points longer. Its faint outer pixels carried a grey colour; they take the glow's colour now, and the sprite is softened a little towards the mockup's blur. |

Export: `UI/Decor/Deco_TrophySparkle.png`, 72 × 84. Mockup rects (glow included): left 673.3, 68.0,
30.5 × 36.0; upper right 924.9, 22.9, 16.9 × 19.7; lower right 954.6, 86.0, 26.4 × 30.8.
Still differs: crisper than the mockup's sparkles; the small upper-right one is more golden on the
mockup.

## Not generated

- `Deco_Seal_Spade` (16): the same element as the medal of 09; `Deco_SpadeMedal` is used for both.
- `UI_DeckBack_*` (15): mapped to the real backs `Cards/<Deck>/Back_<Deck>.png` (see the map).
- Hairlines under about 8 mockup px without ornament (header dividers, row separators, footer and
  heading rules): `keep-as-drawn` in the map.
- The trophy's faint glow and rays (10): 2–10 levels over the haze; left out.

## Requests and attempts

Ten requests after the setup message, one attempt each; nothing needed a second attempt. The chat
could not be opened for about seven minutes after request 3 (HTTP 429, then 503); it came back by
itself and the result was there.

## Checks

Every export was laid over its mockup at the rectangle given above (`fit/<Asset>_check.png`:
mockup, sprite on its ground, difference ×4) and looked at on black and on white
(`fit/<Asset>_alpha.png`); `fit/Decor_Kit_on_ivory.png` and `fit/Decor_Kit_on_sky.png` show the whole
family. Import settings were applied with `KitImporter.Apply()` ("applied to 40 sprites", no
problems). Not verified: no screen was rebuilt with these sprites and no capture of the game was
compared; that is the screen engineers' step.
