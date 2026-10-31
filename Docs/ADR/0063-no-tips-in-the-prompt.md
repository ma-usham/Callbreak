# ADR 063 - No tips in the turn prompt

**Status:** accepted (owner, 30 Oct 2026). The owner: "remove that tips in game play that comes in
the middle."

## Context

GDD §15's first-time experience made the turn prompt explain the rule behind the turn for a
player's first three matches: "Tip: you must beat the highest heart if you can.", "Tip: you can't
follow or win this trick, so any card will do." and two more (TurnTips, since 3912cb5). Since ADR
062 the prompt stands right of the bottom trick card, in the middle of the table, and a tip is
twice as long as the plain prompt.

## Decision

- The prompt never shows a tip. On the local turn it names the turn in every match, as it did
  after the third: "Your turn · Beat Q♥", "Your turn · Follow ♥", "Your turn · Trump with a spade",
  "Your turn · Any card", "Your turn · Lead any card".
- Everything else the prompt says stays: "Waiting for Asha…", "Asha is calling…" (ADR 040),
  "Asha wins the trick", "You lead".
- TurnTips and its tests are deleted, with the strings only it used (`ui.tip.*`, `ui.suit.*`).
- The completed-match count stays: the first offline match still deals friendlier hands (ADR 056).
- How to Play (GDD §11 screen 12) is unchanged.

## Consequences

- Departs from GDD §15's first-match tips. A new player learns the rules from How to Play, the
  prompt's obligation, and the glow on the cards that can be played.
- TrickFitTests now fits the widest prompt the table can show: a 16-letter name (the longest a
  profile takes) of W's winning the trick.
- The table simulation checks that every local turn's prompt is the plain prompt, the first match
  included.
