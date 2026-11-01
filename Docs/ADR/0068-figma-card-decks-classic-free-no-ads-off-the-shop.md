# ADR 068 - Figma's card decks in the game, Classic free and the rest sold; No Ads off the shop

**Status:** accepted by the owner (2 Nov 2026).

The owner asked: "ok also in figma we have new card design us that, give classic card as default and
set other to be purcahsed from shop. also remove the remove ads button from shop".

All four Figma decks together would add about 22 MB to every phone's download. Shown four ways to
handle that, the owner picked: ship Classic, Kathmandu Royal and Mithila Ink now, and leave Vintage out
for now.

## Context

Figma's "Card Decks" page (338:2 in the "Callbreak Game UI" file) has four decks of 52 cards and a back
each. Every card is 512 × 712 (the owner's size, "same as the vintage pack").

| Deck | Set | Back |
| --- | --- | --- |
| Classic | 341:1334 | 341:1335 |
| Vintage (the owner's pack, paper unified) | 342:569 | 342:570 |
| Kathmandu Royal | 344:2308 | 344:2309 |
| Mithila Ink | 348:2132 | 348:2133 |

The decks differ in everything a card shows: paper, frame lines, corner index, pips and courts.

The game had three decks: Classic Crimson (free), Himalayan Ivory (500) and Kathmandu Gold (750). They
shared one set of faces over a base per deck (ADR 054, 061). Its card was 240 × 376 in shape, drawn
144 × 226 canvas units at the table, a fifth of the screen's height (ADR 062's addendum).

The shop had three drawer buttons: the Themes and Cards tabs, and No Ads, the store's one-time purchase
(ADR 064). The main menu has its own No Ads button.

## Decision

**Three Figma decks replace the three old ones, each in an old deck's place.**

- **Ids and prices** (`CosmeticCatalog.Decks`, in shop order):

  | Deck | Id | In the place of | Price (coins) |
  | --- | --- | --- | ---: |
  | Classic | `classic` | Classic Crimson | free, the default |
  | Mithila Ink | `mithila-ink` | Himalayan Ivory | 500 |
  | Kathmandu Royal | `kathmandu-royal` | Kathmandu Gold | 750 |

  - Each deck takes its old deck's price. The prices are the catalogue's constants, for the owner to
    change.
  - The names are localization keys `ui.deck.<id with underscores>`; in English they are Figma's names.
  - Mithila Ink takes Himalayan Ivory's place because both are cream-paper decks.
- **Saves.** `CosmeticCatalog.Decks.Renamed` maps each old deck to the deck in its place, as ADR 064's
  maps did for backgrounds and tables:
  - `classic-crimson` → `classic`
  - `himalayan-ivory` → `mithila-ink`
  - `kathmandu-gold` → `kathmandu-royal`

  What this means for a player:
  - A save that bought an old deck owns its successor, and an equipped old deck equips its successor.
  - Nobody loses a deck they paid for, and nobody pays twice.
  - The next save writes the new ids.
  - A purchase this build doesn't know stays in the file, as before.
  - Online, a seat's deck id is only its profile (ADR 031: each player sees every card in their own
    deck). A seat whose id this build doesn't know, such as an older app's, shows as the default
    deck there.
- **Vintage waits.**
  - It would add 16.2 MB to every phone's download on its own: its aged paper and dense borders barely
    compress.
  - It should come later as a download only for those who buy it (Play Asset Delivery or an asset
    bundle). That needs a package or settings change, so it isn't part of this decision.
  - Figma keeps its sheet in the export section. `cut_decks.py --with vintage` cuts it.
  - Its id `vintage` is free for that.
- **Every deck has its own faces.**
  - Each card is the whole Figma card: paper, frame, index, pips and court, with straight alpha. Only
    the rounded corners are see-through.
  - No deck has a base. ADR 054's shared premultiplied faces and ADR 061's single face set are
    retired.
  - `CardSpriteProvider` already held faces and a base per deck. The library now gives each deck its
    52 faces, its back and no base, so a face draws with the default UI material, as a back does.
  - The runtime code didn't change: every view already asked for a deck's face and base.
- **The art pipeline** is `Tools/ArtPipeline/NewUI/cut_decks.py`.
  - Figma's section "Unity Export · Card Decks" (410:8652 on the Card Decks page) holds one frame per
    deck: 13 × 4 instances of its cards, then its back.
  - Each frame is shot at 1x on a black fill and on a white fill (get_screenshot, maxDimension 6848,
    contentsOnly). The alpha is recovered from the two shots.
  - Each card is scaled to 278 × 386 in premultiplied linear light. That keeps the old 386 px height in
    Figma's shape.
  - The script writes `Art/Cards/<Deck>/Card_<Suit>_<Rank>.png` and `Back_<Deck>.png`.
  - Removed: the old folders (ClassicCrimson, HimalayanIvory, KathmanduGold, Faces), `cut_cards.py`
    (its Figma sheets were deleted on 1 Nov) and `SharedCardFaceTests`.
  - The textures import as ADR 048 keeps card art: RGBA32, mip maps, trilinear, anisotropic 8.
- **The card's shape is Figma's.**
  - `TableCardBuilder.CardHeight` stays 282.6 canvas units. `CardWidth` follows the 512 × 712 shape:
    203.2, was 180.
  - At the table's scale of 0.8 a card is 162.6 × 226.1, still a fifth of the screen's height. Figma's
    "Game Size · Hand & Trick" draws 163 × 226.
  - Rebuilding the Table prefab changed only what follows from the width:
    - the hand's card width and widest spread;
    - the turn prompt, 9 units further right;
    - the opponents' fans. Their cards keep the bidding mockup's 46 px length, so they are 33 wide
      (was 29).
  - The trick's cross (ADR 062) is laid out on the card's height, so it is unchanged.
  - The glow and the subdued overlay are drawn again from Classic's back.
- **The main menu's fan** keeps each ace's height, top edge and tilt, and takes the new width
  (`TableCardBuilder.UseDeckFanFaces` patches the saved prefab).
  - The fan spreads from its foot by as much as a card widened, so the ace of diamonds behind the
    spade still shows its pip and part of its index.
  - The prefab shows Classic's aces, with the default material and no base.
- **The shop's Cards tab** builds one tile per deck in the catalogue, at mockup 15's tile size and
  spacing.
  - Each tile shows its deck's back as tall as before; Figma's card is a little wider than the mockup's
    box.
  - Tapping a tile peeks at its ace of spades, as before.
  - The builder lays out as many tiles as the catalogue has. Four fit the redesigned panel at 16:9,
    21:9 and 4:3, so Vintage can join later.

**No Ads leaves the shop.** The drawer builds only the Themes and Cards tabs, centred in it, with no
gap where No Ads was.

- `ShopDrawer`, `ShopView` and `ShopPresenter` lose No Ads.
- `ShopPresenter` no longer takes `INoAds` or `ILoadingCurtain`.
- `ui.shop.category.no_ads` leaves the localization table.
- The main menu's No Ads button and its `NoAdsOffer` are unchanged.

## Consequences

- **Size.** Estimated as ADR 064 did: each texture's RGBA32 mip chain, deflated as the APK zips game
  data. This is not a release build measured with `android-size.sh`.

  | Card textures | Download |
  | --- | ---: |
  | Before: 52 shared faces, 3 bases, 3 backs | 4.3 MB |
  | Classic | 3.0 MB |
  | Kathmandu Royal | 3.6 MB |
  | Mithila Ink | 3.5 MB |
  | The three decks | 10.0 MB, **+5.7 MB per phone** |
  | Vintage (left out) | 16.2 MB |

  In memory each deck takes 29 MiB, and the card library loads every deck at once: about 87 MiB of
  card textures against 28 MiB before. Loading only the equipped deck would need the same on-demand
  loading as a downloaded Vintage.
- **The corner index** of Figma's decks is a little smaller at the table than the old art's after ADR
  062's addendum. The 10s stand 17.0 canvas units (Kathmandu Royal, Mithila Ink) to 17.6 (Classic),
  against 19.3 before. `TrickFitTests` checks every deck's ranks against 16.9.
- **Courts** are Figma's as of 2 Nov 2026.
  - The owner's pass to redraw them to the Kathmandu Royal J♠'s width stopped at ChatGPT's image
    limit.
  - Measured as body width over height in the middle band (the J♠ measures 0.43), these are still
    wider than 0.50:
    - every Classic court (0.53 to 0.59; the owner later picked this set);
    - Kathmandu Royal's K♥ and K♣;
    - Mithila Ink's four kings, Q♣, J♣ and J♦.
  - Re-exporting after Figma changes means shooting the export sheets again and running
    `cut_decks.py`.
- **Tests:**
  - `CosmeticsShopTests` and `CosmeticsServiceTests` cover the three decks, their prices and names,
    and old ids on disk becoming their successors.
  - `DeckCardArtTests` (new) checks that every deck has its own 52 faces and back in the library, as
    whole cards in Figma's shape, imported RGBA32.
  - `TrickFitTests` checks every deck's index.
  - `ShopFlowTests` checks that the shop has no No Ads and that the two tabs are centred.
- **To undo:** revert this change. The old decks' art is in git history.
