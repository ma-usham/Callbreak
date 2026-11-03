# ADR 087 - A banner clear of the top seat; a larger, higher trick

**Status:** accepted (owner, 4 Nov 2026).

## Context

The table reserved the whole `BannerSlot` by dropping the top seat 80 canvas units and the trick 40. The standard AdMob banner occupies only a 320 × 50 dp rectangle at the slot's top-right. On most wider layouts the top seat can stay at home; on a narrow phone the banner can touch its avatar. The owner asked to keep the top player at its original height, move it sideways only where needed, enlarge thrown cards a little, and raise the trick to open space above the hand.

## Decision

- At table opening, use the ad network's expected 320 × 50 dp screen-pixel size to place its rectangle at the slot's top-right. The full top seat moves left only far enough to leave 8 canvas units from that rectangle. It never moves down; the trick never moves for the banner.
- The top speech box is built to the left of the fan, below the round line, so it can grow leftwards without entering the banner. The call bubble is lower than before, leaving room for a narrow-phone slide. The full 13-card fan is measured from its card placements, not its larger container.
- The slide keeps 48 canvas units from the logo and round pill. If the banner or seat cannot fit, the table has no banner slot for that match. This is a safety fallback, not a change after an ad loads.
- Thrown cards grow from 0.75 to 0.8 of the hand card (about 150 × 208 canvas units). The trick centre rises 26 mockup pixels (about 30 canvas units), leaving more space above a lifted hand card.

## Consequences

- A 1920 × 1080 canvas with a 840 × 131 canvas-unit banner leaves the top seat at home. A 960 × 150 banner shifts the seat left while its full fan stays below the banner. Wider canvases do not need a slide.
- The table decides this layout once when it opens. A banner loading or refreshing later does not move game content.
- Fit and flow tests check the banner rectangle, seat, trick, side seats and hand. Android device placement and ad fill remain to be checked on a phone.
