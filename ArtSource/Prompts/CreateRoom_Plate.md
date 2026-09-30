Accepted: BG_CreateRoom_Clean_a1.png

# Create Room plate (screen 02)

Source: `Assets/ArtRefrences/game-screens/02-create-room.png` (1672 × 941)
Files: `ArtSource/Working/CreateRoom/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcd8c9-8a64-83e8-acfb-6dda79e82da7
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt (a1)

```
Edit the attached Create Room screen (02-create-room.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button, the round avatar and the words "Namaste, Player".
- Top right: the round gear button, the thin vertical divider and the round shop button.
- Top centre: the CALLBREAK logo with the Nepal flag above it, "PLAY TOGETHER" and its thin rule.
- The large ivory panel with its soft shadow and the faint patterns in its corners, and everything inside it: the title "Your table", the subtitle "SET UP A TABLE FOR YOUR FRIENDS" and the diamond divider, "Room name" and the "Friends' Table" field, the Players and Match length tiles with their icons, "Time per turn" with the 15 sec / 30 sec switch, the lock icon with "Private room · Invite with a code", and the crimson "Create Room" button.

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the logo and the panel with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the panel overlapped the temple and prayer flags on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_CreateRoom_Clean_a1.png` | 1672 × 941 | Nothing that blocks use. Every UI element is gone, including the logo's flag; no panel edge or shadow shows in the high-pass check. Untouched scenery matches best at 0,0; colour within 2–8 levels in the scenery and 1–2 levels in the untouched mist at the top. | Accepted |

## Notes for the blend step

- Under the panel ChatGPT continued faint hazy hills behind the temple (to about x = 750) and,
  on the right, the lake and hazy hills to about x = 1000. They sit under the panel in the mockup.
- The centre under the logo and the panel is plain mist, as asked.
