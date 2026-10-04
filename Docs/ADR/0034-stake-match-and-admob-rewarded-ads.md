# ADR 034 - Stake Match, and AdMob rewarded ads

**Status:** accepted (owner, 5 Oct 2026).

## Stake Match

**Context.** The owner asked that PLAY ONLINE offer two choices: Quick (free), and a stake of 50 or
100 coins with an "i" that shows the prizes. The mockups are
`Assets/ArtRefrences/game-screens/23-online-play-stake-50.png` and `24-online-play-stake-100.png`;
the spec is `D:\Nepali Callbreak\game-screens\online-match-options-prompts.md`.

**Decision.**
- **The screen** (`ScreenId.PlayOnline`):
  - PLAY ONLINE opens it; Back and Android back return. Android back first closes the prize
    popover.
  - Quick Match plays without a stake.
  - Stake Match picks 50 or 100 on two chips; 50 is chosen first, and the choice is kept for the
    session.
  - The "i" opens the prize popover for the chosen stake. It follows a change of stake, and ×, the
    "i", a tap elsewhere or Android back close it.
  - Choosing or inspecting a stake moves no coins.
  - Without enough coins, Play shows "Not enough coins" with Earn Coins and Not now (proposed copy)
    and doesn't search.
- **Rules** (`Application/Economy/StakeRules.cs`):
  - Prizes by place: stake 50 pays 100, 40, 30 and 20; stake 100 pays 200, 80, 60 and 40. Four
    people at 50 put in 200 and win 190; at 100, 400 and 380.
  - Bots fill empty seats as in Quick Match (ADR 029); a bot pays no stake and wins nothing.
  - Places come from Core `Standings` (GDD §4 tie-breaks). Players who still share a place each get
    that place's prize: the owner's "both get the higher prize".
  - A player who leaves, or isn't back in time after dropping, loses the stake and wins nothing.
  - A match called off before or while it is played (the host lost and nobody could take over, or the
    hand-over gave up while this device was online) gives the stake back, like ADR 032's "no unfair
    loss".
- **Matchmaking:** players meet only players of the same stake.
  - Each stake has its own EOS lobby bucket: Quick keeps `callbreak-quick:v7`, and the stakes get
    `callbreak-quick:v7:s50` and `:s100`.
  - The change: `IQuickTableDirectory.ForStake(int)`, implemented in `EosPeerNetwork` and the
    in-memory test network. No wire change and no protocol bump.
  - With three queues instead of one, fewer people meet at once while the game has few players, and
    bots cover the waiting (discussed with the owner).
- **The ledger** (`IStakeLedger`, save key `stakes`, the last 50 matches by id):
  - The stake is taken once per match, when the search ends in a match on this device
    (`QuickMatchService.Finish`), and saved before the coins move.
  - If the wallet can no longer cover it then, the match plays unstaked and wins nothing.
  - The prize is paid once per match id at the match's end (`OnlineMatchSession`). Leaving forfeits
    the stake; a called-off match refunds it.
  - A killed app that comes back to the same match and sees its end is still paid.
  - Coins are kept on the device, as the wallet is (ADR 033): there is no server to settle them, so
    a modified app could change its own coins.
- **Proposed, not built:**
  - the match results line "+{0} coins" in a stake match (`ui.match_results.stake_prize`);
  - "· 50-coin stake" on the matchmaking rules line.

## AdMob rewarded ads

**Context.** The owner set up AdMob for the app "Callbreak : Nepali Taas Game":
- App ID `ca-app-pub-1025159441369051~3176202879`;
- rewarded unit `ca-app-pub-1025159441369051/8532471334`;
- banner and interstitial units exist but aren't used.
The owner asked to show real ads on Earn Coins in place of the stand-in.

**Decision.**
- **Packages:** Google Mobile Ads Unity plugin 11.5.0 (Android SDK 25.4.0, UMP 4.0.0) and the External
  Dependency Manager 1.2.187, both from OpenUPM.
  - EDM4U is deprecated upstream in favour of Unity's EDM 2.x; switching later is a packages-only
    change.
- **The shape,** following `IPeerNetwork` / `PeerRoomTransport` / `EosPeerNetwork`:
  - the port is `Application/Economy/IRewardedAdNetwork.cs`;
  - the policy is the engine-free `PreloadedRewardedAds : IRewardedAds`, which keeps one ad
    loaded and retries after 5 s, doubling to 2 min;
  - the adapter is `Infrastructure/Ads` (own asmdef) with `AdMobRewardedAdNetwork` and `AdMobIds`.
- **The policy's details:**
  - an ad older than 55 minutes is replaced;
  - the result arrives on close, true only if the reward was earned (with 1 s grace for a late
    reward); one show at a time;
  - it never throws except on cancellation;
  - callbacks return to the main thread through `MobileAdsEventExecutor.ExecuteInUpdate` (the old
    flag is obsolete).
- **Where:** AdMob is registered only on Android and iOS player builds. The Editor, tests and desktop
  keep `StandInRewardedAds`.
- **Test ads:** development builds (`Debug.isDebugBuild`) use Google's test rewarded unit; release
  builds use the owner's. Tapping one's own live ads can get an AdMob account closed.
- **Consent:** Google's UMP runs at every start (update, then the form if required). Ads are initialised
  and requested only when `CanRequestAds()`. There is no "privacy options" entry yet; the owner
  decides its copy and place.
- **Build:**
  - a custom main Gradle template (`Assets/Plugins/Android/mainTemplate.gradle`) patched by EDM4U,
    with its settings pre-seeded in `ProjectSettings/GvhProjectSettings.xml` (no prompts, analytics
    off);
  - the App ID kept in `AdMobIds` and written into the plugin's settings before every build
    (`AdMobSetup`);
  - a post-generate check (`AndroidAdMobGradle`);
  - `Tools/Unity/android-apk.sh` builds a debug-signed development APK without touching Player
    Settings or the keystore;
  - `ProjectSettings.asset` changes only `useCustomMainGradleTemplate`;
  - `compile-check.js` now reads the plugin's precompiled DLLs.

**Owner actions** (`Docs/Setup/AdMob.md`):
- register test devices in AdMob;
- link the Play listing (to lift "limited ad serving");
- publish the GDPR message under Privacy & messaging;
- host app-ads.txt (`google.com, pub-1025159441369051, DIRECT, f08c47fec0942fa0`);
- in Play Console: "Contains ads", the Advertising ID declaration and the Data safety form;
- confirm the target audience excludes children;
- create an iOS AdMob app before any iOS build.

## Consequences

- **Tests:**
  - Stake Match: 58 EditMode tests (prizes, ties, bots, once-only take and pay, leaving, call-off,
    separate stakes, restart) and the Play Online play tests;
  - AdMob: 17 EditMode tests of the ad policy.
- **Not verified:**
  - on a device: real ads, the consent form, real EOS stake lobbies;
  - a real Android or release build: the APK is still to be built.
- **Open for the owner:**
  - the not-enough-coins and results copy;
  - whether the chips should remember the last stake after a Quick Match or a relaunch;
  - whether Play Again after a stake match should ask again;
  - a privacy-options entry;
  - coin farming against bots-only stake tables.

## Tests no longer sign in to Epic

**What happened.** From 23:34 NST on 4 Oct, Epic's notification websocket accepted one connection per
EOS sign-in, dropped it within a minute, and then refused the same valid token with HTTP 401
("Connect Messaging: Failed to connect to Stomp! HS: ws upgrade unauthorized").
- The SDK logs each refusal as an error, so whichever PlayMode test was running failed.
- Lobbies kept working. New peer-to-peer connections, which Epic signals through that service, may
  not (not verified).
- Epic's status page showed no incident.
- The PlayMode tests had been signing this PC's product user in hundreds of times an hour.

**Decision.** PlayMode tests run with EOS off.
- `GameFixture.Boot(liveOnline: false)` registers `NoRoomTransport` in a parent scope.
- `ProjectLifetimeScope.RegisterRoomTransport` stands aside when a parent scope already provides one.
- Production is unchanged. Only `Boot(liveOnline: true)` starts EOS, and no test uses it yet.

**Owner follow-up:**
- test online play on a phone, to see whether it is product-wide;
- check the Developer Portal client and policy;
- report to Epic support if nothing changed there.

**Suites with everything above:** EditMode 1987, PlayMode 94 (no EOS lines), dotnet 1971 (Release).
The table simulation has 0 failures in both modes (480 and 467 kinds of check).
