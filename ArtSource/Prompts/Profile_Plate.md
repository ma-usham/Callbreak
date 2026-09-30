Accepted: BG_Profile_Clean_a1.png

# Profile plate (screen 14)

Source: `Assets/ArtRefrences/game-screens/14-profile.png` (1672 × 941)
Files: `ArtSource/Working/Profile/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcdc9c-a3a4-83ee-a490-0a704f30de6d
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt (a1)

```
Edit the attached Profile screen (14-profile.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button.
- The title "Your Profile", the thin rule with the small diamond and the spaced subtitle "EVERY HAND TELLS A STORY".
- The large ivory panel with its soft shadow and the faint patterns and mountain drawings in its corners, and everything inside it: the big round avatar with the pencil badge, "Player", "Callbreak enthusiast", the thin vertical divider with its diamond, the four stat tiles (Games played 48, Wins 19, Win rate 40%, Best score 26.4) with their icons, the divider line with the diamond, "Display name" and the "Player" field, the crimson "Save Changes" button and "Your name is visible at the table."

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the title and the panel with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the panel overlapped the temple and prayer flags on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_Profile_Clean_a1.png` | 1672 × 941 | Nothing that blocks use. UI all gone, no ghost edges, centre is plain mist. Every untouched scenery tile matches best at 0,0; colour within ±3.3 levels per channel, untouched mist within 1. | Accepted |

## Notes for the blend step

- Under the panel ChatGPT added a pale distant peak and hazy slopes behind the temple (about
  x = 320 to 560) and carried the right-hand mountains to about x = 1050. All of it sits under
  the panel in the mockup.
