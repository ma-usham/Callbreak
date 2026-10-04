# ADR 033 - The shop, coins, and swappable table scenery

**Status:** accepted (owner, 4 Oct 2026). It supersedes the Card Collection screen (GDD §11 screen
15), which the Shop replaces.

**Context.**
- **The owner's new mockups** are in `Assets/ArtRefrences/game-screens/`:
  - 15 Cards (new), 20 Backgrounds and 21 Tables: one shop with a drawer and carousels;
  - 22 Earn Coins and 23 the main menu with a coin wallet.
- **The designer's spec** is in `D:\Nepali Callbreak\game-screens\`: shop-drawer-and-tables-prompts.md,
  rewarded-coins-prompts.md and README.md.
- **The owner's answers:**
  - everyone starts with 500 coins, and coins come only from watching ads;
  - the balance shows where the mockups put it (the main menu and Earn Coins);
  - use multiple agents and generate the art through ChatGPT.

## Decision

**The shop** (`ScreenId.Shop`; Presentation/Screens/Shop):
- **Layout:** one screen with three categories, Cards, Backgrounds and Tables, picked from the
  right-hand drawer.
  - The drawer opens and closes from its shop icon: 250 ms ease-out, instant with Reduce motion.
  - Back leaves the shop.
  - A fresh visit opens on Cards with the drawer open, every row at its start, and each category's
    choice on its equipped item.
- **The carousel:**
  - Three tiles are visible. Swipe or drag it (it resists at the ends and carries a flick); the
    chevrons move a page of three.
  - The scroll track follows the position. The arrows are dimmed at the ends, and browsing is hidden
    when everything fits: Cards and Tables have three items today.
  - Each category keeps its choice and position.
- **Tiles and the action button:**
  - A tile shows Equipped, Selected (owned, chosen, not equipped), Owned, or a coin price.
  - "Use Selected Style/Background/Table" equips the chosen owned item. It is greyed out while that
    item is already equipped.
  - An unowned item shows "Buy for 🪙 N". Its confirmation reads "Buy {item}?" / "It costs {N} coins.
    You have {M}." (proposed copy). Buying never equips.
  - Too few coins shows "Not enough coins" straight away, offering Earn Coins or Not now (proposed
    copy).
- **Opening it:** the main menu's storefront opens the shop, as do the shop buttons on Play with
  Friends, Create Room and Matchmaking.

**The catalogue and coins** (Application/Cosmetics, Application/Economy):
- **Decks:** classic-crimson (default), himalayan-ivory and kathmandu-gold, all owned.
- **Backgrounds:** kathmandu-courtyard, village-terrace, vintage-home, himalayan-vista (default, today's
  scenery), pokhara-lakeside and monastery-garden, all owned.
- **Tables:** himalayan-ivory (default, owned), royal-crimson (350 coins) and heritage-walnut (250 coins).
  The prices are placeholders.
- **One service:** `CosmeticsService` serves both `ICosmeticsService` (decks) and `ICosmeticsShop`.
  The equipped deck is the selected deck. The cosmetics save moves to schema 2; a schema 1 file keeps
  its deck.
- **The wallet** (`IWallet`, save key `wallet`): starts at 500 coins. No real money.
- **Earn Coins** (`IAdRewards`, save key `ad-rewards`):
  - Three ads pay 20, 30 and 50 coins, each credited when its ad completes.
  - The set resets 5 h after its first completed ad, on the wall clock (UTC). The deadline is saved,
    so time away counts and a restart loses nothing.
  - A clock set backwards never hands out an extra set. A clock moved forward can't be told from time
    away until a real ad network gives trusted time.
  - Progress is saved before coins are added, and a purchase is saved before coins are taken, so
    nothing is ever paid twice or lost.
- **Ads:** no ad network is connected. `StandInRewardedAds` (Infrastructure) completes every ad after
  1.5 s, showing nothing. Choosing a network (AdMob, Unity Ads, ...), its package and its account is
  the owner's decision.
- **Main menu:** a coin wallet pill (balance and a "+" that opens Earn Coins) sits left of the gear,
  as in mockup 23.
- **Earn Coins page** (mockup 22). Proposed copy and looks:
  - after 3/3 the button reads "All rewards claimed";
  - "Resets in hh:mm:ss" shows from the set's first ad;
  - a toast "No ad available right now. Try again soon.";
  - a watched block gets a crimson check.

**Swappable table scenery** (Presentation/Table):
- **Three layers:** the table's backdrop is now three layers in the old `BG_Table` frame (2230×1255,
  same placement): Back (the scenery), Table, and Front (the scenery's foreground, in front of the
  table).
- **The art:**
  - The art agents split today's picture exactly: Back + Ivory table + Front equals BG_Table to
    within rounding.
  - They painted five new scenes and two new tables, all with the Ivory table's exact outline.
- **What each player sees:** the player's own equipped background and table, offline and online (each
  player sees their own, as with decks, ADR 031; no protocol change). The choice is read when a
  table opens. An unknown id, or one without art, falls back to the default and is logged once.
- **Loading:** sprites come from `TableBackdropLibrary` (`Config/Table/TableBackdrops.asset`), held
  by the Table prefab like the deck themes; no Resources and no Addressables.
  `TableBackdropBuilder.Build()` imports them from the art agents' finals.
- **The linear-light rule:** the project renders in linear colour space, but the art agents
  composite in sRGB. On import the builder re-solves the semi-transparent pixels (the soft flower
  edges and the table's anti-aliased rim) in linear light. Without it the soft edges were up to ~65
  levels off.
- **The defaults** render the same as before: mean difference 0.09 levels against captures taken
  before the change.

**Art** (all generated through ChatGPT from the references, per the owner's rule; logs in
ArtSource/Prompts, rows in ArtSource/handoff.csv):
- **New UI pieces:** the drawer, discs, the "+" button, icons (storefront, cards, mountains, table,
  coin, play), reward blocks, the three coin piles, glow, connector, and six shop thumbnails plus
  three table previews.
- **Reused kit pieces:** panels, outline, radios, chevrons, slider track, buttons, clock and the
  shared background.

## Consequences

- **Tests:**
  - EditMode: shop rules, carousel scrolling, the catalogue, buy/equip/persistence and migration,
    the wallet, ad rewards (the clock, double taps, restarts), backdrop resolution and fallback;
  - PlayMode: the shop flow, its deck peek, Earn Coins, the main-menu wallet, the backdrop on a real
    table;
  - the table simulation checks every background/table combination and that the defaults render as
    BG_Table did.
  - Suites: EditMode 1915 and PlayMode 83; the table simulation has 0 failures, with and without
    Reduce motion (477 and 464 kinds of check).
  - Since about 20:45 on 4 Oct, Windows Smart App Control blocks the dotnet harness's unsigned test
    assembly on this PC. It builds but finds no tests, and still exits 0. The same tests ran in
    Unity's EditMode. That Windows security setting is the owner's.
- **Memory:** all 15 backdrop textures load with the Table scene: 12.3 MB on Android (ASTC 6×6),
  where BG_Table was 1.19 MB. Addressables could load only the equipped three; that's a package
  decision for the owner.
- **Not verified:**
  - on a device;
  - a real ad network;
  - a real 5-hour wait (tests use saved deadlines);
  - Nepali text (the `ne` column is empty for the new rows).
- **Open for the owner:**
  - the proposed copy above;
  - chevrons moving a page or one tile;
  - opening Backgrounds on the equipped scene;
  - the greyed "Use" button;
  - whether a resumed offline match should keep the scenery it started with, as its deck does;
  - the busier logo area on Pokhara Lakeside and Monastery Garden.
- **Leftovers:** six `ui.card_collection.*` rows and the old Card Collection art sources remain
  unused.
- **The GDD is out of date here:** §11 screen 15, §13 (cosmetics: decks only) and §15 (no currency).
