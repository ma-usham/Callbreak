# ADR 067 - Festival offers from Firebase Remote Config

**Status:** proposed. The owner, 31 Oct 2026:

- **The package:** approved adding Firebase Remote Config 13.17.0.
- **The offer:** Tihar Lights is "free gift but requires user to view one rewarded ads to claim it",
  "from today to 15 nov".
- **The menu:** "show popup once, on the bare mainmenu like daily reward and if use doesnt claim it
  then show a small red icon on the top of shop like in daily reward and if the user claims then add
  a equip button in the same place where user has clicked to claim with 1 rewarded ads".
- **The shop:** "in shop the tihar theme will be bought and the recently bought theme will come
  first then the theme will low cost and then high".

## Context

The owner wants to give a theme for a festival (Tihar Lights at Tihar, others later) and switch it
on and off from a backend, without an app update. The themes ship in the game (ADR 064). There is no
custom backend.

The game already has Firebase App and Analytics 13.17.0 on Android. GDD §16 planned Remote Config on
Unity Gaming Services. ADR 015 kept every service local until then, and UGS was never linked.

## Decision

**Firebase Remote Config holds the offers**, in one JSON parameter, `festival_offers`
(Docs/Setup/FestivalOffers.md):

- **The package:** `com.google.firebase.remote-config` 13.17.0, a local tarball in `Packages/` like
  App and Analytics. `Firebase.RemoteConfig.dll` joins Infrastructure's precompiled references.
- **What an offer says:** each one names an id (`tihar-2026`), a paid theme, a kind (`watch-ad`,
  `free` or `discount` with a price below the shop's), a start and an end with their time zone, and
  a localization key for its words.
- **On phones:** `FirebaseFestivalOfferSource` reads the value activated in an earlier session at
  start, then fetches (at most once an hour) and activates. A failed fetch keeps the value the phone
  had. The default is empty: no offer.
- **Elsewhere:** the Editor, desktop and the tests get `NoFestivalOffers`.
- **The dependency check:** Remote Config only checks Firebase's dependencies, never fixes them, so
  it can't race the analytics' check-and-fix.

**The rules are engine-free in Application (`Callbreak.Application.Offers`), tested outside Unity.**

- **Reading the config:** `FestivalOfferReader` turns the config into offers and leaves out any entry
  with something wrong (unknown theme or kind, bad id, a date without a zone, an end before its
  start, a discount not below the price). A typo can't give a theme away, and the other offers
  still work. No config, no network on the first run, or text that isn't JSON: no offer.
- **What a player can claim:** `FestivalOffers.Claimable` is the first listed offer that is live
  (from `start` up to, not including, `end`), not claimed by this player, and for a theme the player
  doesn't own. A player who owns the theme sees nothing.
- **Claiming:** `ClaimAsync` claims a `watch-ad` offer when one rewarded ad is watched to the end, a
  `free` one at once, and a `discount` when the wallet pays its price.
  - The theme is given through the shop's own ownership: the new `ICosmeticsShop.BuyAt`, `Buy` at an
    offer's price. It is the same purchase, saved before any coins are taken, owned and not equipped.
  - The claimed offer id is then written to a new save file, `festival-offers.json`, so each offer
    is claimed once per player. A crash between the two writes leaves the theme owned, and the
    offer gone with it.
  - An ad watched while the offer ends still gives the theme.
  - No ad to show, or one skipped, gives nothing.
- **The popup's one opening** per offer is kept in the same file.
- **Time:** `TrustedClock` goes by a server's time once one has answered. At start and on each
  return to the foreground, `ServerTime` sends a HEAD request to `https://www.gstatic.com/generate_204`
  and reads its HTTP `Date` header, carried forward by the monotonic clock. A phone set to another
  date can't bring an ended offer back. Without an answer, the device's time.

**What the player sees (Presentation):**

- **The popup is the shop's theme preview** (ADR 064), opened straight on the offer's theme:
  - `FestivalOfferMoment` opens it once per offer on the bare main menu, under the same conditions
    as Daily Rewards and a moment after it, so a day's reward comes first.
  - Its button is the claim: "(play) Watch ad • Free" for Tihar, a gift or coin mark for the other
    kinds.
  - Its words are the offer's.
  - Once claimed, the same button is Equip theme, and then Equipped.
  - Its cross goes back to the main menu. `ShopOpening` carries the request from the menu to the
    shop.
- **Until claimed:**
  - The main menu's Shop button has a red dot: Daily Rewards' dot, on the shop disc's rim.
  - In the shop, the theme's tile has a marigold FESTIVAL tag in the NEW tag's place, and "Free"
    where the price goes (a discount shows its price). The shop opens scrolled to it.
- **The shop's theme order** (the owner): the theme bought last first, then the rest from the
  cheapest to the dearest, in catalogue order where prices tie (`ShopDisplay.ThemeOrder`, via the
  new `ICosmeticsShop.LastBought`).
  - A claimed theme counts as bought.
  - The preview follows its theme, not its place, since buying moves a theme first.
  - The catalogue (`CosmeticCatalog`, `ICosmeticsShop.Catalog`) keeps its order.
- **New strings** (`UI.csv`, English only like the rest, for the owner to word as they like):
  - `ui.offer.default`, `ui.offer.tihar`;
  - `ui.offer.claim.watch_ad`, `ui.offer.claim.free`, `ui.offer.claim.discount`;
  - `ui.offer.ad_unavailable`, `ui.offer.ad_not_watched`;
  - `ui.shop.themes.festival`, `ui.shop.status.free`.
- **The prefabs are patched in place** (`ShopCustomizer.AddFestivalOffers`, `MainMenuBuilder.AddShopDot`),
  and the builders make the same parts on a rebuild.

## Consequences

- **What the owner can do without an update:** start, change and end offers for any theme the game
  has, from the console. New themes or new words still need an app update. Ending an offer takes
  effect at each player's next start, at most an hour after their last fetch: there is no real-time
  listener.
- **Remote Config is now on Firebase, not UGS** (GDD §16, ADR 015). Its port,
  `IFestivalOfferSource`, keeps the game free of the SDK, so another backend fits behind it.
- **Android build:** the EDM4U resolver adds `firebase-config` 23.1.0 and `firebase-config-unity`
  13.17.0 to the Android dependencies.
- **Network and privacy:**
  - A Remote Config fetch identifies the install to Firebase by its Firebase installation ID, as
    any Firebase call does. It runs whatever the player chose for analytics: it is how the game
    gets its offers, not analytics.
  - The time check sends a bare HEAD request to Google.
  - The privacy policy may want a line on both.
- **The shop's theme order is new:** for a new player, Tihar Lights is the eighth tile, no longer the
  fifth. ShopFlowTests and ShopStateTests follow the new order.
- **Not covered:**
  - A player who changes the phone's date while offline, before the game ever reached the time
    server, can still see an offer early or late. Only an offer the config still lists can come back
    this way.
  - There are no analytics events for offers yet.
