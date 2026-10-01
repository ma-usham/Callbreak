# ADR 027 - Seats placed for the player, phrases in a speech box over the speaker, fans that close up

**Status:** accepted (owner, 2 Oct 2026).

**Context.** On 2 Oct 2026 the owner asked for these changes at the table, with a picture of an ivory
speech box saying "Well played!":
- the left and right players a little lower, toward the player;
- the top player's name and bid above its picture, and its cards lowered so its profile does not
  hide them;
- the player's own name and bid below their picture;
- slightly bigger cards;
- a chat message in a box just above the speaker, like the picture;
- an opponent's cards rearranged near them, seamlessly, after cards are thrown.

Asked, the owner chose:
- the bigger cards are the player's hand;
- their own phrase shows above their own avatar, not by the emote button (ADR 024);
- the box art is made with ChatGPT from the picture.

**Decision.**
- **Seats** (mockup 08 px):
  - **Side seats** move down 55 px, with everything that belongs to them: avatar, name plate, bid
    pill, fan, turn ring, seconds disc, call bubble, chip, the tap-to-mute ring and the speech box.
  - **Top seat**, as the designer's mockup 19 (2 Oct 2026) draws it:
    - its name plate and bid pill sit above its avatar, the plate 5 px under the trump line;
    - the avatar comes down 39 px, to just under the pill;
    - its fan keeps its shape below the avatar, so nothing covers it: its middle cards start
      14 px below the ring and stay 32 px above the top trick card;
    - its seconds disc and call bubble move to the avatar's left, its chip to the ring's bottom
      edge.
  - **Local seat:**
    - its name plate and bid pill, at the opponents' widths, are centred below its avatar, the
      pill keeping its place at the bottom;
    - the avatar, its timer ring and seconds disc move 46.5 px up and 25 px right, so the plate
      fits on screen and lies over the ring's foot as an opponent's does;
    - its seconds disc sits to its left, its chip on the ring's top edge, and its call bubble to
      its right, short of the hand.
  - **Emote picker:** it opens 24 px lower, below the right seat's status pill.
- **The hand's cards are 9% bigger** than the shared card, which the trick, the fans and the deal
  keep.
  - The hand keeps its width, so its cards overlap a little more.
  - The hand's touch area and the turn prompt move up 11 px with the cards' tops.
- **Quick phrases show in a speech box over the speaker**, the player's own included:
  - **The box:** an ivory box with a golden rim and a tail pointing at the speaker. It sits just
    above the avatar (the top seat's at its upper right, so its name, bid and the round title
    stay readable). It grows from its tail to fit the phrase, in bold crimson.
  - **Timing:** it shows for 2.4 s, then shrinks and fades away like the call bubble (ADR 023);
    with Reduce motion it comes and goes without motion. A phrase never blocks a touch on the
    cards under it.
  - **What stays the same:** calls still show in the seat's small call bubble. Muting an opponent
    hides its box too. The emote button keeps its 3 s sweep (ADR 024); the bubble by the button
    is gone.
- **Opponent fans close up after a play.**
  - When a side opponent plays, its remaining cards close up around the middle of their arc,
    beside the player, gliding there in 0.25 s (OutCubic, on unscaled time).
  - The top seat's two halves already shorten evenly from both ends.
  - With Reduce motion the cards jump to their places.
- **The deal lands every card where it stays.** A card dealt to an opponent flies to its own place
  in the fan. Before, two cards for a seat could be in the air at once and aim at the same place,
  then jump (up to 266 canvas units in the top fan).

**Consequences.**
- Replaces ADR 024's "your phrase by the button" and ADR 026's "a fan shortens from its far end"
  (for the side seats).
- **Mockups:**
  - Mockups 07 and 08 still place the seats' names and pills as before; the game follows this
    ADR.
  - Mockup 19 also puts the side seats' names and pills outside their avatars. The owner asked
    only for those seats to move down, so their names stay below the avatars.
- **Smaller knock-on effects:**
  - The "I'm back" banner covers the lower half of the top seat's fan while it shows.
  - The trick counter the GDD asks for ("Trick 6 / 13", hidden; mockup 08 does not draw it) sits
    where the top seat's name plate now is, and needs a new place if it is ever shown.
- **New art:** `Assets/_Project/Art/UI/Table/UI_ChatBubble.png` (the box, 9-sliced; its shadow is
  baked in) and `UI_ChatBubble_Tail.png` (the tail, drawn over the box, mirrored for the right
  seat).
  - Made in ChatGPT from the owner's picture, then cleaned up.
  - The attempts, the prompts and the clean-up recipe are in
    `ArtSource/Working/Table/chatgpt/prompts.md`.
  - The chat is archived:
    https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abec435-6bdc-83e8-acb7-7d91b7ecc22a
- **The table simulation checks:**
  - your phrase in your own box, above your avatar;
  - an opponent's phrase in its box, and muting hiding it;
  - the fans exactly at their places after every play and through the deal;
  - a side fan centred on its middle after a play;
  - a dealt card landing where it stays;
  - the glide under way ending at its places.

  The checks of the bubble by the emote button are gone.
- The Table prefab is rebuilt from its layout.
