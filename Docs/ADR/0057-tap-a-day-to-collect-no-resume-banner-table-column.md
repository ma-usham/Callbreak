# ADR 057 - Tap a day to collect it; no Resume banner; Scores and Settings under Home, the banner top-right

**Status:** proposed (28 Oct 2026). The owner: "for daily login, even clicking the coin block should
collect the coin and remove that resume match button that shows up at the bottom. and also in game
play, move the score and setting buttons below the home button and move the banner ads to the top
right and then round text to the left side."

## Decision

- **Daily Rewards (screen 26): a tap on today's tile collects its coins**, as Claim does: the same
  coin flight, the same sound, once a day.
  - Each day's tile is a button with no press look of its own (`DailyRewardsCustomizer`, view
    field `_dayButton`).
  - The presenter collects only for today's tile while its reward is ready. Another day's tile,
    or today's once collected, does nothing.
- **Main menu: no "Resume match" banner** (GDD §11 screen 00 had one). An unfinished offline match is
  still offered back by the "Resume your offline match?" question: once when the app opens, and
  whenever Play Offline is tapped.
  - `MainMenuBuilder` no longer builds the banner, and `RemoveResumeBanner` took it out of the
    saved prefab.
  - The view, presenter and banner-room code that placed it are gone. The footer line shows
    again whenever no ad banner needs its room.
- **The table's top bar:**
  - **Scores and Settings stand under Home,** one above the other in Home's column, 22 canvas units
    apart, as icons only. Their "Scores" and "Settings" words went the same day (the owner: "remove
    that score and settings text ... just show the icon there"); they had stood on a status pill,
    since on the left they were over the scenery's temple and flags. The divider between the two
    buttons, drawn for buttons side by side, goes.
  - **The CALLBREAK logo on the left and the round under it** (the owner, 29 Oct 2026: "on the left
    side show callbreak logo and below that show round"), with or without a banner:
    - the logo stays where the mockup draws it, right of Home; it no longer makes way for a banner
      (ADR 051);
    - "ROUND 1 / 5" moves from the top centre to just under the logo, centred on it, on a status
      pill that keeps the width of its words, so it reads over the scenery's prayer flags;
    - the round line's two ornaments, drawn to flank it at the top centre, go.
  - **While a banner may show:** its slot runs from 48 units past the logo and the round line's
    pill to the right edge, and the banner sits in the top-right corner, where no button is now.
    The top seat and the trick still come down 52 and 25 units, since the banner reaches over the
    top seat at 16:9.
  - **Built by `TableCustomizer`:** `StackUnderHome`, `PlaceRoundUnderLogo` and `BuildBannerRoom`.
    The Table prefab was rebuilt. `TableView` has one round line (`_roundLabel`) and its pill
    (`_roundPill`); `SetBannerRoom` only opens the slot and moves the top seat and the trick.
    `TryGetBannerSlot` starts the slot past the round line's pill, and the banner is centred on
    the slot's right edge.

## Consequences

- **Supersedes:**
  - ADR 051's logo making way for the banner, "ROUND 1 / 5 beside the Scores button", and its slot
    from Home to the round line;
  - ADR 052's slot "between the round line and the screen's middle";
  - ADR 052's Resume banner moving under Play Offline;
  - GDD §11 screen 08's Scores and Settings in the top-right corner and its round line at the top
    centre, and screen 00's Resume banner.
- **AdMob:** the banner now has no button beside it (it had Scores 48 units away).
- **Tests:**
  - `TableBannerRoomTests`:
    - Scores and Settings under Home, icons only, and clear of the left seat at 16:9, 21:9 and 4:3;
    - the logo on the left and the round centred under it on its pill, with no ornaments;
    - the slot from past the logo and the pill to the right edge, under no button;
    - the pill growing with "ROUND 10 / 10".
  - `MenuBannerRoomTests`: no Resume banner.
  - `MenuStateTests`: a saved match shows no banner, and Play Offline offers it back and resumes it.
  - `DailyRewardsFlowTests`: today's tile pays once; another day's pays nothing.
