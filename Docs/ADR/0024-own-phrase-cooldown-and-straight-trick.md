# ADR 024 - Your phrase by the emote button, its wait on the button, the trick cards lying straight

**Status:** accepted (owner, 1 Oct 2026).

**Context.**
- **Where your phrase shows.** The emote button sits in the bottom-right corner (ADR 021), but
  the player's own quick phrase showed in the local seat's bubble, above the name plate on the
  left.
- **The emote limit.** The limit (one every 3 s, ten per round; GDD §8) showed only inside the
  picker: within 3 s the picker opened with every phrase disabled.
- **The trick cards.** The owner found them uneven: "some are big and small, some are like
  tilted".
  - Mockup 08 and GDD §11 turn the side cards ±6°. Drawn lying in the table's perspective
    (shorter, narrower at the far edge), a turned card looked skewed and larger than the straight
    ones.
  - The winning card also grew 8% before the sweep (GDD §14 "lifts").
- **The first deal of a match.** Checking the deal, the table simulation found the bots calling
  during the first deal of a match:
  - Their wait for the deal was still the deal alone (1.6 s), though ADR 022 put a 1.1 s shuffle
    before it.
  - On a match's first round the wait counted from zero, not from the start: the session was
    started before it had been told the time.
  - Calls arriving while the table deals put it behind, and a table that is behind plays at
    double speed (GDD §6), so the first deal was sometimes rushed.

**Decision.**
- **Your phrase by the button.** The player's own phrase shows in a bubble just above the emote
  button: the seat bubble's white pill, its right edge on the button's, growing to the left.
  - It sits in the button's corner group, so it stays by the button on every screen shape.
  - The others still see the phrase over this player's seat, and the player's call still shows
    over their seat.
- **The wait on the button.** After a phrase, the emote button is covered by a see-through dark
  copy of its disc (black at 50%), which sweeps away clockwise from twelve o'clock over the 3 s
  wait.
  - Until it is gone a tap on the button opens nothing.
  - Once the round's ten phrases are used, the button stays covered until the next round.
  - It shows with Reduce motion too: it is the wait, not a decoration.
- **The trick.** Every trick card lies straight, the same size, in the same perspective. The
  winning card glows and comes to the top without growing.
- **The bots and the deal.** The bots wait 2.7 s after a round starts (the shuffle and the deal),
  offline and on hosted tables. The session is told the time just before it starts, so on the
  first round the wait counts from the match's start.

**Consequences.**
- GDD §8 (where the emote limit shows), §11 T rules (trick cards turned ±6°) and §14 (the
  winning card lifts; a 1.6 s deal) describe the old behaviour until the GDD is updated.
- The wait's colour is one constant in TableCustomizer (`EmoteCooldownColor`), for the owner to
  change if another reads better.
- An online session told the time before it starts also times its first "table ready" retry from
  the start rather than from zero.
- The table simulation checks:
  - your phrase by the button, and not over your seat;
  - the wait: full right after a phrase, about half after 1.5 s, gone after 3 s;
  - the bots' deal wait against the table's shuffle and deal;
  - when a deal takes the wrong time, how it was played (double-speed frames, the events waiting).
- The Table prefab is rebuilt from its layout; no art changed.
