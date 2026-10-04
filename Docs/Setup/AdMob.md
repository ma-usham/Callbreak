# AdMob setup (Earn Coins ads)

The Earn Coins page pays its coins for Google AdMob rewarded ads on phones. They come through the
Google Mobile Ads Unity plugin (`com.google.ads.mobile` 11.5.0, from OpenUPM) and the External
Dependency Manager it brings (`com.google.external-dependency-manager` 1.2.187, EDM4U). In the
Editor, in the tests and in desktop builds a stand-in completes every ad (`StandInRewardedAds`), so
no ad network is touched there.

## Identifiers (not secret: they ship inside the app)

| What | Value |
| --- | --- |
| AdMob app (Android) | `ca-app-pub-1025159441369051~3176202879` |
| Rewarded ad unit, Earn Coins (Android) | `ca-app-pub-1025159441369051/8532471334` |
| Banner `.../6484892497`, interstitial `.../4319920270` | not used anywhere |

They live in code, in `Assets/_Project/Scripts/Infrastructure/Ads/AdMobIds.cs`. Before every Android
build, `Callbreak.Editor.AdMobSetup` writes the app id into the plugin's settings,
`Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`, and the plugin then writes it into
the Android manifest. **Callbreak > Ads > Apply AdMob Settings** does the same by hand. (The
plugin's own **Assets > Google Mobile Ads > Settings...** window shows the same asset.)

## Test ads and real ads

- **Development builds** (Development Build ticked; `Debug.isDebugBuild`) always use Google's test
  rewarded unit, `ca-app-pub-3940256099942544/5224354917`. Those ads say "Test Ad", work on any phone
  and need no AdMob review.
- **Release builds** use the real unit. Never tap your own live ads: AdMob can suspend the account.
  To try a release build on a phone, make it a test device first (below).

## How the app asks for consent

At every start, before any ad is requested, the app asks Google's User Messaging Platform (UMP) for
the player's consent status, and shows Google's consent form when the law asks for it (the messages
set up in AdMob's Privacy & messaging). The SDK starts, and ads are requested, only while UMP says
ads may be requested. A player whose consent from an earlier session still holds gets ads at once
while it is checked again. If the form can't load (no network), the app tries again after 5 s, 10 s,
20 s ... at most every two minutes.

## What the owner does

1. **Test devices.** AdMob > Settings > Test devices > Add test device: Android, with the phone's
   advertising ID (on the phone: Settings > Google > All services > Ads). Do this for every phone
   that runs release builds.
2. **Privacy & messaging.** In AdMob > Privacy & messaging, create and publish a **European
   regulations** (GDPR) message for the Callbreak app; add a **US state regulations** message if you
   want one. Without a published GDPR message, players in the EEA, the UK and Switzerland see no
   form, and AdMob limits their ads.
3. **A privacy options entry point (your decision).** Where a consent form applies, Google's policy
   asks that players can change their choice later. The plugin has the call
   (`ConsentForm.ShowPrivacyOptionsForm`, needed when `ConsentInformation.PrivacyOptionsRequirementStatus`
   is `Required`); the game has no button for it yet. It needs a button, its copy and its place, for
   example in Settings.
4. **Link the Play listing.** Once the game is on Google Play: AdMob > Apps > Callbreak > App
   settings > link the app to its store listing. AdMob then reviews the app; until then it shows
   "Requires review, limited ad serving", and real ads are few or none (test ads are not affected).
5. **app-ads.txt.** The Play listing needs a developer website. Put a file at
   `https://<that domain>/app-ads.txt` with this line:

   ```text
   google.com, pub-1025159441369051, DIRECT, f08c47fec0942fa0
   ```

6. **Play Console.**
   - App content > Ads: "Yes, my app contains ads".
   - App content > Advertising ID: the app uses it, for advertising (the SDK adds the
     `com.google.android.gms.permission.AD_ID` permission).
   - Data safety: declare the data the Google Mobile Ads SDK collects and shares (see Google's
     guide "Prepare for Google Play's Data safety section" for AdMob: device and other identifiers,
     approximate location, app interactions, diagnostics).
   - Target audience: if it includes children, the Families policy applies and ad requests must be
     tagged; today nothing is tagged.
7. **iOS (later).** There is no iOS AdMob app yet. With the plugin installed, an iOS build stops in
   the plugin's build step ("iOS Google Mobile Ads app ID is empty") until an iOS app id is entered
   in the plugin's settings, and a release iOS build shows no ads until `AdMobIds` has an iOS unit.
   Development iOS builds would use Google's iOS test unit.

## The Android build

- **Libraries.** EDM4U writes AdMob's Android libraries (play-services-ads 25.4.0, user-messaging-
  platform 4.0.0, and three AndroidX libraries) into `Assets/Plugins/Android/mainTemplate.gradle`,
  between `// Android Resolver Dependencies Start` and `End`. That file is Unity 6000.3.18f1's own
  template, switched on in Player Settings > Publishing Settings > **Custom Main Gradle Template**.
  The plugin re-runs the resolver before every Android build; by hand it is **Assets > External
  Dependency Manager > Android Resolver > Force Resolve**.
- **EDM4U settings** (`ProjectSettings/GvhProjectSettings.xml`): it patches only
  `mainTemplate.gradle` (no settings template, no Jetifier handling: `gradleTemplate.properties`
  belongs to the EOS plugin), asks no questions, and reports no analytics. It records what it
  resolved in `ProjectSettings/AndroidResolverDependencies.xml`. Its Editor analytics prompt is a
  per-user Editor preference: on another machine it may ask once; answer No.
- **Unsigned packages.** The Package Manager warns that two packages were installed without
  signatures: the OpenUPM packages above, which Unity does not sign. That is expected.
- **A check.** `Callbreak.Editor.AndroidAdMobGradle` stops an Android build whose Gradle project
  lacks the AdMob libraries or the app id, and says what to do.
- **With EOS.** `AndroidEosGradle` (see EOS.md) and the plugin's own Gradle step both change the
  generated project; they touch different parts of it.
- **A development APK:** `Tools/Unity/android-apk.sh` exports the project from the open Editor and
  builds `Builds/Android/Callbreak.apk` with Unity's Gradle, signed with the Android debug key (the
  project's own keystore is left alone), then checks the app id in the APK's manifest.
- **After a Unity upgrade**, copy the new Editor's `mainTemplate.gradle` over the project's and run
  Force Resolve, so the template matches the new Unity.
