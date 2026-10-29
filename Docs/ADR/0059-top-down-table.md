# ADR 059 - The table seen from straight above: the trick's cards lie upright and flat

**Status:** proposed (29 Oct 2026). The owner: "can you make the game play in top down view like in
other callbreak game. right now the thrown cards are rotated. check that once."

## Context

Every trick card already lay straight (ADR 024: no turn). But a card lying on the table was drawn in
the table's perspective, as mockup 08 drew it: 0.64 of its height, and its far edge narrowed to 0.9
of its width. The side cards of the trick looked turned, and the deck in the middle (ADR 022) looked
tipped away. Since ADR 058 the game is played on a flat cloth with no table, so that perspective no
longer has anything to lean on.

## Decision

- **The table is seen from straight above.** A card lying on it (in the trick, or in the deck while
  a round is dealt) is drawn as an upright card is: full height, its far edge as wide as its near one.
  - `CardView`'s `_flatHeight` and `_flatTopScale` are now 1 (the Card prefab and the code's defaults).
  - The flag that a card lies on the table (`CardPlacement.Flat`) and `CardPerspective` stay, so the
    old look is two numbers away.
- **The trick keeps its slots and sizes** (mockup 08): the four cards, upright, form a cross round the
  middle, toward the players who played them. A later card lies over an earlier one where they meet.
- **The felt shuffle's swirl is round**, no longer wider than deep.

## Consequences

- A thrown card no longer flattens as it flies from the hand to the trick.
- Test: `TopDownCardTests` (EditMode).
