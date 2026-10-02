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

## Addendum, 3 Oct 2026: the owner's answers, and who can come back

**The owner's answers** to the questions this ADR left open:
- **Not back in time:** a seat whose player did not return within the 90 s hold
  (`BotControlled`) shows no chip either. "Bot playing" is gone; `ui.seat.bot_playing` is now
  unused, like `ui.banner.im_back` and `ui.toast.left_table`.
- **Gold:** a seat's pill turns gold once its call is met, whoever plays it. That includes a
  player who left, one not back in time, and an Away or reconnecting seat. This replaces "only for
  a player who is at the table".
- **Offline deck:** a resumed offline match stays in the deck it was dealt in, as above.

**Pills that fit their text** (the owner: the won-bid pill was "quite big" for "3-7"):
- **The seat pill** fits its text: the text's width plus 23 on each side (mockup 08's margin),
  never less than 84 (canvas units at 1920×1080).
  - Every one-digit won-bid line ("0-3", "3-7") gets the same 84-wide pill, where the pill used to
    be a fixed 173.
  - "10-8" is 89 wide, "Waiting" 119, "Calling…" 130 and "Choosing bid" 167.
  - Its height, colours, gold, "Won +1" pulse and centre under the name plate stay.
- **The match menu's mode tag** fits its text too, with mockup 13's margins (24.5 on each side).
  "Online game" is 183 wide; it used to be 292, the width drawn for "Offline match · Paused",
  which keeps it.

**Who can come back.** The owner asked what happens when a player swipes the game away. A seat
should say "Reconnecting…" only if its player can really come back; otherwise a bot should take
over at once, as for a player who left.
- **What a swipe does:** the host loses that player's connection. It holds the seat for 90 s
  (GDD §8), and a bot plays it from the first moment, so nothing waits. A killed app looks exactly
  like one in the background or out of signal, so nobody can tell them apart.
- **Private rooms: anyone can come back,** so the seat says "Reconnecting…" as before.
  - A guest's next launch already offered "Rejoin your match?" (GDD §27).
  - **New:** the host keeps a rejoin ticket too, whenever its match would be handed over (ADR 030:
    over the peer network, with another person at the table). Its relaunched app rejoins the new
    host as a player, in its own seat. This lifts ADR 030's known limit "a killed host app keeps no
    rejoin ticket" for private rooms.
  - A host alone with bots keeps none, because its match dies with its app.
  - A host who leaves on purpose (Leave, or its app closing normally) keeps none. Every device now
    drops its ticket the moment its room closes, not a tick later.
  - **Stats:** a killed host that never comes back now has the match recorded as left, like a
    guest (GDD §8: leaving costs the match).
- **Quick Match: nobody can come back after a kill.** The app keeps no ticket there, and a code
  lookup doesn't find quick tables. So a dropped seat shows nothing: no chip, and no "Welcome back"
  when its player returns. A player whose app was only in the background, or out of signal, still
  takes the seat back within the hold. Away keeps its chip.
- **Where:**
  - `SeatStatus.Shown(connection, mode)` decides what the others see; the table's chips and its
    welcome toast both go through it.
  - `RoomService.TrackRejoinTicket` and `GoesOnWithoutThisHost` decide who keeps a ticket.
- **Fixed on the way:** when a private room's hand-over gave up (the host and the next in line
  lost together), `RoomService.KeepTheMatch` threw a NullReferenceException once.

**Tests:**
- `TurnPromptTests`: no chip for a seat not back in time; gold whoever plays the seat; Quick Match
  shows no Reconnecting chip.
- Hand-over scenarios in `RoomServiceTests.HandOver`:
  - a killed host relaunches and plays on from its own seat to the end;
  - a host who leaves or closes its app keeps no ticket;
  - a host alone with bots keeps none;
  - a paused host back as a player keeps its ticket fresh;
  - the give-up without a fault.
- The table simulation checks every chip in both modes and the gold of seats that left. After
  every event it also checks that each pill fits its text, keeps its centre and stays on screen,
  and it checks the mode tag offline and online.
- Play tests: `SeatPillTests` plays a real match and measures every seat's pill; the match menu
  tests check that the tag fits its text.

**Not verified on devices:**
- an Android swipe and relaunch;
- whether EOS lists the new host's generation before a very fast relaunch;
- whether an Android swipe still closes the app normally (then the host counts as leaving on
  purpose: its seat Left, no ticket).

**The GDD is out of date here:** §8 "What others see" ("Bot playing", the gold only for a player at
the table), and §27, where only a guest keeps a ticket.
