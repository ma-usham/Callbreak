Accepted: BG_PlayWithFriends_Clean_a1.png

# Play with Friends plate (screen 01)

Source: `Assets/ArtRefrences/game-screens/01-play-with-friends.png` (1672 × 941)
Files: `ArtSource/Working/PlayWithFriends/chatgpt/`
Chat: https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abcd888-2c50-83ee-8180-4ec9079e4802
(ChatGPT project "CardGame", 30 Sep 2026, new chat with only this screen; model setting left as the owner had it.)

## Prompt (a1)

```
Edit the attached Play with Friends screen (01-play-with-friends.png). Make a clean background plate: remove EVERY user-interface element and keep only the painted scenery.

Remove all of this:
- Top left: the round back-arrow button, the round avatar and the words "Namaste, Player".
- Top right: the round gear button, the thin vertical divider and the round shop button.
- The title "Play with Friends", the spaced subtitle "CREATE A ROOM AND PLAY TOGETHER" and the thin rule with the small diamond under it.
- Both large ivory cards ("Create Room" and "Join Room") with their soft shadows, the faint pattern and mountain drawings inside them, and everything on them: the people and door icons, the headings, the descriptions and the two crimson buttons.

Where each element was, continue the painting naturally, as if the UI had never been there. The middle of the picture is soft pale ivory mist: fill everything under the title and the cards with the same mist that surrounds them, fading naturally into the existing scenery at the edges. Where the cards overlapped the temple on the left or the mountains and village on the right, continue those just a little and let them fade into the mist. Do not add a hill, island, trees, shoreline, water, buildings or new mountains in the middle.

Keep the temple, prayer flags, mountains, village, red rhododendrons, the pale peach textile patterns in the top corners, colours, lighting and composition exactly as they are, in the same place and at the same size. Do not re-paint, re-colour, re-frame, crop, zoom or shift anything. Same 16:9 landscape framing and the same image size (1672 x 941). No new objects, no text, no letters or numbers, no buttons, no panels, no frames.
```

## Attempts

Checked by reading it next to the reference, a local shift search on an 8 × 5 tile grid, mean
colour differences in untouched mist and scenery, an amplified difference image and a high-pass
stretch that shows ghost panel edges.

| File | Output size | What was wrong | Verdict |
| --- | --- | --- | --- |
| `BG_PlayWithFriends_Clean_a1.png` | 1672 × 941 | Nothing that blocks use. Every UI element is gone; the high-pass check shows no panel edge or shadow rectangle. Untouched scenery matches best at 0,0 or 1 px left (resampling), colour within 1–5 levels; the untouched mist above and below the cards matches within 1–3 levels. | Accepted |

## Notes for the blend step

- Under the right card ChatGPT carried the mountain range and the forested slope about 350 px
  further left (to about x = 950) and the village a little further in; under the left card the
  forest behind the temple reaches about x = 560 and the prayer flags run on to about x = 450.
  All of that sits where the cards are in the mockup, so it only shows if the cards move or fade.
- The textile pattern under the back button was continued; the gear and shop area is plain
  textile and mist.
- The gap between the two cards is lighter in the plate than in the reference, because the
  cards' shadows are gone (as intended).
