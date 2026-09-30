Accepted: BG_Scorecard_Clean_a1.png

# Scorecard plate (screen 16)

Source: `Assets/ArtRefrences/game-screens/16-scorecard.png` (1672 × 941)
Files: `ArtSource/Working/Scorecard/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcddf0-c064-83ee-af50-1a31c16fbb58
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt (a1)

```
Edit the attached Scorecard screen (16-scorecard.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button and the CALLBREAK logo with "PLAY TOGETHER" and its thin rule.
- Top right: the round gear button.
- The large ivory panel with its soft shadow and the faint patterns in its corners, and everything inside it: the title "Scorecard", the crimson spade medallion with its line ornaments, "FIVE ROUNDS AT A GLANCE", the whole score table (the header row with Player, R1 to R5, Total and Rank, the four player rows with avatars, names and numbers, and the highlighted gold row), "Final standings · 5 rounds completed" with its lines and diamond, and the crimson "Back to Results" button.

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the logo and the panel with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the panel overlapped the temple and prayer flags on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_Scorecard_Clean_a1.png` | 1672 × 941 | Nothing that blocks use. UI all gone, no ghost edges, centre is mist with soft cloud texture. Untouched scenery matches best at 0,0 or 1 px left/up; colour within ±4.5 levels per channel (untouched mist at the top slightly warm, blue −4.5). | Accepted |

## Notes for the blend step

- A few tiles match best 1 px left or up; feather the mask by a few pixels.
- Under the panel ChatGPT added a pale distant peak and forested slopes behind the temple
  (about x = 200 to 520) and carried the right-hand mountains and cloud to about x = 1040. All
  of it sits under the panel in the mockup.
