Accepted: T1 a1
Final: `ArtSource/Working/ShopBackgroundsB/final/Thumb_HimalayanVista.png` (copy in `ArtSource/Exports/Screens/Shop/`)

# Shop background 4: Himalayan Vista, shop tile picture only

Agent: ShopBackgroundsB, 4 Oct 2026. Himalayan Vista is the current gameplay background
(`Assets/_Project/Art/Backgrounds/BG_Table.png`); its layers (Back, Table, Front) are the ShopTables
agent's (`ArtSource/Working/ShopTables/final/`). This file covers only its shop tile picture.
Chat (the same single chat as Pokhara Lakeside and Monastery Garden):
https://chatgpt.com/c/6ac24827-71e4-83ee-85c7-ba5e04d26741

Spec (`D:\Nepali Callbreak\game-screens\shop-backgrounds-prompts.md`): "snow peaks, blue sky, lake and
distant Nepali town with temple and prayer flags, recalling the CURRENT game's daylight vista".

## T1 (`chatgpt/Thumb_HimalayanVista_a1.png`, 1173 x 1341)

Attachments: `attach/20-backgrounds_tiles.png` (mockup 20's three tiles, crop 375,225 890x430, style
reference) and `attach/HimalayanVista_Back_view.jpg` (ShopTables' `final/BG_HimalayanVista_Back.png`,
16:9 view 279,157 1672x941).

```
Beautiful, thank you. One more tile picture, the last one: "Himalayan Vista", the game's current background.

Image 1 (20-backgrounds_tiles.png) again shows the three shop tiles. Image 2 (HimalayanVista_Back_view.jpg) is the Himalayan Vista background itself, without the table: a sunny stone terrace with a low stone balustrade, red flowering shrubs, a small Newari pagoda temple with strings of prayer flags on the left, a lake with a town and a small temple tower, and the snowy Himalaya with one tall peak under a blue sky with soft white clouds.

Paint the tile picture for "Himalayan Vista" in the same way as the Pokhara Lakeside and Monastery Garden tiles you made: it must sit naturally next to the three tile pictures in image 1, with the same finish, detail, brightness, saturation and painterly style, and the same kind of framing: a close, eye-level postcard view of this place that fills the whole picture, with sky in the upper part, the snowy peaks large in the middle above the lake and the town, the pagoda and prayer flags at one side, and the sunny stone terrace with its balustrade and red flowers in the foreground. Warm daylight from the upper left.

One full-bleed rectangular painting in portrait 7:8 (for example 1176 x 1344 px). No table, no cards, no people, no text or writing anywhere (none on the flags), no caption, no frame, no border, no rounded corners.
```

Result: pagoda with prayer flags on the left, the tall snow peak in the middle above the lake town and
its small temple tower, stone balustrade and paved terrace with red flowers in front; no text.
`scripts/make-thumb.ps1`: centre crop 1173 x 1332 (y 4) to the tile aspect, Lanczos-3 to 590 x 670
(mockup 20's tile pictures are 252-262 x 292 mockup px, x2.3), then luma-preserving saturation x0.75:
sat 0.41, luma 148 (raw 0.51), matching BG_Table's scenery saturation (0.415) and mockup 20's outdoor
tiles (0.39-0.41).

Check: `check/thumbs_catalogue_row.png` (mockup tiles 1-3 crops next to tiles 4-6 at the same size).
