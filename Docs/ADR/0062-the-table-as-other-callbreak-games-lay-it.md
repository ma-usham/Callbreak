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

## Addendum, 30 Oct 2026: a smaller card, its corner index larger

The owner, on this table: "still not good the game card feels way too big. try to reduce the size of
card in figma and then use the reduced size here in game", and "play the game and you can fell the cards
size doesnt suit the game".

Played on our own table, a card 308 units high (28% of the screen's height) was three times an avatar's
height and crowded the cross between the seats. At 89% of the shared card it still did; at 72% the table
looked empty. At 80% the cross sits among the seats with cloth around it.

- **The hand's, the trick's and the deck's cards are 144 x 226 canvas units**, 80% of the shared card
  (`TableCustomizer.HandCardScale` 0.8, was 1.09): a fifth of the screen's height on a 16:9 or wider
  phone. Hand and trick stay one size, so a played card still keeps its size.
- **The corner index is 1.25 times larger on every card**, so it reads at the smaller size about as
  before. The smallest rank, the 10's, stands 19.3 canvas units tall (20.7 before); an A stands 23.
  - In Figma's card set ("V1 Rebuild · Cards", Card 179:2678), each card's Index and Index Turned
    frames are 1.25 times larger and tucked 3.5 units further into their corners. The 10's tracking is
    -5 px, so it stays clear of the pips.
  - The court panel, its figures and its divider are 6% smaller about the card's centre, clear of the
    larger index.
  - The queens' index pip sits 4 units lower, below the Q's tail.
  - The faces are cut again with `Tools/ArtPipeline/NewUI/cut_cards.py`; the bases and Classic
    Crimson's back come out unchanged.
- **The cross meets in the middle**: the top and the bottom card stand half a card from the centre
  (`TrickColumnStep`), touching there as the side cards do. The cross stands midway between the top
  seat's fan and a lifted hand card with the banner's room made (`TrickCentre` 541.8 mockup px down).
- **The hand shows 65% of its middle card** (147 units). It keeps its width (77% of the safe area,
  ADR 021), so each card shows 111 of its 144 units beside its neighbour. It also keeps its arc: the
  end cards lean 5° on 16:9.
- **The turn prompt** stays right of the bottom card, its middle 289 units above the bottom edge.
- **The opponents' fans are unchanged.** Their backs are already 33 x 52 units (ADR 026), less than a
  quarter of a card at the table.
- **Measured on a 16:9 canvas** (units from the top):

  | | Without a banner | With the banner's room |
  |---|---|---|
  | Top seat's fan ends | 325 | 405 |
  | Top trick card | 396 to 622 | 436 to 662 |
  | Side trick cards | 550 to 694 | 590 to 734 |
  | Bottom trick card | 622 to 848 | 662 to 888 |
  | Turn prompt | 769 to 813, right of the bottom card | the same |
  | Hand, middle card (lifted) | from 933 (918) | from 933 (918) |

  The cross is 452 units wide (734 to 1186). With the banner's room there are 31 units of cloth under
  the top seat's fan and 30 above a lifted card, where there were 7 and 6.
- Figma: section "Card Size · 30 Oct" (300:383515) on "V1 Rebuild · Screens". It holds 08 Gameplay with
  the cards where the game lays them, and today's card beside the smaller one.
- Tests: `TrickFitTests.TheTablesCards_AreAFifthOfTheScreen_TheirCornerIndexStillReads` keeps a table
  card between 19% and 22% of the screen's height. It also keeps every face's rank 18 canvas units or
  taller at that size. The fit tests above hold at the new size.
