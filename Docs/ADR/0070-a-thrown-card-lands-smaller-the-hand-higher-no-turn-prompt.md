# ADR 070 - A thrown card lands smaller, the hand stands higher, and there is no turn prompt

**Status:** proposed; the trick's centre moved up with the top seat in ADR 072. The owner, 2 Nov 2026, with three mock-ups of our own table (the green cloth,
our HUD and seats), "just for references for thrown cards":

> In game play when throwing a card make it a bit small so that it feels like card is being thrown
> like in real. … and move our cards a bit up and remove the tips/suggestion box from gameplay as
> well.

This replaces ADR 062's trick (full-size cards in a cross that meets in the middle, the side cards
lying sideways) and its turn prompt beside the bottom card. ADR 063 had already taken the tips out of
the prompt; now the prompt itself goes.

## Context

Since ADR 062 a played card kept its hand size (163 × 226 canvas units since ADR 068) all the way to
the trick. The four met in the middle, overlapping, the side cards turned 90°. Because the cross was
so tall, the hand hung off the bottom edge with only 147 of its 226 units on screen: the corner index
and the top pips.

The owner's mock-ups show:

- the trick at about 70% of the hand card's size;
- the top and the bottom card upright, one above the other with a sliver of cloth between them;
- the side cards upright too, beside them at the middle's height, each leaning about 8° outwards;
- the hand's whole cards on screen;
- no pill by the trick.

The turn prompt ("Your turn · Beat Q♥", "Waiting for Milan…", "Anjali wins the trick", "Asha is
calling…") was that pill.

## Decision

- **A thrown card lands at three quarters of the hand's size:** 122 × 170 canvas units
  (`TableCustomizer.TrickCardScale`, 0.75 of the hand's 0.8). `CardFlight` already carries a card's
  size with its place and turn, so a card shrinks as it flies from the hand and lands like a card
  tossed onto the cloth. An opponent's card grows from its fan to the same size. The deck in the
  middle while a round is dealt is the trick's size too.
- **The trick is a cross of cards that lie apart:**
  - The top and the bottom card stand upright, 12 units of cloth between them.
  - The side cards stand level with the trick's centre, leaning 8° towards the players who played
    them, the left one's top to the left and the right one's to the right.
  - The nearest corner of each side card is 8 units clear of the column.
  - A later card no longer covers an earlier one.
- **The trick's centre is 39 units higher** (mockup y 502.7, was 541.8). That puts it midway between
  the top seat's fan and a lifted hand card with the banner's room made: the top seat comes down 80
  units and the trick 40, as before.
- **The hand shows its whole middle card,** with 10 units of cloth under it (`HandBelow`). The
  gently bent end cards still dip off the bottom edge. The hand keeps its arc, spacing, lift and
  touch area above the cards.
- **No turn prompt.** These are gone:
  - `TablePromptView`, its pill in Table.prefab and `TurnPrompt` with its tests;
  - the `ui.prompt.*` strings and `ui.bidding.calling`.

  Without it:
  - On your turn, the playable cards glow, the others are subdued, and your turn ring and the light
    buzz say it is your turn.
  - A card that cannot be played still shakes and buzzes when tapped.
  - While another seat calls, its pill reads "Calling…".
  - The "Your turn" toast over a page covering the table (online) stays.
- **Measured on a 16:9 canvas** (units from the top):

  | | Without a banner | With the banner's room |
  |---|---|---|
  | Top seat's fan ends | 325 | 405 |
  | Top trick card | 401 to 571 | 441 to 611 |
  | Side trick cards (leaning) | 485 to 669 | 525 to 709 |
  | Bottom trick card | 583 to 753 | 623 to 793 |
  | Hand, middle card (lifted) | 844 to 1070 (830) | the same |

  The cross is 427 units wide (747 to 1174), narrower than ADR 062's 452. 20:9 and 21:9 are the
  same up and down, with more room at the sides; 4:3 has more room everywhere.

## Consequences

- A played card reads as thrown, and the hand's corner indexes and bottom pips all show.
- `TrickFitTests` checks every one of these at 16:9, 20:9, 21:9 and 4:3, with and without the
  banner's room:
  - the trick's size and lean;
  - that no trick card lies over another;
  - that the cross clears the seats, their fans and the lifted hand;
  - that there is no prompt;
  - that the whole middle hand card shows.
- The table simulation no longer checks the prompt's words.
- `SuitSpriteBuilder` and `Config/UI/SuitSprites.asset` drew the prompt's suits and are now unused.
  They are left in place for a later label that names a card.
- Departs from GDD §11 screen 08's "turn prompt copy" and ADR 040's "Asha is calling…" line.
