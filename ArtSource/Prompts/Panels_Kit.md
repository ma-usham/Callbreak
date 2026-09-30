# Panels and controls kit

Family: `Panels` (panels, fields, toggles, sliders, pills, chips, rings and the other controls).
Chat: ChatGPT project "CardGame", chat "Greeting response" (opened 30 Sep 2026 with `hi`):
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

