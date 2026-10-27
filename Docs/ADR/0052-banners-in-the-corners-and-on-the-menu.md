# ADR 052 - Banners in the corners: top-right on the table, bottom-left on the main menu

**Status:** accepted (owner, 27 Oct 2026).

**Context.** ADR 051 put a 320 x 50 AdMob banner at the top of the table, centred, and the main menu
had none. On 27 Oct 2026 the owner asked for banners "even in the admob as well. make a space in top
right or left somewhere in the game play and in the bottom left or right in the menu screen". AdMob
then listed no banner unit: the `Android_Banner` the table used (`.../6484892497`) had been removed
from the account. Asked whether to make one unit or two, the owner chose one for both.

## Decision

- **AdMob:** a new banner unit `Android_Banner`, `ca-app-pub-1025159441369051/8330163494` (no
  frequency cap, no partner bidding), for the table and the menu alike. Development builds keep
  Google's test banner. AdMob says a new unit can take up to an hour to serve.
- **The table: top-right.** In ADR 051's room, the banner sits in the slot's top-right corner, its
  right edge 48 canvas units short of the round line's words. Those words stand between the banner
  and the Scores button, so it is as far from any button as the room allows, and Home is further
  still. Nothing else on the table changes.
- **The main menu: bottom-left.** The same banner, flush with the safe area's bottom and left edges,
  over the scenery. To make room, while a banner may come (No Ads not owned, an ad network):
  - the centre column is drawn 6% smaller from its top, which lifts the play buttons about 55 canvas
    units, clear of a phone's banner band (about 150 units on a 360 dp-tall phone);
  - the footer line goes;
  - the Resume banner moves under Play Offline.

  The left corner, not the right, because the right half holds the Resume banner and the header's
  column of buttons is on that side.
- **As on the table:**
  - the room is decided once per Menu scene, before the menu first shows (so its buttons rise into
    their new places);
  - the banner is off whenever anything covers the bare main menu;
  - No Ads takes it away at once and keeps the look until the menu loads again;
  - a No Ads owner gets the menu as it always was.
- **One mechanism:** `BannerSlot` can now sit the banner on its bottom edge (`AtBottom`), and a
  middle line on the slot's edge puts it in that corner. `BannerAds` is unchanged otherwise. The menu
  makes its own `BannerAds` over the project's banner network, as each table does.

## Consequences

- **Look:** on the main menu, the logo, card fan and play buttons are 6% smaller while a banner may
  show, and the footer line is gone. Players with No Ads see the menu as before.
- **Tests:**
  - `BannerAdsTests`: the bottom and corner placements.
  - `MenuBannerRoomTests` (EditMode): at 16:9, 21:9 and 4:3 the banner fits the bottom-left corner,
    no button overlaps the slot, the Resume banner is clear of it, and giving the room back restores
    everything.
  - `MenuBannerFlowTests` (PlayMode): the banner over the bare menu only; No Ads mid-menu; the next
    menu for a No Ads owner.
  - `TableBannerFlowTests` now checks the top-right corner, and counts the table's banner loads from
    the table's opening, since the menu loads one too.
- **Not verified on a phone:** a real AdMob banner's size and place on Android, and that the new unit
  serves.
