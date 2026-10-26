# ADR 051 - A banner ad at the top of the table

**Status:** accepted (owner, 26 Oct 2026).

**Context.** On 14 Oct 2026 a banner session built the banner logic for the table, to sit left of
the Scores button. A standard 320 x 50 dp banner is about 840-960 canvas units wide on a phone, and
the space left of Scores is about 500 (16:9) to 800 (21:9), so there it would cover "ROUND 1 / 5"
and the top seat's name plate (ADR 027). The owner dropped banners for now (15 Oct 2026, ADR 047).
On 26 Oct 2026 the owner asked again: "add banner ads in gameplay so that user won't notice that
like in top space somewhere".

Constraints:
- the GDD keeps the round line visible for the whole match;
- AdMob forbids ads over app content and placements that invite accidental taps next to buttons;
- the banner is AdMob's own view, drawn over the game, so nothing of the game can be drawn over it;
- AdMob's sizes: 320 x 50 is the smallest standard banner and gets the most ads. Smaller custom
  sizes, and inline adaptive banners capped at 32 dp tall, get far fewer. Anchored adaptive banners
  are at least 50 dp tall and wider. The ad must not be scaled.

The owner was shown mockups of the table (16:9, 21:9 and 4:3), a 32 dp strip and an alternative with
the top seat's name beside its avatar, and picked the one below with the standard banner.

## Decision

- **The banner:** AdMob's standard 320 x 50 (`AdSize.Banner`), the owner's unused unit
  `Android_Banner`, `ca-app-pub-1025159441369051/6484892497`; Google's test banner in development
  builds. Nothing was changed in AdMob.
- **Where:** at the very top edge of the safe area, centred on the screen. It keeps 48 canvas units
  from Home and from the round line's words; on 16:9 it moves left just enough for that.
- **Room for it,** when the table opens and a banner may come (no No Ads):
  - the CALLBREAK logo goes;
  - "ROUND 1 / 5" moves beside the Scores button, its words ending 36 units left of the button,
    without its ornaments (on the left it would sit over the scenery's prayer flags at 21:9);
  - the top seat (name plate, pill, avatar, fan, speech box) comes down 52 units and the trick 25,
    so the top seat's name plate starts 11 units below the largest phone banner.
- **No room** for a player who owns No Ads, or where there is no ad network (the Editor, the tests,
  desktop builds): the table is exactly as before.
- **Decided once per table**, so nothing moves under the player. Buying No Ads mid-match takes the
  banner away at once and the room stays until the next table. A refund mid-match brings a banner
  only at the next table.
- **Never over anything:** the banner is off the screen whenever the bare table isn't in front: the
  call panel, the scorecard, the match menu, settings, the rules, a round's or the match's results,
  a dialog, the loading curtain, the "Reconnecting…" cover or a toast (as built on 14 Oct).
- **Too small:** a phone whose banner doesn't fit the slot (155 units tall, so under about 350 dp
  tall) gets none.

## Consequences

- **Code:**
  - `BannerSlot` gains a middle line (`CentreX`): the banner is centred on it, kept inside the slot,
    at the slot's top. It was right-aligned and centred in the slot's height.
  - `IBannerAds.MayShow` says whether to make room: `BannerAds` while No Ads isn't owned; the
    stand-in never.
  - `TableView.SetBannerRoom` makes or gives back the room. `TryGetBannerSlot` ends the slot 48 units
    short of the round line's words, however long they are ("ROUND 10 / 10", another language), and
    gives the screen's middle.
  - `TableBannerPresenter` makes the room when the table starts.
  - `TableCustomizer.BuildBannerRoom` builds the slot and the round line beside Scores, both off.
    The Table prefab was rebuilt from its layout. Apart from those two pieces it is the same as
    before: every element has the same rectangle at 16:9, 21:9 and 4:3.
- **Tests:**
  - `BannerAdsTests`: the placement and `MayShow`;
  - `TableBannerRoomTests` (EditMode): the room's geometry at 16:9, 21:9 and 4:3, and giving it
    back;
  - `TableBannerFlowTests` (PlayMode): the real scenes with a scripted banner network. The banner
    shows over the bare table and is hidden under the scorecard, settings and the match menu.
    No Ads takes it away and keeps the room; owning No Ads means no room and no requests.
- **The trick counter** ("Trick 6 / 13", off) would sit under the banner: it needs a new place if it
  is ever shown (as ADR 027 already said of the top seat's name plate).
- **Not verified on a phone:** a real AdMob banner's size and place on Android, its refresh, and
  that it never shows over the call panel or results on a device.
