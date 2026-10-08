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
- Only while the player's privacy choice allows it. The game asks no question of its own: it reuses
  the answer to Google's consent form for the ads (UMP, ADR 034).

| UMP state | TCF signals | Recording |
|---|---|---|
| Unknown (first launch before the update, offline) | any | no |
| Required (the form is not answered yet) | any | no |
| NotRequired (outside the EEA, the UK and Switzerland, as Google decides) | none | yes |
| Obtained, GDPR applies | purposes 1, 8 and 10 all consented | yes |
| Obtained, GDPR applies | any of them refused ("Do not consent" refuses all) | no |
| Obtained, not GDPR (e.g. the US states' message) | none | yes |

- **Purposes:** 1 "Store and/or access information on a device", 8 "Measure content performance",
  10 "Develop and improve services". They are listed in one place: `RecordingConsentPolicy.Purposes`.
- **GDPR applies** when the form wrote `IABTCF_gdprApplies = 1`. When it never wrote that flag but
  wrote purpose consents, the GDPR is taken to apply.
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
- **Not verified on a phone:** EDM's resolve, the Gradle build, the manifest merge, the JNI calls,
  the UMP and TCF reads, and an upload to Smartlook.

## Owner

1. **Smartlook.** Done 8 Oct 2026: the owner gave the key. Before that: create a project for
   Callbreak, or confirm that "Plummet" is meant for it. Renaming
   a project keeps its key. Then copy the key and put it in `SmartlookProject.ProjectKey`. It is
   under Project settings, Recording, Project key, Show key.
   - Choose "Upload recordings": Wi-Fi only today. Players on mobile data upload only when they
     reach Wi-Fi.
2. **Unity.** Let EDM4U resolve (Assets > External Dependency Manager > Android Resolver > Force
   Resolve), and commit the `mainTemplate.gradle` change. Then build and check logcat:
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
