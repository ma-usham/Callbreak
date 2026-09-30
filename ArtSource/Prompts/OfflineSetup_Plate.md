Accepted: BG_OfflineSetup_Clean_a1.png

# Offline Setup plate (screen 06)

Source: `Assets/ArtRefrences/game-screens/06-offline-setup.png` (1672 × 941)
Files: `ArtSource/Working/OfflineSetup/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcdb96-cd24-83ee-a679-3a09ee588190
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt (a1)

```
Edit the attached Offline Setup screen (06-offline-setup.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button, the round avatar and the words "Namaste, Player".
- Top right: the round gear button.
- The title "Play Offline", the spaced subtitle "PRACTICE AT YOUR OWN PACE" and the thin rule with the small diamond under it.
- The large ivory panel with its soft shadow and the faint patterns in its corners, and everything inside it: the monitor-with-spade icon, "Choose your opponents", "Play against three computer players.", the three option tiles (Easy, Normal, Hard) with their people icons and texts, the crimson outline and the check badge, "4 players · 5 rounds · No connection needed" with its two thin lines, and the crimson "Start Game" button.

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the title and the panel with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the panel overlapped the temple and prayer flags on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_OfflineSetup_Clean_a1.png` | 1672 × 941 | Nothing that blocks use. UI all gone, no ghost edges, centre is plain mist. Most untouched scenery tiles match best 1 px to the left (a uniform 1 px drift, not a re-framing). Colour within 1–6 levels; the temple is slightly warm (blue −5). | Accepted |

## Notes for the blend step

- Expect a 1 px horizontal offset over the whole plate: shift it 1 px right before blending, or
  feather the mask by a few pixels.
- Under the panel ChatGPT painted a pale extra snow peak and hills behind the temple (about
  x = 350 to 640) and continued the right-hand mountains to about x = 990. All of that is under
  the panel in the mockup.
