# Architecture decision records

GDD v1.1 §16 lists ADRs 001-009. The records here are the decisions taken while building the game
(30 Sep 2026) where the implementation needed a choice the GDD didn't make, or departs from it.
Each says what was decided, why, and what it would take to change it. The owner can overrule any
of them; the GDD is updated once a decision is confirmed.

| ADR | Decision | Status |
| --- | --- | --- |
| [010](0010-screens-rebuilt-from-their-own-mockups.md) | Screens are laid out on their own mockups, but share one background and one 9-slice UI kit | Accepted (owner) |
| [011](0011-task-based-ports.md) | Application ports use `Task`, not Unity `Awaitable` | Proposed |
| [012](0012-csv-string-tables.md) | Localisation from CSV string tables behind `ILocalization`, not the Unity Localization package, for v1 | Proposed |
| [013](0013-no-records-in-first-party-code.md) | No `record` types or `init` accessors in first-party code | Proposed |
| [014](0014-card-faces-cut-from-reference-sheets.md) | Card faces are cut from the reference sheets at their real proportions | Proposed |
| [015](0015-local-services-before-ugs.md) | Local adapters for identity, saves and analytics until a Unity Cloud project is linked | Proposed |
| [016](0016-online-protocol-behind-byte-links.md) | The online protocol is transport-free C#; PurrNet's relay transport only carries its bytes (addendum: the online review, protocol 2, what is still open) | Proposed |
| [017](0017-system-fonts-behind-the-game-fonts.md) | The device's fonts stand behind the game's Latin-only fonts; suit symbols are sprites | Proposed |
| [018](0018-first-launch-and-rejoin-flows.md) | What the menu starts by itself and in which order: invite link, rejoin, resume, first-run guide | Proposed |
| [019](0019-menu-states-the-mockups-do-not-show.md) | Menu states no mockup draws: the Resume banner, the fan's sway, failures with Retry, dialogs that can be closed, UI sound cues | Proposed |
| [020](0020-one-deck-per-table.md) | One deck per table, every face and back: offline the player's deck, a private room the host's, Quick Match one drawn from the catalog (replaces GDD §13's deck per seat) | Accepted (owner) |
| [021](0021-one-hand-order-and-the-emote-button-on-the-right.md) | The hand is always ♠ ♥ ♣ ♦ with no Sort button, the emote button takes its place on the right, and the hand's arc bends half as much (replaces GDD §2's two orders) | Accepted (owner) |
| [022](0022-name-at-the-start-thrown-cards-and-a-shuffled-deal.md) | The name is asked when the game starts; cards are thrown from the hand, not tapped; the deck is shuffled in the middle of the table and dealt from there; "Resume your offline match?" | Accepted (owner) |
| [023](0023-ui-motion-with-litmotion.md) | One way of moving for the whole UI, with LitMotion: screens slide and grow, panels pop in and out, the menu's buttons rise, the table's parts appear softly; only fades with Reduce motion (replaces GDD §14's screen change) | Accepted (owner) |
