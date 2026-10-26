# Art pipeline

Scripts that turn ChatGPT extractions into game-ready sprites and check the Unity build against
the mockups. They were written for the main menu (screen 00); the same steps apply to every
other screen.

Windows PowerShell 5.1 and Git Bash, no installs. The C# helpers compile on first use.

## The steps for one screen

1. **Crop the attachments.** `crop.ps1` cuts close-ups out of the mockup for ChatGPT. `grid.ps1`
   draws a labelled pixel grid over a region, for reading positions off the mockup.
2. **Extract with ChatGPT.** Prompts and results are logged in `ArtSource/Prompts/`. Originals go
   to `ArtSource/Working/<Screen>/chatgpt/` as `<Asset>_a1.png`, `_a2.png` and so on.
3. **Patch the background.** `patch-menu-plate.ps1` keeps the mockup's own pixels outside the UI
   and ChatGPT's pixels only where UI was (`make-menu-mask.ps1` draws the mask).
4. **Fit.** `fit-menu.ps1` re-composes the mockup from the extractions, fits each part's position
   and size, searches the shadow settings and measures colour drift. It writes `layout.csv`,
   `colors.csv`, `composite.png` and `composite-diff-x3.png`. Read the whole-screen error it
   prints: lower is closer.
5. **Export.** `export-menu.ps1` cleans the alpha, trims, colour-corrects, resizes to @2x and
   writes `ArtSource/Exports`, `Assets/_Project/Art` and `ArtSource/handoff.csv`.
6. **Build and capture in Unity.** `unity-rebuild.sh` recompiles in the open Editor, runs the
   screen builder and captures the prefab at 16:9, 21:9 and 4:3 into `Temp/Captures/`.
7. **Check.** `compare.ps1` stacks a region of the mockup over the same region of the capture.
   `text-check.ps1` measures every label in both and prints what is off.

```powershell
$t = 'Tools\ArtPipeline'; $fit = 'ArtSource\Working\MainMenu\fit'
& "$t\patch-menu-plate.ps1"
& "$t\fit-menu.ps1" -Plate "$fit\BG_Menu_Clean_patched_1672.png" -OutDir $fit
& "$t\export-menu.ps1" -FitDir $fit -Plate "$fit\BG_Menu_Clean_patched_1672.png"
```

```bash
bash Tools/ArtPipeline/unity-rebuild.sh
```

## Other helpers

| Script | Use |
| --- | --- |
| `art.ps1` | Dot-source it to load `ArtTools` and `Scene` (the C# in this folder). |
| `sheet.ps1` | Lays several images side by side for review. |
| `sample.ps1` | Reports ink and background colours in regions of an image. |
| `compile-check.sh` | Compiles `Assets/_Project/Scripts` with Unity's own compiler, so a compile error is caught before the open Editor imports it. |
| `compose-decks.ps1` | Prints the three decks' 156 faces from the vintage pack's elements in each deck's own frame (ADR 050; `ArtSource/Prompts/Decks_Vintage.md`). `-Sample` for a few cards, `-Install` to copy them into `Assets/`. |
| `export-decks.ps1` | Cuts ADR 014's decks from the reference sheets; exports and installs the backs (the faces only with `-InstallFaces`). |

## What to know

- ChatGPT's cut-outs come back with alpha 250–254 inside and a faint haze outside. `Get-Cutout`
  in `art.ps1` fixes both before anything else looks at them.
- Do not pipe these scripts into `Select-Object -First`: it stops the script early and the files
  at the end are never written.
- `fit-menu.ps1` fits by lowering pixel error, so a part sitting on its own large shadow drifts
  towards the shadow. Buttons and discs therefore keep measured positions and only get their
  shadow searched; the logo, icons and fan are fitted.
- The mask, the layout table and the label regions are specific to the main menu. Copy
  `make-menu-mask.ps1`, `fit-menu.ps1`, `export-menu.ps1` and `text-check.ps1` for a new screen
  and replace the measurements.
