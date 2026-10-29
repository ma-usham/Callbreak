# Tools/ArtPipeline/NewUI

The V1 rebuild's art for the game (ADR 058), from the Figma file "Callbreak Game UI"
(`Vk5MVgtZaL2vXaAona2Tqk`, pages "V1 Rebuild · Design System" and "V1 Rebuild · Screens"). Needs
Python 3 with numpy and Pillow, and Microsoft Edge for the SVGs (`render.py`).

## Scenes and cloths: drawn here

The rebuild's scenes and cloths were drawn as SVG by these scripts and uploaded to Figma, so the
scripts are their source:

| Script | Draws | Figma |
| --- | --- | --- |
| `scenes.py` | the drawing kit (peaks, hills, haze, pagoda, flags, houses, rhododendrons...) and the menu backdrop, the Himalayan Vista | Art/Menu Backdrop 180:384 |
| `shop_scenes.py` | Pokhara Lakeside and Monastery Garden | Art/Scene · Pokhara Lakeside 209:7413, · Monastery Garden 209:7414 |
| `cloths.py` | the Green and Indigo dhaka cloths, their border, Dusk Himalaya | Art/Dhaka Cloth · Green 191:59, · Indigo 191:61, Art/Cloth Overlay 191:626, Art/Dusk Himalaya 191:649 |

`scenes.set_bleed` widens a scene by the backdrop frame's margin. Whatever fills the margin draws
on its own random numbers, so the 1920 × 1080 picture inside stays exactly as Figma shows it.

```bash
python -P Tools/ArtPipeline/NewUI/build.py
```

`build.py` writes:
- `Art/Backgrounds/BG_<Name>.png` and `Art/Tables/Table_<Name>.png`: 2230 × 1255, the frame with its
  margin, with Figma's grain;
- `Art/Tables/Frame_Dhaka.png`: the dhaka border, at twice the canvas size, with a 168-unit border;
- the shop's `Thumb_<Name>.png` and `Preview_<Name>.png`, cropped as Figma's shop tiles show them.

Then run `Callbreak.Editor.TableBackdropBuilder.Build()` in the Editor to refill the library.

## Logos and the Cloths tab icon: cut from Figma

The sheet "Unity Export · NewUI" on the design-system page holds Art/Logo, Art/Logo Compact and the
drawer's folded-cloth icon, each fitted into the ink box of the sprite it replaces. Shoot it with the
Figma MCP's `get_screenshot` on a black and on a white sheet fill, then:

```bash
python -P Tools/ArtPipeline/NewUI/cut_sheet.py <black.png> <white.png>
```

## The kit's plates: cut from Figma

`plates.json` lists the kit sprites the rebuild redrew as vector plates, each with its Figma node and
options. To export them:

1. `plates.py <layout.json>` lays the sheet out.
2. `figma-plates.js`, with the printed cells pasted in, builds "Unity Export · Plates" in Figma
   (through the MCP's `use_figma`).
3. Shoot the sheet on black and on white.
4. `cut_plates.py <layout.json> <black.png> <white.png> [out-dir]` cuts the sprites. Give an
   out-dir to look first.

Each plate fills its sprite's pixel size at the sprite's own scale (pixels per unit / 100), without
its drop shadow. The sprites keep their GUIDs. A "compose" sprite is drawn wide and cut to its two
ends and a plain middle. Its border, and its redrawn shadow's, are written to the `.meta` and to
ArtSource/Kit.
