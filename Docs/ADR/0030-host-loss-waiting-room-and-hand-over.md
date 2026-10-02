# ADR 030 - When the host is lost: back to matchmaking in the waiting room, a hand-over in the game

**Status:** accepted (owner, 2 Oct 2026).

**Context.**
- **The host is a player's phone.** Online tables run on one player's phone (ADR 016, 028, 029).
  If that phone goes offline, or its player switches apps (the OS then pauses the game), the
  table stalls for everyone.
  - In the Quick Match waiting room, the guests waited until their connections dropped.
  - In a match, the match ended with "host left".
- **The owner decided** (in discussion, 2 Oct 2026):
  - in the waiting room everyone goes back to matchmaking, rather than handing the table over;
  - in a match the host is handed over and the game goes on from the same trick, without losing
    the round, even in the last round.

**Decision.**
- **Waiting room (Quick Match):**
  - While a quick table waits, including its 3-2-1, the host sends its guests a heartbeat about
    every second.
  - A guest that hears nothing for 3 s leaves that table and searches again on its own countdown
    (ADR 029), skipping that host. No match starts without its host.
  - A join that gets no answer within 3 s moves on: a dead table can still show in searches for a
    moment.
  - A host that comes back carries on. Bots fill whatever is empty, and its match starts.
- **In a match (private rooms and Quick Match, over the peer network; the PurrNet development
  relay keeps "host left"):**
  - **Next host in line:** the host names the next host, the next person after it in seat order
    (`Seat.Next`), skipping bots and anyone gone. Everyone is told who that is, and where to reach
    them.
  - **Detection:** the heartbeat continues through the match. 4 s with nothing from the host means
    the host is lost.
  - **Generations:** each hand-over starts a new generation of the room, with its own P2P channel
    (`cbCODE`, `cbCODE.1`, …). Private rooms list their code with its generation (EOS attribute
    `GEN`), and a lookup returns the newest. So a stale host, or a player coming back, finds the
    new one.
  - **Taking over:** the next host hosts the new generation. The others report to it: who they are,
    the last update they got, and the cards they hold. It waits up to 8 s for the reports.
  - **The rebuild:** the round is rebuilt exactly as it stood, mid-trick, and restored through the
    offline save/resume path (`MatchEngine.TryRestore`). Nothing is replayed.
    - The public state comes from the newest update anyone holds.
    - Each person's hand comes from their report.
    - Each bot's hand comes from the cards the host shared (below), minus the cards it has played.
    - The lost host's hand is simply what is left of the 52 cards.
  - **Turn clocks** start again.
  - **The lost host's seat** is played by a bot, held for 90 s like any absent player's.
  - **The old host coming back** finds its people gone. It reports to the host it had named,
    rejoins as a player and takes its seat back. The newer generation always wins, so two hosts
    never both run the table.
  - **Two hands unknown at once** (for example the host and another player lost together): the
    current round is dealt again, with the same dealer and earlier rounds' scores kept. Every
    screen shows "Host lost — dealing this round again." (new key `ui.toast.redeal_host_lost`,
    `RedealReason.HostLost` in Core).
- **An owner-approved exception to the card rule.** AGENTS.md says a seat's cards leave the host
  only for that seat. To keep a match going even with bots at the table, the host also sends the
  next host in line the cards dealt to the bots (and to seats whose player has left), at every deal
  and whenever the next host changes.
  - Those cards are never shown in the app, and never logged.
  - A modified app on that phone could still read them.
  - People's cards still go only to their owner, and to a new host when it takes over (it becomes
    the table's authority, like any host). The send site points here.
- **EOS shutdown.**
  - **The crash:** leaving Play mode in the Editor crashed it three times on 2 Oct. A lobby search
    finished after the plugin had released EOS, and released its search handle.
  - **The fix:** `EosPeerNetwork` now shuts down first, on leaving Play mode or quitting:
    - it closes its links, withdraws its listings and ends every pending wait;
    - every SDK call after an await first checks that EOS is still the one it started with.
  - **Verified** over 6 Play enter/exit cycles with 14 searches in flight each time, and after a
    full PlayMode run.
  - **Logging:** the SDK's boot line no longer shows the client ID (`EosPlatform`).

**Consequences.**
- **Protocol version 5.** It adds the heartbeat, next-host, succession and take-over messages and
  `RoomSnapshot` fields. Older builds never meet these: they use different EOS buckets.
- **Tests:** dotnet 1750, EditMode 1752, PlayMode 49.
  - 15 whole-match hand-over scenarios over the in-memory peer network:
    - the host lost mid-trick in the first and last rounds, with people only and with bots, each
      rebuild compared exactly with the lost host's own state, played to the end;
    - pauses;
    - two drops (round dealt again);
    - a second hand-over;
    - the old host returning;
    - a cut-off next host;
    - private rooms;
    - a payload scan for the card exception.
  - Plus the waiting-room scenarios.
  - Verified live on EOS: the `GEN` listing (generation 0, then 1, then gone).
- **Known limits:**
  - **Host and next host lost together:** nobody takes over; the others wait out the 90 s seat
    hold and the match ends as before.
  - **A second hand-over within 90 s:** it counts as two hands unknown, so the round is dealt again.
  - **A killed host app** keeps no rejoin ticket, so its player can't come back into that match.
  - **Misreported hands:** a modified app could misreport its hand at a take-over. The rebuild only
    checks that the reports add up.
  - **Not verified on devices,** including how EOS reports a paused app's connection on a phone.
- **Old logs** (Editor-prev.log, the 2 Oct crash reports) still show the client ID from before the
  log fix. The client secret appears in none of them.
- **The GDD is out of date here:** §8 ("a room whose host left records nothing") describes the old
  behaviour for matches the hand-over now saves.
