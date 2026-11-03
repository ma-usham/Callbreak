# ADR 080 - Google Play's review card after a win

**Status:** accepted (owner, 3 Nov 2026):

> lets implement the google plays rate review in our game.

## Context

- **Google's in-app review.** Google Play shows its own rating card over the game: stars and an
  optional comment, without leaving the game.
- **Google's rules for it:**
  - The app may not ask the player's opinion first, so it can't send only happy players to it.
  - The app may not reward a rating.
  - Play keeps a quota and decides whether the card shows.
  - Play never says whether the player rated.
  - The card works only for an app installed from Google Play (internal app sharing and test
    tracks count).
- **The game had no rating prompt.**

## Decision

- **Google's card is the whole prompt.** No panel of the game's own comes before it, and there are no
  coins for rating.
- **When it is asked for** (`Application/Reviews/ReviewPrompt`, the rule, tested first):
  - after a win: the last match recorded in this launch was won (first place, not left early);
  - from the player's third finished or left match on, the won one included;
  - once per win;
  - at most once every 30 days, and three times in all, remembered across launches
    (`review-prompt.json`).
  - These numbers are proposals for the owner to confirm (`MinMatchesPlayed`, `MinInterval`,
    `MaxAsks`).
- **Where** (`Presentation/Framework/ReviewMoment`, in the Menu scope):
  - on the bare main menu, once it has stood bare for 1.5 s: the menu up and visible, no dialog, no
    curtain, the name answered and no prize coins still to fly in;
  - at most once per Menu scene;
  - Daily Rewards and a festival offer, which open by themselves on the bare menu, go first;
  - never in a match, on a results screen or in the shop;
  - not once the game has been in the background in that Menu scene, as an app open ad may then
    be over the menu.
  - The ads between matches have closed before the menu loads.
- **How.** Google's Play In-app Review plugin (`com.google.play.review` 1.8.4, from OpenUPM like the
  update plugin; the same Play Core 1.8.6 and Play Common 1.9.2) and its Android library
  (`com.google.android.play:review:2.0.2`, written into `mainTemplate.gradle` by EDM4U):
  - `Infrastructure/Reviews/PlayStoreReview` asks Play for the card, then shows it. It sits in an
    assembly of its own, as the update plugin's adapter does.
  - `Plugins/Android/PlayReviewKeep.androidlib` brings the plugin's keep rules for a minified
    build, as `PlayAppUpdateKeep` does for updates.
  - In the Editor, the tests and desktop builds, `StandInStoreReview` shows nothing.
  - The owner's request is the approval for the new package and the Bootstrap assembly reference.

## Consequences

- A player who wins a match from their third one on may see Google Play's rating card on the main
  menu, at most three times, 30 days apart. Play may still show nothing.
- **Testing on a phone:**
  - The card appears only in a build installed from Google Play: upload to internal testing, or
    use internal app sharing.
  - A sideloaded APK logs "No review card from Google Play" and goes on.
  - Play's quota is strict on test accounts too. The card may not come back after the first time,
    which is expected.
- **Tests:**
  - `ReviewPromptTests`: 11 EditMode tests, also in the .NET suite.
  - Suites: EditMode 2600/2600, .NET 2459/2459, and the PlayMode `MenuBannerFlowTests` 3/3, whose
    menu now builds with the review moment.
  - Not verified: the card on a phone, and a minified Android build with the new keep rules.
- **Left for later:** a "Rate us" button in Settings that opens the game's Play Store page. That needs
  the owner's copy and design.
