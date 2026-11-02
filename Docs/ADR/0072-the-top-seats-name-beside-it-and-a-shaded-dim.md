# ADR 072 - The top seat's name and call beside its avatar, and a subdued card in shade

**Status:** accepted (owner, 2 Nov 2026), with a screenshot of the table in the Editor:

> here make the top player name and bid on the left side so you can move that upper player slightly
> upwards.

And, of the hand on the player's turn: "also look at the cards like when throwing other cards get
blocked but the color isnt looking good fix that too."

## Context

- **The top seat was a column:**
  - its name plate on top, its status pill (the call: "1-3", "Waiting", "Calling…") under it;
  - the avatar below them, and its fan of backs below the avatar.

  That column put the fan's foot 325 canvas units from the top of a 16:9 screen (405 with the
  banner's room). The trick (ADR 070) and the call panel had to fit under it.
- **A card that cannot be played** got mockup 08's subdued overlay: `#BF9A8B` (a pinkish beige) at
  20%. It turned white paper a pinkish grey and dulled the pips. It read as a tint, not as a card
  you cannot play.

## Decision

- **The top seat's name and call stand left of its avatar.**
  - The name plate's top is level with the top of the avatar's timer ring, its right edge 6 mockup
    px short of the ring.
  - The call's pill sits under the name plate, centred under it as on the other seats (the owner,
    after a first try with the two right-aligned: "the bid block on asha isnt in the middle of name
    like in other player"). Its words ("Waiting", "Calling…", "1-3") are never wider than the
    plate, so it stays clear of the avatar.
- **The avatar takes the name plate's old place.** Its timer ring's top is where the plate's top
  was. The whole seat therefore stands about 70 canvas units higher and still clears the banner's
  slot when the table makes room for it.
- **Around the avatar:**
  - the seconds disc moves to its right;
  - the speech box stands right of the disc, its foot just below the avatar's centre, above the
    fan's right end and below the banner's slot;
  - the "auto" bubble moves left of the name and the call, past their widest;
  - the seat chip stays on the avatar's bottom edge.
- **The fan hangs 8 mockup px lower** (its turning point 155.5 px above the avatar's centre, was
  mockup 19's 163.7). Its left end curls up beside the avatar and would otherwise touch the call's
  pill.
- **The trick moves up into the room this makes.** It still stands midway between the top seat's
  fan and a lifted hand card with the banner's room made, now about the middle of the screen
  (mockup y 472.2, was 502.7).
- **A subdued card is the same card in shade.** The overlay is a warm near-black, `#2B211B` at 40%.
  In linear light this leaves white paper a neutral grey about 80% as bright, with red pips still
  red. GDD §13's "warm-dark overlay, never transparency" stands. Card.prefab's overlay is patched in
  place (`TableCardBuilder.ApplyDim`), so nothing else in the prefab changes.
- **Measured on a 16:9 canvas** (units from the top):

  | | Without a banner | With the banner's room |
  |---|---|---|
  | Top seat's timer ring | 81 to 194 (was 151 to 264) | 161 to 274 |
  | Its name plate, call | 81 to 118, 118 to 159, left of the ring | 161 to 198, 198 to 239 |
  | Its fan ends | 264 (was 325) | 344 (was 405) |
  | Top trick card | 366 to 536 | 406 to 576 |
  | Side trick cards | 450 to 634 | 490 to 674 |
  | Bottom trick card | 548 to 718 | 588 to 758 |
  | Hand, middle card (lifted) | 844 to 1070 (829) | the same |

## Consequences

- The top player sits higher. The trick and the call panel have more cloth around them, with about
  100 units each side of the trick without a banner and 60-70 with one.
- In the top seat's fan, the leftmost cards lie close under the call's pill. The tests keep every
  card clear of it.
- Tests:
  - `TrickFitTests.TheTopSeat_HasItsNameAndCallLeftOfItsAvatar_AndStandsWhereItsNameDid` (16:9,
    20:9, 21:9 and 4:3, with and without the banner's room): the arrangement, the pill centred under
    the name and clear of the avatar with each of its words, and the disc, the speech box and the
    bubble clear of each other, of the fan, of the logo and the left column, and below the banner's
    slot;
  - `CardDimTests`: a dark, warm overlay; white paper stays a neutral grey 75-85% as bright; red
    pips stay red.
- `TableBannerRoomTests` holds unchanged.
