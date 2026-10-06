# ADR 039 - Six shuffles, one at random

**Status:** accepted (owner, 6 Oct 2026).

**Context.** ADR 022 put a shuffle in the middle of the table before every deal: the deck splits in
two and the halves riffle together. The owner asked: "add some random shuffle. right now we have
only one kind of shuffle." Later the same day, with four built, the owner asked for two more,
"so that there will be 5". That makes six; the owner can drop one.

## Decision

- **Six styles** (`Presentation/Table/Playback/ShuffleStyles.cs`):
  - **Riffle**: the deck splits in two and the halves riffle together (the one the game had,
    `ShuffleAnimation`, unchanged).
  - **Overhand**: the deck slides left. Packets of 6 to 11 cards leave its top one after another in
    little hops and land on a new pile to the right, so the first packet ends at the bottom. The new
    pile slides back to the middle.
  - **Three-way cut**: the deck's top third slides up, its middle third left and its bottom third
    right. The left packet comes back to the middle, the right one lands on it, and the top one
    drops on both.
  - **Wash**: the cards spread over the felt in a loose ring, each turned its own way. They swirl
    round together and gather back into a deck in the order the swirl left them.
  - **Fan**: the deck spreads from left to right into an arc across the felt, splayed more
    towards the ends, and holds a beat. It then sweeps closed from a cut: the cards from the cut
    to the right end first, and those from the left end to the cut on top of them.
  - **Pinwheel**: the cards bloom into a wheel round the middle, each turned a step further than
    the one below. The wheel turns a little, then the cards fold back into a deck one after
    another, top card first.
- **Which one shows.** Each deal picks a style at random, never the one the deal before showed
  (`ShuffleStyles.Next`). The first deal of a table can be any of the six. Each style also varies
  a little from deal to deal: packet sizes, cut points, where the washed cards lie, and which way
  the wheel turns.
- **The same frame for all.** Every style:
  - starts as the riffle did, with the deck dropping onto the felt as it fades in;
  - plays the shuffle sound once;
  - lasts the riffle's 1.1 s;
  - ends with the 52 cards squared in the middle in a new order, the top card drawn last.

  The deal then takes them from the top as before.
- **Only the look.** The style is chosen on each device (`System.Random` in `TablePresenter`),
  online too, and has no bearing on the cards dealt: those come from the match. With Reduce motion
  there is still no shuffle (the dealt table fades in).

## Consequences

- **Pacing unchanged.** The shuffle and the deal still take 1.1 s + 1.6 s, and the bots' 2.7 s wait
  for them (ADR 024) holds for every style.
- **Tests.** `ShuffleStylesTests` (EditMode, on the table prefab's card views) covers each style's
  length, sound, squared end and new order, also when cut short or finished before it begins, and
  the pick that never repeats.
- **Taps** skip none of them (ADR 038).
