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
