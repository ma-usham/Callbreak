# ADR 077 - Theme and deck prices for the 500 starting coins; no ads in the Editor

**Status:** accepted (owner, 3 Nov 2026):

> tell me the starting coin in 500 . I want to set the price for all the theme and card accordingly so
> that it balances the economy. i want with the default money the user can atleast purchase one theme,
> then he has to warn money to purchase other themes.

To the proposed tiers: "apply the theme and card price". Also: "remove that test ads from the
system. I will check on android device."

## Context

- **Earning.** A new player starts with **500 coins** (`WalletRules.StartingCoins`). Coins come from:
  - Daily Rewards: 10, 15, 20, 25, 30, 40, 60, so 200 a week (ADR 046);
  - Earn Coins' ads: 20, 30 and 50 a set, a new set 5 hours after a set's first ad (ADR 033);
  - Stake Match: stake 50 or 100, first place nets +50 or +100, fourth −30 or −60 (ADR 034).

  Offline matches, Quick Match and private rooms pay nothing. A casual player earns about 130 coins
  a day (the day's reward and one set of ads), an active one 300 to 500.
- **Prices.** They were placeholders (ADR 064, 068): themes at 500, 750 and 1,000, and decks at 500
  and 750, 8,000 in all. The cheapest theme took every starting coin, leaving none for a stake match.
- **Ads in the Editor.** ADR 073 had the Editor use AdMob's test ads, and ADR 075 put Google's
  placeholders over the game. The placeholders are not real ads: the banner is a fixed image, about
  a third of a real banner's size. The owner prefers to check ads on a phone.

## Decision

- **Prices** (`CosmeticCatalog`), three tiers of themes and two decks:

  | Tier | Price | Items | With the coins earned |
  |---|---|---|---|
  | Starter themes | **400** | Himalayan Sunrise, Laligurans Forest, Pokhara Lakeside | One on the first day from the 500 starting coins, 100 left for a stake match; not a second |
  | Themes for a week | **900** | Golden Terraces, Prayer Flag Pass, Monastery Garden | About a week of casual play, 2 to 3 active days |
  | Premium themes | **1,500** | Tihar Lights, Mithila Garden, Phewa Moonlight | About two weeks of casual play; festival offers give or discount them |
  | Decks | **600**, **1,000** | Mithila Ink, Kathmandu Royal | Second goals after a theme |

  Everything together costs 10,000 coins (was 8,000). Durbar Square and the Classic deck stay free;
  the starting coins stay 500.
- **The shop's order is unchanged** (ADR 074): each tier keeps its place, since the old 500, 750 and
  1,000 became 400, 900 and 1,500.
- **The Editor plays with the stand-ins:**
  - no banner, no ads between matches, and Earn Coins pays at once;
  - `EditorPhoneServices` keeps only the festival offers (Callbreak > Editor Play > Festival
    Offers);
  - `AdMobPlaceholdersOnTop` and the banner's Editor placement are gone.

  Phones and builds are unchanged: development builds show Google's test ads, release builds the
  owner's.

## Consequences

- A new player buys a starter theme on the first day, keeps 100 coins for a stake match, and plays
  for the next.
- `CosmeticsShopTests.TheStartingCoins_BuyOneStarterTheme_WithCoinsLeftForAStake_ButNotTwo` holds the
  owner's rule. The shop, deck and offer tests take their prices from the catalogue.
- Players who bought themes keep them; the save records ownership, not prices.
- ADR 073's AdMob test ads in the Editor and ADR 075's placeholder fixes are superseded. ADR 073's
  smooth card edges, its festival offers in the Editor, and ADR 075's "+" trick stand.
