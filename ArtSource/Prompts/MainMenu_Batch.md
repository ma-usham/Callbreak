# Main menu batch (screen 00)

Source: `Assets/ArtRefrences/game-screens/00-main-menu.png` (1672 × 941)
Attachments: `ArtSource/Working/MainMenu/attach/` (the screen plus eight close-up crops)
Chat: ChatGPT project "CardGame"
Prompts: Designer Handbook v1.0 §7–10, placeholders filled in. One asset per request.

Status of every request is logged under "Attempts" as it is sent.

## Attachments

| File | Shows |
| --- | --- |
| `00-main-menu.png` | The approved main menu, full screen |
| `01-crop-logo.png` | Logo: flag, CALLBREAK, PLAY TOGETHER (2×) |
| `02-crop-button-primary.png` | PLAY ONLINE button (2×) |
| `03-crop-button-secondary.png` | Play Offline pill (3×) |
| `04-crop-button-round.png` | Round settings button (4×) |
| `05-crop-avatar.png` | Avatar silhouette (4×) |
| `06-crop-icons-gear-shop-friends-offline-chevron.png` | Five icons, left to right (4×) |
| `07-crop-spade-gold.png` | Gold spade on the primary button (4×) |
| `08-crop-card-fan.png` | The four-ace fan (2×) |

## 0 · Setup (P-0, sent once with all nine attachments)

```
You help me extract production UI assets for "Callbreak -- Play Together", a Nepali card game
for phones in landscape. The reference images are the approved design; reproduce them.

Style (never change it): warm ivory parchment panels; deep crimson #861018 primary buttons with
a satin gradient, a thin antique-gold #B8914F rim and restrained Nepali woven geometry ONLY at
their ends; black silhouette icons #231C19; painterly Nepal scenery: Newari temple and prayer
flags (left), snowy Himalaya and hillside village (right), blurred red rhododendrons (bottom
corners), pale peach textile geometry (top corners).

Rules for every image:
1. Match the reference exactly: colours, ornament, lighting, proportions. Do not redesign,
   restyle or "improve".
2. Never draw letters or numbers unless I ask for the logo.
3. No flowers, leaves or vines on buttons or panels. No neon, glass, blue panels, green felt,
   chips, coins, photorealism.
4. One asset per image, front-on, no perspective, no mockup frame, no background scene unless
   I ask for one.
5. When I ask for transparency, give a real transparent PNG, never a drawn checkerboard.
6. If my request is ambiguous, ask before generating.

I have attached the approved main menu (00-main-menu.png) and eight close-up crops of its
parts (01 to 08). Do not generate anything yet. Reply "Ready" and list the attachments you can
see.
```

## 1 · BG_Menu_Clean (BG-1)

```
Edit the attached main menu image (00-main-menu.png). Remove EVERY user-interface element: all
text, the logo, buttons, panels, icons, avatars, cards, pills and the card fan. Where each
element was, continue the painted scenery and hazy ivory centre naturally, as if the UI had
never been there. Keep the temple, prayer flags, mountains, village, rhododendrons, textile
corners, colours, lighting and composition exactly as they are. Same 16:9 landscape framing.
No new objects, no text.
```

## 2 · Logo_Callbreak (UI-6)

```
Extract the logo from the attached main menu exactly as it is (close-up: 01-crop-logo.png):
the Nepal flag, the crimson CALLBREAK lettering with the two ivory spades inside the A
letters, the "PLAY TOGETHER" subtitle and its thin gold rule. Keep every letter, colour and
bevel identical; do not redraw or respell anything. Output the logo alone on a transparent
background.
```

## 3 · UI_ButtonPrimary_Normal (UI-3)

```
Extract the large crimson button at the bottom centre of the attached main menu (close-up:
02-crop-button-primary.png), EMPTY: remove the words and the gold spade icon and continue the
satin crimson where they were. Keep the rounded pill shape, the thin antique-gold rim, the
top-to-bottom satin gradient and the woven Nepali geometry at BOTH ends; the centre stays
quiet. Transparent background, no shadow.
```

## 4 · UI_ButtonSecondary_Normal (UI-4)

```
Extract the ivory pill button "Play Offline" from the main menu (close-up:
03-crop-button-secondary.png), EMPTY: remove the icon, words and chevron. Keep its warm brown
outline and soft inner shading. Transparent background, no shadow.
```

## 5 · UI_ButtonCircle_Normal (UI-5)

```
Extract the round ivory button behind the settings gear, top-right of the main menu (close-up:
04-crop-button-round.png), EMPTY: remove the gear. Keep the ivory disc, subtle rim and inner
shading. Perfect circle, transparent background, no shadow.
```

## 6 · Avatar_Default (IC-2)

```
Extract the generic person silhouette avatar from the top-left of the main menu (close-up:
05-crop-avatar.png): grey head-and-shoulders silhouette in a dark charcoal circle. Perfect
circle, front-on, transparent outside the circle, no rim, no shadow, no face details.
```

## 7 · Icon_Spade_Gold (UI-7)

```
Extract only the gold spade on the crimson button of the main menu (close-up:
07-crop-spade-gold.png). Output it alone, centred, front-on, on a transparent background. Keep
its exact colours, soft metallic shading and proportions, and add nothing: no text, sparkles,
glow or shadow.
```

## 8–12 · Icons as SVG (IC-1), one request each

Icons in `06-crop-icons-…png`, left to right: settings gear, shop, friends, offline monitor,
chevron.

```
Write clean SVG code for the {settings gear} icon, the {first} icon from the left in
06-crop-icons-gear-shop-friends-offline-chevron.png: a simple solid black silhouette, viewBox
0 0 64 64, one compound path, fill #231C19, no stroke, no text, no transforms, no gradients.
Keep 4 units of padding and match its visual weight to the other icons in this chat.
```

## 13–16 · Menu fan aces (one request per card)

The fan on the mockup shows plain ivory aces that match none of the three decks (every deck
face has a frame). Extracted as drawn, pending the owner's decision on which deck the fan
should show (GDD §11: "the four aces of your deck").

```
Extract the {Ace of Spades}, the {second card from the left} in the card fan of the main menu
(close-up: 08-crop-card-fan.png). Output the full card alone, perfectly front-on and upright
(undo its tilt), portrait 5:7, rounded corners, transparent outside the card. Keep its plain
ivory face with the faint paper texture, the serif A with the small suit symbol at top-left
and the large centre suit symbol exactly as they are. Complete the parts hidden by the button
and the neighbouring cards naturally. No frame, no ornament, no shadow.
```

## Extra prompts written during the batch

### Icons as images (replaces 8–12)

```
The SVG drifted from the reference shape, so extract the icons as images instead. Extract only
the settings gear icon from the attached close-up (06a-crop-icon-settings.png): the exact same
silhouette, tooth count and proportions, as a flat solid #231C19 shape with crisp clean edges.
Output it alone, centred, front-on, on a transparent background. No disc, no rim, no outline,
no glow, no shadow.
```

The shop, friends, offline and chevron requests follow the same pattern with their own close-up
(`06b`–`06e`). The friends request adds: "The front centre figure is flat solid #231C19; the
two figures behind it keep the same flat mid-grey as the reference."

### Clean plate, second attempt

```
Second attempt at the clean main-menu plate (the very first image in this chat). That result
added scenery in the middle that the original does not have: a wooded hill, a shoreline, lake
detail and extra foothills where the UI used to be. In the original the middle of the picture
is soft pale ivory mist: the logo, card fan, buttons and tagline sit in haze, and the painted
scenery appears only at the left (temple, prayer flags) and the right (Himalaya, village).

Edit the attached 00-main-menu.png again. Remove every UI element (all text, logo, buttons,
icons, avatar, cards) and fill those areas with the same soft pale ivory mist that surrounds
them in the original, fading naturally into the existing scenery at the edges. Do not add a
hill, island, trees, shoreline, water detail or new mountains in the middle. Keep everything
outside the UI areas exactly as it is: temple, prayer flags, mountains, village, rhododendrons,
textile corners, colours and lighting. Same size and 16:9 framing. No new objects, no text.
```

### The fan as drawn (replaces 13–16 on this screen)

```
Extract the whole four-ace fan from the attached main menu exactly as it is drawn (close-up:
08-crop-card-fan.png): the same four cards in the same positions, tilts, overlaps and sizes,
with every A and suit symbol exactly where it is now. Output only the fan on a transparent
background: remove the button, logo and scenery. Complete the lower parts of the cards that the
button hides as plain ivory card face with rounded bottom corners, and do not add indices or
symbols in the completed area. Keep the soft shadows the cards cast on each other; no shadow on
the background.
```

## Attempts

Chat: ChatGPT project "CardGame", chat "Greeting response", 30 Sep 2026. Originals are in
`ArtSource/Working/MainMenu/chatgpt/` (`_a1` = first attempt, `_a2` = second).

| Asset | Prompt | Attempts | Result |
| --- | --- | --- | --- |
| BG_Menu_Clean | BG-1 | 2 | a1 invented a hill, shoreline and foothills in the middle. a2 (misty centre) used, patched under the original so untouched scenery keeps its original pixels. |
| Logo_Callbreak | UI-6 | 1 | Letters, flag and rule identical. Crimson slightly too saturated; corrected against the mockup. |
| UI_ButtonPrimary_Normal | UI-3 | 1 | Matches. Crimson slightly too saturated; corrected against the mockup. |
| UI_ButtonSecondary_Normal | UI-4 | 1 | Matches. Rim a little crisper than the mockup; softened by the colour correction. |
| UI_ButtonCircle_Normal | UI-5 | 1 | Matches. Came back 1% out of round; made exactly round. |
| Avatar_Default | IC-2 | 1 | Matches. |
| Icon_Spade_Gold | UI-7 | 1 | Matches. |
| Icon_Settings | IC-1, then image | 2 | The SVG was a rough, uneven gear. The image extraction matches tooth for tooth. |
| Icon_Shop, Icon_Friends, Icon_Offline, Icon_Chevron | image | 1 each | Match. Ink moved from the requested #231C19 to the mockup's near-black. |
| MenuFan_Ace_Spades, _Hearts, _Diamonds, _Clubs | upright aces | 1 each | Clean upright cards, kept in Working for reference. Not used: on the mockup each ace has its own tilt and symbol placement, and no lower index shows above the button. |
| Deco_MenuFan | fan as drawn | 1 | Matches card for card. Used on the menu. |

Rebuilt, not extracted: the white ring behind the avatar (`UI_AvatarRing`) and the four soft
shadows (`UI_Shadow_*`), which are the exported silhouettes blurred.

## How the exports were checked

Each export was placed back over the clean plate at the size and position it has on the
mockup, and the result compared with `00-main-menu.png` pixel by pixel
(`Tools/ArtPipeline/fit-menu.ps1`). Positions, sizes, shadow settings and the colour
corrections come from that comparison; the working files are in
`ArtSource/Working/MainMenu/fit/`.

The Unity build was then captured at 1920 × 1080 and compared with the mockup scaled to the
same size. Mean difference per colour channel over the whole screen: 4.4 out of 255. All six
labels sit within one pixel of the mockup's in left edge, width, cap height and baseline.
Captures are in `Docs/Verification/MainMenu/`.

What still differs: the lettering (the mockup's text is not a real font, so Inter and Source
Serif 4 are narrowed to the same width), the soft bevel on "PLAY ONLINE", and the exact weave
at the ends of the primary button.

## Open points for the owner

- The mockup's fan shows plain ivory aces that belong to none of the three decks, while GDD §11
  says the fan shows the four aces of the player's deck. The build follows the mockup.
- The background is 16:9 only. Wider and taller screens need the BG-2 extension to 2560 × 1440.
- Icons are extracted images, not the SVG set the handbook asks for.
