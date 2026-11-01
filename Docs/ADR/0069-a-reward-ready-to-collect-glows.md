# ADR 069 - A reward ready to collect glows

**Status:** proposed. The owner, 2 Nov 2026:

- "in daily login when its time to collect show some effect if its not been collected so it will be
  easy to identify we should click or claim it. right now the daily login bonus if plain even if it is
  claimable."
- Then: "also if there is offer and user hasnt claim any yet then show the same icon effect as daily
  login in shop as well."

## Context

Daily Rewards (ADR 046, ADR 057) has two places that say a reward is ready:

- **The main menu's button** under Shop: an ivory disc with the gift glyph, and a red dot 20 mockup
  pixels wide on its rim. On a phone the dot is about 2 mm across, and nothing else changes.
- **The Daily Rewards page:** the day to collect has the TodayFrame, a thin gold keyline, its name in
  crimson and the soft cream light behind its coins. "Tap a day to collect" (ADR 057) works, but the
  tile looks almost like the others.

The page opens by itself once a day (ADR 046). After that the player has only the dot to go by.

A festival offer (ADR 067) is in the same place: it opens by itself once on the bare main menu, and
after that, while it is unclaimed, only the Shop button's red dot says so.

## Decision

**While a reward is ready, it wears a gold glow that breathes, and its glyph gives a nudge.** The glow
goes the moment the reward is collected, and comes back when the next day's reward is ready.

- **The menu button:**
  - A warm gold halo round the disc that breathes: it brightens from 35% to full and swells 5%, then
    settles, every 2 s.
  - A steady gold rim on the disc's edge, the gold of the kit's selected disc
    (`UI_ButtonDisc_Selected`).
  - At each breath's height the gift wiggles twice (10°) as it swells 10%.
  - The red dot stays, on top.
- **The Shop button**, while a festival offer waits to be claimed (`IFestivalOffers.Claimable`, the
  red dot's own rule): the same glow, rim and nudge, the shop's icon wiggling. Claimed, or the offer
  ended, it goes with the dot. The offers look at the clock every half second (ADR 067), so an offer
  that starts while the menu is up lights it at once.
- **The page:** the day to collect gets the same halo round its tile, under its gold TodayFrame. The
  halo swells only 2% (the tile is big), and the day's coins hop twice (8 units) as they swell 4%.
  Collected and later days stay calm.
- **A new day** comes at local midnight without an event (ADR 046):
  - The main menu now looks once a second while it is on top, so the glow comes back while the
    player sits on it.
  - The page already looked once a second (ADR 046), so the next day's tile glows while it stays
    open.
- **Reduce motion** (GDD §14): the glow stays lit at full strength, the rim and dot too, and nothing
  moves.
- **Unscaled time:** the glow runs on unscaled time (LitMotion's `UpdateIgnoreTimeScale`, as
  `UiMotion` does). It doesn't run while its screen is hidden, and starts again when the screen shows.
- **No words changed.**

**How it's built:**

- **The widget:** `ClaimHighlight` (Presentation/Widgets) sits on the glow's own object, which it
  shows and hides.
  - One looping LitMotion drives both the halo and the glyph. The halo's opacity goes through its
    `CanvasRenderer`, so its mesh isn't rebuilt each frame.
  - Off, or with Reduce motion, the glyph goes back to where it was built.
- **The views:**
  - `MainMenuView.SetDailyRewardsReady` and `SetShopOffer` turn each button's glow on with its dot.
  - `DailyRewardsView.SetDay` turns a tile's glow on with its TodayFrame.
  - Both views' `SetReduceMotion` makes the glow still.
- **The art:** `Tools/ArtPipeline/NewUI/claim_glow.py` draws three sprites into `Art/UI/Decor`, at
  2 px per unit like the kit:
  - `Deco_ClaimGlow_Disc`: the halo, clear inside the disc.
  - `Deco_ClaimRim_Disc`: the rim.
  - `Deco_ClaimGlow_Tile`: a 9-slice halo for any tile's size, which fades out within the 14 units
    between tiles.
- **The prefabs:**
  - `MainMenuBuilder.AddClaimGlows()` adds a `ClaimGlow` (Halo, Rim) to the Daily Rewards and Shop
    buttons of the saved MainMenu.prefab, over each disc's face and under its icon and dot. The
    builder's own build makes them too.
  - `DailyRewardsCustomizer` adds each tile's `ClaimGlow` (Halo) just under its TodayFrame, bound as
    `DayHighlight0..6`. A rebuild from the layout gives every object a new file id, so
    `DailyRewardsCustomizer.AddClaimGlows()` added them to the saved DailyRewards.prefab instead.
  - Both prefab changes are additions only.

## Consequences

- **The three places read at a glance:**
  - On the menu, the button glows warm gold on the light backgrounds, and more on the night themes.
  - With both a reward and an offer waiting, Shop and Daily Rewards breathe together in the column.
  - On the page, the day to collect is the only thing moving.
- **Size:** three small textures (338², 222² and 172² px) are added. That is well under 0.1 MB.
- **Cost:** one LitMotion per glowing button or tile while something waits; nothing otherwise. The moving halo and glyph
  re-batch their canvas each frame, as the main menu's swaying card fan already does.
- **Tests:** `ClaimHighlightFlowTests` (PlayMode, the real scenes, a clock the test turns):
  - Claimable: the glow is on and moving.
  - Collected: it is off and the glyph rests.
  - The next day: it comes back with the menu, and with the page, open.
  - Reduce motion: it is lit and still.
  - A paused time scale: it still moves.
- **Tests:** `FestivalOfferFlowTests` adds the Shop button's glow.
  - Unclaimed: on and moving.
  - Claimed or ended: off.
  - Reduce motion: lit and still.
  - Both test classes watch the glow through `ClaimGlowAsserts`.

## Alternatives considered

- **A light sweep across the button or the tile:** it hardly shows on ivory, and it needs a mask on
  each.
- **Rotating rays behind the coins:** they would crowd the tile's text and the coins' own cream light.
- **A bigger red dot, or a badge with a word:** it changes the copy or the kit's look, and the owner
  asked for an effect.
