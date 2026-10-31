# ADR 066 - The Rozha Royal logo, without its Nepali line

**Status:** accepted (owner, 31 Oct 2026). The owner: "use rozha royal callbreak font from figma in
our game, but dont use that nepali text. removve the current callbreak logo and use the one from
figna."

## Context

ADR 058 put the owner's first pick, option E "Temple Crown", in the game (14bde53): Marcellus SC
under a gilt pagoda roof, fitted into the old flag logo's sprite boxes. With its roof in the box, its
lettering on the main menu was 553 × 70 units at 16:9 where the old flag logo's was 791 × 86. Option
A on Figma's "V1 Rebuild · Options" page, "Rozha Royal", sets CALLBREAK in Rozha One, crimson on a
cream edge and a gold rim, with the Nepali कलब्रेक under it and PLAY TOGETHER between gold rules.

## Decision

- **Rozha Royal replaces the Temple Crown everywhere the game shows the logo**, without the Nepali
  line. Its lettering, colours, rim, shadow, tagline, rules and diamonds stay as option A draws them.
  PLAY TOGETHER moves up into the Nepali line's place.
  - Figma: Art/Logo · Rozha Royal (314:273) and Art/Logo Compact · Rozha Royal (314:282, its tagline
    tucked closer, for small headers), in the Art section of "V1 Rebuild · Design System", cut from
    the "Unity Export · NewUI" sheet. The Temple Crown components stay in Figma, unused.
  - The sprites keep their names, sizes and GUIDs: `Ornaments/Logo_Callbreak` (the whole logo: the
    main menu, the loading screen, Create Room, How to Play, Private Lobby) and the two
    `Logo_Callbreak_NoFlag` (the compact logo: Join Room, and the table, results and scorecard
    headers). The logo fills each sprite's width, centred, so every screen keeps its layout.
- **On the main menu the logo sits lower**, between the wallet and the card fan: its centre moves
  from mock y 180.25 to 224.75 (51 units down), at its sprite's own size (`MainMenuBuilder`, and
  MainMenu.prefab patched in place). Its lettering is 744 × 80 units at 16:9, nearly the old flag
  logo's size and about as wide as Play Online. Its gold rim stays about 40 units under the wallet,
  and its tagline about 40 above the fan; at 20:9 and 4:3 the corner buttons stand further off.
- **Durbar Square's birds fly further right**, x 1400-1640 instead of 1280-1540
  (`shop_scenes.durbar`): the wider lettering ends at x 1340, where the first birds were. They stay
  under the settings button and left of the gift button.
- The table's header is unchanged: the compact logo's lettering is 348 × 37 units (the Temple
  Crown's was 334 × 42), clear of Home and of the banner's slot, with the round line under it.

## Consequences

- No game rule, string or layout other than the main menu's logo position changed. The loading
  screen's logo grows from 586 to 790 units wide, in the box the old flag logo filled.
- The Temple Crown's art leaves the game: it only ever lived in the three logo sprites.
- `cut_sheet.py` takes an out-dir, to look at a cut before it reaches Assets.
- Figma's Durbar Square still flies its birds where the logo stands; the game's are drawn by
  `shop_scenes.py`.
