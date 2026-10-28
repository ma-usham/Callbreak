# V2 UI kit

The game's panels, buttons, controls and pills come from the Figma file "Callbreak Game UI"
(`Vk5MVgtZaL2vXaAona2Tqk`), page **V2 · Design System**. Python 3 with numpy, scipy and Pillow
(as `Tools/AudioGen`).

1. **Sheets.** Run `figma-kit-sheet.js` with the Figma MCP's `use_figma` tool. It rebuilds the
   section "Unity Export" on the Design System page. The section holds every piece at its export
   size, once on pure black and once on pure white, with labels and icons hidden and drop shadows
   removed. Save the JSON it returns as `kit-sheet.json`.
2. **Export.** Export both sheet frames at 2x with the Figma MCP's `download_assets` (png, scale 2).
3. **Cut.** Run:

   ```
   python Tools/ArtPipeline/V2/kit.py --black <black.png> --white <white.png> [--review <folder>]
   ```

   It does three things:
   - Recovers each piece's alpha from the black and white pair.
   - Writes the faces under `Assets/_Project/Art` (the file names the screens already use) and
     renders the drop shadows from the V2 effects.
   - Writes `ArtSource/Kit/V2.json`: pixels per unit 200, 9-slice borders, and each shadow's spread
     and drop.
4. **Import.** In the Editor, run `Callbreak.Editor.KitImporter.Apply()` (menu *Callbreak/Build/Kit
   Import Settings*).
5. **Layouts.** Run `python Tools/ArtPipeline/V2/kit-layouts.py`. It updates every layout node over
   a V2 shadow with that shadow's drop, spread and full opacity, and points the split pieces (box
   buttons, card shadows) at their sprites. It is for the first move to V2: run it on layouts that
   still have the old values.
6. **Rebuild** the screens (`ScreenLayoutBuilder.Build`, `MainMenuBuilder.Build`,
   `DialogBuilder.Build`) and check them with `ScreenCaptureTool`.

`kit.json` lists, for each piece, the files it is written to and its borders (canvas units, left,
top, right, bottom). It also lists the V2 effects each shadow is rendered from.

## Icons, decor and portraits

1. **Sheets.** Run `figma-art-sheet.js` with `use_figma` and save the JSON it returns as
   `art-sheet.json`. It builds section "Unity Export · Art" with:
   - every V2 icon the game uses, the decor art and the six portraits
   - each portrait in the avatar's gold ring, and the avatar's two rings
2. **Export.** Export both frames at 3x.
3. **Fit.** Run:

   ```
   python Tools/ArtPipeline/V2/art.py --black <black.png> --white <white.png>
   ```

   For each file in `art.json`:
   - It keeps the old file's size and import settings, and fits the V2 piece into the old
     picture's ink box. So every screen shows the new art where, and as large as, the old.
   - Icons get the colour V2 draws them in on the screens.
   - Portraits are cut into discs.
   - Their entries go into `ArtSource/Kit/V2.json` next to the kit's.
4. **Import and rebuild** as above. Then also run `SceneSetup.EnsureAvatarLibrary()` (the presets)
   and `SuitSpriteBuilder.Build` (the suits in text use the suit icons' outlines).

## Scenes, tables and shop pictures

V2 draws its three scenes and three tables at 1920 × 1080. The game shows them in a 2560 × 1440
frame, so that 21:9 phones and 4:3 tablets are covered too. The equipped background shows behind the
menus; the equipped table fills the screen at the table.

1. **Sheets.** Run `figma-scenes-sheet.js` with `use_figma` and save the JSON it returns as
   `scenes-sheet.json`. It builds section "Unity Export · Scenes": each scene and table widened to
   2560 × 1440 by rule (see the script's header; only the sky, the ground and the felt are continued),
   in rows for the scenes' backs, the tables, and the scenes' fronts on black and on white.
2. **Export.** Export every picture in the rows at 1x, one at a time (Figma caps an export at 4096 px
   a side), as `back_<i>.png`, `table_<i>.png`, `front_black_<i>.png` and `front_white_<i>.png`.
3. **Cut.** Run:

   ```
   python Tools/ArtPipeline/V2/scenes.py --folder <exports>
   ```

   It lays each front on its back, so each scene is one opaque picture, and writes:
   - `Art/Backgrounds/BG_<Name>.png` and `Art/Tables/Table_<Name>.png` at 2230 × 1255 (the backdrop
     frame);
   - the shop's `Thumb_<Name>.png` and `Preview_<Name>.png`, cut from the middle of the picture as
     V2's shop tiles crop them.
4. **Library.** In the Editor run `TableBackdropBuilder.Build()` (menu *Callbreak/Table/Build Table
   Backdrops*). Then rebuild the screens.

## The menu screens' type and pieces

1. **Read.** Run `figma-screens-dump.js` with `use_figma` on the V2 screen frames, a few at a time,
   and merge the results into `screens-v2.json`: every layer's box, and for text its font, size,
   colour, letter spacing and the box round its rendered glyphs. `icons-v2.json` holds each icon and
   art instance's ink box (the same walk, keeping the ink of `Icon/` and `Art/` instances).
2. **Pieces.** Run `python Tools/ArtPipeline/V2/pieces.py`. It draws the pieces V2 makes of plain
   shapes (the spinner, the coin glow, Day 7's gold frame, the crimson copy button, the plain vertical
   dividers) into their files.
3. **Layouts.** Run `python Tools/ArtPipeline/V2/screens.py` to see what changes, then with `--apply`.
   For each menu layout it gives every label V2's font, size, colour and letter spacing, and puts its
   words where V2 draws them (`size`, `spacing`, `inkBox`; the builder places such a label instead of
   fitting it). It sizes and places every icon on V2's ink, hides the chevrons V2's crimson buttons
   do not have, and adds V2's few new nodes. Only the keys it sets change in each file.
4. **Main menu.** It is built in code: `MainMenuBuilder`'s label and art constants are V2's, from the
   same data.
5. **Import and rebuild** as above (`KitImporter.Apply()` for the new pieces' import settings).

Rebuild the screens only while the Editor is **not** in Play mode. In Play mode a view's `Awake`
runs as its builder adds it, and its runtime state (a hidden canvas, for one) is saved into the
prefab.
