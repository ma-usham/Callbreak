# ADR 064 - Themes: a menu background and its cloth sold together; the shop redesigned around them

**Status:** proposed (31 Oct 2026). The owner asked on 30 Oct 2026 for themes ("generate the figma
design of bg and its matching cloth ... make your unique design but stay on the game theme"), then
how the shop would list them ("how can we list them in shop redesign the shop as well"). Both were
designed in Figma ("V1 Rebuild · Themes" and "V1 Rebuild · Shop"). On 31 Oct the owner said "ok then
implement what are remained in the game. except cards for now."

## Decision

- **A theme is a menu background and its cloth, sold and equipped together.** One purchase gives
  both. Equipping a theme sets both. A background and a cloth from different themes can't be mixed.
  - `CosmeticKind.Theme` is the item the shop sells (`CosmeticCatalog.Themes`). Each theme names its
    background and its cloth (`CosmeticItem.BackgroundId`, `TableId`).
  - The background and cloth kinds remain the parts the game draws. They are no longer sold or
    equipped on their own: `Buy` returns `UnknownItem` and `Equip` throws.
- **Ten themes**, prices placeholders in the catalogue (the owner may change them):

  | Theme | Background | Cloth | Price |
  |---|---|---|---|
  | Durbar Square | Durbar Square | Green Dhaka | free (default) |
  | Himalayan Sunrise | new | slate blue, stitched peaks | 500 |
  | Laligurans Forest | new | deep teal, rhododendron vine | 500 |
  | Golden Terraces | new | olive, ripe rice | 750 |
  | Tihar Lights | new (night) | plum, marigold swags | 1,000 |
  | Prayer Flag Pass | new | charcoal, five flag colours | 750 |
  | Mithila Garden | new | rust, painted border | 1,000 |
  | Phewa Moonlight | new (night) | midnight navy, stars | 1,000 |
  | Pokhara Lakeside | Pokhara Lakeside | Indigo Dhaka | 500 |
  | Monastery Garden | Monastery Garden | Dusk Himalaya | 750 |

  The seven new themes carry a NEW tag. Their art is vector, drawn by
  `Tools/ArtPipeline/NewUI/themes.py` from the Figma components (Theme/*, page 268:2). Each new cloth
  has its own border (`Frame_<Name>.png`), tiled round the screen like the dhaka cloths' border.
- **Nobody loses what they paid for.** Owning either half of a carried-over theme owns the whole
  theme: a bought Indigo Dhaka or Pokhara Lakeside owns Pokhara Lakeside, and a bought Dusk Himalaya
  or Monastery Garden owns Monastery Garden. A save from before themes (version < 3) equips the theme
  of its equipped background, so the menu a player sees stays the same. Its cloth follows the theme.
  The save's bought backgrounds and cloths are kept as they are.
- **The shop** (Figma "V1 Rebuild · Shop", frames A–K):
  - The drawer on the right holds Themes, Cards and No Ads. No Ads hides once bought.
  - Themes: a scrolling row of tiles. Each tile shows the theme's background over a strip of its
    cloth, its name, "Background + cloth", and then Equipped (filled radio, crimson outline), Owned
    (empty radio), or a coin and its price (red while the wallet can't pay it). The wallet sits in the
    panel's corner, and its "+" opens Earn Coins.
  - Tapping a tile opens the theme's preview: its name, "One purchase, both looks", the main menu on
    its background and a game on its cloth side by side with a "+", its description, and Buy for
    (coin) N, Equip theme, or Equipped. The cross closes it. It has no arrows to step through the
    themes: the owner asked for them to go on 31 Oct 2026 ("remove this left and right side arrow").
  - Buy asks first ("You get its menu background and its cloth for N coins. You have M."). A purchase
    says "Tihar Lights is yours!" in a toast and doesn't equip. Not enough coins offers Earn Coins as
    before. The preview is still open on return from Earn Coins.
  - Cards keeps today's decks, tiles and deck peek. The cards themselves are unchanged.
  - The menu and table pictures in the preview are the game's own screens without their background or
    cloth (`ShopThemeOverlays`, `Art/Shop/Themes/Preview_*.png`), drawn over the real background and
    cloth.
- **Night themes' words.** On Tihar Lights and Phewa Moonlight the words a menu draws straight on the
  background (titles, subtitles, the greeting, the player's name, footers) turn light, since dark ink
  sinks into a night sky (`BackgroundInk`, added by `BackgroundInkBuilder` to every screen with the
  equipped background, the main menu included). A night background is flagged in the library
  (`TableBackdropLibrary.Picture.NightScene`). Words on a panel keep their colours.
- **The drawer reaches the screen's right edge** with a notch on the right too: `ShopDrawer` widens its
  face and shadow by the right safe-area inset. `ShopFlowTests` covers it with and without a notch.

## Consequences

- Size: the themes add about 21.0 MB of texture data on Android (ASTC 6×6; the backdrop frame's
  2230 × 1255 for each background and cloth base, 768 × 768 for each border). A phone downloads it
  compressed: about 14.8 MB (deflate at level 9, the way bundletool estimates a download): backgrounds
  6.5, cloth bases 6.9, borders 0.3, tiles 0.8, preview pictures 0.2. The old shop's six thumbnails and
  previews (`Art/Shop/Backgrounds`, `Art/Shop/Tables`) are no longer used, which takes about 0.9 MB off,
  so about 13.9 MB per phone in all. Measured from the imported textures. A full build with
  `Tools/Unity/android-size.sh` fails in the NewUI worktree, which has no EOS config files (they are
  gitignored).
  - Not done, the owner's call: cloth bases at half size (1115 × 628, a smooth radial colour and a
    faint twill; mean difference about 1.3 levels in 255) would save about 5 MB. ASTC 8×8 for the
    backgrounds would soften their edges.
- Every background and cloth in the library loads with the Menu and Table scenes, as in ADR 058. That
  is now 20 full-screen pictures, about 31 MB of texture memory (Durbar Square's uncompressed picture
  is 8 MB of it). Loading only the equipped theme's pictures would need the library to load on demand.
- The old background and cloth strings (`ui.shop.backgrounds.*`, `ui.shop.tables.*`) stay for the
  layouts that still name them. The background and cloth names are now the theme names.
- `ShopState` and `ShopDisplay` hold the shop's rules, tested without the engine.
