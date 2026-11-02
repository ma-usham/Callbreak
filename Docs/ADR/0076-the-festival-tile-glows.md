# ADR 076 - The festival offer's tile glows

**Status:** accepted (owner, 3 Nov 2026), with a screenshot of the shop's themes, Tihar Lights' tile
saying "Free" under its Festival tag:

> in free text is looking so off the theme should like glow or something so that the user clicks on it.

## Context

A festival offer's theme tile (ADR 067) had its marigold Festival tag and "Free" in crimson where the
price goes. Next to the other tiles' coin prices it read as plain text, nothing that asked to be tapped.

## Decision

- **While a festival offer's theme can be claimed, its tile glows** like a reward ready to collect
  (ADR 069, the same `ClaimHighlight`):
  - a warm gold halo round the whole tile, behind it and reaching 16 units past its edge, breathes
    every 2 seconds, using the Daily Rewards tile's halo art;
  - at each breath's height the tile's "Free" swells by 12%.
- The glow goes as soon as the offer is claimed or ends.
- With Reduce motion the halo stays lit and nothing moves.
- Every theme tile in the Shop prefab has the glow, hidden. It was added in place
  (`ShopCustomizer.AddOfferGlows`) and full builds add it too. The shop turns it on only on the
  offer's tile.

## Consequences

- The offer's tile is the one on the shelf that moves, so the free theme is hard to miss.
- Not verified in the Editor at the owner's request; the code compiles and the prefab holds a glow
  on each of its 10 theme tiles.
