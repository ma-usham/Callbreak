Accepted: BG_PrivateLobby_Clean_a1.png

# Private Lobby plate (screen 04)

Source: `Assets/ArtRefrences/game-screens/04-private-lobby.png` (1672 × 941)
Files: `ArtSource/Working/PrivateLobby/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcda3c-a3f0-83e8-ab65-f0980956e386
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt (a1)

```
Edit the attached Private Lobby screen (04-private-lobby.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button and the CALLBREAK logo with the Nepal flag, "PLAY TOGETHER" and its thin rule.
- Top right: the round gear button.
- The title "Friends' Table", the spaced subtitle "EVERYONE IS HERE" and the thin rule with the small diamond under it.
- The wide ivory panel with its soft shadow and the faint patterns and mountain drawings in its corners, and everything inside it: the "Room code AB12CD" pill with its copy icon, the "Invite Friends" button, the four round avatars with their green dots, the names and the "Host" / "Ready" labels, the thin vertical dividers, the divider line with the diamond, "4 / 4 players · 5 rounds · 30 sec per turn", and the crimson "Start Game" button with the gold spade.

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the title and the panel with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the logo or the panel overlapped the temple and prayer flags on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_PrivateLobby_Clean_a1.png` | 1672 × 941 | Nothing that blocks use. Every UI element is gone, including the logo and flag at the top left (textile pattern and mist continued there); no panel edge or shadow in the high-pass check. Untouched scenery matches best at 0,0 or 1 px left; colour within ±3 levels per channel (untouched mist within 1–2). | Accepted |

## Notes for the blend step

- The panel here is wide (about x = 330 to 1340) and cuts into the temple's right side and the
  mountains' left slope. ChatGPT continued trees and haze beside the temple to about x = 560 and
  the mountains, with drifting cloud, to about x = 950. That is all under the panel in the mockup.
- Many untouched scenery tiles match best 1 px to the left; treat it as resampling, not a
  shift, but use a feathered mask rather than a hard edge.
