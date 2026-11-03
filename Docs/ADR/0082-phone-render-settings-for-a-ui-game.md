# ADR 082 - Phone render settings for a game drawn only in UI

**Status:** accepted (owner, 3 Nov 2026):

> is the shadow is on in my game remove that optamize the whole game.

## Context

- **What draws the game.** Every canvas is a screen-space overlay. There are no lights in Boot,
  Menu or Table, and no sprite, mesh, particle, line or trail renderers in any scene or prefab. Every
  camera has post-processing off. So the camera draws nothing the player sees: it clears the screen,
  and the UI is drawn over it.
- **The phone's render settings** (`Assets/Settings/Mobile_RPAsset.asset`, Android's quality level
  "Mobile") were URP's template defaults:
  - main-light shadows, HDR and a render scale of 0.8;
  - additional per-pixel lights, light cookies, LOD cross-fade, terrain holes and reflection-probe
    blending and box projection;
  - the post-processing data on `Mobile_Renderer`.
- **What that cost.** HDR and the 0.8 render scale make URP render the camera into an intermediate
  texture, then copy it to the screen every frame: a full-screen pass, and a screen-sized texture in
  memory, for a picture of nothing. The shadow and light features drew nothing (no lights), but kept
  their shader variants in the build.
- **Debug code.** `Debug.Log` calls compile into release builds. Since ADR 079 a release build drops
  informational logs at once (`filterLogType = Warning`), before any message or stack trace is made.
  Editor-only code (`#if UNITY_EDITOR`, the Editor folders) and the tests never reach a build.

## Decision

- **On phones** (`Mobile_RPAsset`), via `SerializedObject` in the Editor:
  - Turned off: main-light shadows, HDR, additional lights, light cookies, LOD cross-fade, terrain
    holes, and reflection-probe blending and box projection.
  - Render scale back to 1.
  - `Mobile_Renderer`'s post-processing data unset. Unity upgraded the renderer asset's format on
    save.
- **The PC quality level is unchanged.** The Editor and desktop builds use it, and Android excludes
  it.
- **No UI atlas.**
  - uGUI's sprites are already drawn one texture at a time. An atlas of uncompressed sprites is as
    large as they are, plus its padding.
  - The sprites have different formats (compressed backgrounds; uncompressed rings, cards and
    portraits), and an atlas forces one format. That means a bigger build or a lower quality, which
    ADR 048 already found.
  - Its one gain is fewer draw calls. A card table's draw calls are well within what phones handle,
    so it waits for a profile on a slow phone showing they matter.

## Consequences

- **Every frame on a phone** draws straight to the screen, without the intermediate texture and its
  full-screen copy. That is less GPU work, battery and memory. Not measured on a phone.
- **Nothing visible changes:** the camera draws only the background colour, which the UI covers.
- **Build size is unchanged:** 120.67 MB universal APK, 93.90 MB Play download on 64-bit
  (`render-settings-1103`; it was 120.70 and 93.93 MB). The UI shaders are almost all of the 1.1 MB
  of shaders.
- **Turning things back on** (lights, 3D, post-processing) means turning these back on in
  `Mobile_RPAsset` first.
