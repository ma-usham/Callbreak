# ADR 047 - App open ads on the return to the game

**Status:** accepted (owner, 15 Oct 2026).

**Context.** The owner asked for more AdMob ad units, chosen to fit the game, set up in AdMob and
used in the game, and for the banner ads to be dropped for now. The game already had the Earn Coins
rewarded ads (ADR 034) and the ads between matches (ADR 035). Of AdMob's other formats:
- **App open** fits a card game that players leave and come back to, and needs no new screen or copy.
- **Rewarded interstitial** and a second rewarded unit (for example "watch to double the daily
  reward") would change the coin economy and need new copy, so they are left for the owner to decide.
- **Banner:** a 320x50 banner doesn't fit left of the table's Scores button on phones (the banner
  session's notes). The owner dropped banners for now. The work stays unmerged on
  `agent/T-banner-ads`, and the owner's unused banner unit `.../6484892497` stays in AdMob, since
  removing an ad unit there can't be undone.
- **Native advanced** has no uGUI rendering in the plugin.

The owner approved app open ads with the rules below.

## Decision

- **The ad unit.** `Android_AppOpen`, `ca-app-pub-1025159441369051/9889671064`, created in AdMob on
  15 Oct 2026 with no frequency cap there (the game caps it) and no partner bidding. Development
  builds use Google's test units (`.../9257395921` Android, `.../5575463023` iOS). AdMob says a new
  unit can take up to an hour to serve.
- **When.** As the player comes back to the game after at least **30 seconds** away, only on the
  **bare main menu**: the menu up and visible, no dialog, no curtain (so not over the store's
  purchase sheet), the name answered and no prize coins waiting to fly.
- **Never:**
  - on a start (the game was never away);
  - in a match, matchmaking, a room, the shop or any other screen;
  - for a player who owns **No Ads**;
  - before the player's second match (as for the ads between matches);
  - within **3 minutes** of the last full-screen ad, an app open ad or an ad between matches
    (`FullScreenAdPacing`, shared with `BetweenMatchAds`). The Earn Coins ads don't count;
  - when none is loaded: the game never waits for one.
- Each time away gives one chance.
- **Loading, consent and sound** are as for the ads between matches. One ad is kept loaded and
  replaced after 55 minutes (app open ads expire after 4 hours). AdMob starts once, through the
  Earn Coins ads' consent and SDK start. Nothing is requested for No Ads owners. The game is silent
  while the ad is up.
- **Away and back** come from Google Mobile Ads' app state (`AppStateEventNotifier`). That follows
  the whole app, so closing one of the game's own full-screen ads isn't a return. The game sleeps
  while it is away, so the time of leaving is read when the plugin reports it, not when the main
  thread hears it.

## Consequences

- **The pieces:**
  - the rule `ReturnToGameAds` and its port `IReturnToGameAds`;
  - the shared `FullScreenAdPacing`;
  - the port `IAppForeground` and its stand-in `StandInAppForeground`;
  - the AdMob side, `AdMobAppOpenAdNetwork` (behind `IInterstitialAdNetwork`, kept loaded by
    `PreloadedInterstitialAds`) and `AdMobAppForeground`;
  - the menu's `ReturnToGameAdMoment`, registered in `ProjectLifetimeScope` and `MenuLifetimeScope`.
- In the Editor, the tests and desktop builds, the foreground never moves and there is never an ad.
- **Tests:** `ReturnToGameAdsTests` cover the rule, including the shared three minutes with
  `BetweenMatchAds`.
- **Not verified on a phone:** the ad on a real return, the 30 seconds, and that a return from the
  Play purchase sheet or the Play update screen shows none.
