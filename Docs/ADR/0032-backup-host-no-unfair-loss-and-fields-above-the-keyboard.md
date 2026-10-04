# ADR 032 - A backup host, no unfair loss, and text fields above the keyboard

**Status:** accepted (owner, 3-4 Oct 2026). It lifts ADR 030's known limit "host and next host lost
together".

## A backup host

**Context.** In ADR 030 only the next host in line could take a match over. When the host and the
next in line were lost together (for example both phones lost signal), nobody took over: the others
waited out the 90 s seat hold and lost the match. The owner asked that the next person take over.

**Decision.**
- **The line of succession:** the host names every person after it in seat order (bots and people
  gone skipped), with their addresses, in its next-host notice (protocol version 7).
  - The first in line gets the bots' cards, as before (ADR 030's owner-approved exception).
  - Every later successor gets a package without any cards: only who is in which seat, so it can
    seat returning players. The card exception does not widen.
- **Who takes over, and when:**
  - The first in line takes over at once, at generation g+1, as before.
  - The k-th in line (k ≥ 2) takes over itself when nobody has seated it within (k − 1) × 10 s of
    taking the host as lost (`Protocol.BackupTakeOverSeconds`). It hosts generation g+k, so a late
    earlier successor is always the older one.
  - A backup holds no bots' cards, so it deals the round in play again: same dealer, earlier rounds'
    scores kept. Every screen says "Connection lost — dealing this round again." (the owner's wording;
    the key stays `ui.toast.redeal_host_lost`).
  - The missing players' seats are held for 90 s and played by bots, as for any absent player.
- **Finding the table:** while looking for a host, a player tries every successor and the old host
  in turn. Quick Match tables aren't listed by code, so returning players find them through the
  line's addresses.
- **One table survives:**
  - A host or take-over that is alone at its table probes the others. A newer generation that
    answers wins: the lone one reports there as a player.
  - An older table with other people at it means this device's own connection had dropped: it joins
    that table.
  - A host with people at its table never yields.
- **Timings** (in-memory tests): with three people, the third takes over about 14.5 s after the
  loss. With four, the second in line commits at 16.5 s, once the fourth person's report arrives.

## No unfair loss

When the hand-over gives up at the end of the seat hold while this device is online, the match is not
recorded: no loss and no game played, as when a host leaves. Offline, it is still a loss: the player
was the one who dropped. Other give-ups are unchanged: a seat that is gone still counts as a loss.

## Text fields: a cursor, and above the keyboard

- **The cursor:** on a phone, TextMeshPro draws its own cursor only when the field edits in place.
  With the keyboard's own input box shown, the game's field showed no cursor.
  - The first-run name field and the Profile and Create Room name fields now set "hide mobile
    input", so they edit in place and show their cursor.
  - The room-code boxes (a hidden field behind six boxes) draw a blinking bar of their own in the
    next box to fill while they have focus. It is hidden once all six are filled.
- **Above the keyboard:** a shared `KeyboardFollower` lifts a screen's content so that its field sits
  just above the on-screen keyboard, kept on screen, and settles back when the keyboard goes. It
  moves at once with Reduce motion.
  - Join Room, Profile and Create Room use it, as the first-run name panel already did (ADR 022).
  - The keyboard's height comes from `IOnScreenKeyboard`, read over JNI on Android.

## Consequences

- **Tests:**
  - backup host: private room and Quick Match; four people; the late first in line; the old host
    back; a killed player rejoining with its ticket; the backup's own connection dropping; no backup
    when the first in line is alive; a failed opening tried again;
  - no-loss, online and offline; the line and card-free packages on the wire;
  - the card-leak scan now checks that only the first in line's package carries cards;
  - cursor and keyboard-lift play tests for the four fields.
- **Suites:** dotnet 1795, EditMode 1797, PlayMode 64.
- **Not verified on devices:**
  - how fast EOS fails a connection to an offline phone;
  - how fast a private room's new listing shows;
  - real app pauses;
  - the real keyboard's height and the cursor on Android.
- **Known risks:**
  - In rare compound failures two tables, each with people, could both carry on, because a host with
    people never probes.
  - A cut-off but online backup briefly hosts a table of its own (round dealt again) until it finds
    the others.
  - The no-loss rule looks at the network only at the moment of giving up, and Unity counts a captive
    Wi-Fi portal as online.
  - Lifting a screen moves its header too: on Profile the avatar mostly leaves the top while typing.
  - On Join Room the Join button stays under the keyboard until Done closes it. Whether Done should
    join once all six characters are in is the owner's call.
- **The GDD is out of date here:** §8 (who takes over) and §27 (failure modes).
