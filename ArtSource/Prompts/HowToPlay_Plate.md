Accepted: BG_HowToPlay_Clean_a1.png

# How to Play plate (screen 12)

Source: `Assets/ArtRefrences/game-screens/12-how-to-play.png` (1672 × 941)
Files: `ArtSource/Working/HowToPlay/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcdc9c-a7ec-83ee-8d34-99c83c53cd71
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt (a1)

```
Edit the attached How to Play screen (12-how-to-play.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button.
- Top right: the round gear button.
- Top centre: the CALLBREAK logo with the Nepal flag above it, "PLAY TOGETHER" and its thin rule.
- The large ivory panel with its soft shadow and the faint patterns and mountain drawings in its corners, and everything inside it: the title "How to Play", the spaced subtitle "CALL YOUR TRICKS. PLAY YOUR HAND." and the diamond divider, the four numbered steps (the crimson number discs, the card, spade and clipboard icons, the headings, the descriptions and the thin dividers), the two tilted playing cards (the ace of spades and the king of hearts), the gold-framed note "Highest total after 5 rounds wins.", and the crimson "Got It" button.

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the logo and the panel with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the panel overlapped the temple and prayer flags on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no cards, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_HowToPlay_Clean_a1.png` | 1672 × 941 | Nothing that blocks use. UI all gone including the two playing cards, no ghost edges, centre is plain mist. Untouched scenery matches best at 0,0 (one tile 1 px left); colour within ±2.5 levels per channel, untouched mist within 2–5. | Accepted |

## Notes for the blend step

- Under the panel ChatGPT carried the right-hand mountains to about x = 1080 and put a few more
  trees beside the temple (to about x = 500). The mist under the panel's bottom edge is about
  4 levels brighter than the reference, where the panel's shadow darkened it.
