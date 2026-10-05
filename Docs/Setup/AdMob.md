# AdMob setup (Earn Coins ads and ads between matches)

The Earn Coins page pays its coins for Google AdMob rewarded ads on phones, and a full-screen AdMob
interstitial ad can show between matches (below, "Ads between matches"). They come through the
Google Mobile Ads Unity plugin (`com.google.ads.mobile` 11.5.0, from OpenUPM) and the External
Dependency Manager it brings (`com.google.external-dependency-manager` 1.2.187, EDM4U). In the
Editor, in the tests and in desktop builds stand-ins take their place: one completes every Earn
Coins ad (`StandInRewardedAds`), the other never has an ad between matches
(`StandInInterstitialAds`). No ad network is touched there.

## Identifiers (not secret: they ship inside the app)

| What | Value |
| --- | --- |
| AdMob app (Android) | `ca-app-pub-1025159441369051~3176202879` |
| Rewarded ad unit, Earn Coins (Android) | `ca-app-pub-1025159441369051/8532471334` |
| Interstitial ad unit, between matches (Android) | `ca-app-pub-1025159441369051/4319920270` |
| Banner `.../6484892497` | not used anywhere |

They live in code, in `Assets/_Project/Scripts/Infrastructure/Ads/AdMobIds.cs`. Before every Android
build, `Callbreak.Editor.AdMobSetup` writes the app id into the plugin's settings,
`Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset`, and the plugin then writes it into
the Android manifest. **Callbreak > Ads > Apply AdMob Settings** does the same by hand. (The
plugin's own **Assets > Google Mobile Ads > Settings...** window shows the same asset.)

## Test ads and real ads

- **Development builds** (Development Build ticked; `Debug.isDebugBuild`) always use Google's test
  units: rewarded `ca-app-pub-3940256099942544/5224354917`, interstitial
  `ca-app-pub-3940256099942544/1033173712` (iOS: `.../1712485313` and `.../4411468910`). Those ads
  say "Test Ad", work on any phone and need no AdMob review.
- **Release builds** use the real units. Never tap your own live ads: AdMob can suspend the account.
  To try a release build on a phone, make it a test device first (below).

## Ads between matches

The owner's rule (5 Oct 2026). As the player leaves the results of a finished match, a full-screen
interstitial ad shows before the next screen:

| Mode | Play Again | Main Menu |
| --- | --- | --- |
| Offline | the ad, then the new match is dealt | the ad, then the main menu |
| Quick or Stake Match | the ad, then the search (before its countdown) | the ad, then the main menu |
| Private room | no ad: back to the lobby, where the friends wait | the room is left, then the ad, then the main menu |

**Leaving a match early** (the owner, 5 Oct 2026, evening: "show ads when user leaves the game in
the middle"). In every mode, Leave Match in the match menu, once confirmed, first leaves for real:
the leave, a host's hand-over and a private room left. The ad then waits one second, so those
messages go out before the ad pauses the game. Then the ad, then the main menu.

- **Never:** during play, on a round's results, in matchmaking or in the lobby; after a match
  called off (the host lost: the player didn't leave); after the player's very first match (the
  statistics count every match played, finished or left early; an offline match left for later is
  not counted yet); for a player who owns **No Ads**.
- **At most one every three minutes**, on the wall clock, from when the last one came up. That time
  is kept in memory, so each launch starts afresh.
- **Never waited for:** if no ad is loaded at that moment, the game goes on without one.
- A private room whose host left under the results sends Play Again to the main menu: that counts
  as Main Menu, and the message saying why comes after the ad.
- **Loading.** One ad is kept loaded, tried again after 5 s, 10 s, 20 s ... at most every two
  minutes, and replaced after 55 minutes (AdMob's ads expire after an hour). A player who owns No
  Ads gets nothing requested: buying it lets the loaded ad go, and a refund starts loading again.
- **Consent.** The ads between matches start AdMob through the Earn Coins ads' start (consent,
  then the SDK), so the consent form never shows twice and the SDK starts once. An ad is requested
  only while UMP says ads may be requested.
- **Sound.** No game sound plays over an ad: Unity pauses on Android while a full-screen ad shows,
  and on iOS `MobileAds.SetiOSAppPauseOnBackground(true)` does the same.
- **The pieces:** the ports `Application/Economy/IInterstitialAdNetwork.cs` and
  `IBetweenMatchAds.cs`; the rule `BetweenMatchAds.cs`; the loading `PreloadedInterstitialAds.cs`;
  the stand-in `StandInInterstitialAds.cs`; the AdMob side `Infrastructure/Ads/AdMobInterstitialAdNetwork.cs`;
  the hook in `Presentation/Overlays/MatchResultsPresenter.cs`; registered next to the rewarded ads
  in `ProjectLifetimeScope`.

## How the app asks for consent

At every start, before any ad is requested, the app asks Google's User Messaging Platform (UMP) for
the player's consent status, and shows Google's consent form when the law asks for it (the messages
set up in AdMob's Privacy & messaging). The SDK starts, and ads are requested, only while UMP says
ads may be requested. A player whose consent from an earlier session still holds gets ads at once
while it is checked again. If the form can't load (no network), the app tries again after 5 s, 10 s,
20 s ... at most every two minutes.

**Privacy options.** Where a consent form applies, Google's policy asks that players can change
their choice later. Settings then shows a **Privacy options** button under its panel (copy
`ui.settings.privacy_options`), which opens Google's privacy options form
(`ConsentForm.ShowPrivacyOptionsForm`); a second tap while the form is up opens nothing more.

- The button shows only while UMP says an entry point is required
  (`ConsentInformation.PrivacyOptionsRequirementStatus` is `Required`), as Google's guidance asks.
  Elsewhere Settings looks exactly as it did before.
- UMP knows this once the consent information has been updated at start, and the app reads it again
  after the consent form and after the privacy options form. It appears while Settings is open if
  the answer comes then.
- If that update fails at start (no network) while the player's earlier consent still lets ads be
  requested, the button stays hidden until the next start.
- In the Editor, the tests and desktop builds a stand-in (`StandInAdPrivacy`) never requires it.
- The pieces: the port `Application/Economy/IAdPrivacy.cs`, the AdMob side
  `Infrastructure/Ads/AdMobAdPrivacy.cs` (told by `AdMobRewardedAdNetwork` when the consent
  information may have changed), registered next to the rewarded ads in `ProjectLifetimeScope`.

## What the owner does

1. **Test devices.** AdMob > Settings > Test devices > Add test device: Android, with the phone's
   advertising ID (on the phone: Settings > Google > All services > Ads). Do this for every phone
   that runs release builds.
2. **Privacy & messaging.** In AdMob > Privacy & messaging, create and publish a **European
   regulations** (GDPR) message for the Callbreak app; add a **US state regulations** message if you
   want one. Without a published GDPR message, players in the EEA, the UK and Switzerland see no
   form, and AdMob limits their ads.
3. **Privacy options: check the button.** Settings' **Privacy options** button (above, under "How
   the app asks for consent") is built. Its place under the panel, its size (How to Play's button,
   smaller) and its lock icon are proposals for you to confirm. It shows only for players UMP says
   need it (for example in the EEA, the UK and Switzerland), so it never shows on a phone in Nepal:
   the app sets no UMP debug geography.
4. **Link the Play listing.** Once the game is on Google Play: AdMob > Apps > Callbreak > App
   settings > link the app to its store listing. AdMob then reviews the app; until then it shows
   "Requires review, limited ad serving", and real ads, rewarded and between matches, are few or
   none (test ads are not affected).
5. **app-ads.txt.** The Play listing needs a developer website. Put a file at
   `https://<that domain>/app-ads.txt` with this line:

   ```text
   google.com, pub-1025159441369051, DIRECT, f08c47fec0942fa0
   ```

6. **Play Console.**
   - App content > Ads: "Yes, my app contains ads" (rewarded ads, and full-screen ads between
     matches).
   - App content > Advertising ID: the app uses it, for advertising (the SDK adds the
     `com.google.android.gms.permission.AD_ID` permission).
   - Data safety: declare the data the Google Mobile Ads SDK collects and shares (see Google's
     guide "Prepare for Google Play's Data safety section" for AdMob: device and other identifiers,
     approximate location, app interactions, diagnostics).
   - Target audience: if it includes children, the Families policy applies and ad requests must be
     tagged; today nothing is tagged.
7. **iOS (later).** There is no iOS AdMob app yet. With the plugin installed, an iOS build stops in
   the plugin's build step ("iOS Google Mobile Ads app ID is empty") until an iOS app id is entered
   in the plugin's settings, and a release iOS build shows no ads until `AdMobIds` has iOS units.
   Development iOS builds would use Google's iOS test units.

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
