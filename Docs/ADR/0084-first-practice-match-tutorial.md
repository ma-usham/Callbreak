# ADR 084 - The first practice match teaches the game

**Status:** accepted (owner, 3 Nov 2026); the copy and the look wait for the owner's review of the
16:9 and 20:9 captures:

> for the first time if player logins and if he selects practice match then teach him how to play
> like a tutorial video.

> if you need figma design for point use that and design pointer in figma and use it in game

> in practice match if we play and leve then if i tried playing offline then it says resume the previous
> match that shouldnt happen. the practice match shouldnt be resumed later.

## Context

- **The first run** (GDD §15, ADR 022) asks the player's name, opens How to Play by itself and then
  offers "Play a practice match?" (Practice / Skip), an easy offline match. Nothing in the match itself
  teaches: ADR 070 took the in-match tips and the turn prompt away at the owner's request, so a tutorial
  may only run in that first practice match, never in normal play, and must be skippable.
- **Playing a card is a gesture:** a card leaves the hand when it is pulled up and let go (ADR 038);
  a tap only looks at it. Nothing on the table says so.
- **An offline match deals from one seed** (ADR 056) and its bots pace themselves with the table
  (ADR 055). The engine takes any `IRandom`, and `MatchSetup` can fix the first dealer.

## Decision

- **The first hand of the first practice match is a guided hand, like a short video the player
  follows.** Accepting the first-run offer launches `FirstRun.PracticeMatch`: Offline, Easy, with
  `MatchLaunchRequest.Tutorial` set until the tutorial is done. No other way into a match sets it, and a
  resumed match never has it.
- **A scripted, friendly deal** (`TutorialDeal.First`). The player deals, so the seat on their right
  calls and leads first, and the player calls last and plays last to trick 1. The first shuffle is given
  (the draws that make the engine's own Fisher–Yates deal these hands); every later shuffle is the match
  generator's, which is what a save keeps, so leaving and resuming deals what an uninterrupted match
  would. The bots make scripted calls (2, 1, 3) and play scripted cards in the two guided tricks
  (`TutorialBot`, only when legal); after that they are the match's own Easy bots.
  - Trick 1: hearts are led; the player has no heart that beats the ace and follows with the 4.
    West's ace takes it.
  - Trick 2: West leads the ace of diamonds; the player has no diamond and trumps with the 3 of spades,
    and wins. The rest of the hand is the player's.
  - The player's hand (A K Q 7 3 of spades, two low hearts, A K and four clubs) is worth more than the
    suggested call of 3, so the first score is usually a call met with extra tricks.
- **Steps** (`MatchTutorial`, engine-free in Application, followed by the session as it emits events):
  1. Welcome (centred, Next): "Your first hand".
  2. Call: the pointer is on chip 3, then on Confirm.
  3. Follow the suit: the pointer pulls the 4 of hearts up.
  4. Highest card wins (Next): who won and why; the winner leads next.
  5. Spades are trump: "No card of the led suit? Play a spade: it beats every other suit."; the pointer pulls
     the 3 of spades up.
  6. You won the trick! (Next): "play the rest of the hand yourself; the cards you can play glow".
  7. Count your score (centred, over the round results, Next): "You called 3 and won 5: +3.2 points",
     then how a call scores.
  8. You're ready! (Let's Play): the other four rounds are the player's own.
  A step shows once the table has played the event that opened it (`OpenedAt`), so a caption never
  comes before what it talks about. While a caption waits for Next the bots wait too
  (`HoldsBots`, checked in `OfflineMatchSession.Tick`); a caption that waits for Next also ends when the
  player calls or plays. The player may call any number and play any legal card: the pointer suggests,
  the rules decide.
- **After the guided hand the match carries on as a normal easy match**, rather than ending with a card
  and a trip back to the menu. The player already has a match going, the next four rounds are the
  practice the offer promised, and the "You're ready" card sits on the first hand's results, where the
  score it explains is.
- **It runs once.** `settings.json` schema 3 adds `tutorialDone`; finishing the tutorial, "Skip tutorial"
  on any step, or the next round starting marks it. Nothing offers the tutorial again (the first-run
  offer itself comes once).
- **The practice match is never kept for Resume** (the owner, later the same day).
  `FirstRun.PracticeMatch` sets `MatchLaunchRequest.Practice`, and `OfflineMatchSession` never saves
  such a match: not after a trick, not when it pauses (as the app goes to the background), and not when
  it is left. Leaving it, or the app closing during it, leaves nothing to resume: Play Offline later
  starts a new match, and the launch offers no Resume. Its "Leave match?" dialog drops the line "Your
  match is saved…". Restart Game starts an ordinary easy match. Ordinary offline matches are saved as
  before.
- **The overlay** (`TutorialView`, `TutorialPresenter`; built by `TutorialOverlayBuilder` into the Table
  scene, canvas order 45: above the pages, below dialogs and toasts) uses the dialog's kit: the modal
  panel as a caption card, the serif crimson title, the brown body, the crimson Next and the ivory "Skip
  tutorial". During play the card sits in the top-right corner, under the banner and clear of the hand,
  the trick and the call panel, and nothing else of the overlay takes a touch; the welcome and the score
  are centred over a dim that takes every touch. While the match menu, the scorecard or settings cover
  the table the caption steps aside.
- **The pointing hand was designed in Figma** at the owner's request: Art/Tutorial Pointer 446:518 on the
  design-system page (an ivory glove, index finger up, thumb across the curled fingers, a crimson cuff
  with a gold band, the kit's dark-crimson outline and a soft shadow), exported on black and white with
  the alpha recovered into `UI_TutorialPointer.png`. On a card it shows the pull that plays it; on a chip
  or a button it taps. With Reduce motion it stands still.
- **Copy** is in `UI.csv` (`ui.tutorial.*`), reusing the rules primer's unused `rules.primer.*_title`,
  `rules.primer.score_body`, `rules.primer.highlight` and `rules.must_follow`.

## Consequences

- A new player's first match teaches calling, following suit, trumping with spades, winning a trick and
  scoring in the first hand, then plays on. Every other match is unchanged.
- The first practice match's first hand is the same for everyone; its rounds 2-5 are fair deals from the
  match's seed, without ADR 056's friendlier choice of seed.
- A practice match left on a phone by an earlier build is still saved and offered once; starting a new
  match replaces it.
- `ISettingsService` gained `TutorialDone` / `MarkTutorialDone`; `MatchLaunchRequest` gained `Tutorial`
  and `Practice`; `OfflineMatchSession` exposes `IsPractice`;
  `BiddingPanelView` exposes its chips' and Confirm's rects for the pointer.
- Not added: a "Replay tutorial" entry in How to Play (UI the owner did not ask for; the launch request
  makes it a small change if wanted).
