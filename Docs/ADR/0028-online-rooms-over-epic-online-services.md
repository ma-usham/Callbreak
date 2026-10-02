# ADR 028 - Online rooms over Epic Online Services, peer to peer

**Status:** accepted (owner, 2 Oct 2026).

**Context.**
- **The relay can't ship.** Online rooms ran over PurrNet's public relay
  (`purrtransport.purrservers.com`, ADR 016). PurrNet's own inspector says that relay is for
  development only and that production use is strictly prohibited.
- **The owner wants production multiplayer that costs nothing.** The options looked at:
  - **Self-hosting PurrNet's relay** (PurrLay, MIT) on a free cloud machine: free, but someone runs
    the server, and free tiers get reclaimed or cut.
  - **Unity Relay:** free only up to about 50 concurrent players.
  - **Epic Online Services (EOS):** free at any scale, with device sign-in (no Epic account), lobbies,
    and P2P that does NAT traversal with Epic's relays as fallback, encrypted by default.

  The owner chose EOS.
- **No PurrNet layer on top.** PurrNet has a community EOS transport, but it carries only the P2P
  connection: no sign-in, no lobbies. The game's protocol doesn't use PurrNet's object layer (ADR
  016). So the game talks to EOS directly.

**Decision.**
- **A network port, `IPeerNetwork`** (Application/Online):
  - sign in;
  - list or look up a room code;
  - accept, close, send and receive packets of at most `MaxPacketSize` on a channel.
- **`PeerRoomTransport`** (engine-free) is the `IRoomTransport` over that port:
  - **Channel:** the channel is "cb" + the room code, a valid EOS socket name.
  - **Framing:** a framer (Open / Whole / First / More packets) splits each room message into
    1170-byte EOS packets and puts it back together per sender.
    - The host drops a guest message over 256 bytes as soon as it knows the size.
    - A guest keeps up to 1 MiB from the host.
    - In a whole match the largest message is 629 bytes, so nearly everything fits one packet.
  - **Lookups:** a guest retries a lookup that finds nothing for 12 s. On EOS a new room takes
    about 8.6 s to become findable, and the old relay also took about 10 s to say "not found".
  - **Timeouts:** connecting times out 15 s after the host is found.
  - **Relisting:** a host whose listing is lost lists it again every 3 s, and its guests'
    connections stay.
- **`EosPeerNetwork`** (Networking/Eos), on the PlayEveryWare plugin `com.playeveryware.eos` 6.2.0
  (EOS SDK 1.19):
  - **Sign-in:** EOS Connect with a Device ID. The display name is the player's name.
  - **Directory:** EOS lobbies.
    - A public, advertised lobby in bucket `callbreak-rooms:v<protocol version>`, with a public
      CODE attribute.
    - Capacity 4; host migration, presence, invites and voice are off.
    - Listing first looks the code up: a code another device lists is "already started".
  - **P2P:** reliable and ordered, with relays allowed.
  - **Events:** SDK callbacks only note events, which are raised in order with the packets,
    outside the callbacks.
- **Which transport the app uses:**
  - EOS, when its config holds client credentials;
  - otherwise the PurrNet relay as a development fallback, when the Boot scene sets one;
  - otherwise none.

  `ITickedRoomTransport` lets the online ticker drive either.
- **Epic Developer Portal:**
  - organization "DayZero Games", product "Callbreak";
  - Live sandbox and deployment;
  - client "Callbreak Game" with a Peer2Peer client policy;
  - the "Standard Services" agreement accepted by the owner.

  The owner pastes the client ID and secret in EOS Plugin > EOS Configuration. The plugin's config
  files are gitignored, so no credential is ever committed (Docs/Setup/EOS.md).
- **Editor safety:** the scripting define `EOS_DO_NOT_UNLOAD_SDK_ON_SHUTDOWN` (Android, iOS,
  Standalone) keeps the SDK loaded in the Editor between Play sessions. Unloading it once froze the
  Editor. Builds are unaffected.

**Consequences.**
- **Cost:** none from Epic at any scale. Its per-player limits (such as 30 lobby creations a minute)
  are far above what a player does.
- **Verified on the real EOS service:**
  - sign-in in about 2 s;
  - listing in about 5 s, lookup in about 3 s;
  - a new room findable after about 8.6 s;
  - a room hosted through `RoomService`, found by its code, and gone after the host left;
  - three Play sessions in a row.
- **Tests:** suites as on this branch:
  - dotnet 1621 (83 new: framer, transport, the in-memory peer network, whole matches over it);
  - EditMode 1620;
  - PlayMode 44.
- **Not verified yet:**
  - **Two devices:** the P2P data path between two devices, i.e. a whole match between two phones,
    one on mobile data.
  - **Losses:** a listing lost after a network drop.
  - **Login refresh:** the hourly EOS login refresh.
  - **Android build:** it needs custom Gradle templates, a namespace fix for the plugin's Android
    module, and the plugin's link.xml; the list is in Docs/Setup/EOS.md. As it stands, a Unity 6
    Android build is likely to fail.
  - **iOS build:** it probably needs a Microphone Usage Description, because the plugin links its
    voice code. That text is the owner's.
- **Old and new builds:** they use different lobby buckets, so they see "room not found" rather
  than "update required".
- **Quick Match:** it still hosts on the player's own device and fills the table with bots. Finding
  real players through EOS lobbies is the next step.
- **PurrNet:** it stays as a development fallback. Removing the package is the owner's call, once
  EOS has been proven on phones.
- **Privacy:** the privacy policy must mention Epic: a device ID, the player's name as their EOS
  display name, and IP addresses through P2P and the relays.
- **Fresh clones:** they repeat the EOS setup (Docs/Setup/EOS.md). The plugin's Android build
  output (`Assets/Plugins/Android/EOS`, `aar`) carries the client ID and is gitignored too.
