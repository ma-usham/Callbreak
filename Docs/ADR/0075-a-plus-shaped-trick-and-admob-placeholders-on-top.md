# ADR 075 - A "+"-shaped trick; AdMob's Editor placeholders over the game

**Status:** accepted (owner, 3 Nov 2026); its Editor placeholder fixes superseded by ADR 077 the same day:

> why even after enabling test abd the place holder for banner ads doesnt shows up in editor play
> mode. and the thrown card at any angle kinda looks like something is off lets do one thing the
> thrown card of left and right will be rotated on 90 like we did before so this time the thrown card
> will look like "+" sign

## Context

- **The trick.** ADR 070 leaned the side cards 8° outwards. The owner found any angle looked off.
- **The placeholder banner in the Editor.** ADR 073 has the Editor use AdMob with Google's test
  units. The log said "Banner ads: a banner is ready", but no banner showed. In Play mode,
  `BANNER(Clone)` was there, with two problems:
  - It was on a canvas of its own at sorting order 0, under the game's screens (sorting order 10).
    So were all of the plugin's placeholders: the banners, the interstitials, the rewarded and app
    open ads, and the consent form.
  - It was placed below the screen. Our banner places itself in screen pixels divided by AdMob's
    device scale. In the Editor that scale is 1, while the placeholder's canvas scales a 1280 × 800
    design to the screen's height (1.41 on a 2436 × 1125 simulator).

## Decision

- **The side trick cards lie sideways** (turned 90°, their tops towards the players who played them),
  8 units clear of the column of the top and the bottom card. The four make a "+".
  - Size, centre and gaps are as in ADR 070 and 072.
  - The cross is 478 units wide on a 16:9 canvas (was 427), clear of the side seats.
- **In the Editor, every Google placeholder is put over the game as it appears**
  (`Editor/AdMobPlaceholdersOnTop`: its canvas to sorting order 30000), as AdMob's own views lie
  over everything on a phone.
- **In the Editor, the banner is placed in its canvas's units:** screen pixels divided by the
  screen's height over 800 (`AdMobBannerAdNetwork.PlacementScale`). Phones keep AdMob's device scale.

## Consequences

- Play in the Editor shows Google's placeholder banner in the table's and the menu's slots, and the
  full-screen placeholders over the game, where they can be closed.
- `TrickFitTests` checks the side cards turned 90° and a "+" no wider than a card and two sideways
  cards.
- Found on the way, not changed here:
  - **The hand's minimum strip.** The hand takes a 7 mm strip per card from the screen's dpi, so in
    the Editor's Device Simulator (a phone's dpi), and on 16:9 phones, the hand spreads to 86% of
    the width.
  - **The local name plate.** Since ADR 070 raised the hand, its leftmost card then reaches the local
    seat's name plate. `TrickFitTests`' hand test fails in the Device Simulator for that reason, and
    passes in the plain Game view (96 dpi).
