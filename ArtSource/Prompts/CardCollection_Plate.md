Accepted: BG_CardCollection_Clean_a1.png

# Card Collection plate (screen 15)

Source: `Assets/ArtRefrences/game-screens/15-card-styles.png` (1672 × 941)
Files: `ArtSource/Working/CardCollection/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcddf0-d404-83e8-b32d-2e3c6d4f554f
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt (a1)

```
Edit the attached Card Collection screen (15-card-styles.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button.
- Top right: the round card-deck button.
- The title "Card Collection", the spaced subtitle "A LITTLE NEPAL IN EVERY HAND" and the thin rule with the small diamond under it.
- The large ivory panel with its soft shadow and the faint patterns in its corners, and everything inside it: the three card tiles (Classic Crimson with its crimson outline, Himalayan Ivory and Kathmandu Gold) with their card backs, names, radio buttons and the "Equipped" / "Owned" labels, and "Choose a card back for your next match."
- The crimson "Use Selected Style" button below the panel, with its shadow.

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the title, the panel and the button with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the panel overlapped the temple and prayer flags on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no cards, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_CardCollection_Clean_a1.png` | 1672 × 941 | Nothing that blocks use. UI all gone, including the button under the panel and the deck button at the top right; no ghost edges; centre is plain mist. Every untouched scenery tile matches best at 0,0; colour within ±4.4 levels per channel (right-hand mountains slightly less red), untouched mist within 1–2. | Accepted |

## Notes for the blend step

- Under the panel ChatGPT added hazy hills and trees beside the temple (to about x = 560) and
  carried the right-hand mountains to about x = 1130; the rest is mist.
