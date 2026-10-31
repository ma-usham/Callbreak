# ADR 058 - The V1 rebuild's vector art; play on a cloth, the shop sells cloths; the chosen background behind the menus

**Status:** proposed (29 Oct 2026). The owner, having approved the V1 rebuild in Figma ("these are
good now"), on 29 Oct 2026: "lets remove the table feature", "right now use green cloth we will sell
other in store instead of tables", then "implement the current UI we made into this game. each and
everything", in a new branch, NewUI. Asked how: keep today's cards ("We need to redesign and change
the card, for now you can use today's card"), the cloths at 0 / 500 / 750, and the chosen background
behind every menu.

## Decision

- **The game is played on a cloth, with no table.** What the table showed (the equipped background's
  scenery, the table, the scenery's foreground) gives way to the equipped cloth filling the screen.
  - A dhaka cloth has a stitched gold border round the screen's edges (`TableView` field
    `_clothFrame`, `Art/Tables/Frame_Dhaka.png`): its corners are fixed and its edges tiled, so it
    hugs the screen at 16:9, 20:9 and 4:3. Dusk Himalaya, a scene, has none.
  - The library holds one picture per cloth and its border, if any (`TableBackdropLibrary.Picture.Frame`).
- **The shop sells cloths where it sold tables**, at the tables' prices:
  - Green Dhaka (free, the default), Indigo Dhaka 500, Dusk Himalaya 750;
  - the category reads "Cloths" and its title "Cloth Collection", with a folded-cloth icon.
  - The kind keeps its name in code (`CosmeticKind.Table`) and its save fields.
  - A saved table becomes the cloth in its place, owned and equipped (`CosmeticCatalog.Tables.Renamed`):
    Himalayan Ivory → Green Dhaka, Royal Crimson → Indigo Dhaka, Heritage Walnut → Dusk Himalaya.
    Nobody pays twice; the file keeps the cloths' ids the next time it is saved.
- **The equipped background shows behind every menu screen**, the main menu's included, and changes
  as soon as another is equipped (`EquippedBackground`, from the V2 scenes work, agent/T-v2-scenes).
- **Durbar Square is the free background, in the Himalayan Vista's place.** The owner, on 30 Oct 2026:
  "use burbar square as mainmenu bg image", then asked how, "replace the default". It is Figma's
  "Art/Menu Option · Durbar Square" (240:103587): golden hour in a Kathmandu durbar square.
  - Its id is `durbar-square`. Every player sees it behind the menus unless they equip Pokhara
    Lakeside or Monastery Garden.
  - A save that names the Himalayan Vista owns and equips Durbar Square
    (`CosmeticCatalog.Backgrounds.Renamed`). The file keeps the new id the next time it is saved.
  - Its picture keeps the Vista's files' GUIDs (renamed to `BG_DurbarSquare` and `Thumb_DurbarSquare`),
    so every screen that showed the default shows Durbar Square without a rebuild.
  - Its picture is uncompressed: the owner wants no softening.
  - Its birds fly right of the logo. Figma's fly where the game's Temple Crown logo stands.
- **The art is the V1 rebuild's own vector art** (Figma "V1 Rebuild · Design System"), not V1's
  paintings:
  - the three backgrounds (Durbar Square, Pokhara Lakeside, Monastery Garden) and the three cloths,
    drawn with a margin for 21:9 and 4:3 (the backdrop frame, 2230 × 1255 frame pixels), with the
    shop's thumbnails and previews cut as Figma's tiles show them;
  - the CALLBREAK logos: the owner chose option E, "Temple Crown", on 30 Oct 2026 (Marcellus SC in
    crimson and gold under a gilt three-tier pagoda roof; the compact logo drops the roof);
  - the table's "Reconnecting…" art: the owner chose option A, "Dhaka Ring", on 30 Oct 2026. Twelve
    stitched gold diamonds turn round an ivory seal with the crimson spade, the bright one running
    anticlockwise ahead of its fading trail. The ring is centred on the seal (Figma's sits 10 px
    off), so it turns without a wobble. The same day the owner asked for it on the loading screen too
    ("use the same dhaka rotating image for loading as well"): it takes the old ring's place there,
    three quarters the size, and the black cover over the store's purchase sheet (ADR 042) keeps its ring;
  - the kit's panels, buttons and controls, as the rebuild's plates, each at its sprite's size and
    scale. The crimson buttons keep their dhaka ends whole (drawn wide, cut to ends and a plain
    middle; shadows redrawn), and the menu panel and the shop drawer get borders that hold their
    dhaka corners. The results tables' header bar turns crimson, so its captions turn cream.
- **What stays as it is:**
  - the cards (the owner will redesign them);
  - the icons, portraits and card backs (the rebuild kept V1's too);
  - the toast (ToastBuilder draws it);
  - the avatar ring, a white disc behind the portraits.
- **Tools/ArtPipeline/NewUI** redraws all of it (see its README).

## Consequences

- The table's three-layer backdrop (BG_*_Back/_Front, BG_Table) and the painted menu pictures
  (BG_Menu_Wide/Clean) are gone. The six full-screen pictures took 7.15 MB on Android (ASTC 6×6),
  all loaded with the Menu and Table scenes, as V2's did. Since Durbar Square they take 13.97 MB:
  its uncompressed picture is 8.01 MB where an ASTC 6×6 one is 1.19 MB.
- The layouts' `"background": "equipped"` builds the equipped background into a screen.
  `MainMenuBuilder.UseEquippedBackground` patched it into the saved main menu, and
  `TableCustomizer.UseDhakaRing` and `SceneLoadingCustomizer.UseDhakaRing` the Dhaka Ring into the saved
  table and loading curtain (their sprites uncompressed).
- `TableView.SetTable` takes the cloth's border as well.
- The Figma file keeps two export sheets on its design-system page, "Unity Export · NewUI" and
  "Unity Export · Plates", for the next export.
