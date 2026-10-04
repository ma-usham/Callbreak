# ShopBackgroundsA: Kathmandu Courtyard, Village Terrace, Vintage Home

Agent: ShopBackgroundsA (art), 4 Oct 2026. Brief: scenery plates (Back), optional foreground
layers (Front) and shop tile pictures (Thumb) for the first three backgrounds of mockup
`Assets/ArtRefrences/game-screens/20-backgrounds.png`. Prompt logs: `ArtSource/Prompts/Shop_<Name>.md`
(named like the ShopBackgroundsB logs).

## Finals (`final/`, also copied to `ArtSource/Exports`)

| File | Size | What |
| --- | --- | --- |
| `BG_KathmanduCourtyard_Back.png` | 2230 x 1255, 24-bit | courtyard scenery, no table (Exports/Backgrounds) |
| `BG_KathmanduCourtyard_Front.png` | 2230 x 1255, RGBA | pots of marigolds, a brass pot (Exports/Backgrounds) |
| `BG_VillageTerrace_Back.png` | 2230 x 1255, 24-bit | village terrace scenery, no table |
| `BG_VillageTerrace_Front.png` | 2230 x 1255, RGBA | copper gagri with cosmos and grass; red rhododendron |
| `BG_VintageHome_Back.png` | 2230 x 1255, 24-bit | Newari interior, no table |
| `BG_VintageHome_Front.png` | 2230 x 1255, RGBA | brass oil lamp and carved chest; brass water pot with marigolds |
| `Thumb_KathmanduCourtyard.png` | 590 x 670, 24-bit | shop tile picture (Exports/Screens/Shop) |
| `Thumb_VillageTerrace.png` | 590 x 670, 24-bit | shop tile picture |
| `Thumb_VintageHome.png` | 590 x 670, 24-bit | shop tile picture |

`TableBackdropBuilder` (Editor) imports the BG_* files straight from `final/`; `ShopCustomizer` expects
the thumbnails at `Assets/_Project/Art/Shop/Backgrounds/Thumb_<Name>.png`.

## Decisions

- **ChatGPT.** The owner's "CardGame" project no longer exists in the signed-in account ("This
  project can't be found"; no project matches "Card" in search), so the chats are outside any project:
  - chat 1 (plates): https://chatgpt.com/c/6ac2480d-4040-83ec-b9c7-09916bb855f2 (renamed "Callbreak
    ShopBackgroundsA (Courtyard, Village, Home)"; around 19:05 its title became "card", not by this
    agent, and from then on it answered HTTP 429 on every load for ~45 minutes);
  - chat 2 (thumbnails, fronts, the shikhara fix): https://chatgpt.com/c/6ac25d2d-5150-83ec-906c-06e36b2d761c
    ("Callbreak ShopBackgroundsA 2 (thumbs, fronts)", opened with "hi").
- **Plate frame.** 2230 x 1255 = the 1672 x 941 design area (mockup pixels, 1:1) plus 279 px left and
  right and 157 px top and bottom (`Tools/ArtTool/Job.cs` ExtendX/ExtendY), the same frame as
  `Assets/_Project/Art/Backgrounds/BG_Table.png`. 16:9 shows x 279..1951, y 157..1098; 21:9 x 17..2212;
  4:3 y 0..1255.
- **Layout method.** Asking ChatGPT for "the same camera and horizon as BG_Table" gave its own camera
  (courtyard a1, a2: the far buildings' bases at plate y ~540, under the table's far rim at y 417).
  What works: BG_Table with the table's area painted flat magenta, edited by ChatGPT with every element
  replaced one for one (terrace wall -> low brick wall / dry-stone wall / wainscot; lake and town ->
  palace / village / back wall; mountains -> pagoda roofs / ridges / carved window; sky -> sky / sky /
  plaster and ceiling). The result keeps BG_Table's eye level and floor perspective.
- **Upscale.** ChatGPT returns the whole plate at 1672 x 941 (0.75 of the frame). Final plates are a
  Lanczos-3 resample to 2230 x 1255 (x1.334) followed by an unsharp mask (amount 0.35, sigma 1.2 px,
  in sRGB), saved as 24-bit PNG. At 1:1 the result is close to BG_Table's centre in apparent detail.
- **HUD readability.** The game's dark HUD text sits on the top band of the plate: "ROUND x / 5" at plate
  x 932-1298, y 175-211; "Scores"/"Settings" labels at x 1714-1773 / 1838-1908, y 266-288 on 16:9 and at
  about x 1977-2037 / 2098-2170 on 21:9 (measured on `Temp/Captures/Table_1920x1080.png` and
  `_2520x1080.png`). Vintage Home was re-asked twice and given a soft light wash so that pale plaster,
  not dark wood, sits behind that text. Courtyard: the shikhara's finial reached into the 16:9 "Scores"
  label (see the prompt log for the fix).
- **Thumbnails.** The tile pictures in mockup 20 are not landscape: measured with `Tools/art fitrect`,
  261.7 x 292.2, 254.3 x 292.6 and 252.3 x 288.3 mockup px (radius 8-10), about 0.88:1. The mockup wins
  over the brief's "landscape". Mean 256.1 x 291.0 mockup px -> 590 x 670 at x2.3 (even), the same size
  as ShopBackgroundsB's. Square corners: `ShopCustomizer.BuildThumbPicture` puts every thumbnail in a
  rounded frame (Mask with the kit card, ThumbCornerScale 2.2). Colour matched to each mockup tile in
  CIELAB (mean/std transfer).
- **Fronts.** ChatGPT gives real RGBA. Each front is resized to the frame, its two halves scaled about
  their corners (and moved where needed) until nothing covers the playing surface (ellipse centre
  1114.3, 742.2, semi-axes 780.2 x 301.4, from ShopTables' notes) or the bottom-centre band (x 700-1530,
  y >= 1000) while still showing on 16:9; then a 1.5 px Gaussian blur to BG_Table's flower softness.

## Check composites

Back + ShopTables `final/Table_HimalayanIvory.png` (published 19:33) + Front, composited in linear
light (`Tools/ArtTool/Img.Over`, as the GPU blends in this Linear project; TableBackdropBuilder assumes
sRGB composites and re-solves fronts accordingly, which only moves the soft edge pixels a few levels).
`check/<Name>_check_full.png` (2230 x 1255), `_16x9`, `_21x9`, `_4x3`, and `_hud` (0.6 scale, with the
16:9 frame, the HUD boxes and the table outline).

"Sits on the floor": a pinhole camera fitted to the table (playing surface far edge y 441, near edge
y 1043, half-width 780.2; outer lip 1.115 R) with the horizon at plate y 280-330 and a round or oval
(depth 0.65-1.0 x width) table puts the floor under the far lip at plate y ~760-815 (table 0.75 m,
radius 0.9 m). So:
- Courtyard: the low brick wall meets the paving at y ~547, about 1.5-2.3 m behind the far lip.
- Village: the dry-stone wall meets the terrace at y ~556, about 1.5-2.2 m behind.
- Vintage Home: the back wall meets the floor at y ~720, about 0.3-0.5 m behind the far lip.
- Objects beside the table (lions, pillars, the plant and pot) stand at depths where the table's ends
  correctly hide or clear them; none overlaps the table in a wrong order.
All three: nothing stands on the floor inside the table's silhouette; the floor converges to about the
same eye level as BG_Table's terrace; light from the upper left.

## Incidents

- ~19:05: chat 1 answered HTTP 429 (and 503 elsewhere) while the courtyard thumbnail request was in
  flight; another agent's tab (400632871) had chat 1 open and its title changed to "card". Thumbnails
  and fronts moved to chat 2. Chat 1's late answer (a second courtyard thumbnail) was not used.
- Chat pages often failed to render in the background tab (screenshots timed out); images were opened
  and downloaded from the chat or from the account's image library (Space > Images) with real clicks.
