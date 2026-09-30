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
| [016](0016-online-protocol-behind-byte-links.md) | The online protocol is transport-free C#; PurrNet's relay transport only carries its bytes | Proposed |
| [017](0017-system-fonts-behind-the-game-fonts.md) | The device's fonts stand behind the game's Latin-only fonts; suit symbols are sprites | Proposed |
| [018](0018-first-launch-and-rejoin-flows.md) | What the menu starts by itself and in which order: invite link, rejoin, resume, first-run guide | Proposed |
| [019](0019-menu-states-the-mockups-do-not-show.md) | Menu states no mockup draws: the Resume banner, the fan's sway, failures with Retry, dialogs that can be closed, UI sound cues | Proposed |
