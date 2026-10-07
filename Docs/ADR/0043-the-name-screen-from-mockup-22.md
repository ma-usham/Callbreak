# ADR 043 - The name screen as mockup 22 draws it, its field at the top

**Status:** accepted (owner, 7 Oct 2026).

**Context.** The question asked when the game starts (ADR 022) was a dialog: "What's your
name?", a row of the avatars (ADR 041), the field and Continue. On 7 Oct 2026 the owner sent a
mockup for it, now `Assets/ArtRefrences/game-screens/22-name-entry.png`. It is the Profile screen
(14) with a character grid where the stats were, and the portraits in their own gold rings. The
owner asked for:

- the screen to look like the mockup;
- the name field at the top, because the keyboard would hide it;
- the keyboard not to open by itself when the screen comes up;
- the keyboard's Enter to put the keyboard away, with Save Changes leading on to the next screen.

## Decision

- **The screen** is built like every other screen, from a layout:
  - `ArtSource/Screens/NamePrompt/layout.json`, written by `Tools/ArtPipeline/name-entry-layout.js`.
  - Mockup 22's title, panel, field, Save and note are mockup 14's pixel for pixel, so their
    measured nodes come from Profile's layout. The rest was measured on mockup 22.
  - `NamePromptCustomizer` builds the parts that no layout node draws:
    - the field with its "Enter your name" placeholder;
    - the big portrait, framed and chosen;
    - the grid of five, three over two.
  - The prefab moved from `Prefabs/UI/Overlays` to `Prefabs/UI/Screens` with its asset id, so the
    Menu scene keeps its instance. `NamePromptBuilder` now builds it through `ScreenLayoutBuilder`.
- **The owner's change to the mockup: the name at the top.**
  - The caption, the field and the line under it (the note, or why a name cannot be used) open
    the panel, then the divider. A sideways phone's keyboard, about 60 % of the screen, leaves
    the field in sight.
  - The portrait section moves down under the divider. Its grid is a little smaller (128 instead
    of 150 mockup pixels) so that Save Changes stays where the mockup has it.
  - The note moved from under Save to under the field, so a reason a name cannot be used shows
    while it is typed.
  - Should a taller keyboard ever reach the field, the screen still rises above it.
- **The portraits as mockup 22 draws them:**
  - `Avatar_Framed_00..04`: the owner's portraits with their own gold rings, cut at the ring's
    outer edge by `portraits.ps1`. The other screens keep the plain discs.
  - The chosen one is hugged by a thin dark-crimson ring and wears a gold check badge: dark brown
    in a gold rim, with a gold tick. Both are drawn by `avatar-frames.ps1`.
  - The big portrait on the left is always the chosen one. The name under it follows what is
    typed, and shows the current name ("Player") while the field is empty.
- **The keyboard:**
  - It comes only when the player taps the field.
  - Its Done (Enter) puts it away and nothing else.
  - **Save Changes** answers the question, once the name can be used; until then it is greyed.
    The rules primer follows, as before.
- **No back button:** mockup 22 shows Profile's, but the question cannot be left (Android back
  does nothing, as before).
- **Copy:**
  - The title and subtitle are Profile's ("Your Profile", "EVERY HAND TELLS A STORY").
  - New: "Choose your character" (`ui.name_prompt.choose_character`).
  - The old title "What's your name?" and "Continue" are no longer shown; their keys stay in the
    table.

## Consequences

- `AvatarLibrary.Framed(id)` holds the framed set. `SceneSetup.EnsureAvatarLibrary` fills it.
- `AvatarPickerBuilder` builds the framed look in a grid (`BuildFramedGrid`) and the chosen frame
  alone (`BuildChosenFrame`). The Profile screen's picker keeps the plain look (ADR 041).
- **Tests.**
  - `NamePromptFlowTests`: Save Changes answers; the field sits above a sideways keyboard and
    only a taller one lifts the screen; no keyboard until a tap, and Done only puts it away.
  - `AvatarPickerTests`: the framed grid, the big portrait, and the name that follows the typing.
  - `MenuFlowTests` answers with Save Changes.
- **Not verified on a phone:** where the keyboard ends on a real device, and that Done closes it
  there. The Editor has no on-screen keyboard.
