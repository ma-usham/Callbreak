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

Rebuild the screens only while the Editor is **not** in Play mode. In Play mode a view's `Awake`
runs as its builder adds it, and its runtime state (a hidden canvas, for one) is saved into the
prefab.
