# ADR 056 - Friendlier offline deals: the match chooses the seed that deals the player the best hands

**Status:** proposed (28 Oct 2026). The owner: "in offline game can you make that the user gets good
cards so that he has the chances to win? to make offline easy for player in all levels."

**Context.**
- **An offline match is dealt from one seed.** `OfflineMatchSession` draws it from the
  cryptographic generator and runs the engine on `Pcg32Random(seed)`. The engine uses that
  generator only to draw the first dealer and to shuffle (Fisher–Yates, GDD §4), so the seed alone
  fixes all five of the player's hands, whatever anybody plays.
- **Measured.** Whole offline matches were simulated with a stand-in for the player in the South
  seat (staging simulation, `FriendlyDealSim`). With fair deals, the player won:

  | Player's skill | vs Easy bots | vs Normal bots | vs Hard bots |
  | --- | ---: | ---: | ---: |
  | Normal | 56 % | 27 % | 10 % |
  | Easy (a casual player) | 23 % | 5.5 % | 2.5 % |

  The Easy and Normal columns are from 400 matches each, the Hard column from 40.

## Decision

- **How a new offline match picks its seed (`FriendlyDeal`, Application):**
  - It draws a few seeds from the same cryptographic generator.
  - It works out each seed's five deals as the engine will deal them (`FriendlyDeal.PlayerHands`).
  - It keeps the seed whose deals give the player the strongest hands, the sum of the bots' own
    trick estimate (GDD §9 `CallEstimator`).
- **The harder the bots, the more draws:** Easy 3, Normal 6, Hard 12 (`FriendlyDeal.Candidates`).
- **Everything else is unchanged.**
  - Every shuffle is still the engine's fair one; only which of them the match gets is chosen.
  - Nothing changes in Core: no rule, no shuffle, no engine API.
  - The bots play as before and see only their own hands.
  - A seed plus the command log still replays the match.
  - A resumed match keeps its saved seed.
- **Online is untouched.** Hosted tables, Quick Match and private rooms deal from their own seeds
  (GDD §4 "the server shuffles with a cryptographically secure RNG"), so play between people stays
  fair.

## Consequences

- **The same simulation with friendlier deals:**

  | Player's skill | vs Easy bots (3 draws) | vs Normal bots (6) | vs Hard bots (12) |
  | --- | ---: | ---: | ---: |
  | Normal | 83 % | 71 % | 58 % |
  | Easy (a casual player) | 46 % | 24 % | 20 % |

  With 12 draws against Normal bots, a Normal-skill player wins 83 % and a casual one 38 %.
- **Tuning:** the number of draws is the one knob. One draw is a plain fair deal; more draws help
  more, with diminishing returns.
- **The help is not shown anywhere:** no setting and no text. The owner decides whether the game
  should say so.
- **Offline wins and win rates in the player's stats** come easier. Online stats and anything with
  stakes are unaffected.
- **Cost:** choosing the seed shuffles 5 × 12 decks at most when a match starts. That is
  microseconds.
- **Tests:** `FriendlyDealTests`:
  - the hands worked out from a seed are the hands the engine deals, every round, for several seeds;
  - the strongest draw is kept;
  - one draw is the fair draw;
  - the harder the bots, the more draws;
  - a new offline match is dealt from the friendliest draw, and a resumed one draws nothing.
