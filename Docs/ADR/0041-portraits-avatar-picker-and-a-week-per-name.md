# ADR 041 - Portraits for everyone, an avatar picker, a week per name, no turn sound

**Status:** accepted (owner, 6 Oct 2026).

**Context.** The owner asked for four things at once:

- "In game play when its player turn to do anything there is a sound that might irritate player,
  remove that."
- Five portraits added to `Assets/ArtRefrences/cartoon-adults`, with the player choosing one when
  entering the name and on the Profile screen.
- "The name should be change only in 7 days."
- "Do not show that default avatar anywhere, even the AI should have avatars."

Until now the five avatar presets were grey silhouettes in different shades (GDD §11 screen 14),
the Profile screen's pencil stepped through them, and a bot drew any of them, often the player's.

## Decision

- **No sound when the player's turn comes.** `TablePresenter` no longer plays `SoundCue.YourTurn`
  at the start of the local call or play turn. The light vibration, the prompt above the hand and
  the "Your turn" banner under an overlay stay. The cue keeps its number in `SoundCue` (and its
  clip in the audio library), so nothing else shifts. The ticks of the last five seconds stay.
- **The five portraits are the presets** (`Tools/ArtPipeline/Kit/Decor/portraits.ps1`):
  - Each source has a gold ring of its own. Every screen already frames an avatar in its own ring,
    so each disc is cut just inside the gold (the ring is fitted to its outer edge, its width
    measured where the cream ground meets it).
  - The disc is made fully opaque inside an exact circle and saved at 512 x 512 with mip maps.
    The Profile screen shows it at up to about 240 px, the scorecard far smaller.
    `UiBuildKit.LoadSpriteAt` keeps the mip maps of the `Avatar_*` sprites; all other UI art
    stays without.
  - The files keep their names, because 26 screen layouts and the kit records name them:

    | Preset | File | Portrait |
    | --- | --- | --- |
    | 0 | `Avatar_Default` | Dhaka topi |
    | 1 | `Avatar_01` | Crimson braid |
    | 2 | `Avatar_02` | Himalayan cap |
    | 3 | `Avatar_03` | Teal shawl |
    | 4 | `Avatar_04` | Indigo jacket |

    So every screen that showed the silhouette as its picture before it knows the player's now
    shows a portrait. A player who never picked (preset 0) has the Dhaka topi.
  - `avatars.ps1` (the silhouettes) refuses `-Install`, and `export-menu.ps1` no longer exports
    the menu mockup's silhouette, so neither can bring one back.
- **No silhouette anywhere else either.**
  - The Quick Match screen's seats show each player's portrait, bots included, over the mockup's
    black silhouette (`MatchmakingSeat`, `MatchmakingScreenState.Seat.AvatarId`).
  - Its searching seats keep only their dashed circle: the faint silhouette inside was cleared
    from `UI_Seat_Searching` (the kit's export keeps the original).
  - An empty seat in a private room shows that dashed circle, dimmed, instead of a dimmed
    silhouette.
- **Every bot has a face of its own.**
  - `SeatRoster.DrawAvatar` gives a bot a preset nobody at the table has while there is one: the
    player's and the other bots' offline; the members' in a private room that fills with bots;
    the lobby's in Quick Match.
  - Five presets, four seats: offline every face is different. Online two people may have picked
    the same one, and the bots still take the others.
  - It is one draw, as before, so nothing else the table draws moves.
- **The picker** (`AvatarPicker`, built by `AvatarPickerBuilder`):
  - The presets side by side, each on the white disc every avatar sits on.
  - The chosen one is framed by the kit's crimson "selected" disc and wears its check badge.
  - A tap saves the preset at once (`IProfileService.SetAvatar`).
  - **In the name question** (ADR 022) the row sits between the title and the field, 90 px a
    portrait.
  - **On the Profile screen** the pencil or the big avatar opens it on a panel over the dimmed
    screen ("Choose your avatar", 112 px a portrait, Done). The big avatar follows each tap.
    Done, a tap beside the panel, or Android back closes it.
- **A week per name.**
  - A name saved on the Profile screen stays 7 days: `ProfileService.NameChangeWait` counts from
    `profile.json`'s `nameChangedAt` (Unix seconds).
  - Until the week is over the field takes no typing and Save stays greyed. The line under the
    field says how long: "You can change your name again in 6 days." (days rounded up from two
    days on, then hours, then "within the hour").
  - A tap on the locked field opens no keyboard. It nudges that line instead (the owner, 6 Oct
    2026): the line turns the error colour and fades back over 1.1 s, and shakes three times over
    450 ms. With Reduce motion it only changes colour. A week that ended while the screen was
    open unlocks the field at that tap instead (`TapRelay` on the field, `ProfileNameMessage.Nudge`).
  - While a valid new name is typed, the line says before Save that the name then stays: "You can
    change your name once every 7 days."
  - The name answered at the start starts no wait, so a typo there can be put right at once.
  - A profile from before this has no time saved, so it can change at once.
  - A clock put back never makes the wait longer than 7 days.
  - `TrySetDisplayName` returns `NameCheck.TooSoon` during the week; the question at the start
    (`ChooseName`) is never held back.

## Consequences

- **Copy, new in `UI.csv`** (English; the owner can reword any of it):
  - `ui.profile.name_week_note`, `ui.profile.name_wait_days`, `ui.profile.name_wait_hours` and
    `ui.profile.name_wait_soon` for the week;
  - `ui.profile.choose_avatar` and `ui.profile.avatar_done` for the picker.
- **No protocol change.** Avatars still travel as preset numbers 0-4.
- **Tests.**
  - EditMode: `ProfileServiceTests` (the week), `SeatRosterTests`, `BotPortraitTests` (online
    bots), `MatchmakingDisplayTests` (seat portraits), `NameWaitTests`.
  - PlayMode: `AvatarPickerTests` (the name question's row, the Profile picker, the locked field
    and its nudge).
  - `FriendsFlowTests` renamed the player twice in a row to check Create Room's default name.
    The second rename is now refused, as it should be, so the test gives the profile a clock of
    its own and moves it on a week before renaming again. Its checks are unchanged.
  - The table simulation checks that no sound plays when the player's turn comes.
- **Prefabs rebuilt** from their builders: NamePrompt, Profile, Matchmaking, PrivateLobby.
- **The GDD is out of date here:**
  - §11 screen 14's preset picker (the pencil stepped through silhouettes);
  - §14's your-turn cue;
  - screen 05's searching silhouette.
