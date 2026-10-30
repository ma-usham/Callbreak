# ADR 060 - The trick fits the table: four smaller cards apart, the top seat higher, the hand lower

**Status:** proposed (30 Oct 2026); superseded by ADR 062 (the trick of full-size cards). The owner: "while playing the card gets overlap. either reduce the
card size or make move the player card quite below and move the top player quite upside so that the
card fits."

## Context

Since the table is seen from straight above (ADR 059), a trick card is drawn full height: 147 x 231
canvas units, where mockup 08's slots were laid out for cards 0.64 as high. On a 16:9 canvas:

- the top and the bottom trick card lay over each other by 54 units;
- the top card touched the top seat's fan of backs;
- the turn prompt ("Waiting for Asha...", "Asha wins the trick") lay over the bottom card.

While a banner may show (ADR 051, 057: every player without No Ads), the top seat and the trick come
down to clear the banner's slot, and the top card covered the bottom of the fan by 32 units. That is
the tightest case: the top seat must stay below the slot, so the trick has the room between the top
seat's fan (405 units from the top) and the turn prompt above the hand.

## Decision

This replaces ADR 059's "the trick keeps its slots and sizes".

- **The four trick cards form a cross with a gap between them, none over another.**
  - The top and the bottom card stand one above the other, and the side cards stand between them,
    toward the players who played them, 10 canvas units apart.
  - The cross is centred on the table's middle (`TrickCentre`). The deck lies there too while a
    round is dealt.
- **The trick's cards are a third smaller:** 98.75 x 155 canvas units (`TrickCardWidth` 86 mockup
  px, was 128.4). With the banner's room made, that is the largest card that fits four apart
  between the top seat's fan and the turn prompt.
- **The top seat stands 28 canvas units higher** (name plate, status pill, avatar, fan).
- **The hand and the turn prompt above it hang 20 units lower.** The hand's cards keep their size,
  and 250 of their 308 units still show.
- **The banner's room:** the top seat now comes down 80 units (was 52), to where it stood with the
  room before, below the banner's slot. The trick comes down 40 (was 25).
- **Measured on a 16:9 canvas** (units from the top):

  | | Without a banner | With the banner's room |
  |---|---|---|
  | Top seat's fan ends | 325 | 405 |
  | Top trick card | 375 to 530 | 415 to 570 |
  | Bottom trick card | 541 to 696 | 581 to 736 |
  | Turn prompt | 746 to 790 | 746 to 790 |
  | Hand (middle card) | from 828 | from 828 |

  20:9 and 21:9 are the same up and down; 4:3 has more room.

## Consequences

- `TableCustomizer` builds the cross from the card's size and the gap. `TableView`'s banner drops are
  80 and 40.
- Tests: `TrickFitTests` (EditMode) checks a full trick at 16:9, 20:9, 21:9 and 4:3, with and without
  the banner's room: the four cards lie apart; the top one is below the top seat and its full fan;
  the side ones clear the side seats; the bottom one is above the turn prompt and the hand.
  `TableBannerRoomTests` takes the new drops.
- Bigger trick cards would need the hand lower still, or the top and the bottom card over each other
  again.
