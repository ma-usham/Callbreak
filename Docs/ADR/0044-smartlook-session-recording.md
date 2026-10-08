# ADR 044 - Smartlook session recording

**Status:** proposed (7 Oct 2026). The owner asked for it ("integrate smartlook in this project as
well"). The privacy choices below are the agent's, for the owner to confirm or change.

**Context.**
- **The dashboard.** The owner's Smartlook organisation (DayZero Games) has one project, "Plummet":
  mobile, Europe region, free plan (3,000 sessions a month), no sessions yet. Its recording settings
  are recording on, uploads on Wi-Fi only, low quality, native rendering, 2 fps. There is no project
  named Callbreak. On 8 Oct 2026 the owner gave a project key, now in `SmartlookProject.ProjectKey`.
- **The SDK.** Smartlook publishes no UPM package for Unity. Its Unity docs
  (mobile.developer.smartlook.com, v1.0 Unity reference, linked from the dashboard) say to import
  its Unity package. The newest is release 1.2 (github.com/smartlook/UnityPackage, 30 Jun 2023). It
  holds:
  - a C# bridge (`SmartlookUnity.Smartlook`);
  - the Android SDK 1.8.7 as an AAR;
  - an iOS framework;
  - a copy of the External Dependency Manager (EDM4U 1.2.175);
  - a settings asset with a Resources loader, an auto-start component and a demo.
- **Masking in Unity.** The docs say Unity can't hide parts of the screen: "selectively hiding
  sensitive parts of the screen is not possible". The only way is to stop capturing the screen with
  the `no_rendering` rendering mode.

## Decision

**What is installed.** Only the Android runtime, under `Assets/Smartlook/SmartlookAnalytics`, with
the package's own paths and GUIDs, so a later import of an official package lands on the same files:
- `Android/app-1.8.7-unity-lite-support.aar` (1.25 MB). It is byte-identical to Smartlook's Maven
  artifact `com.smartlook.recording:app:1.8.7-unity-lite-support` (SHA-1 9aeaaf98…d480).
- `Editor/SmartLookDependencies.xml`: `org.jetbrains.kotlin:kotlin-stdlib-jdk8:1.6.10`, for EDM4U.

**Left out, and why:**
- **The C# bridge.** It can't hide a screen on Android:
  - its `SetRenderingMode` always sends "native" (it compares an `int` with a boxed enum);
  - its `StartFullscreenSensitiveMode` calls a Java method the 1.8.7 SDK no longer has (checked with
    `javap` against the AAR).
  The game calls the SDK class `com.smartlook.sdk.smartlook.Smartlook` over JNI instead, with the
  same calls the docs name: `setup`, `startRecording`, `stopRecording` and `setRenderingMode`
  ("native" / "no_rendering"). So no asmdef changes, and no third-party C# in the game.
- **The settings asset, its Resources loader, the auto-start component and the editor menu.** They
  would be a second place for the key, and a way to record without the consent check.
- **The demo.**
- **The EDM4U copy.** The project already has EDM4U 1.2.187 from OpenUPM.
- **The iOS framework** (29 MB). iOS is not a target.
- **No `Packages/manifest.json` entry**, as there is no UPM package.

**The shape:**
- **Application/Recording** (engine-free):
  - the ports `ISessionRecorder` (the SDK) and `IRecordingConsent` (the player's privacy choice, as
    `ConsentSignals`);
  - `RecordingConsentPolicy`, the consent rule;
  - `SessionRecording : ISessionRecording`, which starts, stops and hides;
  - `StandInSessionRecording`, which records nothing.
- **Infrastructure/Recording:**
  - `SmartlookSessionRecorder`, the JNI adapter;
  - `SmartlookProject.ProjectKey`, the one place for the key, as `AdMobIds` keeps the AdMob ids.
- **Infrastructure/Ads:**
  - `UmpRecordingConsent` reads UMP's `ConsentStatus` and the TCF signals;
  - `AdMobAdPrivacy` gains a `Refreshed` event, raised after every refresh.
- **Bootstrap:** `ProjectLifetimeScope.RegisterSessionRecording`. On Android device builds with a
  key, it registers the Smartlook path and builds it with the scope. Everywhere else it registers the
  stand-in. The log says which, never with the key.
- **Presentation:** `NamePromptPresenter` and `ProfilePresenter` hide the screen while they are up.

### When it records

- Only on Android phones: release and development builds alike. Never in the Editor, the tests, on
  desktop or on iOS. To leave development builds out, register the stand-in when
  `Debug.isDebugBuild`.
- Only with a project key in `SmartlookProject.ProjectKey`. Empty means no recording.
- Only outside the GDPR regions (the owner, 8 Oct 2026: "dont record in europe that requires some
  conditions, record in else regions"). The game asks no question of its own: it reads where the
  player is from Google's consent information for the ads (UMP, ADR 034). Until 8 Oct, a player
  there who consented to TCF purposes 1, 8 and 10 was recorded; now no one there is.

| UMP state | TCF signals | Recording |
|---|---|---|
| Unknown (first launch before the update, offline) | any | no |
| Required (the EEA, the UK and Switzerland; the form not answered yet) | any | no |
| NotRequired (outside the EEA, the UK and Switzerland, as Google decides) | none | yes |
| Obtained, GDPR applies | any answer, "Consent" included | no |
| Obtained, not GDPR (e.g. the US states' message) | none | yes |

- **GDPR applies** while Google says consent is required, when the form wrote
  `IABTCF_gdprApplies = 1`, or, when it never wrote that flag, when it wrote purpose consents (it
  writes them only where the GDPR applies). The rule is in one place: `RecordingConsentPolicy`.
- **When it is read:**
  - the stored choice at launch, so a returning player is recorded from the first screen;
  - again after every refresh of the ads' consent: the update when the menu opens, the consent form,
    and Settings' "Privacy options" form. A withdrawn consent stops the recording at once.

### What is recorded

- **By Smartlook's SDK:**
  - the screen as video, at the dashboard's frame rate (2 fps, low quality today);
  - touches and gestures, the session's timing;
  - the device, OS and app version;
  - a visitor id the SDK keeps on the device.
  - Smartlook also sees the address the upload comes from. Crash reports may come too: the dashboard
    has a Crash Reports page. Not verified: Smartlook's own documentation decides both.
- **By the game itself: nothing.**
  - No custom events, no user id, no user properties, no names, no cards. The port has no call that
    could send any of them.
  - Network capture (the dashboard's DevTools) is not set up.

### Masking

- **Hidden** (`no_rendering`: a blank screen in the replay, touches still recorded):
  - the name question at first launch;
  - the Profile screen, while either is up.
  The fields hold the player's name, including names typed and then changed or rejected.
- **Safeguards:**
  - a recording that starts while a hidden screen is up starts hidden;
  - if the SDK can't hide the screen, the recording stops until the screen has gone.
- **Not hidden:**
  - the player's name on the main menu;
  - every name at the table (own and others');
  - room names on Create Room and in the lobby.
  Hiding them would blank most of the game. The owner can add `HideScreen()` to any other presenter.
- **Cards.** The recording keeps what this phone's screen shows. The table shows only this player's
  hand face up. Other seats are card backs; played cards are public. So no other seat's hand reaches
  a recording. Other seats' cards never reach logs or events either: the game sends no events, and
  its log lines say only "Session recording: on/off (why)".

## Consequences

- **The consent is a proxy.** Google's form asks about ad partners and TCF purposes. It doesn't name
  Smartlook. Outside the GDPR regions there is no opt-in, and Settings shows "Privacy options" only
  where UMP requires it, so most players have no in-game way to opt out.
  - For a stricter rule, the owner can ask for a question of the game's own, or a Settings toggle.
    That is new UI copy, so it is the owner's call.
- **Android manifest.** The AAR's manifest merges into the app:
  - `android:largeHeap="true"` on `<application>`, for the whole game;
  - an exported `INSTALL_REFERRER` receiver and an upload `JobService`;
  - the `INTERNET` and `ACCESS_NETWORK_STATE` permissions (AdMob already brings both).
  Refusing `largeHeap` needs a manifest override (`tools:replace`). That is an Android setting, so it
  was not changed.
- **Gradle.**
  - EDM4U adds `kotlin-stdlib-jdk8:1.6.10` to `Assets/Plugins/Android/mainTemplate.gradle` on its
    next resolve. The line is committed in the template.
  - Should Gradle then report duplicate Kotlin classes against a newer stdlib from AdMob's libraries,
    raise that line's version to the newer stdlib's, or drop it.
- **R8.** The AAR carries its own keep rule (`-keep class com.smartlook.** { *; }`), applied when
  minify is on.
- **An old SDK.** Android SDK 1.8.7 dates from Dec 2021, and the Unity package from Jun 2023.
  Smartlook's newer Android SDK has no Unity package.
- **iOS** would need the framework and an adapter.
- **Tests:** 31 EditMode tests (41 cases) in `Tests/EditMode/Application/Recording`:
  - the consent rule;
  - start, stop and retry;
  - hiding, overlapping handles, and a hide that fails;
  - the stand-in.
- **Verified in the Editor:** `compile-check.js` (the Editor's view and `--player`, an Android
  player build's view) compiles every assembly; the EditMode and PlayMode suites pass.
- **Not verified on a phone:** the JNI calls, the UMP and TCF reads, and an upload to Smartlook.
  EDM's resolve, the Gradle build and the manifest merge were checked on 8 Oct (see the audit).

## Audit (8 Oct 2026)

The owner asked whether Smartlook really works ("I think smart look hasnt yet been implemented
correctly"). An agent checked everything it could without a phone. It found no defect in the code,
so only this ADR changed.

**The SDK is current, and Smartlook's servers accept it.**
- Smartlook's Unity page (mobile.developer.smartlook.com, updated 13 May 2025) still installs
  Android SDK 1.8.7 "unity-lite-support". SDK 2.x has no Unity bridge. Smartlook's release notes
  (2022, with SDK 2.0) say 1.8 stays supported. No end of life is announced.
- A script read the key from `SmartlookProject.cs` and printed nothing of it. It sent the request
  SDK 1.8.7 sends at setup to `manager.smartlook.com/rec/check-recording/mobile`, as SDK version
  `1.8.7-unity-lite`. The answer: recording allowed, uploads to `sdk-writer.eu.smartlook.cloud` (the
  EU region). A made-up key gets "Project not found". The request uploads nothing.
- The project's settings, as the server sent them: 2 fps, 480 px high, native rendering, the app
  may switch the rendering mode, uploads on Wi-Fi only, recordings kept 30 days, no network capture,
  a session ends after 5 minutes away or after 1 hour.

**The calls are right.** Checked with `javap` against the AAR:
- `com.smartlook.sdk.smartlook.Smartlook` has `setup(String)`, `startRecording()` and
  `stopRecording()` (from `SmartlookBase`) and `setRenderingMode(String)` (from
  `SmartlookBridgeBase`). All are static and return nothing. Android finds a superclass's static
  methods, and Smartlook's own bridge relies on that.
- Smartlook's bridge (UnityPackage 1.2, `Smartlook.Android.cs`) makes the same calls on the same
  class, from Unity's thread, with no activity. Inside the SDK, `setupAndStartRecording` is `setup`
  then `startRecording`.
- A setup after launch is supported. The SDK takes the activity from `UnityPlayer.currentActivity`.
  Unity 6 still has that field, and sets it for GameActivity, the game's entry point.
- "native" and "no_rendering" are the SDK's own codes. A mode the app sets wins over the
  dashboard's; only "wireframe" is restricted. The game sets the mode at every start, so the
  dashboard's rendering mode setting does not change this game.
- Region and relay: nothing to set. The server names the upload host.
- Smartlook's session and visitor links contain the project key. The game never asks the SDK for
  them, and must never log them. The SDK's own logging stays off for the same reason: its API log
  line for `setup` prints the key ("setup() called with: smartlookAPIKey = ...").

**The build is right.** The owner's release bundle of 8 Oct, 17:35 (in
`Library/Bee/Android/Prj/IL2CPP/Gradle`) has:
- the SDK's classes and the Kotlin library in its dex files;
- Smartlook's upload job, its referrer receiver, `INTERNET` and `largeHeap` in the merged manifest;
- the project key and the recording code in the IL2CPP metadata (counted, not printed).
- EDM4U resolved `kotlin-stdlib-jdk8:1.6.10` (`ProjectSettings/AndroidResolverDependencies.xml`).
  Minify is off. The AAR has no native libraries, so Android's 16 KB pages don't concern it.

**When it records**, traced from launch:
- A returning player whose choice allows it: from the Boot scene, as the app's scope is built.
- A first launch: UMP's state is unknown, so nothing yet. The menu builds Earn Coins, which starts
  the rewarded ads. Their consent update refreshes the privacy options, and the recording starts
  then: a few seconds after the menu opens outside the GDPR regions (since 8 Oct 2026, never inside them).
- Offline at first launch: the ads try again (5 s, 10 s and so on, up to 2 minutes). The recording
  starts after the first update that works.
- No path waits for an event that never fires. No Editor or debug-build guard stops a phone build.

**Why the dashboard may still be empty:**
- Uploads are Wi-Fi only. A phone on mobile data records, but uploads only once it reaches Wi-Fi.
- Android's job scheduler uploads in the background, so a session shows up some minutes after it.
- On a first launch, the recording starts only once the menu has updated the consent.

**Not verified:** no phone is on this PC's adb. So the SDK on a phone (setup, the capture of Unity's
surface, touches, the upload), the hiding, and the UMP and TCF reads are not verified. The dashboard
was not read: Smartlook was signed out in the shared browser, and agents don't sign in.

**Nothing else is needed.** The SDK takes only the project key. The project id in the dashboard's
links is not used.

**To check on a phone**, on Wi-Fi:
1. Install, open, play a minute, press Home.
2. `adb logcat -s Unity` shows "Session recording: Smartlook, once the player's privacy choice
   allows it.", then "Session recording: on.". A first launch shows "Session recording: off (the
   player's privacy choice is not known yet)." first. "[Smartlook] Recording did not start: ..."
   means the SDK refused. Smartlook's own warnings have tags that start with `Smartlook_`.
3. The session shows in the dashboard a few minutes later.

## Owner

1. **Smartlook.** Done 8 Oct 2026: the owner gave the key. Before that: create a project for
   Callbreak, or confirm that "Plummet" is meant for it. Renaming
   a project keeps its key. Then copy the key and put it in `SmartlookProject.ProjectKey`. It is
   under Project settings, Recording, Project key, Show key.
   - Choose "Upload recordings": Wi-Fi only today. Players on mobile data upload only when they
     reach Wi-Fi. To test from a phone on mobile data, allow mobile data for a while.
2. **Unity.** Done 8 Oct 2026: EDM4U resolved, and the release bundle of 8 Oct has the SDK and the
   key. Install a build and check logcat (see the audit):
   - "Session recording: Smartlook, once the player's privacy choice allows it.", then
     "Session recording: on.";
   - the session in the dashboard after an upload on Wi-Fi.
3. **Play Console, Data safety.** The saved answers declare neither session recording nor app
   interactions. Review at least these:
   - App activity: App interactions (collected; analytics);
   - Device or other IDs (Smartlook's visitor id);
   - App info and performance: Crash logs and Diagnostics, if Smartlook collects them;
   - Personal info: Name. Google counts a nickname, and recordings show the player's name on the
     main menu and at the table;
   - Location: approximate, if Smartlook derives it from the IP address.
   - Smartlook processes the data for the game, so it counts as a service provider.
   - Check with Smartlook that uploads are encrypted in transit.
4. **The privacy policy** must name Smartlook (Cisco). It must say:
   - what is recorded, and why;
   - that data is stored in Smartlook's EU region, and for how long;
   - how to object: Privacy options in the EEA, the UK and Switzerland, and a contact address
     elsewhere;
   - how to ask for deletion.
5. **Decide:**
   - the consent rule and its purposes;
   - development builds;
   - which screens are hidden;
   - `largeHeap`;
   - an in-game opt-out.
