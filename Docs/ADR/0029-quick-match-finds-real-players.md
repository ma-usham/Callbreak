# ADR 029 - Quick Match finds real players over EOS, with a 20-second countdown

**Status:** accepted (owner, 2 Oct 2026).

**Context.**
- **Quick Match was never real.** It hosted a table on the player's own device and filled it with
  bots after 6 s: the "Phase 1 fallback" of ADR 016. Nobody could ever join, and since ADR 025 the
  bots weren't labelled.
- **EOS lobbies can be a free matchmaking directory.** With online rooms on EOS (ADR 028), Quick
  Match can find real players without a server.
- **The owner designed the flow in discussion:**
  - search first, then join or host;
  - a 20 s countdown;
  - real players first, then bots until the table is full;
  - a locked "3-2-1" that every screen shows together;
  - every player sees themselves in the first seat.

**Decision.**
- **A table directory, `IQuickTableDirectory`** (Application/Online), over EOS lobbies:
  - **Bucket:** `callbreak-quick:v<protocol>`, kept apart from private rooms
    (`callbreak-rooms:v<protocol>`). Private rooms are only ever found by their exact code.
  - **Attributes:** CODE (a random code nobody types), FREE (the free seats) and OPENED (the host's
    tap time, UTC ms).
  - **Search:** FREE ≥ 1, oldest first, skipping the device's own table. FREE 0 hides a table that
    is full or starting.
- **The flow (`QuickMatchService`):**
  1. A tap signs in. The sign-in and the slow first directory call are warmed up from app start by
     the online ticker.
  2. It searches.
  3. It tries tables in turn, by the host's address, joining only tables whose host tapped no later
     than this player.
  4. A refused join (full, or locked while the table still showed) moves on to the next table.
  5. Otherwise the player hosts: a table under a random code, listed with its free seats.
- **The timeline, on each player's own clock from their tap:**

  | Time | What happens |
  | --- | --- |
  | 0–14 s | Only real players can take seats. |
  | From 14 s | Bots sit down one by one, 0.8–1.2 s apart, so the table is full by 17 s. A real player can still take an empty seat. |
  | All 4 seats taken | The table locks: hidden, joins refused, an emptied seat refilled by a bot. Every screen shows the same 3-second "Table found" countdown, sent as time from now (`RoomSnapshot.StartsInMs`, protocol version 3). |
  | 3 s later | The match starts, by the host's own 20 s at the latest. |
- **Merging:** a host with no real guests keeps searching every 2.5 s. If it finds an older table
  (earlier tap, then the smaller code), it closes its own and joins that one. Only a host with no
  real guests ever moves.
- **The countdown on screen** (the existing clock pill, bottom right):
  - **While searching:** each player's own countdown, 00:20 to 00:00.
  - **Once locked:** the shared 00:03 to 00:01. The switch rolls down over 0.3 s; with Reduce motion
    it just changes.
  - **Why it works:** a joiner tapped later than the table's host, so its own countdown is never
    below the table's.
- **Seats:** every player sees themselves first, and the others in table order.
- **During the locked countdown:** Cancel, the back arrow and Android back are disabled.
- **Host leaves before the start:** the guests search again on their own clocks. From 14 s they
  host straight away, so bots still fill in time.
- **Fallback:** with no peer network, no directory, a failed sign-in or an unreachable directory,
  Quick Match runs today's local bot table (6 s).
- **Unchanged:** the bots are still not labelled (ADR 025; the owner's decision).

**Consequences.**
- **Cost:** free, with no server. Real players meet whenever two or more tap Play Online within
  about 20 s of each other; otherwise the table fills with bots.
- **Measured on EOS** (Editor, two runs):

  | Step | Time |
  | --- | --- |
  | Listing a table | 0.7–0.8 s |
  | A new table found by a search | 1.7–3.2 s after listing |
  | FREE 0 gone from searches | 0.3–1.7 s |
  | One search | 0.3–0.9 s |
  | First lobby call after sign-in | 3.4–3.9 s (hence the warm-up) |

  The owner chose 20 s from a ~9 s visibility figure that turned out to be a cold start. 15 s
  would likely do; tune it after the two-phone test.
- **Protocol 3:** older builds can no longer meet newer ones. They use different buckets and see
  no tables, not an error.
- **The GDD is out of date here:**
  - §11 screen 05 ("no fake ETA", an elapsed clock) and the 25 s bot fill (§27) describe the old
    behaviour.
  - Mockup 05 still shows an elapsed 00:12.
- **Known limits:**
  - **Clock skew:** a table's age is the host's clock against the joiner's.
  - **No rejoin after an app kill:** Quick Match keeps no rejoin ticket after the app is killed
    (private rooms do).
  - **One table at a time:** a device can't host a private room and a Quick Match table at once.
  - **Host pauses or drops in the waiting room:** the table stalls until the guests' connections
    drop. Faster detection (a heartbeat, then search again) is planned.
  - **Host drops mid-match:** the match ends ("host left"). A host hand-over that rebuilds the round
    from the remaining players' hands is planned.
- **Not verified:**
  - two real devices (the Android build needs setting up first, ADR 028);
  - many hosts at once;
  - more than 10 open tables.
- **Tests:** dotnet 1688, EditMode 1690, PlayMode 49, including:
  - 21 multi-device scenarios over an in-memory peer network: tapping together, staggered, merges,
    racing for the last seat, a late joiner refused, the host leaving, cancels, offline fallbacks,
    reconnects, and the countdown never below the table's;
  - the matchmaking screen's seat order, countdown and locked buttons.
