# ADR 086 - Hand and trick cards a little bigger again

**Status:** accepted (owner, 4 Nov 2026):

> can you again increase the size of hand cards and thrown cards as well?? by small amount will that
> affect anything??

> increase thrown card size as well by small.

## Context

- **Since ADR 083** the hand's cards are 0.86 of the shared card, 175 × 243 canvas units, and a thrown
  card shrinks to 0.75 of the hand's, 131 × 182 (ADR 070: smaller than the hand, "so that it feels like
  card is being thrown like in real"). The hand spreads to at most 80% of the safe area, with strips of at
  most 130 units.
- On 30 Oct the owner found cards 9% larger than the shared card "way too big" (ADR 062).

## Decision

- **The hand's cards are 0.92 of the shared card**, 187 × 260 canvas units (+7%). `HandCardScale` in
  `TableCustomizer`, and Table.prefab rebuilt by `ScreenLayoutBuilder.Build("Table")`.
- **The thrown cards grow by the same 7%:** they stay 0.75 of the hand's, 140 × 195, so they still look
  thrown. The trick's centre and gaps are unchanged.
- Nothing else moves: the hand's 80% width, its 130-unit strips, the arc and the trick's place are ADR
  083's.

## Consequences

- **A card is about a quarter of the screen's height** (0.241 at 1080 canvas units). `TrickFitTests`' size
  check now expects 0.22–0.26 in place of 0.21–0.24. Its other checks pass unchanged at 16:9, 20:9, 21:9
  and 4:3, with and without the banner:
  - the trick clear of the seats and the hand;
  - the hand clear of the local seat and the emote button;
  - the arc's lean under 7°;
  - the rank index readable.
- **The hand's cards overlap a little more:** its width is capped, so each card shows about the strip it
  did. The middle card stands 17 units taller.
- **The tutorial (ADR 084) is unaffected:** its pointer follows the card it points at, and its caption
  card sits top-right, clear of the larger trick.
- **Build size is unchanged:** the same card sprites are drawn larger, and still from larger art.
- **Tests:** TrickFitTests 21/21, EditMode 2623/2623. Off-screen captures at 16:9 and 20:9 compare it
  with ADR 083. PlayMode not run. Not checked on a phone.
