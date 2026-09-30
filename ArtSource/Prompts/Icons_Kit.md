# Icons kit (family `Icons`)

Chat: ChatGPT project "CardGame", chat "Greeting exchange", 30 Sep 2026
<https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcffed-33fc-83ee-9fbf-3d94b57992d2>

One chat for the whole family. Model and thinking setting left as the owner had them
(GPT-6.1 Sol, Light).

Sources: `Assets/ArtRefrences/game-screens/NN-*.png` (1672 x 941).
Attachments: `ArtSource/Working/Kit/Icons/attach/`. ChatGPT originals:
`ArtSource/Working/Kit/Icons/chatgpt/` (`S<n>_a<k>.png` = sheet n, attempt k; `cells/` = the
sheets cut into one image per icon). Fitted sprites and their check images:
`ArtSource/Working/Kit/Icons/fitted/` and `check/`. Scripts: `Tools/ArtPipeline/Kit/Icons/`.

## How an icon is made

1. `icons.json` names each distinct icon, the mockup it is drawn largest on and a loose rect
   around it. `icons.sh measure` reads its ink box, ink colour and ground.
2. `icons.sh sheet --sheet S<n>` lays the enlarged crops out on a 1536 x 1024 grid
   (`sheets/S<n>.json`). Single-ink icons are shown as their ink on white, so the sheet has no
   tile edges; multi-coloured ones as the mockup pixels.
3. ChatGPT redraws the sheet clean on white (prompts below). `icons.sh split` cuts it into cells.
4. `icons.sh make` turns a cell into a matte and a distance field, finds the position, size,
   width/height ratio and outline weight at which it lands on the mockup crop (compared in linear
   light, as Unity blends), renders it at @2x (mockup x 2.3) in the flat family ink with 2 px of
   clear margin, and writes the check image and the numbers quoted below: `iou` = overlap of the
   two silhouettes, `edge` = how far the outlines run apart in mockup px (mean / 98th percentile),
   `aspect` = width/height correction applied, `grow` = outline offset applied in mockup px.
5. A sprite is accepted when its check image reads as the same icon at 3x and the numbers agree
   (iou >= 0.95, edge mean <= 0.2 px). Otherwise the cell goes back to ChatGPT, at most five
   attempts.

Family ink: `#0E0B0B` (the ink of the approved main-menu icons). ChatGPT is asked for `#231C19`
(handbook); the fitted sprite is re-inked.

## 0 · Opening

`hi` -> "Hi! What would you like to work on today?"

## 1 · Setup (P-0 adapted to icons)

Sent with ten attachments (nine screens + `00-approved-icons.png`):

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
Please reattach the images". Ten images in one message did not load; four do.

```
Reattached, fewer at a time: three of the reference screens (08-gameplay, 11-settings, 06-offline-setup) and 00-approved-icons.png (settings, shop, friends, offline, chevron: finished icons of this family, match their visual weight). I will attach the other screens together with the requests that use them. Do not generate anything yet. Reply "Ready" and say in one line each what you see in these four images.
```

Reply: "Ready." with a correct one-line description of each of the four images.

(requests and attempts follow)
