# ADR 078 - The Temple Crown logo, without its temple

**Status:** accepted (owner, 3 Nov 2026). The owner: "use temple crown as logo from figma. but without
temple."

## Context

- **The Temple Crown** (option E on Figma's "V1 Rebuild · Options" page) was the owner's first pick
  (ADR 058): CALLBREAK in Marcellus SC, crimson on a cream edge and a gold rim, under a gilt
  three-tier pagoda roof, with PLAY TOGETHER between gold rules and diamonds.
  - Figma has it as two components: Art/Logo · Temple Crown (247:292, with the roof) and Art/Logo
    Compact · Temple Crown (247:316, the same lettering and tagline without the roof).
  - The game showed the whole logo on the main menu and the compact one in the small headers.
- **ADR 066** replaced it with the Rozha Royal everywhere, in the same three sprites.

## Decision

- **The Temple Crown without its roof replaces the Rozha Royal everywhere the game shows the logo.**
  - That is Art/Logo Compact · Temple Crown (247:316) as Figma draws it: the lettering, rim, shadow,
    tagline, rules and diamonds are unchanged.
  - It is cut from the "Unity Export · NewUI" sheet with `cut_sheet.py`, now holding three instances
    of it (434:26, 434:35, 434:44).
  - The roofed Art/Logo · Temple Crown and the Rozha Royal components stay in Figma, unused.
- **The sprites keep their names, sizes and GUIDs:**
  - `Ornaments/Logo_Callbreak`: the main menu, the loading screen, Create Room, How to Play and the
    Private Lobby.
  - The two `Logo_Callbreak_NoFlag`: Join Room, and the table, results and scorecard headers.
- **In the two small sprites the logo is as large as fits 8 px inside the sprite, centred.**
  - It is taller for its width than the Rozha Royal, so its height sets its size.
  - These are the same sprites the Temple Crown's compact logo had under ADR 058, and the same
    layout.
- **In `Logo_Callbreak` the logo stands as tall as the Rozha Royal did** (338 of the sprite's 636 px),
  centred.
  - Filling the sprite's width, as the Rozha Royal did, would have brought the lettering within
    13 px of the wallet on the main menu, at 1920 × 1080.
  - At this height the main menu keeps ADR 066's gaps: 27 px under the wallet and about 45 px above
    the card fan. The lettering is 653 px wide there.
  - No layout or prefab changed: the logo's position and its sprite's size stay as ADR 066 left them.

## Consequences

- The game's logo is CALLBREAK in Marcellus SC with PLAY TOGETHER under it, with no roof and no Nepali
  line.
- On the table the header logo's lettering is 312 px wide at 1920 × 1080, clear of Home and of the
  round pill under it.
- Durbar Square's birds (`shop_scenes.durbar`) still fly right of the main menu's logo, which ends
  further left than the Rozha Royal's.
- Captured at 16:9 (the main menu and the table) only; not checked in Play or on a phone at the
  owner's request (they test it themselves). Art and comments only: no code, prefab or test changed.
