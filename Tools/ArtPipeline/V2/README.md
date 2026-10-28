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
