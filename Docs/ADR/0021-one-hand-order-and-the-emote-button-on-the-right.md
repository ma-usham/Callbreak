# ADR 021 - One hand order, the emote button on the right, a gentler hand arc

**Status:** accepted (owner, 1 Oct 2026).

**Context.** GDD §2 lets the player sort the hand two ways, suits ♠ ♥ ♦ ♣ (the default) or
alternating colours ♠ ♥ ♣ ♦, switched by the Sort button in the table's bottom-right corner
(screen 08). The emote button sits in the bottom-left corner, above the local avatar. Mockup 08
draws the hand on an arc whose end cards lean about 10°. The owner asked for three changes.

**Decision.**
- **One hand order.** The hand is always ♠ ♥ ♣ ♦, so the colours alternate; within a suit the
  highest card comes first, as before. The table has no Sort button, and nothing switches the order.
- **The emote button on the right.** It sits where mockup 08 draws the Sort button (centred on the
  same point, at its own size), and the phrase picker opens beside it, 112 mockup px in from the
  right edge, the mirror image of where it opened on the left. The local seat's bubble stays above
  its name plate.
- **A gentler arc.** The hand's arc radius is doubled, from the 3740 measured on mockup 08 to 7480:
  a full hand's end cards lean 5° instead of 10° and hang 28 px below the middle card instead of
  57. A smaller hand leans less, as before.

**Consequences.**
- The saved sort order (`ISettingsService.SortOrder`, `HandSortOrder`) is still read and written,
  but nothing uses it. Removing it, with its tests, changes the settings API and the save file, so
  it is left for the developer.
- The Editor's table preview lays the mockups' sample hands out in the game's order, so captures
  of the table and the call panel differ from mockups 07 and 08 where those draw ♦ before ♣, and
  in the two bottom corners.
- GDD §2 (hand sorting), §11 screen 08 (the Sort and emote buttons) and §14 ("Sort hand")
  describe the old table until the GDD is updated. The slide §14 gives the sort still moves the
  cards when the hand closes up after a play.
- The table's plate did not change. `Tools/art job Table` regenerates a plate that differs from
  the committed `BG_Table.png`, so only its `layout.json` was kept.
