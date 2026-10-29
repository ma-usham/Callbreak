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
- **The art is the V1 rebuild's own vector art** (Figma "V1 Rebuild · Design System"), not V1's
  paintings:
  - the three backgrounds (Himalayan Vista, Pokhara Lakeside, Monastery Garden) and the three cloths,
    drawn with a margin for 21:9 and 4:3 (the backdrop frame, 2230 × 1255 frame pixels), with the
    shop's thumbnails and previews cut as Figma's tiles show them;
  - the CALLBREAK logos;
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
  (BG_Menu_Wide/Clean) are gone. The six full-screen pictures take 7.15 MB on Android (ASTC 6×6),
  all loaded with the Menu and Table scenes, as V2's did.
- The layouts' `"background": "equipped"` builds the equipped background into a screen.
  `MainMenuBuilder.UseEquippedBackground` patched it into the saved main menu.
- `TableView.SetTable` takes the cloth's border as well.
- The Figma file keeps two export sheets on its design-system page, "Unity Export · NewUI" and
  "Unity Export · Plates", for the next export.
