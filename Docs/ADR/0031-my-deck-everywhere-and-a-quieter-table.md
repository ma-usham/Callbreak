# ADR 031 - My deck everywhere, and a quieter table

**Status:** accepted (owner, 3 Oct 2026). It supersedes:
- ADR 020's table deck, and the deck bullet of ADR 025;
- the call bubble of ADR 027;
- the "Bot playing" chip and "left the table" toast for players who left;
- the "I'm back" banner;
- the Trump label of mockup 08.

**Context.** The owner tested online play on two devices and asked for these changes.

**Decision.**
- **My deck everywhere.**
  - **What the player sees:** every card on their own screen is in the deck they have equipped (shop or Card Collection). That covers their hand, the trick, the opponents' face-down fans, the shuffle pile and every card dealt, whatever deck anyone else at the table chose. It holds offline, in private rooms, in Quick Match, and after a host hand-over (ADR 030).
  - **Online:** the deck is the one the player's hello named when they joined or rejoined the room. `RoomClient` passes it to `OnlineMatchSession`, which shows every seat in it.
  - **Offline:** it is the deck the match was dealt in, as before.
  - **Drawing:** the table draws every face and back from that one deck (`TableRenderer.LocalDeck`) and reads no other seat's deck. The host still fills each seat profile with that person's deck (bots: the host's), for information only.
- **The table.**
  - **No "Trump ♠" label** in any game. "ROUND x / 5" stays.
  - **The seat pill:** before a player has called it keeps its words ("Waiting", "Choosing bid"). After the call it shows only won-bid, for example "0-3" (0 tricks won, bid 3), still gold once the call is met.
  - **No call bubble:** a call no longer pops up as a number by the seat. The quick-phrase speech boxes (ADR 027) stay.
  - **Players who left on purpose,** a host who handed over included: their seat shows no chip, and nobody gets a "left the table" toast. A bot plays their cards quietly. Away and Reconnecting, which are temporary, keep their chips.
  - **Reconnecting** shows the loading screen's emblem (mockup 17): the turning ring around the still spade, with "Reconnecting…" below. It stands still with Reduce motion.
  - **No "I'm back" button:** while the player is Away, any touch on the table brings them back.
- **The match menu, online:**
  - The first button reads "Back to Game", because an online game can't be paused. Offline it stays "Resume Game".
  - The mode tag reads just "Online game". "Online match · Game continues" overflowed its pill.
- **Settings:**
  - The Settings page no longer draws its own gear in the header. Mockup 11 has it, but it did nothing, and it sat exactly where the table's and the menus' gear is, so it looked as if the table's gear showed through the open page.
  - This is a deliberate deviation from mockup 11.

**Consequences.**
- **Protocol:** unchanged (version 6). Deck ids still travel in the hello, the lobby snapshot and the seat profiles, but nothing on screen reads another player's deck, so a later protocol bump may drop them.
- **Changing decks mid-match:** a player who changes deck and rejoins a running online match sees the new deck. A resumed offline match stays in the deck it was dealt in ("Choose a deck for your next match").
- **The GDD is out of date here:**
  - §13 "Whose deck appears where", and §11 screen 15 ("opponents see its back");
  - the Trump line and the call bubble in §11's table description;
  - the "I'm back" banner (§8).
- **Tests:**
  - **Decks:** the private-room deck tests now expect each player's own deck. A Quick Match test seats three devices on three decks through a hand-over, and a play test checks every card on the real table against the chosen deck.
  - **Menus:** the match menu and Settings have their own play tests.
  - **The table simulation's checks** follow the new pill, the missing bubble and label, the chips and the Away touch.
