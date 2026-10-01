# ADR 020 - One deck per table

**Status:** accepted (owner, 1 Oct 2026).

**Context.** GDD §13 ("Whose deck appears where") lets every player see the faces in their own
deck, and each opponent's face-down cards in the back of the deck that opponent chose; bots use a
random back. Only the deck id travels over the network (§25), one per seat. The owner replaced
this rule: a table is played with one deck.

**Decision.** Every card at a table, every face and every back for every player, is in one deck,
the table deck:
- **Offline** against bots: the player's selected deck (`ICosmeticsService.SelectedDeckId`).
  Bots no longer draw a deck of their own. A resumed match stays in the deck it was dealt in,
  even when the player has selected another since, and so does a match saved before this
  decision, whose bots still had backs of their own.
- **Private room** with friends: the host's selected deck, for every seat, the ones filled with
  bots included, in every match the room plays (Play Again) and for a player who rejoins. Guests
  see it for all faces and backs, whatever deck they selected themselves.
- **Quick Match** ("any card can appear"): the hosting device draws one deck at random from the
  deck catalog (`ICosmeticsService.DeckIds`), the same for all four seats.

Whoever deals decides the deck when it builds the seat profiles (the device offline, the host
online), and every seat's profile carries it (`SeatProfile.DeckThemeId`). The wire format and the
protocol version are unchanged, and the table draws as before: faces from the local seat's deck,
each fan's backs from its seat's deck.

**Consequences.**
- A guest's own selection only matters when that player hosts, plays offline, or looks at the
  main-menu fan and Card Collection. The lobby still carries each member's own deck; at the
  table only the host's is used.
- GDD §13 "Whose deck appears where", §11 screen 15 ("opponents see its back") and the overview's
  deck sentence describe the old rule until the GDD is updated. Card Collection's copy ("Choose a
  deck for your next match.") is unchanged; it is the owner's to revise.
- A host on a build from before this decision speaks the same protocol and still gives each seat
  its own deck. Nothing breaks: its guests see the old rule at that table.
