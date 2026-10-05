# ADR 037 - PurrNet removed

**Status:** accepted (owner, 5 Oct 2026: "remove it").

**Context.** Online play has run on Epic Online Services since ADR 028; Quick Match, Stake Match and
private rooms all go through EOS (lobbies, then peer to peer with Epic's relays). PurrNet stayed in
three places:
- a fallback to PurrNet's public development relay when EOS wasn't set up or didn't start;
- `RelayCheck`, an Editor tool that joined a room as a second player in the same process;
- an empty `Callbreak.Server` assembly from the first plan.

Nothing players use depended on it, and a release build should never fall back to a development
relay.

## Decision

PurrNet is gone:
- **Code:** `Networking/PurrRoomTransport.cs`, `Editor/RelayCheck.cs` and the `Callbreak.Server`
  assembly are deleted.
- **Start-up:** `ProjectLifetimeScope` has no `_roomTransport` field and no relay fallback. Without
  EOS (no credentials, or it didn't start) the game has no online rooms (`NoRoomTransport`), and the
  log says so.
- **Boot scene:** its `Network` object (the `PurrTransport` component) is removed, and `SceneSetup`
  no longer builds it. The scene's diff is only that object and the reference to it.
- **Assembly references:** `PurrNet.Runtime` is dropped from Bootstrap, Editor, Networking and the
  PlayMode tests.
- **Packages:** `dev.purrnet.purrnet` 1.23.0 and the `dev.purrnet` OpenUPM scope are out of the
  manifest, and `ProjectSettings/PurrNetSettings.asset` is deleted.
- **Comments** that named PurrNet as the transport now name `PeerRoomTransport` over EOS.

ADR 016's transport-free protocol stands: the room and table logic never saw PurrNet, only bytes
through `IRoomTransport`.

## Consequences

- **Smaller builds.** The app is smaller, and there is one package fewer to keep up to date.
- **One online path.** EOS is the only one. The PlayMode tests keep their in-memory network and
  never start EOS (ADR 034).
- **No in-Editor second player.** `RelayCheck` is gone, so a real two-player online check needs two
  devices (or a future EOS-based tool).
- **No relay to fall back on.** On a day Epic refuses this PC's sign-ins (the 401 of 4 Oct), there is
  no other relay for testing.
- **For the owner:** `AGENTS.md` still names PurrNet in its header line. Agents may not edit that
  file, so the owner updates it. The GDD's PurrNet sections are history: ADR 028 replaced them.
