Accepted: BG_Table_Clean_a2.png (wide version: BG_Table_Wide_a1.png)

# Table plate (screen 08, gameplay)

Source: `Assets/ArtRefrences/game-screens/08-gameplay.png` (1672 × 941)
Files: `ArtSource/Working/Table/chatgpt/`, extension input in `ArtSource/Working/Table/attach/`
ChatGPT project "CardGame", 30 Sep 2026. Model setting left as the owner had it.

- Clean plate chat (a1, a2): https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcd6a5-5704-83e8-b3f6-c22b79d22fdf
- Wide extension chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcd974-0758-83e9-83e1-9fb518ead9d0

## Prompt 1 (a1), new chat, attachment `08-gameplay.png`

```
Edit the attached gameplay screen (08-gameplay.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scene and the empty table.

Remove all of this:
- Top left: the round home button and the CALLBREAK / PLAY TOGETHER logo.
- Top centre: the text "ROUND 1 / 5" and "Trump ♠" with its thin line-and-arrow ornaments.
- Top right: the round Scores and Settings buttons, the thin divider between them, and the labels "Scores" and "Settings".
- All three opponent seats (Asha at the top, Bikash on the left, Suman on the right): the round avatars, the white name plates, the "Bid 3 · Won 1" pills and the fans of red face-down cards behind each avatar.
- The three face-up cards in the middle of the table.
- The whole row of 13 cards along the bottom edge.
- Lower left: the round chat button and the "You / Bid 4 · Won 2" badge with its avatar, orange timer ring and the "18" disc.
- Lower right: the round card-stack button.

Where each element was, continue what lies behind it naturally, as if the UI had never been there: the plain beige table surface, the gold carved rim with its key-pattern band, the wooden outer edge, the stone walls, trees, flowers, temple, prayer flags, lake, village and mountains. Along the bottom, where the hand of cards was, continue the table's near rim in the same curve it follows at the lower left and lower right, with the blurred red flowers below it.

Keep the table surface, its gold carved rim, the ornament motifs on the table, and all the scenery exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no cards, no buttons, no frames.
```

## Prompt 2 (a2), same chat

```
Very good, one correction. In the original screenshot the sky behind the CALLBREAK logo (top left) and behind "ROUND 1 / 5" and "Trump" (top centre) is a soft, pale, warm whitish haze, lighter and less saturated than the blue sky elsewhere. In your result that area became saturated blue sky with bright clouds.

Redo the clean plate, starting again from the original 08-gameplay.png I attached (not from your result). Remove exactly the same UI in the same way, but where the logo and the round text were, keep the same soft pale haze so the sky there has the same colour and brightness as the original around it. Keep every pixel outside the removed UI as it was; do not re-paint, re-colour, sharpen or shift the scenery. Same 16:9 framing and the same image size (1672 x 941). No text, no letters, no buttons, no cards.
```

Correction to my own prompt: measured afterwards, the reference's sky around the logo and the
round text is the same light blue as the open sky (above the logo 168,206,240; open sky at the
right 168,202,237). "Pale, warm whitish haze" overstated it, which is why a2 went too pale.

## Attempts

How each was checked: read next to the reference; best local shift searched on an 8 × 5 grid of
tiles (±8 px, on luminance gradients); mean colour measured in untouched areas and in thin
strips just outside each UI element; amplified difference image and a high-pass contrast
stretch to look for ghost edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_Table_Clean_a1.png` | 1671 × 941 | All UI gone and the scene is not shifted (every untouched tile matches best at 0,0). But the sky around the logo and "ROUND 1 / 5" came out a more saturated blue than the reference (above the logo 139,197,242 against 168,206,240; red 10–30 lower in the strips next to the logo and the round text), it invented a carved ornament at the bottom centre of the table rim, and it is one pixel narrower than the reference. | Not used |
| `BG_Table_Clean_a2.png` | 1672 × 941 | All UI gone, no ghost edges, no shift (untouched tiles match best at 0,0). Near rim continued in a clean curve with the key-pattern band, no invented ornament, flowers continued in both lower corners, seats and centre cards replaced by plain table and rim. Colour in untouched areas within 1–3 levels on the table and 4–7 mean difference in the detailed scenery (re-drawn texture, not a shift). The sky around the logo and the round text is now too pale and white: above the logo 191,211,235 against 168,206,240; red +12 to +23, blue −4 to −6 in the strips next to the logo and the round text. | Accepted, sky needs a local colour fix |

No third attempt: the owner stopped per-screen plates before it was sent.

## Wide extension (step 2)

Input: `attach/BG_Table_Extend_input.png`, 2560 × 1440, flat #FF00FF with `BG_Table_Clean_a2.png`
resized to 1920 × 1080 and centred (320 px margins left and right, 180 px top and bottom). New
chat, this image only.

```
The attached image (BG_Table_Extend_input.png, 2560 x 1440) is a finished painting of a carved game table in a Nepali mountain landscape, placed in the middle of a flat pure-magenta (#FF00FF) border: 320 px of magenta on the left and right, 180 px at the top and bottom. The painting itself is 1920 x 1080.

Outpaint: fill ONLY the magenta border by continuing the scene outward, so the whole canvas becomes one seamless painting:
- Top: more sky with the same soft clouds and pale haze, and the tops of the trees that the upper edge cuts off.
- Bottom: more of the table's carved wooden rim and its gold key-pattern band, continuing their curve down and out of frame, and more of the blurred red flowers in the lower corners.
- Left and right: more of the stone walls, stone paving, trees, flowering shrubs and prayer flags, continuing the temple side on the left and the village and lake side on the right.

Do not change any pixel of the central painting: keep it exactly where it is, at the same size and with the same colours. Match its brushwork, lighting, colours and atmospheric haze in the new areas so there is no visible seam. No new landmarks (no extra temples, pagodas, peaks, lakes or large buildings), no text, no people, no frames, and no magenta left anywhere. Output the full 2560 x 1440 canvas, 16:9.
```

| File | Output size | Notes | Verdict |
| --- | --- | --- | --- |
| `BG_Table_Wide_a1.png` | 1672 × 941 | ChatGPT returned the whole 2560 × 1440 canvas scaled to 1672 × 941 (0.653×); it does not output 2560 × 1440. No magenta left, no text, no visible seam. The central painting was re-drawn, as ChatGPT itself warned: it sits about 1 px left of where it should at this scale (about 1.5 px at 2560), colour within 1–6 levels, texture detail differs (mean difference 11). New content in the border: more sky and clouds, a large yellow-leaved tree and more prayer flags at the top left, flowering shrubs and stone walls on both sides, stone paving, the table's carved wooden side band (scroll carving) below the gold rim, red flowers in the bottom corners. No new temples, peaks or lakes. | Accepted, with the resolution caveat |

## Notes for the blend step

- Use `BG_Table_Clean_a2.png` only inside the UI areas; the reference's own pixels are sharper and
  exact everywhere else. Alignment is exact (no shift), so a feathered mask is enough.
- Fix the sky colour locally around the logo and "ROUND 1 / 5 / Trump" before blending (fit the
  plate to the reference in a ring just outside each of those two areas): a2's fill there is
  paler and whiter than the reference's light-blue sky. Everywhere else the fill matches within a
  few levels.
- The hand area (bottom) and the three seats are pure invention by ChatGPT (plain table, rim and
  flowers); nothing of the reference survives there to compare against.
- For the wide version, upscale `BG_Table_Wide_a1.png` to 2560 × 1440 and use only its border;
  put the patched 16:9 plate (scaled to 1920 × 1080) in the centre with a feathered seam. The
  border is 1.53× upscaled, so it is softer than the centre, and above the logo it continues
  a2's paler sky.
