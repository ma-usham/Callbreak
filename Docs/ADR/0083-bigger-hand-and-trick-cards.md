# ADR 083 - Bigger hand and trick cards

**Status:** accepted (owner, 3 Nov 2026):

> increase the hand cards and thrown cards size by small amount. so that it looksnice. if required
> move the hand card little upward as well.

## Context

- **Card sizes.** The hand's cards were drawn at 0.8 of the shared card, 163 × 226 canvas units (ADR
  062's addendum, after the owner found 9% larger than the shared card "way too big"). The trick's
  cards were 0.75 of the hand's, 122 × 170 (ADR 070).
- **The hand's spread.** The hand keeps a 7 mm strip of each card visible, and on a phone's pixel
  density that strip decides its width: it may spread up to 86% of the safe area (the GDD's figure)
  to keep it.
  - With bigger cards at 86%, the hand's first card reached the local seat's avatar and name plate in
    the bottom-left corner on 16:9 and 4:3 screens.
  - The widest strip was 0.8 of a card's width. Bigger cards widened the hand on wide phones, and its
    end cards leant just over 7°.

## Decision

- **The hand's cards are 0.86 of the shared card**, 175 × 243 canvas units (7.5% larger). The
  trick's cards stay 0.75 of the hand's, so they grow the same: 131 × 182. The trick's centre and
  gaps are unchanged.
- **The hand spreads to at most 80% of the safe area** (`HandMaxWidthFraction`, was the GDD's 86%), so
  its first card stays clear of the local seat.
- **The widest strip stays 130 canvas units** (`HandMaxStrip`), as it was at 0.8. The larger cards
  overlap a little more instead of spreading further.
- **The arc is a touch flatter:** a radius of 7600, was 7480. The widest hands' end cards still lean
  under 7°.
- **The hand does not move up.** Its whole middle card already shows, 10 units above the screen's
  bottom edge, and it grows upwards. The trick stays well clear of a lifted card, with the banner's
  room too.

## Consequences

- **Cards are a little over a fifth of the screen's height** (0.225 at 20:9). `TrickFitTests`' size
  check now expects 0.21–0.24 in place of 0.19–0.22, the owner's new size. Its other checks pass
  unchanged:
  - the trick clear of the seats and the hand;
  - the hand clear of the local seat and the emote button;
  - the arc's lean;
  - the rank index readable.
- **On phones where the 7 mm strip can't fit in 80%,** the strip comes out narrower (never under the
  44-unit hard minimum). On a 16:9 1080p phone it is about 113 canvas units, close to 7 mm.
- **Tests:** EditMode 2600/2600. Table.prefab is rebuilt by `ScreenLayoutBuilder.Build("Table")`.
- **Not checked on a phone.** PlayMode not run.
