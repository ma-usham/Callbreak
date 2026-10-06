# ADR 040 - "Asha is calling…" in the prompt above the hand

**Status:** accepted (owner, 6 Oct 2026).

**Context.** While another seat made its call, the middle of the table read "Asha is calling…",
where the call panel appears on the player's own turn (GDD §11 screen 07). The owner asked that
the line show in the tip area near the player instead: the prompt pill above the hand, which
names or explains the turn during play.

## Decision

- **While another seat calls**, the prompt above the hand reads "{name} is calling…"
  (`ui.bidding.calling`, as before), with the same pill, fade and font as the turn prompts and tips.
- **The middle of the table stays clear.** The bidding panel's own calling line is never shown now
  (`BiddingPanelView.SetCalling(null)`), and the line stays in the prefab, unused.
- **On the local call turn** the pill is hidden and the call panel speaks, as before.
- **From the first play on**, the pill names or explains the turn as before.

## Consequences

- `TablePresenter.RenderPrompt` now handles the calling phase too. `BiddingPresenter` no longer
  names the caller.
- **Checks.** The table simulation looks for the line in the prompt, with the middle of the table
  clear. TurnPromptTests still covers the copy.
- **The GDD is out of date here:** screen 07's centre line.
