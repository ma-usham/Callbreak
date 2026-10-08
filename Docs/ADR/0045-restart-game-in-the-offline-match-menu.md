# ADR 045 - Restart Game in the offline match menu

**Status:** accepted (owner, 8 Oct 2026).

**Context.** The match menu (GDD §11 screen 13) opens from the table's Home button and from
Android back. In an offline match its first button was "Resume Game", which did what the close ×
already does: back to the paused match. A player who wanted to start the match again had to leave
for the main menu and deal a new one from there. On 8 Oct 2026 the owner asked for the first
button to be "Restart Game" in an offline match, so a player can restart without going back to the
main menu.

## Decision

- **Offline, the first button is "Restart Game"** (`MatchMenuText.FirstButtonKeyFor`, new string
  `ui.match_menu.restart`). Online it stays "Back to Game": an online match can't be restarted
  for one player.
- **What it does** (`MatchMenuPresenter.RestartAsync`):
  - A new offline match is dealt at once, on a fresh table, at the difficulty of the match in play
    (not the offline setup's remembered choice, which a practice or a resumed match may not
    share).
  - The match in play is dropped: no save of it is left for Resume on the main menu. It stays
    paused under the menu until the new table is up.
  - Like Play Again, a full-screen ad shows first when one is due (`AfterMatch.NewMatch`, the
    same rules: never for No Ads, never after the first match, at most one every three minutes).
  - A second tap while the restart is under way does nothing.
- **The close × resumes** the match, as before. "Resume Game" (`ui.match_menu.resume`) stays in
  the tables for the main menu's Resume.
- **No confirmation.** The tap restarts at once; the owner didn't ask for a question first.

## Consequences

- The dropped match isn't counted in the statistics, the same as a saved match that a new match
  from the main menu replaces.
- The menu's view and prefab are unchanged: the button keeps its name (`ResumeButton`) and only its
  words and its action change.
- **Tests.**
  - `ResultsTextTests`: the first button's words per mode.
  - `MatchMenuAndSettingsTests` (PlayMode): offline the menu offers "Restart Game", and the button
    deals a new match at the table (a new session, not resumed, the same difficulty, still in the
    match, no save left).
  - `TableFlowTests`: the pause check now goes back to the table with the close ×.
  - `TableSimulation`: the restart asks the app's flow for a new offline match at the same
    difficulty; the × then goes back to the table.
- **Not verified on a phone.**
