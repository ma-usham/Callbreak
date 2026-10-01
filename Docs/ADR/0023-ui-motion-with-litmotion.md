# ADR 023 - One way of moving for the whole UI, with LitMotion

**Status:** accepted (owner, 1 Oct 2026).

**Context.** The owner asked for smoother UI animation, all UI and panels, with LitMotion (the
tween library the project already uses) rather than adding PrimeTween. Until now:
- GDD §14 moved a screen change with a fade and a 24 px slide (220 ms, OutCubic).
- The dialog and the name panel faded with an 18 px rise.
- Many parts appeared or vanished at once: the emote picker, the turn prompt, the call panel at
  the start of the call turn, the crimson call chip, the seat chips, the end of a seat's bubble, the
  Reconnecting cover, the "I'm back" banner, the first How to Play's rules, toasts (fade only),
  and the selected look of option cards.
- A button let go simply scaled back (90 ms).

**Decision.** One set of motions, `UiMotion` in Presentation/Framework, on unscaled time:
- **Pop in:** from 92% with a soft overshoot (OutBack, 300 ms), the fade done early.
- **Pop out:** to 96% while fading (InCubic, 140 ms).
- **Swell:** 4-5% and back (220 ms).
- **Rise:** 36 px and 94% into place (300 ms), lists 60 ms apart.

With Reduce motion only the fades are left, everywhere. What a part says (showing, open, up) changes
at once; only its look moves, so a picker or prompt on its way out takes no taps.

- **Screens** (280 ms): the arriving screen fades in, slides 36 px in the direction of travel
  (OutQuart) and grows from 98.5%. The leaving one slides on and is faded out by 60% of the time.
  This replaces GDD §14's 24 px, 220 ms.
- **The match menu** is a panel over the table, not a page. It does not slide: its dim fades with
  the screen, and its panel pops in and shrinks a touch as it goes.
- **The dialog and the name panel:** the dim fades in (180 ms) as the panel pops in. Closing, the
  panel shrinks as everything fades (140 ms; the name panel 220 ms).
- **The first How to Play** (ADR 022): when the name is answered, its dim fades in over the same
  220 ms as the name panel's fades out, so together they stay within a few percent of one dim. The
  rules pop in 100 ms later.
- **The main menu:** the first time it shows after the menu loads, the three play buttons and the
  line under them (the Resume banner or the footer) rise into place, 60 ms apart, after 120 ms.
- **Buttons** press as before (0.97 in 90 ms). Let go, they spring back 2% past their size and
  settle (220 ms).
- **Toasts** drop 20 px into place as they fade in, and lift 12 px as they fade out.
- **Option cards and segmented controls:** a tapped option's selected look fades in (140 ms). One
  set by a presenter is simply there.
- **The table:**
  - The turn prompt pops in from 90%, swells when its words change, and shrinks as it fades out.
  - The emote picker grows out of its corner by the emote button and shrinks back into it.
  - When the local call turn begins, the call panel rises 24 px as it fades in: its slide away,
    backwards.
  - The crimson call chip pops in on the first choice and glides to the next (160 ms, OutCubic).
  - Seat chips pop in.
  - A seat's bubble shrinks and fades when its time is up, instead of vanishing.
  - The Reconnecting cover fades in, and the "I'm back" banner pops in. Both go at once when they
    are no longer needed.

**Consequences.**
- GDD §14's screen change and dialog rise, and §12/§14's button release, describe the old motion
  until the GDD is updated. The timings are new for the owner to review on a phone.
- The table simulation (TableSimulation) settles the new table motions before it checks or
  captures, as it already did for the hand and the screens.
- No package was added and no prefab rebuilt. Parts that fade get a CanvasGroup the first time
  they need one.
