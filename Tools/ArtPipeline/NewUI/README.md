# Tools/ArtPipeline/NewUI

The V1 rebuild's art for the game (ADR 058), from the Figma file "Callbreak Game UI"
(`Vk5MVgtZaL2vXaAona2Tqk`, pages "V1 Rebuild · Design System" and "V1 Rebuild · Screens"). Needs
Python 3 with numpy and Pillow, and Microsoft Edge for the SVGs (`render.py`).

## Scenes and cloths: drawn here

The rebuild's scenes and cloths were drawn as SVG by these scripts and uploaded to Figma, so the
scripts are their source:

| Script | Draws | Figma |
| --- | --- | --- |
| `scenes.py` | the drawing kit (peaks, hills, haze, pagoda, flags, houses, rhododendrons...) and the menu backdrop, the Himalayan Vista (no longer in the game) | Art/Menu Backdrop 180:384 |
| `shop_scenes.py` | Durbar Square (the default background since 30 Oct 2026), Pokhara Lakeside and Monastery Garden | Art/Menu Option · Durbar Square 240:103587, Art/Scene · Pokhara Lakeside 209:7413, · Monastery Garden 209:7414 |
| `cloths.py` | the Green and Indigo dhaka cloths, their border, Dusk Himalaya | Art/Dhaka Cloth · Green 191:59, · Indigo 191:61, Art/Cloth Overlay 191:626, Art/Dusk Himalaya 191:649 |
| `themes.py` | the shop's seven themes, each a background, a cloth and its border (see below) | page "V1 Rebuild · Themes" 268:2, Theme Art 269:3 |

`scenes.set_bleed` widens a scene by the backdrop frame's margin. Whatever fills the margin draws
on its own random numbers, so the 1920 × 1080 picture inside stays exactly as Figma shows it.

```bash
python -P Tools/ArtPipeline/NewUI/build.py
```

Names after a work dir rebuild only those pictures, e.g. `build.py <work-dir> DurbarSquare`; `Frame`
rebuilds the dhaka border, and a theme's name its background, cloth and border.

`build.py` writes:
- `Art/Backgrounds/BG_<Name>.png` and `Art/Tables/Table_<Name>.png`: 2230 × 1255, the frame with its
  margin, with Figma's grain;
- `Art/Tables/Frame_<Name>.png`: a cloth's border, 768 × 768 (twice the canvas size) with a 168-unit
  border: `Frame_Dhaka` for both dhaka cloths, and one per theme;
- the shop's `Thumb_<Name>.png` and `Preview_<Name>.png`, cropped as Figma's shop tiles show them (the
  first three backgrounds and cloths only);
- `Art/Shop/Themes/Tile_<Theme>.png`, the theme tiles (`tiles.py`, below).

Each picture keeps its grain seed (58 + its place in `build.py`'s list), so the themes come after the
first six: rebuilding any of them leaves the others' files as they were.

Then run `Callbreak.Editor.TableBackdropBuilder.Build()` in the Editor to refill the library.

## The shop's themes: drawn here

`themes.py` draws the seven themes the owner was shown on 30 Oct 2026, each a menu background and a
matching cloth with its border. Key (the catalogue id) and art name:

| Id | Name | Background | Cloth and border |
| --- | --- | --- | --- |
| `himalayan-sunrise` | HimalayanSunrise | peaks over a sea of cloud at dawn, a lodge and its flags | slate blue; a stitched range of peaks, a gold line, rising suns |
| `laligurans-forest` | LaliguransForest | rhododendron trees in bloom, a misty forest, a path | deep teal; a stem with blossoms and leaf pairs, blossom clusters |
| `golden-terraces` | GoldenTerraces | rice terraces at harvest, a peepal over a chautara | olive; rice ears between dashed terrace lines, sheaves |
| `tihar-lights` | TiharLights | Tihar night: a carved window, marigolds, diyo, a rangoli | plum; a marigold garland knotted in red, rangoli quarter-flowers |
| `prayer-flag-pass` | PrayerFlagPass | flags from a cairn, snow peaks, yaks | charcoal; the five flag colours as stripes, diamonds, interlaced squares |
| `mithila-garden` | MithilaGarden | Mithila painting: peacock, sun, tree of life, fish | rust; a triangle band between cream lines, a dotted line, fish |
| `phewa-moonlight` | PhewaMoonlight | a full moon over Machhapuchhre and the lake | midnight navy, stars thick at the edges; a double silver line, a sprinkle, crescents |

```bash
python -P Tools/ArtPipeline/NewUI/themes.py <out-dir> [bleed-x bleed-y]   # the SVGs alone
python -P Tools/ArtPipeline/NewUI/build.py <work-dir> HimalayanSunrise ...  # the game's pictures
```

- The backgrounds are Figma's (the notes' `themes.py`, 1920 × 1080) with the backdrop frame's bleed:
  skies, haze, hills, the lake and the floors run on over the margin, hills ending at the screen's foot
  bend on down off the frame, and the margin gets its own peaks, firs, stars, ripples and flag poles on
  `scenes.EXT`. Without bleed each draws Figma's picture exactly, except Mithila Garden, which has no
  painted border: drawn round the 1920 × 1080 screen it would sit inside the picture on 21:9 and 4:3, so
  the paper runs on to every edge and the tree's trunk goes down off the frame.
- A cloth is two pictures, as the dhaka cloths are: `Table_<Name>.png`, the base (the radial colour, a
  faint twill, a warm light, the vignette round the 1920 × 1080 picture and darker beyond it; a plain
  centre), and `Frame_<Name>.png`, its border, which the game lays round the screen.
- A border is a 384-unit square drawn at 2x (768 px), its 168-unit corners fixed and the 48 units between
  them repeated along each edge (`TableBackdropBuilder` makes it a 9-slice with a 336 px border, the
  table's ClothFrame Image is Tiled). Unity tiles an edge from its left (or bottom) corner and cuts the
  last repeat short: the sides of every screen of 16:9 or wider (1080 tall) end on half a repeat, and
  21:9's top and bottom do too. So every edge pattern repeats every 48 units and is whole at each
  multiple of 24 from the corner: motifs sit inside 24-unit cells, lines are straight or cross each cell's
  ends at the same height (the peaks' slopes, the garland's swags). A half repeat then only makes one peak
  or swag short, or doubles or drops one motif by a corner; other remainders (a 19.5:9 phone's top edge)
  cut a motif, as with any repeat.

## The theme tiles

`tiles.py` builds the shop's theme tiles, `Art/Shop/Themes/Tile_<Theme>.png`, from the pictures above
(`build.py` runs it for what it rebuilt):

```bash
python -P Tools/ArtPipeline/NewUI/tiles.py [Theme ...]
```

Each is 492 × 444 with corners rounded 21 px round (transparent outside): the background's 1920 × 1080
picture 492 wide on top, a gold seam (#D2A957) from 276 to 283.5 px down, and under it the cloth's
top-left corner as the game shows it round a 1920 × 1080 screen (base and tiled border, 0.45 px a canvas
unit: units 0..1093 across, 0..357 down). The themes are the first three backgrounds with their cloths
(Durbar Square with Green Dhaka, Pokhara Lakeside with Indigo Dhaka, both with `Frame_Dhaka`; Monastery
Garden with Dusk Himalaya, no border) and the seven above. `tiles.cloth_on_screen` composes a cloth on any
screen size the way the game does, for a look at other aspect ratios.

## The Dhaka Ring: drawn here

`dhaka_ring.py` draws the Dhaka Ring (Figma "Reconnecting A · Dhaka Ring" 238:3 on "V1 Rebuild ·
Options") from Figma's colours, sizes and effects, for the table's "Reconnecting…" cover:
`Art/UI/Table/UI_Reconnecting_Ring.png` (the ring that turns) and `UI_Reconnecting_Seal.png` (the seal
that stays), two pixels to a canvas unit. Their translucent pixels are set for linear-light blending over
the dimmed cloth. (The loading screen had it too until 31 Oct 2026; it has the Gold Arc now, below.)

```bash
python -P Tools/ArtPipeline/NewUI/dhaka_ring.py [work-dir [Reconnecting]]
```

Then run `Callbreak.Editor.Screens.TableCustomizer.UseDhakaRing()` in the Editor.

## The Gold Arc: drawn here

`gold_arc.py` draws the Gold Arc (Figma "Reconnecting C · Gold Arc" 238:56383; the owner, 31 Oct 2026:
"use golder arc for every loading screen") for the loading screen: a faint cream track with the gold arc
on it and its glow (the ring that turns), and the logo's crimson seal in its gold rim with the cream spade
(the seal that stays). It uses the Dhaka Ring's scale and sprite sizes, three quarters of the cover's, so
the two stand as Figma draws them side by side:
- the curtain: `Art/UI/Decor/Deco_LoadingRing.png` and `Deco_LoadingSeal.png` (the files the Dhaka Ring
  had there), set for linear-light blending over Durbar Square's light middle;
- its black look over the store's sheet (ADR 042): `Deco_LoadingRing_Dark.png` and
  `Deco_LoadingSeal_Dark.png`, set for black.

```bash
python -P Tools/ArtPipeline/NewUI/gold_arc.py [work-dir [Loading|Dark]]
```

Then run `Callbreak.Editor.Screens.SceneLoadingCustomizer.UseGoldArc()` in the Editor.

## The icons and the avatars: drawn here (ADR 065)

`icons.py` draws every icon the game shows as vector art in the rebuild's palette (ink #131211,
crimson, browns, ivory, the logo's gold), keeping each V1 icon's meaning and silhouette. Each is
fitted into the box the old icon's ink filled, at the sprite's own size, so the file, its GUID and
every screen stay as they were. `avatars.py` draws the five player portraits (512 px) and their
framed copies for the name prompt (384 px) the same way.

```bash
python -P Tools/ArtPipeline/NewUI/icons.py <work-dir> [--write] [Icon_Name ...]
python -P Tools/ArtPipeline/NewUI/avatars.py <work-dir>
```

Without `--write`, `icons.py` only draws the work dir's sheets: `compare.png` (old beside new, on the
menu's ivory and the table's green) and `svg/` (each design as an SVG, as uploaded to Figma's
"V1 Rebuild · Icons & Avatars" page).

## Logos and the Cloths tab icon: cut from Figma

The sheet "Unity Export · NewUI" on the design-system page holds Art/Logo · Rozha Royal and Art/Logo
Compact · Rozha Royal (the owner's pick, 31 Oct 2026, ADR 066), each filling the width of the sprite it
replaces, centred, and the drawer's folded-cloth icon, fitted into the ink box of its sprite. Shoot it
with the Figma MCP's `get_screenshot` (maxDimension 1602, contentsOnly) on a black and on a white
sheet fill, then:

```bash
python -P Tools/ArtPipeline/NewUI/cut_sheet.py <black.png> <white.png> [out-dir]
```

Give an out-dir to look first. Durbar Square's birds (`shop_scenes.durbar`) fly right of the main
menu's logo: move them too if the logo grows.

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
