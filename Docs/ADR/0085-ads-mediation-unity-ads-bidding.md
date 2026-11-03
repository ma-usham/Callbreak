# ADR 085 - Ads mediation: Unity Ads bidding

**Status:** accepted (owner, 3 Nov 2026):

> I want to add ads mediation in my game so that the ads gets filled. Can you setup ads mediation in
> admob.

From the proposal they first picked "admob, applovin, unityads bidding only", then, the same evening:

> lets just use unity ads and admob for bidding.

## Context

- **One source of ads.** The rewarded (Earn Coins), interstitial (between matches), app open and
  banner ad units took ads only from Google's own advertisers: AdMob's mediation had nothing but its
  "AdMob (default)" group. A request Google can't fill shows nothing.
- **The app in AdMob** is "Ready, ad serving enabled" and linked to its Google Play listing, so no
  limited ad serving stands in the way.
- **What was proposed** to the owner: AppLovin, Unity Ads, Liftoff Monetize and Mintegral, with
  Pangle, InMobi and Meta Audience Network as extras, by the formats each fills, its demand for games
  and in South Asia, its account steps and its SDK's size. The owner asked who pays most and how
  payments work:
  - for games, AppLovin is usually the highest bidder, then Unity Ads (strong on rewarded);
  - Meta pays well in the US and Europe but fills little in lower-income countries;
  - nobody publishes fill by country, so AdMob's mediation report will show what fills in Nepal;
  - each network pays its own share to the owner's account with it, under its own payment minimum.
- **AppLovin dropped.** The owner's new AppLovin account was still waiting for AppLovin to confirm
  it (its dashboard, and so its SDK Key, wasn't open yet). AppLovin stays an unused bidding partner
  in AdMob, with nothing mapped.

## Decision

- **Unity Ads, bidding only.** For every request AdMob runs a live auction between Google's
  advertisers and Unity Ads; the higher bid shows. No waterfall: no reporting API key, no eCPM floors
  (and Unity's waterfall ended on 31 Jan 2026).
- **Unity's side** (Unity Dashboard, organization DayZero Games, the existing project Callbreak):
  - the app "Callbreak: Nepali Taas Game", from its Google Play listing (`com.dayzerogames.callbreak`);
  - general audience, not directed to children (the owner's answer), and no developer data shared
    with Unity Ads (the owner's choice);
  - Android Game ID `800422836`;
  - three bidding placements: `BP_Rewarded_Android`, `BP_Interstitial_Android`, `BP_Banner_Android`.
- **The mediation groups** (AdMob, Android, one per format, made with the owner on 3 Nov 2026, all
  countries, enabled; the owner accepted Unity Ads' bidding agreement):

  | Group | Ad unit | Ad sources | Unity placement |
  | --- | --- | --- | --- |
  | Callbreak Android Rewarded | `.../8532471334` | AdMob, Unity Ads | `BP_Rewarded_Android` |
  | Callbreak Android Interstitial | `.../4319920270` | AdMob, Unity Ads | `BP_Interstitial_Android` |
  | Callbreak Android Banner | `.../8330163494` | AdMob, Unity Ads | `BP_Banner_Android` |

  The app open unit (`.../9889671064`) stays in the AdMob (default) group: Unity Ads doesn't bid on
  app open ads.
- **The adapter:** Google's Unity mediation plugin `com.google.ads.mobile.mediation.unity` 3.21.1,
  from OpenUPM under the existing `com.google.ads.mobile` scope.
  - It brings the Android adapter `com.google.ads.mediation:unity:4.20.1.0` and Unity Ads 4.20.1.
  - It needs `com.google.ads.mobile` 11.0.0 or later and builds on the plugin's `play-services-ads`
    25.4.0. The newer 3.22.0 would pull `play-services-ads` up to 25.5.0, past what the plugin
    (11.5.0) ships, so it waits for the plugin's next upgrade.
  - EDM4U wrote both libraries into `mainTemplate.gradle` and
    `ProjectSettings/AndroidResolverDependencies.xml`.
  - The owner's request is the approval for the package.
  - The AppLovin adapter (8.7.7), added before the owner's second answer, was removed again.
- **No game code.** The Google Mobile Ads SDK finds the adapter by itself and the ad units are
  unchanged. Unity's IDs live only in AdMob's mappings.
- **Consent.** UMP keeps the player's choice as IAB TCF strings. The adapter (since 3.20.0) passes
  that GDPR consent on to Unity Ads, so nothing is called. AdMob's European regulations message must
  list Unity Ads among its ad partners, and it does: "Callbreak - European regulations" uses the
  commonly used ad partners, Unity Ads (IAB vendor 1549) among them. US state choices are not
  passed on, as the game shows no US states message. If one is published,
  `UnityAds.SetConsentMetaData("privacy.consent", ...)` must be called before `MobileAds.Initialize`.
- **No R8 keep module.** The SDK's own consumer rules (`play-services-ads-api`) keep every class that
  implements or extends its mediation adapter types. The Unity Ads library carries keep rules of its
  own (`-keep class com.unity3d.ads.** { *; }`, `com.unity3d.services.**` and more). Nothing in the
  game calls its Java by name.

## Consequences

- **More ads filled, at better prices.** A request Google can't fill may go to Unity Ads, and
  Unity's bids push Google's up. Whether Unity wins in Nepal shows in AdMob's mediation report after
  a week or two.
- **Two payouts.** AdMob and Unity each pay their own share, each at its own minimum.
- **The build** (3 Nov 2026, checked without the shared Editor, which other sessions' tests held):
  - The Editor's release export with the adapters (`BuildCheck`) succeeded: the game and the
    adapter's C# compile with IL2CPP.
  - Today's 21:44 release export was rebuilt with Unity's Gradle and JDK, plus the adapter's
    libraries, as `android-size.sh` does (signed with the debug key):

    | Release APK | Without mediation | With Unity Ads |
    | --- | --- | --- |
    | As Player Settings build it (no minify) | 120.67 MB | 122.67 MB (+2.0 MB) |
    | With R8 minify on (a check only) | not measured | 118.59 MB |

  - Dependency resolution, the duplicate-class check, dexing and R8 all passed.
  - Where the 2.0 MB goes (zipped, in the universal APK): code (dex) 8.84 → 10.35 MB, native
    libraries 60.93 → 61.32 MB (Unity's `libunitycoherencenative.so`, for both ABIs), the rest under
    0.1 MB. A phone downloads one ABI, so about 1.7 MB more.
  - Not run: `android-size.sh`'s App Bundle and per-phone download sizes with the final set of
    libraries.
- **Testing.** Development builds use Google's test units, which never reach mediation. Mediation
  is checked with a release build on a phone registered as an AdMob test device, with Unity's test
  mode on, and AdMob's Ad Inspector.
- **The owner's part** (`Docs/Setup/AdMob.md`, "What the owner does"):
  - Unity's payment and tax details;
  - Unity's lines in `https://dayzerogames.tech/app-ads.txt`, the company site Unity's Developer
    website now points at (`callbreak.dayzerogames.tech/app-ads.txt` has them, checked 4 Nov);
  - test devices.
- **Done with the owner on 4 Nov 2026:** Unity's Developer website set to
  `https://dayzerogames.tech`. Play Console's Data safety now matches Unity Ads 4.20's guide:
  purchase history shared for Advertising or marketing and Analytics, and App functionality added
  to the sharing purposes of approximate location and diagnostics. Sent for Google's review.
