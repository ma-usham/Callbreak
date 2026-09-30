# Callbreak: status on 1 Oct 2026

Branch `agent/full-game`, local commits only: nothing is pushed or merged. This page says what
is built, how each part was checked, how close the screens are to the mockups, where the build
departs from the GDD, what was not verified, and what needs the owner's decision. Screenshots of
the playthroughs are in `Playthrough/`, the table simulation's reports in `TableSimulation/`.

## What is built

- **Game rules** (`Core`, engine-free): deal and redeal, calls 1-8, follow suit, must beat,
  must trump, trick winner, scoring in tenths, five rounds, standings with ties. All rule values
  come from `RuleSet`.
- **Bots** (`Bots`, engine-free) in three strengths. Hard samples the hidden hands (Monte Carlo)
  and thinks on the thread pool.
- **Offline matches** against bots. They pause under menus, save after every trick and can be
  resumed after the app is killed.
- **Private rooms** over PurrNet's relay:
  - create with a six-symbol code; join by code, paste or invite link;
  - lobby with ready flags, filling empty seats with bots, removing a player;
  - the host leaving; rejoining within 90 s; a turn clock; bots for players who drop.
- **Quick Match**: this device hosts and fills empty seats with bots after 6 s. There is no
  matchmaker yet (ADR 016).
- **All 17 screens** of the mockups:
  - built from `ArtSource/Screens/*/screen.json` on one shared background;
  - uses a UI kit generated in ChatGPT: panels, decor, icons and controls;
  - includes the table with its hand, trick area and seats, the call panel, the match menu,
    round and match results, and the scorecard.
- **First launch**: How to Play, a practice match, the name nudge, and tips in the first three
  matches.
- **Settings** apply at once and survive an app kill. Also: three card decks, a profile with
  stats, audio (menu and table music, 14 cues), haptics, Reduce motion, safe areas, and layouts
  from 16:9 to 21:9 and 4:3.

## How it was checked

All of the following ran on the final code on the night of 30 Sep to 1 Oct 2026, in this order.

| Check | Command | Result |
| --- | --- | --- |
| Compile, Editor and player | `node Tools/Unity/compile-check.js --quiet [--player]` | both exit 0 |
| Engine-free tests (Core, Bots, Application, Presentation logic) | `dotnet test Tools/DotnetTests/Callbreak.Tests` | 1502 passed, 0 failed |
| Unity EditMode | `Tools/Unity/utest.sh EditMode` | 1501 passed, 0 failed |
| Unity PlayMode (real scenes: menus, friends, rejoin, table, overlays) | `Tools/Unity/utest.sh PlayMode` | 36 passed, 0 failed |
| Every screen rebuilt from its layout, captured, compared | `Tools/Unity/screen.sh <Name>` (MainMenu: `MainMenuBuilder`) | see the table below |
| Scenes rebuilt | `SceneSetup.BuildAll(true)` | identical to the committed scenes |
| Private room over the real relay | `RelayCheck` + `AutoPlayer` in `playcheck.sh` | see below |
| Offline matches through the real UI, over app restarts | `AutoPlayer` + `PlayDriver` in `playcheck.sh` | see below |
| Table simulation (real prefabs and presenters, frame by frame) | `TableSimulation.Run()` and `Run(true)` (Reduce motion) | 384 kinds of check, 100,648 and 97,644 checks, none failed |
| Android export (Gradle project) | `Tools/Unity/android-export.sh` | succeeded, 0 errors, 2 warnings (below); the invite-link filter is in the manifest |

The rebuilt prefabs and scenes are identical to the committed ones except for the object ids
Unity draws on every build, and the PurrTransport component's default room name. The committed
assets are therefore reproducible from `screen.json` and the builders.

**Offline matches through the real UI.** The game ran in the Editor's Play mode, with this
machine's real saves. The local seat was played through the real views: the call chips, a tap on
a legal card, and Next Round. Stopping Play mode stood in for killing the app.
1. A match the saves still held was offered at launch ("Resume your match?"), resumed at its
   round 2 summary, and played to the match results ("4th Place · Next time!").
2. With nothing saved, the main menu showed no Resume banner. Play Offline and Start Game dealt
   a new match. After about two rounds the app was "killed".
3. On relaunch, the offer came up and the match resumed where the save left it. It was played
   through rounds 3-5 to the match results.
4. Play Again dealt a new match at the same difficulty. Back opened the match menu; Leave and
   Confirm returned to the main menu. The menu then showed the Resume banner and, once, the
   name nudge.

Screenshots `Playthrough/01` to `05` are from these runs; `06` and `07` are from the relay run.

No run logged an error from the game. The only console error in each was the Unity CLI's own
request timeout while the Editor entered Play mode.

**Table simulation.** Plays whole offline matches through the real table prefabs and presenters,
frame by frame, and stages what only online play has: turn clocks, reconnects, players going
away, refused moves. It logs one exception on purpose, a staged playback failure, and checks that
the table recovers from it. Every kind of check with its count: `TableSimulation/report.txt` and
`report-reduced.txt`.

**Android export.** Unity exported the three scenes as a Gradle project, with every runtime
script compiled for Android. The exported manifest has the `callbreak://join` filter on the game
activity. Two warnings:
- The Unity CLI's Pipeline package is left out of player builds, as it should be.
- Diagnostics Data is on, but the Debug Symbols setting is too low to resolve crash stack
  traces. That is a Player Settings choice for the owner.

The Gradle build itself, which includes the IL2CPP native compile, was not run.

**Real relay.** The game in the Editor created a private room through its own screens. A second
player in the same process joined it by code over PurrNet's public relay (`RelayCheck`). The
host started with two bots, and the match was played to round 3: the host through the real
table, the guest through its session. Both sides agreed on every report. No seat ever saw
another seat's cards. The run logged no errors from the game; the only console error was the
Unity CLI's own request timeout while the Editor entered Play mode. Protocol version 2.

## Screens against their mockups

Mean absolute error per channel (0-255) of the 16:9 capture against the mockup. The reports and
the side-by-side images are in `ArtSource/Screens/<Name>/check/`.

| Screen | Error | Screen | Error |
| --- | --- | --- | --- |
| 00 Main menu | 7.84 | 09 Round results | 24.76 |
| 01 Play with Friends | 21.69 | 10 Match results | 31.05 |
| 02 Create Room | 7.93 | 11 Settings | 20.64 |
| 03 Join Room | 17.02 | 12 How to Play | 23.79 |
| 04 Private lobby | 21.05 | 13 Match menu | 11.91 |
| 05 Matchmaking | 23.06 | 14 Profile | 23.17 |
| 06 Offline setup | 23.14 | 15 Card collection | 23.97 |
| 07 Call panel | 23.53 | 16 Scorecard | 23.56 |
| 08 Table | 20.54 | | |

Where the error comes from:
- **The shared background.** Most of the error on screens 01-16 is here. Each mockup was painted
  with its own slightly different scenery; the owner asked for one shared background and one UI
  kit (ADR 010), so the scenery can't match every mockup at once.
- **Text.** The mockups' lettering isn't a real font, so edges and weights differ a little,
  most on large headlines such as "You Win!".
- **Avatars.** The mockups show illustrated avatars; the game shows its preset avatars.

Element-by-element errors are in each `capture-report.txt`.

## Where the build departs from the GDD

Recorded in ADRs 010-019 (`Docs/ADR`), in short:
- **010**: one shared background and one 9-slice kit, not a picture per screen.
- **011**: `Task`-based ports.
- **012**: CSV string tables, not the Unity Localization package. Nepali text is not written yet.
- **013**: no `record` types.
- **014**: card faces cut from the reference sheets.
- **015**: local stand-ins for UGS sign-in, cloud saves, analytics and Remote Config.
- **016**: a transport-free protocol over PurrNet's relay, with no NetworkBehaviours, no lobby
  service and no matchmaker. Its addendum lists what the online review closed and what is still
  open.
- **017**: the device's fonts behind the game fonts.
- **018**: the launch order and the rejoin ticket.
- **019**: menu states that no mockup draws (Resume banner, card fan, failures with Retry,
  closable dialogs, UI cues).

Also:
- There is no AudioMixer asset: volumes and ducking are applied by `AudioService`.
- Tunables (Quick Match, protocol, bot timing) are code constants, not ScriptableObjects.
- Saves are split over several files, and there are no migrations yet.
- How to Play reads its copy from the UI table rather than the Rules table (the copy is the same).
- Background parallax (GDD §14) is not built.
- There is no 30 fps battery-saver option.
- Beginner assist exists in Settings data but has no control, because the mockup has no row for it.

## Not verified

- **Anything on a phone or tablet.** Performance, safe areas on a notch, Android back, deep links
  (cold and warm), the share sheet, vibration patterns, the code field with a phone keyboard,
  system-font fallbacks, and file writes on phone storage.
- **Two real devices over the relay.** The relay check used two players in one Editor process,
  so there was no packet loss, no backgrounding and no network change. PurrNet's "not found"
  timing and the relay's answer to the room-name check were not observed.
- **An installable Android build.** Only the Gradle export ran, so no APK or AAB was built and
  nothing ran on a phone.
- **iOS**: no build was made.
- **Audio by ear**: the crossfade, the ducking and the loop seams.
- **Nepali**: no Nepali text exists yet, so Devanagari shaping was not seen in the game.
- The PurrRoomTransport changes of protocol 2 were checked only by the single-process relay run
  above.

## Decisions for the owner

1. **Copy that was invented** (no mockup or GDD wording): the Resume banner, the rejoin dialog
   ("Rejoin your match?" / "Not now"), the practice offer and name nudge, error messages, the
   About text, the host's leave warning. All of it is in `Assets/_Project/Localization/UI.csv`.
2. **Looks that no mockup draws**:
   - the Resume banner;
   - the BOT tag on seats;
   - the call chips' states and the "your turn" prompt;
   - the emote picker;
   - the main-menu fan, which shows the equipped deck's real aces.
3. **Visual items the screen agents judged weaker than the mockup**:
   - the code pill's bevel;
   - the seat shading;
   - the red of the call panel (07);
   - the toggles lack a shadow.
4. **Project settings** changed early on:
   - Run In Background is on (the Editor stands still without it, and phones ignore it);
   - the build scenes are set;
   - VContainer, PurrNet and LitMotion are pinned;
   - PurrNet's settings asset was added.
   The company name and bundle id are still the template's. Debug Symbols should be SymbolTable
   or Full if crash reports are to show stack traces (the Android export warns about it).

   The Android export made Unity save settings of its own. They are left **uncommitted** for
   you, because AGENTS.md keeps these folders yours:
   - `ProjectSettings/ProjectSettings.asset`: portrait autorotation off, the Input System
     actions in Preloaded Assets, Android batching;
   - `ProjectSettings/UnityConnectSettings.asset`: services enabled;
   - the URP assets in `Assets/Settings`: shader prefiltering and volume defaults;
   - a new `ProjectSettings/ApplicationConstants.json`.
5. **Online**:
   - The relay path is unencrypted UDP. Accept it for Phase 1, or add payload encryption.
   - The join limit is client-side.
   - Updates are about 16-24 KB per round against the GDD's 2 KB.
   - Quick Match has no matchmaker.
   - UGS is not linked.
   See the ADR 016 addendum.
6. **Links and ids**:
   - Invite links use `callbreak://join/`. An https link on a domain you own would survive an
     install and be tappable in messengers.
   - Still needed: the privacy policy URL, the credits, and the store ids (for "Update required").
   - Not built: the iOS URL scheme, share sheet and haptics. They need a native plugin.
7. **Rules of the house**:
   - Offline and practice matches count toward profile stats.
   - The avatar "picker" steps through five presets.
   - Beginner assist and a 30 fps option would each need a Settings row.
8. **ChatGPT chats.** The image work is finished. The four chats that made the UI kits (Decor,
   Panels, Icons, Controls; project CardGame) are archived, not deleted: deleting is yours to
   do, in ChatGPT's Settings > Data controls > Archived chats. The earlier per-screen chats were
   already gone.
