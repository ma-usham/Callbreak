# Callbreak.Core: public API

The rules engine (GDD §2–§5 rules, §7 state machine, §22 domain model). Pure C# 9 on netstandard2.1:
no UnityEngine, no clock, no statics with state. Deterministic: the same `RuleSet`, the same
`IRandom` sequence and the same commands always produce the same events. Not thread-safe: drive
one `MatchEngine` from one thread; `PlayerView` and everything it returns is immutable and may cross
threads. Invalid input returns a result; exceptions mean a bug in the caller or the engine.

Units: seats are absolute `South 0, East 1, North 2, West 3`; cards are ids 0–51
(`suit * 13 + rank - 2`, ♣ 0 · ♦ 1 · ♥ 2 · ♠ 3; 2♣ = 0, A♥ = 38, A♠ = 51); scores are integer tenths
(41 = 4.1). Rounds are numbered from 1; trick indexes from 0 ("Trick 1 / 13" is index 0).

## Cards (`Callbreak.Core.Cards`)

| Type | Members |
| --- | --- |
| `enum Suit : byte` | `Clubs, Diamonds, Hearts, Spades` (spades are always trump) |
| `enum Rank : byte` | `Two = 2 … Ten = 10, Jack = 11, Queen = 12, King = 13, Ace = 14` |
| `readonly struct Card` | `Card(Suit, Rank)`, `static Card FromId(int)`, `int Id`, `Suit Suit`, `Rank Rank`, `bool IsTrump`, `static bool TryParse(string, out Card)`, `static Card Parse(string)`, `ToString()` → `"A♠"`, `"10♥"`. Parses `"A♠"`, `"AS"`, `"10h"`, `"Th"`. `default(Card)` is 2♣. |
| `readonly struct CardSet` | 52-bit mask. `Empty`, `FullDeck`, `ulong Bits`, `static FromBits(ulong)`, `static Of(Card)`, `static OfSuit(Suit)`, `static Above(Card)` (higher cards of the same suit), `Count`, `IsEmpty`, `Contains`, `HasSuit`, `InSuit(Suit)`, `With`, `Without`, `Union`/`\|`, `Intersect`/`&`, `Except`/`-`, `IsSubsetOf`, `Overlaps`, `Highest()`, `Lowest()` (by id; on a one-suit set, by rank), `ElementAt(int)` (ascending id), allocation-free `foreach`, `Parse`/`TryParse` (`"A♠ K♠ 10♥"`, `"-"` = empty), `ToString()` (highest id first). Nothing allocates except `ToString`/`Parse`. |
| `static class Deck` | `Size = 52`, `HandSize = 13`, `Card[] Shuffled(IRandom)` (Fisher–Yates from ids 0–51, exactly 51 draws), `CardSet[] Deal(IReadOnlyList<Card> order, Seat firstSeat, Direction)` (one card at a time, indexed by seat). |

## Rules (`Callbreak.Core.Rules`)

`sealed class RuleSet` (immutable, value equality): constructor
`RuleSet(id, rounds, minCall, maxCall, direction, mustBeatLedSuit, mustTrumpWhenVoid, mustOvertrump, discardWhenCannotOvertrump, overtrickTenths, redealIfNoSpade, redealIfNoHonour, bigCallBonus, autoPlayLastTrick, autoPlayForcedMoves)`
validates ranges (rounds 1–10, calls 1–13 with min ≤ max, overtrick 0–10) and throws
`ArgumentException`/`ArgumentOutOfRangeException`. `RuleSet.NepaliStandard` (id `"nepali-standard"`):
5 rounds, calls 1–8, anticlockwise, all four legality switches on, overtrick 1, no-spade redeal on,
no-honour redeal off, big-call bonus off, last trick auto-plays, forced moves do not. Derive variants
with `With(...)`, e.g. `RuleSet.NepaliStandard.With(mustOvertrump: false)`; give production
variants their own `id`. `PlayerCount` is always 4.

| Type | Members |
| --- | --- |
| `enum Direction` | `Anticlockwise` (next seat = +1 mod 4), `Clockwise` (+3 mod 4) |
| `readonly struct Trick` | `Trick(Seat leader, Direction)`, `With(Card)`, `Leader`, `Direction`, `Count`, `IsEmpty`, `IsComplete`, `this[int]` (play order), `SeatAt(int)`, `NextSeat`, `LedSuit` (throws when empty), `Cards`, `HasSpade`, `IsTrumped` (spade on a non-spade lead), `TryGetCardOf(Seat, out Card)`. `ToString()` → `"West: 9♥ Q♥"`. |
| `readonly struct CompletedTrick` | `CompletedTrick(int index, Trick)`, `Index`, `Trick`, `Leader`, `Winner`, `WinningCard` |
| `static class LegalMoves` | `CardSet Compute(CardSet hand, in Trick, RuleSet)` (GDD §22 algorithm; allocation-free; never empty for a non-empty hand), `PlayObligation Classify(...)`, `bool IsLegal(Card, CardSet hand, in Trick, RuleSet)` |
| `enum ObligationKind` | `LeadAny`, `BeatCard`, `FollowSuit`, `TrumpWithSpade`, `AnyCard` (GDD §11 screen 08 prompts) |
| `readonly struct PlayObligation` | `Kind`, `Legal` (== `Compute`), `Suit` (led suit for BeatCard/FollowSuit, ♠ for TrumpWithSpade), `HasCardToBeat`, `CardToBeat` (BeatCard; TrumpWithSpade when it must overtrump) |
| `static class TrickResolver` | `WinningCard`, `WinningIndex`, `Winner` (`in Trick`): highest spade, else highest led-suit card; works on partial tricks |
| `static class CallRules` | `NotCalled = -1`, `IsValid(int call, RuleSet)`, `Clamp(int call, RuleSet)` (for auto-calls) |
| `static class RedealPolicy` / `enum RedealReason` | `Check(IReadOnlyList<CardSet> hands, RuleSet)` → `None`, `NoSpade`, `NoHonour` (NoSpade wins if both); `HasHonour(CardSet)` (any J/Q/K/A) |

## Scoring (`Callbreak.Core.Scoring`)

| Type | Members |
| --- | --- |
| `static class ScoreCalculator` | `RoundScore(int call, int won, RuleSet)` → met: `call*10 + (won-call)*OvertrickTenths`, missed: `-call*10`, big call (bonus on, call ≥ 8, met): 130. `IsMet(call, won)`. `BigCallThreshold = 8`, `BigCallTenths = 130`. |
| `static class ScoreTenths` | `Format(41)` → `"4.1"`, `Format(-30)` → `"−3.0"` (U+2212), `FormatSigned(41)` → `"+4.1"`, `FormatSigned(0)` → `"0.0"`. Culture-invariant. `MinusSign`. |
| `readonly struct SeatRoundResult` | `Seat`, `Call`, `Won`, `DeltaTenths`, `TotalTenths`, `Met` |
| `sealed class RoundResult` | `Round`, `this[Seat]`; constructor checks one line per seat and tricks totalling 13 |
| `sealed class ScoreSheet` | Immutable. `Empty`, `Rounds`, `Append(calls, won, RuleSet)`, `Append(RoundResult)`, `TotalOf(Seat)`, `RoundsMetBy(Seat)`, `BestRoundOf(Seat)` |
| `readonly struct Standing` | `Seat`, `Rank` (1–4), `IsShared`, `TotalTenths`, `RoundsMet`, `BestRoundTenths` |
| `static class Standings` | `Compute(ScoreSheet)` or `Compute(totals, roundsMet, bestRounds)` → 4 standings sorted by rank then seat; `Compare(in a, in b)`. Tie-breaks: total, rounds met, best round; still tied → shared place (1, 1, 3, 4). |

## Randomness (`Callbreak.Core.Randomness`)

`interface IRandom { int NextInt(int maxExclusive); }`. `Pcg32Random(ulong seed[, ulong stream])`:
PCG-XSH-RR, `NextUInt32()`, `State`, `Increment`, `static FromState(state, increment)`.
`CryptoRandom`: OS CSPRNG, `NextSeed()`. For a replayable production match draw a seed with
`CryptoRandom.NextSeed()`, run on `new Pcg32Random(seed)`, and log the seed privately (GDD §22).

## Match (`Callbreak.Core.Match`)

- `enum Seat : byte` + `SeatExtensions`: `Next(Direction)` (the only way to advance a seat),
  `Advance(Direction, steps)`, `StepsTo(to, Direction)`, `RelativeTo(viewer)` (0 bottom, 1 right,
  2 top, 3 left), `IsValid()`, `Count = 4`.
- `enum Phase`: `Lobby, Dealing, Calling, Playing, TrickResolve, RoundScoring, RoundSummary, MatchOver, Closed`.
  Between calls you only ever see `Lobby, Calling, Playing, RoundSummary, MatchOver, Closed`.
- `readonly struct TurnId`: `Value`, `IsNone`, `None`. The first turn is 1; +1 for every call or play turn.
- `sealed class SeatProfile(string name, string avatarId, string deckThemeId, bool isBot)`.
- `sealed class MatchSetup(string matchId, IReadOnlyList<SeatProfile> seats, Seat? firstDealer = null)`.

```csharp
public sealed class MatchEngine
{
    public MatchEngine(RuleSet rules, IRandom rng, MatchSetup setup);
    public Phase Phase { get; }
    public Seat CurrentSeat { get; }        // meaningful while CurrentTurn is not None
    public TurnId CurrentTurn { get; }      // None outside Calling/Playing
    public RuleSet Rules { get; }  public string MatchId { get; }  public int Round { get; }
    public void Start();                    // once, in Lobby; else InvalidOperationException
    public CommandResult Execute(in PlaceCall cmd);
    public CommandResult Execute(in PlayCard cmd);
    public CommandResult Execute(in ReadyNext cmd);
    public void Close();                    // any phase -> Closed; later commands: WrongPhase
    public PlayerView ViewFor(Seat seat);
    public MatchSnapshot Snapshot();
    public static MatchEngine Restore(MatchSnapshot s, RuleSet r, IRandom rng);   // ArgumentException if invalid
    public static bool TryRestore(MatchSnapshot s, RuleSet r, IRandom rng, out MatchEngine engine, out string error);
    public int DrainEvents(List<MatchEvent> into);   // appends oldest first, clears the outbox
    public string FindInvariantViolation();          // null when every GDD §22 invariant holds
    public void AssertInvariants();                  // throws InvalidOperationException otherwise
}
```

`static class MatchReplay`: `List<MatchEvent> Run(RuleSet, ulong seed, MatchSetup, IReadOnlyList<RecordedCommand>)`
(original ran on `new Pcg32Random(seed)`), or `Run(RuleSet, IRandom, MatchSetup, commands)`.

## Commands (`Callbreak.Core.Commands`)

`PlaceCall(Seat, int call, TurnId, bool isAuto = false)`, `PlayCard(Seat, Card, TurnId, bool isAuto = false)`,
`ReadyNext(Seat)`: readonly structs. `isAuto = true` when a timer or bot acts for a human.
`CommandResult`: `IsAccepted`, `Reason`, `Accepted`, `Rejected(reason)`.
`RecordedCommand.Of(in cmd)` / `ExecuteOn(engine)` / `Kind` builds a command log for replays.

Validation order (GDD §7); the first failure is returned and nothing changes:

| # | Check | `RejectReason` |
| --- | --- | --- |
| 1 | Phase accepts the command (Calling / Playing / RoundSummary) | `WrongPhase` |
| 2 | Seat is 0–3 | `InvalidSeat` |
| 2 | Seat holds the open turn (not checked for ReadyNext) | `NotYourTurn` |
| 3 | `TurnId` equals `CurrentTurn` (not for ReadyNext) | `StaleTurn` |
| 4 | Call within MinCall…MaxCall | `InvalidCall` |
| 4 | Card in hand, then card legal | `CardNotInHand`, `IllegalCard` |
| 4 | Seat not ready yet | `AlreadyReady` |

A late or double-tapped intent from a seat whose turn has passed is `NotYourTurn`; one from the seat
that now holds a newer turn is `StaleTurn`. Both are harmless.

## Events (`Callbreak.Core.Events`)

`abstract class MatchEvent { EventVisibility Visibility; }`; closed catalogue, all sealed with public
constructors (so a client can rebuild them from DTOs). `EventVisibility`: `Public`, `Private(seat)`,
`IsPublic`, `Seat`, `IsVisibleTo(viewer)`. Only `HandDealt` and `LegalHint` are private. `ToString()` is
stable and culture-invariant; private events end with ` [to Seat]`.

| Event | Payload |
| --- | --- |
| `MatchStarted` | `MatchId`, `RuleSetId`, `FirstDealer`, `Seats` |
| `RoundStarted` | `Round`, `Dealer` |
| `Redealt` | `Round`, `Reason` (NoSpade / NoHonour); one per redeal, same dealer |
| `HandDealt` (private) | `Seat`, `Hand` |
| `CallTurnStarted` | `Seat`, `TurnId` |
| `CallPlaced` | `Seat`, `Call`, `IsAuto` |
| `PlayTurnStarted` | `Seat`, `TurnId`, `Suit? LedSuit` (null when leading) |
| `LegalHint` (private) | `Seat`, `TurnId`, `Legal` |
| `CardPlayed` | `Seat`, `Card`, `IsAuto`, `IsForced` |
| `TrickCompleted` | `Completed` (`TrickIndex`, `Trick`, `Winner`) |
| `RoundScored` | `Round`, `Result` (per seat call, won, delta, total) |
| `ReadyConfirmed` | `Seat` (addition to the GDD catalogue) |
| `MatchEnded` | `Standings` |

Exact order:

- `Start()`: `MatchStarted`, `RoundStarted(1, dealer)`, `Redealt`\*, 4 × `HandDealt` (dealer's right
  first, in play direction), `CallTurnStarted(dealer's right, turn 1)`.
- Accepted call: `CallPlaced`, then `CallTurnStarted(next)`, or after the 4th call the first play turn.
- Play turn: `PlayTurnStarted(seat, turn, led)`, `LegalHint(seat, turn, legal)`.
- Accepted play: `CardPlayed`; after the 4th card `TrickCompleted`; then the next play turn (the winner
  leads a new trick).
- Forced play (`AutoPlayLastTrick`: one card in hand; `AutoPlayForcedMoves`: one legal card): only
  `CardPlayed(seat, card, IsAuto = true, IsForced = true)`. No turn id, no `PlayTurnStarted`, no hint.
  With the default rules trick 13 is four forced plays. Show an "auto" tag only for
  `IsAuto && !IsForced`.
- After trick 13: `RoundScored`, then `ReadyConfirmed` for every bot seat (seat order). Phase is
  `RoundSummary`, `CurrentTurn` is None.
- Accepted `ReadyNext`: `ReadyConfirmed(seat)`. When all four are ready: before the last round
  `RoundStarted(r + 1, dealer.Next)` and the deal as above; after it `MatchEnded`, phase `MatchOver`.
  With four bot seats the summary is skipped immediately. When a summary countdown expires, submit
  `ReadyNext` for every seat where `view.IsReady(seat)` is false.

## Views (`Callbreak.Core.Views`)

`sealed class PlayerView` (immutable, value equality, no link to the engine): `Viewer`, `Rules`,
`MatchId`, `Seats`, `Phase`, `Round`, `TrickIndex`, `Dealer`, `FirstDealer`, `CurrentSeat`,
`CurrentTurn`, `IsMyTurn`, `Hand` (the viewer's cards only), `CallOf(seat)` (−1 = not called),
`HasCalled(seat)`, `TricksWonBy(seat)`, `HandCountOf(seat)` (the only data about other hands),
`IsReady(seat)`, `CurrentTrick`, `CompletedTricks` (this round, with winners), `TryGetPreviousTrick(out)`,
`Scores` / `History` / `TotalOf(seat)`, `FinalStandings` (empty until MatchOver), `LegalCards()` (empty
unless it is the viewer's play turn), `TryGetObligation(out PlayObligation)`. Its public constructor
exists so a client can rebuild a view from a network snapshot.

`MatchSnapshot` (`[Serializable]`, public fields only, JsonUtility-friendly): `schemaVersion` (1),
`ruleSetId`, `matchId`, `phase`, `round`, `hasFixedFirstDealer`, `firstDealer`, `dealer`,
`currentSeat`, `currentTurn`, `turnCounter`, `seats[4]` (`SeatSnapshot`: profile, `hand` ids, `call`,
`tricksWon`, `ready`), `currentTrick` / `completedTricks[]` (`TrickSnapshot`: `leader`, `cards`),
`rounds[]` (`RoundSnapshot`: `round`, `calls`, `won`, `deltas`), `matchEnded` (final standings exist,
also after `Close()`). It contains every hand: authority only.

Snapshot rules: take one in any phase (the GDD takes them at trick boundaries and during calling);
drain events first, they are not part of it. `Restore` needs the same `RuleSet` (id checked; deltas are
re-scored, so changed scoring values are refused), validates every field and every invariant, and
never repairs: on failure discard the file. The restored engine's views equal the original's, it
accepts the same next command, and its outbox is empty. Future shuffles use the `IRandom` you pass; save
`Pcg32Random.State`/`Increment` beside the snapshot and use `FromState` to continue a seeded match exactly.

## Invariants

`FindInvariantViolation()` checks: all 52 cards held once across hands, current trick and completed
tricks; hand sizes `13 − completed` (−1 after playing to the current trick); per-seat tricks won equal
completed tricks won; each trick led by the previous winner; dealer rotation; the current seat is next
in rule direction; the open turn is the newest id; calls in turn order and in range before any card;
each delta follows the rules and each total is the sum of deltas; standings sorted and consistent with
the tie-breaks.

## Snippets

Drive a match (offline session; bots and timers submit ordinary commands):

```csharp
var engine = new MatchEngine(RuleSet.NepaliStandard, new Pcg32Random(seed), setup);
var events = new List<MatchEvent>();
engine.Start();
while (engine.Phase != Phase.MatchOver)
{
    engine.DrainEvents(events);                  // hand these to the MatchEventPlayer, by visibility
    PlayerView view = engine.ViewFor(engine.CurrentSeat);
    if (engine.Phase == Phase.Calling)
        engine.Execute(new PlaceCall(view.Viewer, CallRules.Clamp(estimate, view.Rules), view.CurrentTurn));
    else if (engine.Phase == Phase.Playing)
        engine.Execute(new PlayCard(view.Viewer, view.LegalCards().Lowest(), view.CurrentTurn));
    else if (engine.Phase == Phase.RoundSummary)
        for (int s = 0; s < 4; s++) engine.Execute(new ReadyNext((Seat)s));   // AlreadyReady is harmless
}
```

Turn prompt and a rejected tap (UI):

```csharp
PlayerView view = session.CurrentView;
if (view.TryGetObligation(out PlayObligation o))
{
    string prompt = o.Kind == ObligationKind.BeatCard ? "Beat " + o.CardToBeat
                  : o.Kind == ObligationKind.FollowSuit ? "Follow " + o.Suit : o.Kind.ToString();
    bool dimmed = !o.Legal.Contains(tappedCard);        // explain with the same obligation
}
```

Replay a logged match:

```csharp
var log = new List<RecordedCommand>();                  // RecordedCommand.Of(cmd) for every command sent
List<MatchEvent> replayed = MatchReplay.Run(rules, seed, setup, log);
// replayed[i].ToString() == recorded[i].ToString() for every i
```
