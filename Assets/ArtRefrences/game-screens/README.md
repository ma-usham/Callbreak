# Nepali Callbreak — screen collection

Open **index.html** in a browser to browse the full set, view images full screen, or save individual PNGs.

The collection contains **17 landscape PNGs at 1672 × 941**: the existing main menu, the revised room-selection screen, and 15 new screen concepts. These are raster UI mockups showing the proposed game experience.

## Requested room changes

**01-play-with-friends.png** enlarges the two Create Room and Join Room panels, removes the lower room-code/share strip, and preserves the restrained crimson texture without visible flowers on the buttons. The original `../Room.png` and `../mainmenu.png` remain untouched.

Room-code and invite controls appear only in the created-room lobby; the join screen has its code entry field.

## Screen inventory

| File | Screen | Purpose |
| --- | --- | --- |
| [00-main-menu.png](00-main-menu.png) | Main menu | Existing main menu and visual reference. |
| [01-play-with-friends.png](01-play-with-friends.png) | Play with friends | Larger Create Room and Join Room panels; bottom code and invite strip removed. |
| [02-create-room.png](02-create-room.png) | Create room | Choose a room name and turn timer before opening a private table. |
| [03-join-room.png](03-join-room.png) | Join room | Enter a six-character invitation code. |
| [04-private-lobby.png](04-private-lobby.png) | Private lobby | A created room with four players, invitation controls, and host start action. |
| [05-matchmaking.png](05-matchmaking.png) | Online matchmaking | Find three opponents for an online match. |
| [06-offline-setup.png](06-offline-setup.png) | Offline setup | Select a difficulty for a five-round match against bots. |
| [07-bidding.png](07-bidding.png) | Make your call | Choose a bid over the updated scenic board, with a bottom-edge 13-card hand and avatar countdown. |
| [08-gameplay.png](08-gameplay.png) | Live gameplay | A 13-card hand emerging from the bottom screen edge, lower-left You panel with avatar-border countdown, a three-card trick with room for the fourth, and daylight Nepal scenery. |
| [09-round-results.png](09-round-results.png) | Round results | Round-one scores with bids, tricks won, and updated totals. |
| [10-match-results.png](10-match-results.png) | Match results | Final standings after five rounds, with replay and main-menu actions. |
| [11-settings.png](11-settings.png) | Settings | Audio, language, accessibility, and help controls. |
| [12-how-to-play.png](12-how-to-play.png) | How to play | A concise overview of the standard five-round rules and scoring. |
| [13-match-menu.png](13-match-menu.png) | Match menu | A menu over the updated gameplay board and bottom-edge hand, with resume, audio, scorecard, and leave controls. |
| [14-profile.png](14-profile.png) | Player profile | An illustrative profile with editable identity and simple play statistics. |
| [15-card-styles.png](15-card-styles.png) | Card styles | A cosmetic collection screen reached from the main menu's shop icon. |
| [16-scorecard.png](16-scorecard.png) | Full scorecard | A consistent five-round score history matching the final standings. |

## Suggested flow

- Main menu → Play with friends → Create room / Join room → Private lobby → Bidding.
- Main menu → Online matchmaking → Bidding.
- Main menu → Offline setup → Bidding.
- Bidding → Gameplay → Round results → Next round → Match results → Full scorecard.
- Gameplay → Match menu (offline pause example).
- Main menu → Settings / Profile / Card collection. Settings → How to play.

The match, score, and profile values are illustrative. The example full scorecard totals are internally consistent with the final standings.

## Art direction

Warm ivory panels, deep crimson primary controls, black silhouette icons, classic serif display type, soft painterly Nepal scenery, pale geometric textile accents, and crimson card backs. Primary-button centers remain quiet, with restrained woven geometry at the ends.

The latest 08-gameplay.png follows the user's camera-only reference: the tabletop fills the foreground, the hand emerges from the bottom screen edge with all lower card edges cropped, and the You/avatar/bid panel sits beside the hand at lower left. The suggestion banner is removed; the active avatar border carries the countdown, and the trick sits higher to leave a fourth-card position above the hand. The original ivory/crimson palette, silhouette avatars and daylight Nepal scenery are retained. The reference is saved at ../references/gameplay-camera-reference.png; use it only for camera and composition, not its green/nighttime styling or other UI. This supersedes the distant seated view and earlier floating/tall-table attempts. The refreshed 07-bidding.png and 13-match-menu.png now use this same board composition, bottom-edge hand and lower-left player badge, with their own bidding and paused-menu controls. Their edit prompts are in [match-screens-board-update-prompts.md](match-screens-board-update-prompts.md). Previous gameplay versions are under ../archive/gameplay/. The latest edit prompt is [08-gameplay-bottom-anchored-hand-prompt.md](08-gameplay-bottom-anchored-hand-prompt.md).

## Generation and rules reference

Latest refinement: all 13 local cards emerge from the bottom edge with their lower bodies intentionally cropped, giving a held-hand appearance. The You/avatar countdown and bid/won labels sit at lower left beside the hand, with chat above them. The three-card trick retains room for a fourth card and the suggestion banner stays removed. This supersedes placing the player panel beneath the cards. The full-screen board remains a separate experiment. See [08-gameplay-bottom-anchored-hand-prompt.md](08-gameplay-bottom-anchored-hand-prompt.md).

All new/revised images were produced with the **built-in image editor**. The exact per-screen prompts are saved in [generation-prompts.md](generation-prompts.md); the original main menu was copied unchanged.

Game examples use four players, 13 cards each, five rounds, spades as trump, mandatory higher-suit play when possible, and bid-based scoring with 0.1 for each extra trick. Rule reference: [KhelLabs Callbreak Multiplayer — rules](https://callbreakmultiplayer.com/), with the eight-choice bidding UI following a common 1–8 variant described by [Cardems](https://www.cardems.com/en/games/call-break). These are design choices for the proposed experience; rule variants can be adjusted during implementation.

