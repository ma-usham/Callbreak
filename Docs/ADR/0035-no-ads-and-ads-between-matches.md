# ADR 035 - No Ads, and ads between matches

**Status:** accepted (owner, 5 Oct 2026).

**Context.** The owner asked for an in-app purchase, No Ads, at $0.99. Until then the game's only ads
were the Earn Coins videos (ADR 034), which the player chooses to watch, so there was nothing for
No Ads to remove. The owner chose:
- full-screen ads between matches, with your own AdMob interstitial unit, which No Ads removes;
- the Earn Coins videos stay for everyone, buyers included;
- the No Ads button goes on the main menu;
- the places where an ad shows (the table below).

This replaces "No real money" in ADR 033's wallet.

## Ads between matches

**Where.** Only as the player leaves the Match Results of a finished match:

| Mode | Play Again | Main Menu |
|---|---|---|
| Offline | the ad, then the new match is dealt | the ad, then the main menu |
| Quick / Stake Match | the ad, then the search and its 20 s countdown | the ad, then the main menu |
| Private room | no ad: the friends wait in the lobby | the room is left first, then the ad, then the main menu |

A private room that closed under the results sends Play Again to the main menu. That counts as Main
Menu: the ad if one is due, then the toast saying why.

**Never:**
- during bidding or play, on Round Results, in matchmaking or in the lobby;
- after a match that ended early (left from the match menu, or called off), since only the results
  screen asks;
- after the player's first match (`gamesPlayed <= 1`, counting every match played);
- with No Ads;
- within 3 minutes of the last ad (wall clock, from when it came up, kept in memory only);
- when no ad is loaded. The game never waits for one.

**The shape** mirrors the rewarded ads:
- the port `IInterstitialAdNetwork` (Application/Economy);
- `PreloadedInterstitialAds`, which keeps one ad loaded, retries after 5 s doubling to 2 min, and
  replaces an ad after 55 min. While No Ads is owned it requests nothing (not even the consent and SDK
  start), lets a loaded ad go when No Ads is bought, and starts again after a refund;
- the rule `BetweenMatchAds : IBetweenMatchAds.ShowIfDueAsync(AfterMatch, ct)`, called by
  `MatchResultsPresenter` inside its existing leave guard;
- `AdMobInterstitialAdNetwork` and `AdMobIds.InterstitialAdUnit`: the owner's unit
  `ca-app-pub-1025159441369051/4319920270` in release builds, Google's test units in development
  builds;
- `StandInInterstitialAds` (never an ad) in the Editor, the tests and on desktop.

**Consent and sound.** AdMob starts once for both kinds of ad: the interstitial adapter gets ready
through the rewarded adapter's consent (UMP) and SDK start, so the consent form never shows twice.
Unity pauses under a full-screen ad on Android, and `SetiOSAppPauseOnBackground(true)` does the same
on iOS, so no game sound plays over an ad.

## No Ads, the purchase

- **The port** is `INoAds` (Application/Economy): `Owned`, `CanBuy`, `Price`, and `BuyAsync`, which
  answers a `NoAdsPurchase`. The product id is `NoAdsProduct.Id = "no_ads"`, a non-consumable. Play
  never lets an id change or be used again.
- **The implementations:**
  - `StoreNoAds` (engine-free) sits on the store port `IStoreBilling`;
  - on phones, `UnityIapBilling` (Infrastructure/Purchasing, its own asmdef) puts Unity IAP 5.4.4
    behind that port;
  - `StandInNoAds` serves the Editor, the tests and desktop. It sells at once, in memory only.
- **Ownership** is saved in `no-ads.json`, so the ads are off from the first frame, offline too. The
  store has the last word at each start:
  - a listing that has `no_ads` makes it owned (a reinstall, a new phone, a payment that settled while
    the game was closed);
  - a successful listing without it turns it off (a refund), unless a purchase arrived while the
    listing was in flight;
  - a failed listing changes nothing.
- **A paid purchase** is saved, then acknowledged (Google refunds a purchase left unacknowledged for
  3 days). A failed acknowledgement is retried with backoff, and again at every start.
  - Deviation: when `no-ads.json` comes from a newer app version, it is never overwritten, so the
    purchase lives in memory only. It is still acknowledged.
- **A pending payment** (e.g. cash) grants nothing until it is paid, and blocks a second purchase
  meanwhile.
- **One purchase at a time:** a second `BuyAsync` gets the first one's answer.
- **Back in the game:** when the game regains focus, the account is listed again if that can bring
  news (not owned, or an acknowledgement still owed). That happens at most every 30 s, and never
  while a purchase sheet is up.
- **No Restore button.** Android restores purchases by itself. iOS would need one, and App Store
  Connect set up.
- **Under the hood:**
  - Unity IAP needs no `UnityServices.InitializeAsync`.
  - Play Billing Library 9.0.0 comes from the package's own Gradle step.
  - `Assets/Resources/BillingMode.json` is Unity IAP's own file, and only Unity IAP reads it, so the
    no-`Resources.Load` rule still holds for our code.

## The No Ads button

- **The spot.** No mockup draws it. ChatGPT's main menu with it added
  (`ArtSource/Working/MainMenu/chatgpt/NoAds_MainMenu_a1.png`) put it in the top-right row, left of
  the coin wallet, and the build follows that.
- **The look:**
  - the gear's own kit disc and shadow (`UI_ButtonCircle_Normal`, `UI_Shadow_Circle`);
  - one new ChatGPT glyph, `UI/Icons/Icon_NoAds.png` (a ring, a slash and "AD"), in the gear's ink
    #0E0B0C;
  - no price, since the disc has no room for one and Google's sheet shows it.
  - The checks are in `ArtSource/Screens/MainMenu/check-noads/`. With the button hidden, the main
    menu's capture is byte-identical to before.
- **When it shows:** while `INoAds.CanBuy`, following `Changed`. It stays during its own purchase, so
  it doesn't vanish under the finger, and goes once No Ads is owned. A pending payment hides it until
  the payment settles.
- **A tap** opens the store's sheet, which is the confirmation. Further taps do nothing until it
  closes. Then a toast (proposed copy):
  - bought: "Ads removed. Thank you!"
  - already owned: "You already have No Ads. Ads are off."
  - pending: "Your purchase is pending. No Ads arrives once it's paid."
  - unavailable or failed: "The store isn't available right now. Try again later."
  - A closed sheet says nothing.
- **The logic** is the engine-free `NoAdsOffer` (Presentation/Screens/MainMenu), which
  `MainMenuPresenter` drives.

## Release builds (R8)

Release builds are minified (`AndroidMinifyRelease: 1`, no custom ProGuard file), and R8 removes Java
classes that only C# reaches by name through JNI.
- Unity IAP 5.x ships no keep rules for the Billing classes it calls that way; its 2.2.2 changelog
  records the `ClassNotFoundException` this causes.
  `Assets/Plugins/Android/UnityIapKeep.androidlib` keeps `com.android.billingclient.api.**` through
  `consumerProguardFiles`.
- Google Mobile Ads brings its own rules inside its AAR, so it needs nothing.

## Consequences

- **Tests:**
  - ads between matches: 47 EditMode, 5 PlayMode;
  - the purchase: 46 EditMode;
  - the button: 16 EditMode, 4 PlayMode.
- **Privacy.** Unity IAP 5.4.4 posts purchase events to Unity by itself
  (`prd.insights.analytics.unity3d.com`). The paid and acknowledged events carry Google's receipt,
  purchase token included. Nothing in the package gates this, and our code can't switch it off. The
  owner either turns it off in Unity Dashboard > Project Settings > Developer Data, or declares it in
  Play's Data safety form (Financial info > Purchase history) and the privacy policy. Our own code
  saves and logs no receipt or token.
- **Owner actions** (`Docs/Setup/PlayBilling.md`, `Docs/Setup/AdMob.md`):
  - build a signed App Bundle with a higher version code and upload it to internal testing; that
    clears Play's "add the BILLING permission" block;
  - create `no_ads` at $0.99 with the backwards-compatible purchase option, and activate it;
  - add license testers and run the test purchases in the guide;
  - link the AdMob app to the Play listing (until then the interstitial unit serves few or no real
    ads);
  - in Play Console, App content > Ads: "contains ads".
- **Not verified:**
  - on a device: real interstitials, the purchase sheet, pending payments, refunds and the leave
    order in a real private room;
  - a minified release build: the BILLING permission in the merged manifest, and that the keep rules
    reach R8.
- **Open for the owner:**
  - the four toasts;
  - the button's spot and glyph, and no price on it;
  - whether the first-match rule should count only finished matches. Today a player who left their
    first match early sees an ad after their first finished one.

## Addendum, 5 Oct 2026 (evening): an ad when a match is left early

The owner asked for an ad "when user leaves the game in the middle". This replaces "after a match
that ended early" in the "Never" list above, for a match the player leaves; a match called off (the
host lost) still shows none, as the player didn't leave it.

- **Where:** Leave Match in the match menu, once confirmed, in every mode
  (`MatchMenuPresenter`, `AfterMatch.LeftEarly`).
- **Order:**
  1. The game leaves for real: the session (its leave message, the stake forfeit, a host's
     hand-over) and, in a private room, the room.
  2. The ad waits `BetweenMatchAds.LeaveSettle` (1 s), so those messages leave the device before the
     ad pauses the game. Otherwise the others could wait for a silent player, or a host's hand-over
     could become a redeal.
  3. The ad shows, and the main menu opens once it has gone.
- **The same rules hold:** No Ads, the first match (a match left early counts as played; an offline
  match kept for later doesn't yet), at most one every three minutes, never waiting for an ad that
  isn't loaded. Without a due, loaded ad there is no wait either.
- **Tests:** 6 EditMode (the wait, no wait when no ad is due, the three minutes, a cancelled or
  failed wait) and 2 PlayMode (a Quick Match left early: the leave sent before the ad, then the
  menu; the very first match left early shows none).
- **Not verified:** on a device, whether one second is enough for the leave to go out over a slow
  connection.
