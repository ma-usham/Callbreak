# ADR 081 - Smaller compiled code; the old art references and pipeline removed

**Status:** accepted (owner, 3 Nov 2026):

> why dont you create atlas or like pack it do something dont reduce the game quality but optamize
> the game size and built. remove old UI and art ref and everything we dont use in game infact remove
> everythign you dont need and hasn't being used in game.

## Context

- **Atlases were tried, and reverted.** ADR 048 packed the card decks and portraits into atlases. The
  atlases themselves save nothing on uncompressed pictures: a 2048 page of RGBA32 costs what its
  sprites cost, plus padding. The saving came from the ASTC 4×4 compression that atlases allow for
  mip-mapped cards (about 20 MB of the APK). The owner found the game looked worse and had it all
  reverted (ADR 048 addendum, 27 Oct 2026). So atlases are not a way to save space without touching
  the pictures.
- **Where the size is** (release export of main after ADR 080, `before-cleanup-1103`, universal APK
  138.4 MB):
  - **Compiled game code (`libil2cpp.so`):**
    - 64-bit: 93.7 MB raw, 23.6 MiB in the APK;
    - 32-bit: 76.1 MB raw, 22.6 MiB in the APK.
    - IL2CPP compiled the C# for speed (`Il2CppCodeGeneration.OptimizeSpeed`, the default), with
      minimal managed stripping.
  - **The Unity engine:** 20.6 MB raw on 64-bit.
  - **EOS:** 23.4 MB raw on 64-bit.
  - **Game data:** 47.3 MiB, 95% of it textures.
- **Unused files.** The project carried files nothing in the game or its builders reads:
  - `Assets/ArtRefrences`: 85 MB of V1 mockups and reference sheets, which Unity still imported.
  - 433 MB of verification captures in `ArtSource/Screens/*/check`.
  - ChatGPT working files (`ArtSource/Working`, 42 MB) and prompts (`ArtSource/Prompts`).
  - The layout of the Card Collection screen, which is no longer built.
  - The shop's old background thumbnails and cloth previews (ADR 064).
  - The V1 kit scripts that read them.

## Decision

- **Android's IL2CPP compiles for size** (`PlayerSettings.SetIl2CppCodeGeneration(Android,
  OptimizeSize)`; ProjectSettings `il2cppCodeGeneration: Android: 1`).
  - IL2CPP shares more generic code instead of a copy per type. Nothing the player sees changes.
  - The game's CPU work is light (a card game), so the slightly slower generic code costs nothing
    visible.
  - Managed stripping stays Minimal: a higher level can strip code that the plugins reach only by
    reflection (AdMob, Firebase, IAP, VContainer), which only shows on a phone.
- **Removed (about 560 MB, 2,340 files; none in the build):**
  - `Assets/ArtRefrences` (85 MB);
  - every `ArtSource/Screens/*/check*` capture. `screen.sh` still writes them, now git-ignored;
  - `ArtSource/Working`, `ArtSource/Prompts` and `ArtSource/Screens/CardCollection`;
  - `Art/Shop/Backgrounds/Thumb_*` and `Art/Shop/Tables/Preview_*`, and the step of
    `NewUI/build.py` that drew them;
  - the V1 kit scripts that read the removed files: `Tools/ArtPipeline/Kit/`, `fit-menu.ps1`,
    `patch-menu-plate.ps1`, `text-check.ps1` and `export-decks.ps1`. `Tools/ArtPipeline/README.md`
    says so.
- **Kept, as the game's builders and current art scripts use them:**
  - the screens' `layout.json` and `screen.json`;
  - `ArtSource/Kit` (import settings);
  - `ArtSource/Audio` (`AudioGen`, the sound sources);
  - `ArtSource/Cards` and `compose-decks.ps1` (the vintage deck, ADR 050);
  - `ArtSource/Exports`;
  - the six shared-kit sprites that `cut_plates.py` still writes.

## Consequences

- **Release export after this ADR** (`after-cleanup-1103`):

  | | Before | After |
  |---|---|---|
  | Universal APK | 138.4 MB | **120.7 MB** |
  | Play download, 64-bit phone | 102.5 MB | **93.9 MB** |
  | Play download, 32-bit phone | 91.3 MB | **83.4 MB** |
  | `libil2cpp.so`, 64-bit | 93.7 MB raw | **50.6 MB raw** |

  Game data and every picture are unchanged.
- The repository is about 560 MB lighter, and Unity no longer imports the 85 MB of references.
- Not verified on a phone: the size-optimised code. It changes how generics are compiled, not what
  the code does, but the owner's Android test is the check.
- **Larger cuts are the owner's call:**
  - **Leave out 32-bit phones** (ARMv7): 24 MiB less in a universal APK, no change for 64-bit phones
    on Play. Older 32-bit phones could no longer install the game.
  - **Higher managed stripping**, with a `link.xml` that keeps what the plugins reach by reflection:
    more code gone, to be tested on a phone.
  - **Play Asset Delivery for the shop's decks and themes:** they would download after install or
    when bought, at full quality. This needs new packages and an offline design.
  - **Compressed cards (ADR 048):** reverted for quality.
