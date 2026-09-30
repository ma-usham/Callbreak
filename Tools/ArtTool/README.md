# arttool and the screen pipeline

Every Callbreak screen is laid out on its own reference mockup (`Assets/ArtRefrences/game-screens/NN-*.png`,
1672 × 941): every element at the position and size measured on the mockup, and every word re-set
in TextMeshPro at the size, width and position measured there. The art itself is shared wherever
the same element appears on several screens (the owner's rule: reuse, don't re-cut): one menu
background, one set of panels, buttons, header discs, dividers and common icons, 9-sliced where
their size changes. Only what is unique to a screen is cut from its mockup. Screen 01
(`ArtSource/Screens/PlayWithFriends/screen.json`) is the worked example; read it first.

## 0 · The shared kit

| Sprite (`use` path) | What | How to place |
| --- | --- | --- |
| background `"shared"` | `Backgrounds/BG_Menu_Wide.png`: the main menu's scenery with margins for 21:9 and 4:3 | top-level `"background": "shared"` on every menu-style screen (01-06, 09-12, 14-16) |
| `UI/Common/UI_Panel_Large.png` | big ivory panel, textile corners at the top, faint mountains at the bottom corners; 9-slice (corners 160 × 170 top, 160 × 110 bottom) | `rect` = the panel's edge (`fitrect`); `"shadowUse": "UI/Common/UI_Panel_Large_Shadow.png", "shadowSpread": 24, "shadowDrop": 3`; minimum 330 × 290 |
| `UI/Common/UI_Panel_Card.png` | small plain ivory card/tile; 9-slice (corners 26) | `"shadowUse": "UI/Common/UI_Panel_Card_Shadow.png", "shadowSpread": 12, "shadowDrop": 2` |
| `UI/Buttons/UI_ButtonPrimary_Normal.png` | crimson pill with gold rim and woven caps; sliced horizontally, scales to the button's height | `"shadowUse": "UI/Buttons/UI_Shadow_Primary.png", "shadowDrop": 6, "shadowOpacity": 0.45` |
| `UI/Buttons/UI_ButtonSecondary_Normal.png` | ivory pill with brown outline; sliced horizontally | `"shadowUse": "UI/Buttons/UI_Shadow_Secondary.png", "shadowDrop": 2, "shadowOpacity": 0.4` |
| `UI/Buttons/UI_ButtonCircle_Normal.png` | ivory header disc | `"shape": "ellipse"`, `"shadowUse": "UI/Buttons/UI_Shadow_Circle.png", "shadowDrop": 3.5, "shadowOpacity": 0.2` |
| `UI/Avatars/UI_AvatarRing.png` + `UI/Avatars/Avatar_Default.png` | the player's avatar (white ring + picture) | ring: `"shadowUse": "UI/Buttons/UI_Shadow_Circle.png", "shadowDrop": 5, "shadowOpacity": 0.15` |
| `UI/Common/Deco_TitleDivider.png` | thin line with a diamond under titles | `"shape": "ink"` + the mockup rect (lands on this mockup's own divider) |
| `UI/Common/Logo_Callbreak_NoFlag.png`, `UI/Ornaments/Logo_Callbreak.png` | the logo without / with the flag | `rect` = the logo's box on the mockup |
| icons `UI/Common/Icon_{Back,Home,Scorecard,Close,Chevron_Light}.png`, `UI/Icons/Icon_{Settings,Shop,Friends,Offline,Chevron,Spade_Gold}.png` | dark (or white) silhouettes | `"shape": "ink"` (or `"ink": "light"`) + a loose rect around this mockup's icon: the sprite lands exactly on its ink |

A `use` node is still measured on the mockup and still erased from what is behind it; `"opacity"`
dims a shared sprite and `"sliceScale"` overrides the 9-slice scale. If the kit lacks something
several screens need, don't cut private copies: say so in your report. Kit sources are
`ArtSource/Screens/CommonKit*/screen.json` and `Assets/_Project/Scripts/Editor/CommonKit.cs`
(import settings).

```
ArtSource/Screens/<Name>/screen.json   you write this: the screen's elements, back to front
        │  Tools/art job <Name>
        ├─ Assets/_Project/Art/Screens/<Name>/*.png      sprites (+ ArtSource/Exports copy, handoff.csv rows)
        ├─ Assets/_Project/Art/Backgrounds/BG_<Name>.png  plate, 2230 × 1255 (the mockup plus margins for 21:9 and 4:3)
        ├─ ArtSource/Screens/<Name>/layout.json          everything the Unity builder needs
        └─ ArtSource/Screens/<Name>/check/               composite.png, diff-x6.png, report.txt
        │  Tools/Unity/screen.sh <Name> [--compile]
        ├─ Assets/_Project/Prefabs/UI/Screens/<Name>.prefab
        ├─ Temp/Captures/<Name>_{1920x1080,2520x1080,1920x1440}.png
        └─ ArtSource/Screens/<Name>/check/capture-*.png, capture-report.txt
```

All coordinates are **mockup pixels** (top-left origin). `Tools/art` builds the tool on first use.

## 1 · Read the mockup

| Command | What for |
| --- | --- |
| `Tools/art grid --screen 02 --rect x,y,w,h --scale 4 --out f.png` | Zoomed crop with a labelled pixel grid; open it with the Read tool |
| `Tools/art fitrect --screen 02 --rect x,y,w,h` | Exact edges and corner radius of a panel/button from a rough (±6 px) rect |
| `Tools/art profile --screen 02 --row y --from a --to b` | Brightness along a line with the strongest edges to 0.1 px (`--col x` for vertical) |
| `Tools/art inkbox --screen 02 --rect x,y,w,h [--ink light]` | Ink box of the text inside a rect |
| `Tools/art probe --screen 02 --rect x,y,w,h` | Colours in a region (mean, median, darkest, brightest) |
| `Tools/art crop --screen 02 --rect x,y,w,h --scale 3 --out f.png` | Smooth enlarged crop |

Circles: `fitrect` gives the box; use `"shape": "ellipse"` with that rect.

## 2 · Write screen.json

```jsonc
{
  "name": "CreateRoom",                 // = prefab name = view class CreateRoomView = ScreenId
  "mockup": "02-create-room.png",
  "plate": { "generated": "ArtSource/Working/CreateRoom/chatgpt/BG_CreateRoom_Clean_a1.png" },  // optional, see §5
  "nodes": [ /* back to front; a parent comes before its children */ ]
}
```

Node fields:

| Field | Meaning |
| --- | --- |
| `id` | Unique. Also how the view binds it (§4). PascalCase. |
| `kind` | `sprite` (default), `button`, `label`, `group`, or a widget kind your customizer builds (`toggle`, `slider`, `input`, ...). arttool treats every non-label, non-group kind like a sprite. |
| `parent` | Id of the node it sits on. It is erased from its parent's pixels and moves with it. |
| `group` | For roots: `Centre` (default, scales with the design area), `HeaderLeft`, `HeaderRight`, `BottomLeft`, `BottomRight`, `Top`, `Bottom` (pinned to that corner/edge inside the safe area on wider or taller screens). |
| `rect` | `[x, y, w, h]`. For shapes: the exact outer edge (use `fitrect`). For labels and ink icons: a loose box that contains the whole text/icon and nothing else. |
| `shape` | `rect`, `roundrect` (+ `radius`), `pill`, `ellipse`: alpha from the exact shape. `ink`: a flat-coloured icon or line (alpha from how far each pixel is pulled toward the ink colour). `diff`: a multi-coloured object (alpha from its difference to the background; `"solid": true` fills its inside). |
| `out` | Cut this element out of the mockup as `<out>.png`. Its children and labels are erased from it and filled from its own interior. |
| `use` | Draw an existing sprite instead of cutting: a name cut earlier on this screen (`"Icon_Chevron_Light"`) or a path under `Assets/_Project/Art` (`"UI/Avatars/Avatar_Default.png"`, the player's avatar). |
| `make` | Rebuild as a crisp vector: `{ "type": "roundrect", "radius": 12, "fill": "#FFFBF5", "fill2": "#F3E9DC", "stroke": "#C9B49A", "strokeWidth": 1.5, "out": "UI_Field" }` (for controls with states). |
| `scale` | `2` exports at twice the mockup resolution with analytically sharp edges (buttons, discs, icons). Default 1 (big panels). |
| `shadow` | **Only for elements that move or appear/disappear at runtime** (header buttons, which slide to the screen corners on wide phones; seats that fill in; overlays): pad in px; the drop shadow is erased from behind and exported as `<out>_Shadow.png`. Everything else keeps its shadow baked into what is behind it. |
| `grow` | How far beyond the shape the element is erased from its parent (default 1.5, or the shadow pad). |
| `hole` | `false` = do not erase it from the parent (rarely needed). |
| `erase` | Extra `[x,y,w,h]` rects to erase inside this element (e.g. a baked number that becomes dynamic but is drawn by a label elsewhere). |
| `grain` | `false` = fill what is erased from this cut element smoothly, without the grain borrowed from its surroundings. For small flat elements whose label changes at runtime (status pills, number discs): borrowed grain shows as speckles beside a shorter text. Default `true`. |
| `clip` | `ink`/`diff` only: `{ "shape": "roundrect", "rect": [x,y,w,h], "radius": r, "outside": true }` (or an array of them) keeps only the part of the matte inside the shape, or outside it with `"outside": true`: a selection outline without the card it surrounds, a ring without the icon inside it. `"feather": px` softens the clip's edge. |
| `ink` | Labels / ink icons: `dark` (default) or `light` (white text on crimson), or an exact `#RRGGBB`. |
| `text`, `font`, `align`, `tracked`, `style`, `key` | Labels, §3. |
| `hidden` | Build the node inactive (an alternate state the view switches on). |
| `bind` | The view field name (without `_`) when it should differ from `id`. |
| anything else | Passed through to layout.json for your customizer (`on`, `off`, `options`, `note`, ...). |

Rules that keep screens exact:

- **Everything that changes at runtime is its own node**: every word (so it can be localised and
  re-set), numbers, avatars, anything with states (toggles, selected outlines, radio dots,
  ready dots), anything that appears or disappears. Static decoration inside a panel or button
  (woven button ends, chevrons, corner ornaments, divider lines) can simply stay baked into the
  parent - don't list it, and nothing is erased there.
- Text over busy art (a woven pattern, scenery) erases badly; keep label rects tight and don't
  erase static icons that sit on patterns.
- Reuse the player's avatar sprites (`UI/Avatars/UI_AvatarRing.png` + `UI/Avatars/Avatar_Default.png`)
  for avatar slots, since players pick their avatar. Cut everything else from this screen's mockup.
- Check `ArtSource/Screens/<Name>/check/report.txt` after every `job`: the reconstruction error
  (layers re-composed over the plate vs the mockup, text excluded) should be below ~1.0 overall,
  and look at `check/diff-x6.png` for bright patches.

## 3 · Labels

`{ "id": "Title", "kind": "label", "text": "Your table", "font": "SerifBold", "rect": [700, 238, 275, 72], "key": "ui.create_room.title" }`

- `font`: `SerifBold` / `SerifSemiBold` (Source Serif 4: titles, headings, big button labels),
  `SansRegular` / `SansMedium` / `SansSemiBold` / `SansBold` (Inter: everything else). Pick by eye
  from the stroke weight; the size, width, position and colour are measured automatically.
- The builder fits each label so its rendered ink box equals the mockup's (height → size, width →
  horizontal scale), and uses the measured colour, including the vertical gradient of the crimson
  titles. `"tracked": true` for letter-spaced capitals (subtitles like `SET UP A TABLE FOR YOUR
  FRIENDS`): width comes from spacing instead of squeezing.
- `align`: which edge stays put when the text changes at runtime: `left` (names), `centre`
  (default), `right` (numbers in a right-aligned column).
- `style`: `buttonLabel` adds the soft dark drop shadow under white serif text on crimson buttons.
- `key`: string-table key, `ui.<screen>.<name>` (e.g. `ui.create_room.cta`). Omit for dynamic text
  (player names, codes, scores).
- One label per line; multi-line copy is several labels.

## 4 · The view and widgets

`Assets/_Project/Scripts/Presentation/Screens/<Name>/<Name>View.cs`: `public sealed class <Name>View : UIScreen`
with `[SerializeField] private` fields, events for every intent, and setters for everything the
presenter renders (see `PlayWithFriendsView`). No logic, no services.

The builder adds the view and binds fields by name: `_createButton` ← node `CreateButton`
(or the node whose `bind` is `CreateButton`); arrays `_seatName` ← nodes `SeatName0`, `SeatName1`, ...
Type decides what is bound: `Button`, `TMP_Text`, `Image` (a sprite node's face), `GameObject`,
`RectTransform`, or any component on that node.

Widgets (`Assets/_Project/Scripts/Presentation/Widgets`): `ToggleSwitch` (on/off images),
`SelectableGroup` (segmented controls, option cards, radio lists), `Spinner`. A screen whose
widgets need assembling gets `Assets/_Project/Scripts/Editor/Screens/<Name>Customizer.cs`:

```csharp
namespace Callbreak.Editor.Screens
{
    public static class SettingsCustomizer
    {
        public static void Customize(ScreenBuildContext ctx)
        {
            // ctx.Nodes["MusicToggle"] is the built node; add components, wire serialized fields,
            // and put anything the view needs into ctx.Bound["_musicToggle"].
        }
    }
}
```

## 5 · Plates

The plate is the mockup with every UI element (and every separately exported shadow) removed. By
default the removed areas are filled from their surroundings, which is right for parchment and
haze and reads as soft blur over detailed scenery. When a ChatGPT clean plate exists
(`ArtSource/Working/<Name>/chatgpt/BG_<Name>_Clean_aN.png`, see `ArtSource/Prompts/<Name>_Plate.md`
for the accepted one), set `"plate": { "generated": "<that path>" }`: its pixels are used only
inside the removed areas, colour-matched to the mockup. `"generatedWide"` does the same for the
21:9/4:3 margins (a 2230 × 1255 image); without it the margins mirror and blur the plate's edges.

## 6 · Build, capture, compare

```bash
node Tools/Unity/compile-check.js          # every assembly compiles? (run before --compile)
Tools/Unity/screen.sh <Name> --compile     # recompile, build prefab, capture, compare
Tools/Unity/screen.sh <Name>               # same without recompiling (layout-only changes)
```

`screen.sh` holds a lock on the shared Unity Editor (other people use it too), so it may wait.
Never leave `.cs` files that don't compile in `Assets/`: a compile error blocks everyone's builds.
Read `check/capture-report.txt`: every label's `dx dy` should be within ±1 px and `w`/`h` within
~1 px of the mockup. Then look at `check/capture-split.png` (left half mockup, right half Unity)
and `capture-split-v.png` (top/bottom): seams show anything misplaced. Also look at
`Temp/Captures/<Name>_2520x1080.png` and `_1920x1440.png` for the wide and tablet layouts.
