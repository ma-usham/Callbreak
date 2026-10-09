# ADR 046 - Daily Rewards, a weekly login streak

**Status:** accepted (owner, 8 Oct 2026).

**Context.** On 8 Oct 2026 the owner added mockup 26, `26-daily-rewards.png`: a full-screen
"Daily Rewards" page with seven day tiles, a crimson "Claim 10 Coins" button and "Come back
tomorrow for Day 2". The designer's spec (`26-daily-rewards-prompt.md`) calls the amounts
illustrative and leaves the reset and streak rules open. The owner asked for a weekly login system,
with the art made by ChatGPT from the mockup, and for a small button below No Ads on the main menu
to open the page. The designer's other idea, a pill under the greeting, was not chosen.

## Decision

- **The owner's rules** (`IDailyRewards`, `DailyRewards`, save file `daily-rewards`):
  - Seven days pay 10, 15, 20, 25, 30, 40 and 60 coins, 200 a week. The mockup's Day 7 shows
    75; the owner lowered it to 60.
  - One claim per calendar day on the device's own clock. The next day comes at local midnight.
  - Claiming on consecutive days walks the week. After Day 7, a new week starts at Day 1.
  - A missed day starts the week again at Day 1, so Day 7 needs seven days in a row.
  - The claim is saved before the coins go into the wallet, so an app killed in between can't pay
    a day twice.
  - A clock set back pays nothing until it passes the last claimed day again. A clock moved
    forward can't be told from time passing; only trusted time could stop that.
  - A save file from a newer app version is ignored, never overwritten, and pays nothing.
- **The page** (screen 26, `DailyRewardsView`, `DailyRewardsPresenter`):
  - It is laid out from mockup 26 as the other menu screens are
    (`ArtSource/Screens/DailyRewards/screen.json`). From the kit: the shared background, the back
    disc, the wallet, the title divider, the card panel, the Claim pill (`UI_ButtonPrimary_Normal`),
    the coin light and the footer lines. New, made by ChatGPT in one TestProject chat that was
    archived afterwards (`ArtSource/Working/DailyRewards`, prompts in
    `ArtSource/Prompts/DailyRewards_Kit.md`): the day tile, the gold Day 7 tile, today's frame (an
    overlay laid over whichever tile is today), seven coin piles with their shadows, the laurel,
    the ribbon, the heading flourish, the gift icon and the red dot.
  - The 16:9 capture matches the mockup at a mean error of 18.7 levels, every label within half a
    pixel of its place.
  - Today's tile, while its reward is ready, wears the crimson frame and the light behind its
    coins, as Day 1 does on the mockup. Days already paid this week carry the gold check badge in
    their top right corner: the designer's own claimed mark, which they took off the unclaimed
    Day 1.
  - "Day n of 7", "Claim n Coins", and "Come back tomorrow for Day n" (Day 1 after Day 7).
  - Once today is claimed, the button is off and reads "Claimed" until midnight.
  - A new day while the page is up shows the next reward.
- **Opening it:**
  - The main menu's small button below No Ads opens it, with a red dot while today's reward is
    ready. The owner had already moved No Ads under the Shop button, so the new button sits in that
    right-hand column, clear of the logo at 16:9, 21:9 and 4:3. It is the header disc at 0.72 of
    its size, with ChatGPT's gift icon. It stays put when No Ads has been bought and that button is
    gone.
  - The page also opens by itself once a day while a reward is ready (`DailyRewardsMoment`). It
    only acts on the bare main menu: the menu visible, no dialog open, and the name answered. So
    the name question, the rules primer and its practice offer, an invite link, a rejoin and the
    offer to resume an offline match all go first. Closing the page without claiming uses up the
    day's opening, also across restarts; the button and its dot remain.

## Consequences

- New copy (`ui.daily_rewards.*`): the mockup's title, subtitle, "Weekly bonus", "Day n of 7",
  "Day n", "+n coins", "Claim n Coins" and "Come back tomorrow for Day n". **"Claimed"** (the button
  once today is claimed) is not on the mockup: proposed, for the owner to confirm.
- The Menu scene got the screen with `SceneSetup.AddMenuScreen`, and the main menu prefab got the
  button with `MainMenuBuilder.AddDailyRewardsButton`. Both are targeted edits that keep the owner's
  own changes to the scene and the prefab. The full builders include both for later rebuilds.
- PlayMode tests: the fixture starts every game with today's opening already used, so the main
  menu stays bare; `DailyRewardsFlowTests` seed their own file.
- **Tests:** `DailyRewardsTests` (the rules), `DailyRewardsDisplayTests` (tile looks, footer day),
  `DailyRewardsFlowTests` (PlayMode: the button, the dot, a claim, the opening by itself, the name
  first).
- **Proposals for the owner:**
  - When Day 7 is today, its gold tile wears the crimson frame like any other day.
  - The gift icon, the red dot and their sizes are on no mockup.
  - The Claim button reuses the kit's crimson pill. Mockup 26 draws it a little brighter, with
    larger woven ends, as mockup 22 did.
- **Not verified on a phone.**

## Addendum, 9 Oct 2026: coins fly into the wallet

The owner asked to see the coins collected: flying up to the wallet and added there, with a sound.
It applies to both pages that pay coins and show the wallet, Daily Rewards and Earn Coins.

- **The flight** (`CoinFlight`, a layer over the whole screen, built last by `CoinFlightBuild`):
  - 6 to 12 of the wallet's own gold coins (`Icon_Coin`), more for a bigger reward: a fifth of the
    amount, at least 6 and at most 12.
  - Each coin pops out of the reward's pile (today's tile, or the ad's block), then arcs up into
    the wallet's coin, which gives a small bump at every landing. They leave the pile 0.06 s apart
    and take 0.75 s each, so 6 coins land within about a second and 12 within about 1.4 s.
- **The wallet counts up** from the old balance to the new one as the coins land. The coins are
  in the wallet (and saved) the moment the reward is paid; only the page's number waits for them.
- **The sound** (`SoundCue.CoinCollect`, made with ElevenLabs as the other effects) plays once,
  as the first coin lands.
- **Reduce motion:** nothing flies; the number changes at once and the sound still plays.
- Leaving the page mid-flight lands every coin at once, so the number is never left behind.
- **Tests:** `DailyRewardsFlowTests` (the flight, the count-up, one sound; Reduce motion at once);
  `EarnCoinsFlowTests` waits for the coins to land before it checks the page's wallet.
