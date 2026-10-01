# ADR 025 - Online play in your own deck, and no bot labels

**Status:** accepted (owner, 1 Oct 2026).

**Context.**
- **The deck.** ADR 020 put every table in one deck: offline the player's own, a private room the
  host's, and Quick Match a deck the hosting device drew from the catalog ("any card can appear").
  The owner wants Quick Match in the deck the player chose, like offline and their own rooms.
- **The bot labels.** GDD §9 marks a bot that fills a seat of an online table:
  - the Quick Match search shows "BOT" under a bot it found, where a person shows "Ready";
  - at the table such a seat carries a small "BOT" chip.

  The owner wants online opponents shown as players: no bot label while finding players, nor in
  the game.

**Decision.**
- **Quick Match plays in the player's own deck:** every face and every back of every seat, as
  offline and in a room the player hosts. A private room still plays in its host's deck (ADR
  020). Quick Match's table no longer draws a deck, so the room host takes no table deck any
  more: a room's tables are always in its host's deck.
- **The Quick Match search shows every player found as "Ready"**, a bot that fills the table
  too.
- **At the table a bot that fills a seat has no chip.** The chip only says how a person is
  connected: "Away", "Reconnecting…", and "Bot playing" once a bot has taken over a person's
  seat for good (GDD §8), so friends still see that someone left.

**Consequences.**
- GDD §9 ("Online fill-ins show a small BOT tag") and ADR 020's Quick Match deck describe the old
  behaviour until they are updated.
- The engine and the online protocol still know which seats are bots (the bots play them, and the
  analytics count them); only what is shown changed.
- The "BOT" string (`ui.matchmaking.seat_bot`) is no longer shown; it stays in the UI table until
  the owner decides to drop it.
- Tests: the Quick Match deck test now checks every deck the player can choose; the chip test lost
  its BOT case; a new play test checks that a search's bots read "Ready"; the table simulation
  checks that an online table's bots carry no chip.
