# ADR 018 - What the menu does by itself: first launch, rejoin, invite links

**Status:** proposed.

**Context.** The GDD describes four things the menu starts without a tap, in different sections:
the offer to resume an offline match (§8), "Rejoin match" after an app kill (§27, §29
`session.json`), the invite link that opens Join Room (§11 screens 00, 03, 04) and the first-time
experience (§15: primer, practice match, name nudge). It does not say what happens when two of
them apply at once, and its first-launch diagram ("How to Play, then Practice?, then Main menu")
reads differently from screen 12 ("shown automatically before a player's first match").

**Decision.**
- Order of precedence on the main menu: an invite link, then the rejoin offer, then the resume
  offer, then the first-run guide. Each is a small class of its own; a later one only acts on a
  bare main menu with no dialog open.
- First launch follows the §15 diagram: How to Play opens by itself; closing it offers an easy
  offline practice match (Skip leads to the main menu). Entering any play mode still shows the
  primer first if it has somehow not been seen, which keeps screen 12's rule true.
- Rejoin: only a guest keeps a ticket (room code and time, refreshed every minute, valid ten
  minutes). The host's room dies with its app in Phase 1, so there is nothing for it to rejoin. A
  player who comes back after the 90 s seat hold is refused ("Match already started"): the bot
  keeps the seat, as §8 says.
- The name nudge appears once, on the return from the first finished match, and only while the
  name is still "Player".
- An invite link that arrives during a match waits for the menu; in a lobby or a Quick Match
  search it is dropped.

**Consequences.** New copy (practice, name nudge, rejoin dialogs) needs the owner's review. With a
dedicated server (Phase 2) the host can rejoin too and the ticket should then carry the
connection info the GDD lists.
