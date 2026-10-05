# ADR 036 - Forced updates from Google Play

**Status:** accepted (owner, 5 Oct 2026).

**Context.** The owner asked for forced in-app updates: "any new Play version" is installed, so
everyone online runs the same version. Google's Play In-app Updates plugin 1.8.5 (OpenUPM, with
Play Core 1.8.6 and Play Common 1.9.2) provides Play's full-screen immediate update.

## Decision

**When Play is asked:**
- **At launch, first.** `GameEntryPoint` asks behind the curtain before the Menu scene loads, so the
  question comes ahead of the invite link, rejoin, resume and first-run guide (ADR 018).
  - It waits at most 3 s.
  - A later answer is judged against the main menu's rules below.
- **On the main menu.** `ForcedUpdateMoments`, an entry point in the menu scope, asks whenever the
  main menu becomes the top screen again, and when the app comes back to the front on it.
- **Only where it can't hurt:**
  - with the main menu on top and no room open, so never mid-match, in matchmaking or in a room;
  - at most once per 30 s;
  - never while a rejoin into a running match may still come (online, with a live rejoin ticket).

**What happens:**
- **A newer version Play will install now:** Play's IMMEDIATE update starts. An update already in
  progress is resumed.
- **No update, no answer, or one Play won't run now:** the player plays on. "No answer" covers no
  Play Store, a sideloaded APK and offline; "won't run now" covers e.g. low storage.
- **The player backs out of Play's screen:** "Update required" appears (proposed copy: "Update
  required" / "A new version of Callbreak is ready. Update to keep playing." / "Update").
  - It can't be dismissed: taps on the backdrop are ignored and Android back is consumed.
  - Update asks Play again (each answer from Play starts only one update) and starts the update
    again. If nothing is left to install, the player plays on.
  - While it is up, a later moment only resumes an update in progress.

**The shape:**
- **Application/Updates:** the port `IAppUpdates` and the engine-free policy `ForcedUpdate`.
- **Infrastructure/Updates:** `PlayAppUpdates`, in its own asmdef referencing Google.Play.AppUpdate
  and Google.Play.Common. Its source is compiled only for Android and the Editor.
  - It wraps `GetAppUpdateInfo` and `StartUpdate(info, ImmediateAppUpdateOptions())` in Tasks.
  - A check times out after 30 s.
  - It never starts an update with an answer Play wouldn't accept, because plugin 1.8.5 would then
    refuse every later request as "already in progress".
- **The stand-in:** `StandInAppUpdates` serves the Editor, the tests and desktop. It answers at once
  and never has an update; tests can set what it reports.
- **Dialogs:** `DialogRequest.Dismissable` (default true) is new. `DialogService` answers any dialog
  asked while a non-dismissable one is up with Dismissed, at once and unseen.

**Release builds (R8).** Unity applies none of the plugin's own ProGuard rule files.
`Assets/Plugins/Android/PlayAppUpdateKeep.androidlib` brings them as `consumerProguardFiles`: the
plugin's `app_update.txt` and Play Core's `common.txt`, copied verbatim with their sources named. The
Android export includes the module, with minify on for release.

## Consequences

- **A declined rejoin delays the update.** After "Not now" on the rejoin offer, the update waits
  until the ticket runs out (up to 10 minutes) or the next launch. An old version can play online
  meanwhile.
- **Other prompts are swallowed.** While "Update required" is up, every other prompt (invite link,
  rejoin, resume, practice, Exit) is answered Dismissed without being seen.
- **A slow Play at launch.** If Play takes more than 3 s and the player follows an invite link
  meanwhile, the answer is set aside, and Play is asked again at the next main-menu moment.
- **No iOS.** iOS gets no forced update; it would need a server-side minimum version.
- **Plugin upgrades** must refresh the keep rules.
- **Tests:** 41 EditMode and 7 PlayMode. The PlayMode fixture's boot time is unchanged within its
  noise, because the stand-in answers at once.
- **Not verified:**
  - the Play adapter on a device or against Google Play;
  - a minified (R8) build;
  - two plugin behaviours read only from its source: the request reports "cancelled" as soon as the
    app pauses behind Play's screen, and an update in progress still allows IMMEDIATE;
  - an offline player whose Play Store keeps a cached "update available" answer might be held at
    "Update required".
- **Owner:**
  - approve the copy;
  - decide whether Android back on "Update required" should close the game (today it does nothing);
  - test on a phone as in `Docs/Setup/PlayUpdates.md`. Play Console needs no setup for immediate
    updates.
