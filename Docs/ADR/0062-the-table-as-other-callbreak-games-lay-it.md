# ADR 062 - The trick as other Callbreak games lay it: full-size cards in a cross that meets in the middle

**Status:** proposed (30 Oct 2026). The owner: "in game play why doesnt my game fell like other
callbreak games?? look at theirs how cards being renderer and in my the thrown cards get small. can you
fix that?? ... if you reed to remove that ADR or something lets do that as well". With a screenshot of
another Callbreak game: "here is how other games card are shown same size evven after throwing".

The owner made three choices along the way:

- Asked whether the trick's side cards should lie sideways as in that screenshot, or all stand upright
  as ADR 059 had them, they chose "sideways, like the reference".
- On a first version with a straight hand and no opponent fans: "make the hand carb slightly bend like
  before".
- And: "you also removed the other players cards that used to be shown there how that as well. like
  before".

This replaces ADR 060 and ADR 059's "the trick keeps its slots and sizes". The hand's gentle arc (ADR
021) and the opponents' fans (ADR 026) stay.

## Context

The owner's screenshots of other Callbreak games show a trick of cards as large as the hand's, in a
cross that meets in the middle: the top and the bottom card upright, the side cards lying sideways
towards the players who played them.

Our table drew a played card at half its size: 196 x 308 canvas units in the hand, 99 x 155 in the
trick. ADR 060 had shrunk the trick to fit four cards apart under the top seat's fan, so a card shrank
as it flew from the hand to the trick.

## Decision

- **The trick's cards are the hand's cards.** They are drawn 9% larger than the shared card (the
  owner, 2 Oct 2026), so a played card keeps its size from the hand to the trick. The deck in the
  middle while a round is dealt (ADR 022) is the same size.
- **The trick is a cross that meets in the middle** (`TrickAreaView`'s slots, built by
  `TableCustomizer.BuildTrickArea`):
  - The top and the bottom card stand upright, 82 canvas units above and below the trick's centre.
    They lie over each other there, among the side cards. That is as tall as the table has room for
    between the top seat's fan and a lifted hand card.
  - The side cards lie sideways (turned 90°), their tops towards the players who played them, their
    inner ends meeting at the centre.
  - A later card lies over an earlier one where they meet, as before. Each card's outer corner index
    always shows.
  - The cards are still seen from straight above (ADR 059: full height, not narrowed).
- **The hand makes room below.** It keeps its gentle arc and spacing (ADR 021, mockup 08), now
  centred under the trick, and hangs from the bottom edge.
  - 175 of the middle card's 308 units show: the corner index and the top pips. The end cards hang
    28 lower.
  - A legal card still rises 14.4 units.
  - The touch area reaches 35 units above the middle card.
- **The turn prompt stands right of the trick's bottom card**, 24 units past it, 290 units above the
  bottom edge, and grows to the right with its words. The prompt above the hand would lie over the
  bottom card. Even the longest English tip stays clear of the right seat, its fan and the emote
  button.
- **The seats, their fans and the banner's room are unchanged.** The top seat stands where ADR 060 put
  it, and with the banner's room (ADR 051, 057) it comes down 80 units and the trick 40.
- **Measured on a 16:9 canvas** (units from the top):

  | | Without a banner | With the banner's room |
  |---|---|---|
  | Top seat's fan ends | 325 | 405 |
  | Top trick card | 372 to 680 | 412 to 720 |
  | Side trick cards | 510 to 706 | 550 to 746 |
  | Bottom trick card | 536 to 844 | 576 to 884 |
  | Turn prompt | 768 to 812, right of the bottom card | the same |
  | Hand, middle card (lifted) | from 905 (890) | from 905 (890) |

  The cross is 616 units wide (652 to 1268), clear of the side seats' fans. 20:9 and 21:9 are the
  same up and down, with more room at the sides; 4:3 has more room everywhere.

## Consequences

- A thrown card keeps its size, and the trick looks as the owner's screenshots show it.
- The trick's cards are drawn at 196 x 308 from 246 x 386 textures, so they stay sharp. No new art
  was needed.
- The cross is tight with the banner's room: 7 units of cloth under the top seat's fan and 6 above a
  lifted hand card. A taller cross (the top and the bottom card overlapping less) would need one of:
  - the top seat's fan beside its avatar instead of below it;
  - a smaller top seat;
  - the banner elsewhere.
- To stand the side cards upright again, change `TableCustomizer.TrickTurns` and the side slots.
- `TableCustomizer` builds the cross from the hand's card and the prompt beside the bottom card. The
  Table prefab is rebuilt from it.
- Tests:
  - `TrickFitTests` (EditMode) now checks a full trick at 16:9, 20:9, 21:9 and 4:3, with and without
    the banner's room:
    - each card is the hand's size and points at its player, and the four meet in the middle;
    - the top card is below the top seat and its full fan;
    - no card touches a side seat or its fan, or lies under the prompt;
    - the bottom card and the prompt are above the hand with a card lifted.

    It also checks that the hand is still a 5° arc, centred under the trick and clear of the local seat
    and the emote button.
  - `TableBannerRoomTests` is unchanged (drops 80 and 40).
