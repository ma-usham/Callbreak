# ADR 016 - The online protocol is transport-free; PurrNet carries bytes

**Status:** proposed.

**Context.** GDD §25 describes one `TableNetworkController` NetworkBehaviour with ServerRpc,
ObserversRpc and TargetRpc messages, SyncVars for seats and a SyncTimer, and a lobby backend
(PurrLobby on Unity Lobby, Nakama or PurrServices) for private rooms. No lobby backend is linked
(ADR 015), and protocol logic written inside NetworkBehaviours can only be tested in Play mode.

**Decision.** The room and table protocol is plain C# in `Callbreak.Application.Online`
(`RoomHost`, `TableHost`, `RoomClient`, `OnlineMatchSession`, `RoomService`) behind two small
interfaces, `IHostLink` and `IClientLink`, that move byte arrays reliably and in order, and
`IRoomTransport`, which opens them for a room code. `Callbreak.Networking.PurrRoomTransport`
implements that over a PurrNet transport: with `PurrTransport` the room code is the relay's room
name, so a private room needs no lobby service. The transport is pumped directly; there is no
NetworkManager, no network prefab and no scene synchronisation (each device loads the table
itself when the host says the match is starting).

Differences from the GDD's message catalogue, and why they keep its guarantees:
- Every seat receives its own numbered updates (the GDD's TargetRpc path) carrying the events that
  seat may see, its `PlayerView` and the table status, instead of public events by ObserversRpc
  plus private ones by TargetRpc. A seat's hand still only ever goes to that seat, the client
  never reconstructs state from events, and a gap in the numbers still triggers a resync.
- Seats, presence and the turn clock travel in those updates instead of SyncVars and a SyncTimer.
  The host's clock is still the only one that counts; clients display "milliseconds from now".
- The host's own player joins its room through an in-process link and runs the same client code
  as a guest, which is the GDD's "server and client in one process".
- Reconnection is by the stable player id in the hello message (GDD §27), not by PurrNet cookies.

Quick Match has no matchmaker behind it yet, so `QuickMatchService` is the GDD's Phase 1 fallback
only: this device hosts, and after the bot-fill delay the empty seats are Normal bots shown as
bots. The delay defaults to 6 s rather than the GDD's 25 s because nobody can join during the
wait; `QuickMatchConfig` sets it back when a queue exists.

**Consequences.** The whole protocol runs in the dotnet harness over an in-memory relay (46
tests: whole matches, hidden information, rejects, timeouts, drops and rejoins, lost updates,
floods, malformed payloads), and one live match through the real relay was played in the Editor.
Moving to dedicated servers (Phase 2) means running `RoomHost` in a server process behind another
`IRoomTransport`; adopting NetworkBehaviours later means writing a different `IHostLink` and
`IClientLink`, not touching the rules of the protocol. What is lost: PurrNet's inspector tooling
for SyncVars and RPCs, which this design does not use.

## Addendum, 30 Sep 2026: after the online review (protocol version 2)

A read-only review of the online code against GDD §22-27 found gaps; these are now closed, each
with a test written first:
- **No seed leaves the host early.** The match id is drawn apart from the shuffle seed, and each
  round is dealt from a seed drawn when that round starts (`RoundSeededRandom`), so no later
  round's deck exists while a round is played. The bots draw their own seeds. A test scans every
  payload of a whole match for every seed.
- **Per-room identity.** The hello carries a SHA-256 of the room code and the player id
  (`RoomIdentity`), not the permanent player id, so an id seen in one room is worthless in
  another. A remote hello can never take the host's seat; a removed player stays out.
- **Limits on what a guest sends.** Every string is read with a byte limit, guest payloads over
  256 bytes are dropped (also in the transport, before copying), unknown avatars and decks fall
  back to defaults. Rate limits (10 intents per second, emotes) were already there.
- **Faults stay contained.** A handler that throws (a presenter, a subscriber) no longer leaves a
  room half-updated or a player deaf: the room's own state always completes, each handler runs
  isolated, and the first exception is rethrown after the frame's work.
- **Presence and clocks.** An unreadable message keeps an Away seat Away; a player back during
  their own turn gets no fresh clock (the covering bot finishes it); people coming and going
  cannot hold a bot's move back; the clock clients see starts when the turn does.
- **Leaving costs the match.** A lost connection records the match as left, like Leave. The
  rejoin ticket carries the match id: a ticket that runs out, or one replaced by another match,
  records its match as left; a rejoin the host refuses because the seat hold is over does too.
  A room whose host left records nothing (GDD §8).
- **Host gone.** After three "not found" answers the session closes as "host left". A player
  who returns after the match ended is shown the final results.
- **Code collisions.** A code that is live on the relay is refused and redrawn (the relay's
  join endpoint, as PurrNet's `RoomExistsAsync` asks it).

Still open, for the owner:
- **The relay path is not encrypted.** Everything between a phone and PurrNet's public relay
  (`purrtransport.purrservers.com`) is plain UDP; a seat's hand, its room identity and the room
  code can be read by the relay operator or someone on the same network, who could also take that
  seat in that room. Accept this for Phase 1, or add a key agreement to hello/welcome and encrypt
  every payload (a wire change).
- **The join rate limit is the client's own** (six attempts a minute, forgotten on restart). The
  GDD's per-device limit needs a service in front of the relay.
- **Wire size.** Each update carries the seat's whole view, so a guest receives about 16-24 KB
  per round (54-59 messages, measured by a test), against the GDD §25 figure of under 2 KB per
  client. Either the figure changes, or updates stop repeating the seat profiles and the score
  sheet.
- **"Not found" is slow** on the real relay (PurrNet retries for about 10 s per attempt), and an
  unreachable relay also reads as "not found".
