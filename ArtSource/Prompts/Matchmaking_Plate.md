Accepted: BG_Matchmaking_Clean_a2.png

# Matchmaking plate (screen 05)

Source: `Assets/ArtRefrences/game-screens/05-matchmaking.png` (1672 × 941)
Files: `ArtSource/Working/Matchmaking/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcdab8-2864-83ee-88c1-6ac4392b41c9
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt 1 (a1)

```
Edit the attached Matchmaking screen (05-matchmaking.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button, the round avatar and the words "Namaste, Player".
- Top right: the round gear button, the thin vertical divider and the round shop button.
- The title "Play Online", the spaced subtitle "FINDING YOUR TABLE" and the thin rule with the small diamond under it.
- The large ivory panel with its soft shadow and the faint patterns and mountain drawings in its corners, and everything inside it: the crimson progress ring with the black spade, "Finding players...", "3 of 4 players connected", the four round avatars with the names and the "Ready" / "Searching" labels, "5 rounds · Spades are trump" with its two thin lines, the "Cancel" pill button and the "00:12" timer pill with its clock icon.

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the title and the panel with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the panel overlapped the temple and prayer flags on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no buttons, no panels, no frames.
```

## Prompt 2 (a2), same chat

```
Second attempt, please. Your result has a warm yellow cast over the whole picture: the temple, the mountains, the flowers and the mist are all more yellow and less blue than in the original. Faint hills also now run across the middle.

Edit the original 05-matchmaking.png again (the image I attached first, not your result) and remove exactly the same UI. Keep every pixel outside the removed UI as it was; do not re-paint or re-colour the scenery. Where the title and the panel were, use the same soft pale ivory mist as the untouched mist around them, with the same colour and brightness, and no hills, trees or flags across the middle. Same 16:9 framing and the same image size (1672 x 941). No text, no letters or numbers, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_Matchmaking_Clean_a1.png` | 1672 × 941 | UI all gone and no shift, but a warm yellow cast over the whole image: blue 6–12 and green 4–10 levels low on the temple, mountains and flowers, blue 6–9 low in the untouched mist. The prayer flags were run on to about x = 580 and faint hills ran right across the middle. | Not used |
| `BG_Matchmaking_Clean_a2.png` | 1672 × 941 | Nothing that blocks use. UI all gone, no ghost edges, centre is plain mist. Untouched scenery matches best at 0,0; colour within ±3 levels per channel, untouched mist within 1–2. | Accepted |

## Notes for the blend step

- Under the panel the temple side gets a little more forest and haze (to about x = 480) and the
  mountains continue to about x = 1100 on the right. All of it sits under the panel in the mockup.
