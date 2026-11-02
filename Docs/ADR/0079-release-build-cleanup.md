# ADR 079 - Cleanup before the release APK; quieter release logs

**Status:** accepted (owner, 3 Nov 2026):

> now i want to build the apk and test it on andorid. before that. refactor the project clean the code
> and remove unwanted codes andbebugs and images from the project try to optamize the game size. the
> real ads should deliver check everything and inform me.

## Context

- **Audits.** Two audits read the project:
  - **Assets:** every asset's GUID was followed from the three build scenes, ProjectSettings,
    `Resources/` and `StreamingAssets/`, and every asset path was searched in C# and Tools.
  - **Code:**
    - a compile of the ten runtime assemblies, as the Editor and as the Android player;
    - every log call in runtime code;
    - members, types and localization keys with no reference anywhere.
- **What the audits found:**
  - The build had no compiler warnings, no TODOs and no commented-out code.
  - 42 sprites were referenced by nothing that ships or builds. They were mostly old per-screen
    sprites the shared kit replaced.
  - 13 members and 21 UI keys were unused.
  - About 100 informational log lines ran in release builds: ads loading, the store, updates and
    analytics.
- **Size.** A release export of today's main (`Tools/Unity/android-size.sh before-cleanup-1103`):
  - the universal APK is 138.4 MB, of which:
    - 47.3 MiB is game data (177 MiB raw);
    - 42.7 MiB is ARM64 native code;
    - 32.3 MiB is ARMv7 native code;
    - 8.4 MiB is Java code.
  - The Play download is 102.5 MB on an ARM64 phone and 91.3 MB on a 32-bit one.
  - Textures are 95% of the game data. The 159 card pictures alone are about 91 MB raw, uncompressed
    by the owner's choice (ADR 048).
  - Unity packs only what the scenes reach, so the unused sprites were never in the build.

## Decision

- **Unused art removed:**
  - 36 sprites: the old per-screen icons, plates and Card Collection shadows, and five kit icons;
  - their entries in `ArtSource/Kit/{Icons,Shop,PlayOnline}.json`;
  - the emptied CardCollection and Profile folders.
  - The shop's old background thumbnails and cloth previews stay: the shop builder still reads their
    layout. The 85 MB `ArtRefrences` folder also stays; it is the owner's, and art scripts read it.
- **Unused code and copy removed:**
  - 10 getters no code, prefab or test reads;
  - 21 UI keys no code, prefab or Remote Config reads: the old shop and Card Collection titles, the
    table's Trump, Scores and Settings words, the old invite text, the matchmaking BOT tag, a room
    name error and the menu's Resume match. No copy that shows changed.
- **Release builds log warnings, errors and exceptions only** (`ProjectLifetimeScope.Awake`:
  `Debug.unityLogger.filterLogType = LogType.Warning` when `!Debug.isDebugBuild`).
  - The Editor and development builds log as before.
  - Logcat keeps every failure, as there is no crash reporter.
- **The keyboard height's warning is told once** (`OnScreenKeyboard`): it was read every frame while a
  text field had focus.

## Consequences

- The game plays as before. The APK's size is unchanged: none of the removed art was in the build.
- Tests:
  - EditMode passed 2589/2589 and the .NET suite 2448/2448.
  - PlayMode was not run. Its one change is the release log filter, which the Editor never
    applies.
- Left for the owner (size and settings they decide):
  - ARMv7 (32-bit) support costs 32 MiB in a universal APK; Play sends 64-bit phones only the ARM64
    code.
  - The uncompressed card pictures and Durbar Square background (ADR 048, 058).
  - IL2CPP's code generation and stripping settings.
  - The Rules.csv strings that nothing reads.
  - The retired deck-base path of ADR 054.
  - The unused hand-sort setting.
