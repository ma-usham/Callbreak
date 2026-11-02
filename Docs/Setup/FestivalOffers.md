# Festival offers: how to run one from Firebase

A festival offer gives the players a theme from the shop for a festival: free, free for one rewarded
ad, or for fewer coins. It is switched on and off in the Firebase console, with no app update
(ADR 067). The theme must already be in the game. A new theme still needs an app update.

## What the players see

- **The popup.** Once per offer, on the bare main menu, the shop opens on the theme's preview by
  itself. It shows the theme's menu background and cloth, the offer's words and the claim button
  ("Watch ad • Free" for Tihar). The cross goes back to the menu.
- **The claim.** Once the player claims the theme, it is theirs. The same button turns to
  **Equip theme**, and tapping it puts the theme on.
- **Until they claim it:**
  - The Shop button on the main menu has a red dot.
  - In the shop, the theme's tile has a **FESTIVAL** tag and says **Free** (or the offer's price),
    and the shop opens scrolled to it.
- **The shop's order.** Claimed or bought, a theme counts as bought. The shop shows the theme bought
  last first, then the rest from the cheapest to the dearest.
- **Once per player.** Each offer can be claimed once per player, and opens by itself once.
- **Players who already own the theme** see nothing: no popup, no dot and no tag.

## Where it lives

- **Firebase project:** the one in `Assets/google-services.json` (app `com.dayzerogames.callbreak`).
- **Console path:** Engage → Remote Config.
- **Parameter:** one parameter, `festival_offers`, of data type **JSON**. Its value is a list of
  offers.
- **Empty or missing value:** no offer. You can list next year's festivals ahead of time, since each
  offer is only live between its own `start` and `end`.

## Tihar 2026 (the live offer)

Tihar Lights, free for one rewarded ad, from 31 October to the end of 15 November 2026, Nepal time:

```json
{
  "offers": [
    {
      "id": "tihar-2026",
      "theme": "tihar-lights",
      "kind": "watch-ad",
      "start": "2026-10-31T00:00:00+05:45",
      "end": "2026-11-16T00:00:00+05:45",
      "title": "ui.offer.tihar"
    }
  ]
}
```

## Setting it up in the console

1. Open Firebase → your project → **Remote Config**.
2. **Add parameter**:
   - Key: `festival_offers`
   - Data type: **JSON**
   - Default value: paste the JSON above.
3. **Save**, then **Publish changes**.

The game reads the value when it starts:

- **Once fetched, the value is kept on the phone.** A later start without a network still knows the
  offer.
- **A phone fetches again at most once an hour.**

So after you publish, a player sees the offer the next time they open the game, or within an hour
of their last fetch.

## The fields

| Field | What it is |
| --- | --- |
| `id` | The offer's name, lowercase letters, digits and hyphens, at most 64: `tihar-2026`. A player claims each id once, so use a new id each year (`tihar-2027`). |
| `theme` | A paid theme's id: `himalayan-sunrise`, `laligurans-forest`, `golden-terraces`, `tihar-lights`, `prayer-flag-pass`, `mithila-garden`, `phewa-moonlight`, `pokhara-lakeside`, `monastery-garden`. (Durbar Square is everyone's already.) |
| `kind` | `watch-ad` (free for one rewarded ad watched to the end), `free` (one tap), or `discount` (for `price` coins, after the shop's usual "Buy?" question). |
| `price` | Coins, for `discount` only: above 0 and below the theme's shop price (Tihar Lights is 1,000). Leave it out for the others. |
| `start` | When it starts, with its time zone: `2026-10-31T00:00:00+05:45` is midnight in Nepal. `Z` means UTC: `2026-10-30T18:15:00Z` is the same moment. |
| `end` | The first moment it is gone, written the same way. `2026-11-16T00:00:00+05:45` means "until the end of 15 November in Nepal". |
| `title` | The words in the theme's preview, as a key of the game's UI table (`Assets/_Project/Localization/UI.csv`). Leave it out and the game says "A festival offer, for a few days only." New words need a new key, so an app update. |

**An offer with any field wrong is ignored on its own:** a typo never gives a theme away, and the
other offers still work. These all count as wrong:

- an unknown theme or kind;
- a date without a time zone;
- an end before its start;
- a discount that isn't below the shop price.

## Ending an offer early

You have two ways. Both take effect for each player at their next start, at most an hour after
that phone last fetched.

- **Bring its end forward:** set `end` to now, for example `"2026-11-10T12:00:00+05:45"`, then
  Publish.
- **Remove it:** take its entry out of the list, or set the value to `{ "offers": [] }`, then
  Publish.

A player who already claimed the theme keeps it.

## Other festivals: examples to adapt

Check each festival's dates on the Nepali calendar for that year. The dates below are placeholders.

Dashain, Mithila Garden for 500 coins:

```json
{
  "offers": [
    {
      "id": "dashain-2027",
      "theme": "mithila-garden",
      "kind": "discount",
      "price": 500,
      "start": "2027-10-01T00:00:00+05:45",
      "end": "2027-10-16T00:00:00+05:45"
    }
  ]
}
```

Two festivals set up at once. Each is only live in its own window, and if two overlap the first
one listed shows first:

```json
{
  "offers": [
    {
      "id": "tihar-2026",
      "theme": "tihar-lights",
      "kind": "watch-ad",
      "start": "2026-10-31T00:00:00+05:45",
      "end": "2026-11-16T00:00:00+05:45",
      "title": "ui.offer.tihar"
    },
    {
      "id": "new-year-2027",
      "theme": "phewa-moonlight",
      "kind": "free",
      "start": "2027-04-13T00:00:00+05:45",
      "end": "2027-04-16T00:00:00+05:45"
    }
  ]
}
```

## Good to know

- **Android and the Editor.** The offers come to Android builds, and to a game played in the Editor
  through Firebase's desktop SDK (ADR 073; **Callbreak > Editor Play > Festival Offers** turns that
  off). Desktop builds and the tests have none; `FestivalOfferFlowTests` brings its own offers.
- **The clock is a server's.** At start, and whenever the game comes back to the foreground, it asks
  Google's connectivity check (`https://www.gstatic.com/generate_204`) for the time, so a phone set
  to another date can't bring an ended offer back. The request sends nothing about the player.
  Without a network the game uses the phone's own clock.
- **The ad.** A player with no rewarded ad available sees "No ad available right now. Try again
  soon." and keeps the offer. An ad skipped or closed early gives nothing. An ad watched while the
  offer ends still gives the theme.
- **Testing on a phone without touching players:** in Remote Config, add a **condition** (for example
  your test device's app instance or a user property), give the parameter a conditional value for
  it, and publish.
