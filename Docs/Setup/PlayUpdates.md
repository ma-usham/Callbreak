# Forced updates from Google Play: how to test

The game installs any newer version on Google Play through Play's immediate in-app update
(ADR 036). Play Console needs no setup for it. Update priority exists only in the Google Play
Developer (Publishing) API (`inAppUpdatePriority`), and the game ignores it.

## What you need

- An Android phone with the Play Store, signed in with a tester account that has installed the game
  from Play at least once.
- Builds with the same application id (`com.dayzerogames.callbreak`) and signing key, installed
  through Play. A sideloaded APK never sees an update.
- Release builds are minified. The keep rules ship in
  `Assets/Plugins/Android/PlayAppUpdateKeep.androidlib`, but check the flow on the release App
  Bundle, not only on a development build.

## Internal testing track

1. Publish version code N to internal testing, and install it from the opt-in link.
2. Publish N+1 to the same track. Wait until the game's Play Store page shows the update, but don't
   tap Update there.
3. Open Callbreak. Play's full-screen update should appear at launch.
4. Back out of it: "Update required" appears. Tapping outside it and Android back don't close it.
5. Tap Update: Play's screen comes back. Accept: Play installs N+1 and restarts the game.

## Internal app sharing (faster)

1. Turn it on in the Play Store app: Settings > About, tap "Play Store version" 7 times, then
   Settings > General > Internal app sharing.
2. Install build N from its internal-sharing link.
3. Upload N+1, and open its link without installing from that page.
4. Open Callbreak from the launcher, and follow steps 3-5 above.

## Also check

- No update appears mid-match, in matchmaking or in a room.
- After "Not now" on a rejoin offer, the update waits until the rejoin ticket runs out (up to 10
  minutes) or the next launch.
- `adb logcat -s Unity` shows "App update: ..." from the policy and "[AppUpdate] ..." from the
  adapter.
