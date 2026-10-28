# ADR 055 - Bots wait for the card before to land; a forced card rests 0.5 s

**Status:** proposed (28 Oct 2026). The owner: "sometime the AI throws card so fast that we cant see
while playing ... check that too and fix that".

**Context.**
- **GDD §6 and §9:** bots think 0.6–1.4 s offline and 0.8–1.6 s online, and 0.35 s when the move is
  forced. "Bots never act instantly. A short, varied think feels human and keeps the table readable."
- **GDD §14:** a played card flies to the trick in 260 ms (OutCubic).
- **What the code did:** `BotDriver` started a bot's think time when its turn opened, which is the
  moment the card before was played, while that card was still flying for 0.26 s.
  - A normal move left the card before 0.34–1.14 s on the table.
  - A forced move left it about 0.1 s, so the cards looked thrown back to back.
  - The last trick, which the engine plays by itself, waited 0.35 s before each card.
- **Measured:** 40 simulated matches of four Normal bots, run through the table's real event queue
  (`MatchEventPlayer`), about 7,800 cards.
  - 24.9 % of cards were thrown within 0.15 s of the card before landing.
  - 35 % within 0.4 s.
  - The median rest was 0.55 s. For forced moves it was 0.10 s.

## Decision

- **A bot's think time starts once the card before has landed.**
  - `BotDriver.Observe` holds the bots for the card's flight on every `CardPlayed`
    (`BotTiming.CardFlightSeconds`).
  - The flight is `BotTiming.TableCardFlightSeconds`, 0.26 s, the same value as `CardFlight.Seconds`.
  - Offline and hosted tables use it; `BotTiming.Instant` (tests, simulations) waits for nothing.
- **A finished trick's pause** (0.9 s shown, 0.35 s swept) **starts once its last card has landed**,
  as the table shows it.
- **A forced move waits 0.5 s** instead of 0.35 s, counted from the landing, offline and online.
  - The table's pause before each card of the last trick (`MatchEventPlayer.ForcedBeatSeconds`)
    is 0.5 s too.
  - A tap still skips it, as any pause longer than 0.3 s.
- **Unchanged:**
  - the think ranges (0.6–1.4 s, 0.8–1.6 s);
  - the 260 ms flight;
  - the deal;
  - double speed for a table that has fallen behind.

## Consequences

- **The same simulation now:**
  - no card is thrown within 0.15 s of the card before landing;
  - 0.1 % within 0.4 s (round ends in a four-bot simulation, where no one waits on the results);
  - the median rest is 0.82 s, and 0.52 s for forced moves.
- **A round with three bots takes longer:** each bot card waits about 0.3 s more (the flight, plus
  0.15 s when it is forced), roughly 10–12 s a round.
- **GDD §9's "0.35 s if forced" is now 0.5 s**, counted from the landing.
- **Tests:**
  - `BotPaceTests` (new): each card's wait from the card before's landing; the next lead's wait
    after the trick is shown; one flight and one forced pause shared by the bots and the table.
  - `TablePaceTests` (new, Unity): the bots' flight is `CardFlight.Seconds`.
  - `MatchEventPlayerTests` (the 0.5 s pause) and `ShuffleSecrecyTests` (its model of the bots'
    holds) follow the new values.
