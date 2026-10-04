# ShopBackgroundsB: Pokhara Lakeside, Monastery Garden, Himalayan Vista tile picture

Brief (4 Oct 2026): shop backgrounds 5 and 6 as gameplay plates (Back + Front) plus the shop tile
pictures for 4, 5 and 6. Mockup 20 shows tiles 1-3 only; the spec is
`D:\Nepali Callbreak\game-screens\shop-backgrounds-prompts.md` (scene descriptions) and
`shop-drawer-and-tables-prompts.md` (carousel). Catalogue order: Kathmandu Courtyard, Village
Terrace, Vintage Home, Himalayan Vista, Pokhara Lakeside, Monastery Garden.
Prompt logs: `ArtSource/Prompts/Shop_PokharaLakeside.md`, `Shop_MonasteryGarden.md`,
`Shop_HimalayanVista_Thumb.md`. Handoff rows: the last seven rows of `ArtSource/handoff.csv`
(copies of the finals in `ArtSource/Exports/Backgrounds/` and `ArtSource/Exports/Screens/Shop/`).

## Finals (`final/`)

| File | Size | What |
| --- | --- | --- |
| `BG_PokharaLakeside_Back.png` | 2230 x 1255, RGB | scenery plate, BG_Table's frame, lawn under the table |
| `BG_PokharaLakeside_Front.png` | 2230 x 1255, RGBA | bougainvillea + grass at the table's bottom corners, transparent elsewhere |
| `BG_MonasteryGarden_Back.png` | 2230 x 1255, RGB | scenery plate, stupa right of centre, lawn under the table |
| `BG_MonasteryGarden_Front.png` | 2230 x 1255, RGBA | marigolds at the table's bottom corners |
| `Thumb_HimalayanVista.png`, `Thumb_PokharaLakeside.png`, `Thumb_MonasteryGarden.png` | 590 x 670, RGB | shop tile pictures 4, 5, 6 (no table), square corners |

Stack in the game, back to front: Back, ShopTables' `Table_*.png`, Front.
Rebuild everything from `chatgpt/` + `attach/`: `scripts/build-finals.ps1 [-OutDir <dir>]` (checked:
reproduces the finals; intermediates go to `work/`, which is not kept). Checks:
`scripts/final-checks.ps1` writes `check/<Name>_check_{full,16x9,21x9,4x3}.png`.

## Frame

- Plate 2230 x 1255 = the mockup frame plus margins. The 16:9 gameplay view (mockup 08, 1672 x 941)
  sits at (279, 157): checked, `BG_Table.png` cropped there equals `08-gameplay.png` (mean difference
  0.000 on sky and table samples).
- 21:9 phones see x 17..2212 (y 157..1098); 4:3 tablets see y 0..1255 (x 279..1951).
- Table: ShopTables' `final/TableMask.png` / `Table_HimalayanIvory.png` (far lip at y 417 in the
  centre, widest x 251..1978 at y ~780; the drum runs out of the bottom; ivory surface ellipse centre
  (1114.3, 742.2), semi-axes 780.2 x 301.4).
- HUD labels (dark text, no backing), measured on the game captures `Temp/Captures/Table_*.png`:
  "ROUND x / 5" plate x 932-1298, y 175-211 (16:9, 21:9) / y 18-54 (4:3); "Scores" + "Settings"
  x 1714-1908, y 266-288 (16:9), x 1977-2195, y 265-290 (21:9), x 1714-1910, y 108-130 (4:3).

## ChatGPT

- The owner's "CardGame" project no longer exists in the signed-in account ("This project can't be
  found"; the project search finds nothing named CardGame/Card/Callbreak), as ShopBackgroundsA also
  found. So my one chat is a plain chat: https://chatgpt.com/c/6ac24827-71e4-83ee-85c7-ba5e04d26741
  (I renamed it "Callbreak ShopBackgroundsB (Pokhara, Monastery, Vista thumb)"; the title later showed
  as "Greetings" again).
- Opened with "hi"; model/thinking left as found ("Extra High").
- Output size: 1672 x 941 for a 16:9 attachment, 1173 x 1341 for the 7:8 tile.
- Tabs: my first tab (400632871) was taken over by another agent's chat mid-run, and my second
  (400632877) disappeared from the group; I continued in a third (400632888) and never touched the
  others' tabs.
- Several times the conversation answered HTTP 429 on load (rate limit) for 1-4 minutes; results were
  waiting when it came back. No request was sent twice.

## Method (per scene)

1. Scene around the table: attach `attach/BG_Table_view.jpg` (BG_Table's 16:9 view), keep the table
   and camera, replace the scenery. The kept table pins the camera (rim tile shifts dx -1, dy 0..+5).
2. Same picture without the table and without the foreground plants -> centre of the Back plate,
   used 1:1 (no resampling) at (279, 157).
3. Margins (left 279, right 279, top 157 px): "repaint the strip" edits (`scripts/strip-canvas.ps1`):
   the picture 1:1, shifted so a strip is free, the strip filled with a blurred mirror of the edge.
   A flat magenta border made ChatGPT re-frame the whole picture (3 attempts, 8-16% zoom); the
   blurred placeholder asked as an edit kept the layout (scale 0.98-1.012).
   `scripts/assemble-back.ps1` aligns each output to its canvas (similarity fit on gradient NCC over
   the upper, structural part), matches its colour on the overlap (low-frequency per-channel
   gain/offset) and joins it along a minimum-difference seam inside the view's edge band. Columns a
   strip did not reach are mirrored from the strip; the bottom margin (under table + Front) and the
   four corners (never on screen) are shifted copies of the rows above / the top margin.
4. Front: soft foreground plants alone on real transparency (ChatGPT, 1672 x 941, framed like the
   plate at 75%), alpha levelled (12 -> 0, 250 -> 255), upscaled x1.3337 (Lanczos-3, premultiplied),
   each corner cluster scaled toward its corner (x0.82 left, x0.85 right) to BG_Table's flower reach.
5. Tile pictures: ChatGPT 7:8 painting from the plate view + mockup 20's tiles as the style reference,
   centre-cropped to 590:670, Lanczos-3, colour grade.

## Decisions

- **Tile picture size 590 x 670**, the same as ShopBackgroundsA: mockup 20's tile pictures are slightly
  portrait (fitrect 261.8 x 292.8, 254.4 x 292.6, 252.3 x 292.1 mockup px), not landscape as the
  brief says; the mockup wins. Average ~256 x 291, x2.3. Corners left square (the tile UI rounds them,
  radius 8-11.5 mockup px on the mockup).
- **Pokhara upper right**: the willow and the dark hills sat behind "Scores"/"Settings" (16:9 box luma
  137, 36% dark pixels; BG_Table 230, 0%). The willow was removed and a bright cloud bank put over the
  right hills by a blurred-guide edit of the plate's upper-right window (`scripts/pokhara-topright.ps1`;
  aligned with a local displacement field, 355 tiles, median NCC 0.77). Now 199 / 0%.
- **Pokhara blue grade**: its sky and lake came back much more saturated than BG_Table's (sky
  107,180,246, sat 0.56, against 165,207,245, sat 0.33). Blue hues (195-225 deg, 25 deg fade)
  desaturated x0.70 with value kept, on the Back and the tile picture. Monastery's sky was already
  pale (170,199,243): not graded.
- **Tile grades**: luma-preserving saturation x0.75 (Pokhara, Himalayan Vista) and x0.70 (Monastery):
  sat 0.41 / 0.41 / 0.44 against mockup 20's outdoor tiles 0.39-0.41 and BG_Table's scenery 0.415.
- **Front reach**: ChatGPT's clusters reached 0.8% into the ivory playing surface; scaled toward their
  corners they cover 0% of it and 0% of the bottom centre (x 799-1429, y 900+).
