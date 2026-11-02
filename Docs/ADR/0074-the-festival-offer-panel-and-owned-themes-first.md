# ADR 074 - The Festival Offer panel on the main menu; the player's themes first in the shop

**Status:** accepted (owner, 2 Nov 2026), with a screenshot of the offer as it opened over the shop:

> this should be on the main menu screen like daily login popups panel then the panel should say
> something like Festival offer, manage text on your own and other are good. then in shop right now
> the theme are arranged according to price right the free one should come after owned themes. if you
> need new panel for festival offer panel create it.

## Context

- **The offer opened over the shop.** When a festival offer opens by itself on the bare main menu
  (ADR 067), the main menu pushed the shop, opened straight on the offer theme's preview. The
  shop's own page stayed visible behind the preview's dim: its back arrow, its title, the Themes and
  Cards tabs and its tiles. The panel was titled with the theme's name ("Tihar Lights") over "One
  purchase, both looks". It read as the shop, not as a popup on the main menu the way Daily Rewards
  reads.
- **The shop's themes ran by price.** Only the one bought last came first (ADR 064). A festival
  offer's free theme sat at its full price's place, often last, and the player's other themes were
  scattered by price.

## Decision

- **The Festival Offer panel.** When the offer opens by itself, the shop shows only the preview over
  the menus' shared background (ADR 058's addendum):
  - The shop's page (its `Content`: header, tabs, tiles, wallet) is hidden.
  - The panel stands alone on its dim, as Daily Rewards' panel does.
  - Its title says **Festival Offer** (`ui.offer.popup.title`), and the line under it names the
    theme: **"TIHAR LIGHTS · FOR A FEW DAYS ONLY"** (`ui.offer.popup.subtitle`).
  - The rest stays as the owner liked it: the two halves, their captions, the offer's words, "Watch
    ad • Free" (then Equip theme in the same place), and the cross back to the main menu.
  - No wallet in its corner (the owner, 3 Nov 2026: "remove this coin section form festival offer").
    A preview opened from the shop keeps it.
  - Opened from the Shop button, the shop and its previews are as before.
  - This is the shop's own screen in a mode of its own, so the Menu scene, which holds the owner's
    scene changes, needs no new screen. The Shop prefab gains two references, patched in place
    (`ShopCustomizer.BindFestivalPanel`, which full builds also bind): the view's page and the
    preview's subtitle.
- **The shop's theme order:**
  1. the theme bought last;
  2. the player's other themes;
  3. a festival offer's theme;
  4. the rest.

  Each group runs from the cheapest to the dearest, in catalogue order where prices tie
  (`ShopDisplay.ThemeOrder`). The shop still opens scrolled to the offer's theme, now right after
  the player's own.

## Consequences

- The offer looks like a main menu popup and says it is a festival offer, not a shop page.
- Tests:
  - `ShopStateRulesTests` checks the new order, owned first and then the offer before the rest by
    price; its tile statuses and the offer's index follow it;
  - `FestivalOfferFlowTests` checks the panel's title, its subtitle, the hidden page, and the page
    back when the Shop button opens the shop.
- dotnet 2447/2447. The PlayMode suite was not run at the owner's request; the code compiles.
