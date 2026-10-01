# ADR 022 - The name asked at the start, cards thrown from the hand, a shuffled deal from the middle

**Status:** accepted (owner, 1 Oct 2026).

**Context.** The owner asked for four changes that depart from the GDD:
- GDD §15 asks a player still called "Player" for a name once, in a dialog after the first
  finished match ("Pick your name?", which opens the Profile screen).
- The resume offer read "Resume your match?" (GDD §8).
- GDD §11 (T rules, screen 08) plays a card with a tap, with tap-and-slide along the hand and an
  upward flick as alternatives.
- GDD §14 deals the cards "from the dealer to each seat"; the shuffle is only a sound.

**Decision.**
- **The name is asked when the game starts.** Until a name is chosen, the menu opens with a panel
  in the dialog's style: "What's your name?", a name field with the Profile screen's rules and
  messages, and Continue. It cannot be dismissed, and Android back does nothing while it is up.
  It sits above the menu and its dialogs (canvas order 55, the dialog's is 50), so whatever else
  the menu starts by itself (an invite link, a resume offer) waits underneath. On a first launch
  the app opens on How to Play's background and logo, without the rules or its buttons, under
  the panel; once the name is answered the rules appear, and the main menu (with the practice
  offer) comes after "Got It". The rules never show behind the panel, also on the way to Join
  Room for an invite link on a new phone.
  Keeping "Player" is an answer too. The profile remembers the answer (`nameChosen`); a profile
  that already holds a name of its own counts as answered, and one whose name had to fall back to
  "Player" is asked again. The nudge after the first match is gone.
- **"Resume your offline match?"** is the resume offer's title. The main menu's "Resume match"
  button is unchanged.
- **Cards are thrown.** A card pulled up out of the hand (24 px, more up than sideways) follows
  the finger; let go 110 px or more above where the pull began, or flicked up at 900 px/s or
  more, it is played and flies from there to the trick. Let go sooner, or refused (not the
  player's turn, or a card the rules do not allow), it slides back into its place; the refusal
  still flashes the prompt and nudges. A tap plays nothing. Sliding along the hand still lifts the
  card under the finger, and the card the pull starts on is the one thrown. While the hand takes no
  plays a touched card is only looked at, as before.
- **The deal is shuffled in the middle of the table and dealt from there.** The round's 52 cards
  appear as a deck in the middle of the table, face down and lying in the table's perspective at
  the trick's size. They split into two halves that slide apart, riffle back together to the
  shuffle's sound, and square up (1.1 s). The deal then takes the top card each time, in the same
  order and time as before (1.6 s), so a round now starts in 2.7 s. With Reduce motion nothing of
  this plays: the dealt table fades in, as before.

**Consequences.**
- The saved `nameNudgeSeen` setting is still read and written, but nothing uses it, like the sort
  order of ADR 021; both can go with a change to the settings file.
- The deck uses the scene's 52 card views (no extra ones). A deal skipped with a tap puts the cards
  not yet dealt back at once.
- GDD §8 (the resume offer's words), §11 screen 08 (playing a card), §14 (the deal) and §15 (the
  name) describe the old behaviour until the GDD is updated. The name panel's three lines are new
  copy for the owner to review: "What's your name?", "Enter your name", "Continue".
