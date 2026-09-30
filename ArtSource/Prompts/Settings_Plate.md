Accepted: BG_Settings_Clean_a1.png

# Settings plate (screen 11)

Source: `Assets/ArtRefrences/game-screens/11-settings.png` (1672 × 941)
Files: `ArtSource/Working/Settings/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcdc9c-9d58-83ee-bc07-97651902796f
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt (a1)

```
Edit the attached Settings screen (11-settings.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button, the round avatar and the words "Namaste, Player".
- Top right: the round gear button.
- The title "Settings", the spaced subtitle "MAKE YOURSELF AT HOME" and the thin rule with the small diamond under it.
- The large ivory panel with its soft shadow and the faint patterns in its corners, and everything inside it: the Sound and Preferences headings with their speaker and gear icons, all the labels (Music, Sound effects, Volume, 70%, Language, Vibration, Reduce motion), the crimson and grey toggle switches, the volume slider, the "English" drop-down, the thin divider lines, the "How to Play" and "About" buttons with their icons, and the crimson "Done" button.

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the title and the panel with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the panel overlapped the temple and prayer flags on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_Settings_Clean_a1.png` | 1672 × 941 | Nothing that blocks use. UI all gone, no ghost edges, centre is plain mist. Every untouched scenery tile matches best at 0,0; colour within ±3.4 levels per channel, untouched mist within 2–3. | Accepted |

## Notes for the blend step

- Very little invention under the panel: some trees beside the temple (to about x = 470) and
  the right-hand mountains carried to about x = 1130; the rest is mist.
