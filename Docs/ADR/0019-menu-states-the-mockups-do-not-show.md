# ADR 019 - Menu states the mockups do not show

**Status:** proposed.

**Context.** A review of the menu against the GDD found behaviour the GDD asks for that no mockup
draws, so each needed a small visual or behavioural choice: the "Resume match" banner and the
swaying card fan of screen 00, the failure states of screens 02 and 05 ("inline message with
Retry", "Retry / Play Offline"), the contents of About, and the three UI sound cues of §14.

**Decision.**
- **Resume banner.** While an unfinished offline match can be loaded, the main menu shows a small
  ivory pill "Resume match" (the secondary button, 336 x 64) where the footer line "A GAME THAT
  BRINGS US CLOSER" stands; the two never show together. A tap resumes at once. The question
  "Resume your match?" is still asked once when the app opens and when Play Offline is tapped.
- **Card fan.** The fan is four real cards, the aces of the equipped deck (GDD §13: "the
  main-menu fan shows that deck's four aces"), hung from the top edges and tilts measured on
  mockup 00, each with the table cards' soft shadow. The mockup paints its two right-hand cards
  narrower than the middle one; real cards are one size, so they are stacked to keep every
  index visible (hearts, diamonds, spades, clubs from back to front). The fan changes with the
  deck chosen in Card Collection.
  The mockup's own fan shows a generic face, drawn before the decks existed, so this part of the
  screen no longer matches mockup 00 pixel for pixel; the painted fan (`Deco_MenuFan.png`) is
  kept in the art folder but no longer used. The fan rocks by 1.2 degrees about its foot every
  5 s and stands still with Reduce motion.
- **A dialog can be closed without an answer.** `IDialogs.AskAsync` tells the secondary button
  from a dialog that was dismissed (a tap outside, Android back, the asking screen going away).
  "Resume / New Game" and "Retry / Play Offline" use it, so closing them starts nothing.
  `ConfirmAsync` stays for yes-or-no questions, where dismissed means no.
- **Failures.** Create Room shows the reason where its "Private room" line is and the button reads
  Retry. Quick Match asks "Couldn't join the table" with Retry and Play Offline; Play Offline opens
  the offline setup in the search screen's place.
- **Sound cues.** A button taps, a back, close or cancel button sounds "back", a switch sounds
  "toggle", and Android back sounds "back". The audio service plays one UI cue per frame, the
  first asked for, so a presenter that answers a press with its own cue is heard instead of the
  button's tap, never on top of it.
- **Volume.** The level follows the slider while it moves; the file is written once the slider
  rests for 0.4 s, when Settings closes and when the app goes to the background.
- **About.** Version, build (the first eight characters of the build's id; "dev" in the Editor),
  protocol version, the open-source licences and the fonts. Credits and the privacy-policy link
  wait for the owner's text and URL.
- **Avatar.** Every header that shows the player shows the picked avatar.

**Consequences.** The banner's look and place, the new line "Resume match" and the About text are
the owner's to change. Offline, the two online buttons are muted and Play Offline is left as the
one full-strength button; the GDD's "Play Offline highlighted" has no look of its own yet. A store
build number (Android version code) in About would need a stamp written at build time.
