# ADR 026 - Opponent fans as the new bidding mockup draws them, and bots that chat online

**Status:** accepted (owner, 2 Oct 2026).

**Context.**
- **The fans.** The table drew each opponent's face-down cards as mockup 08 has them: fans that
  rise like hats above the side seats' heads, and on both sides of the top seat's head. On 2 Oct
  2026 the designer redrew the opponent fans in mockup 07 (`07-bidding.png`; prompts in
  `07-bidding-opponent-fans-prompt.md` and `07-bidding-gentle-fans-prompt.md`). The owner asked
  for the game's fans in that style.
- **Silent bots.** Online, the bots that fill seats now look like players (ADR 025), but they
  never used the quick phrases. The owner asked for opponents that chat now and then, with sense.

**Decision.**
- **The fans follow the new mockup 07** (measured on it):
  - A side seat's 13 backs lie on the table side of its profile, in a gentle vertical arc:
    - each card points away from a pivot behind the profile (64 px out from the avatar's centre
      and 36 px down, radius 158 px);
    - the cards are 5.35° apart, 64° in all, and the bottom card lies in front.
  - The top seat's backs hang below its profile, behind its name and status pill:
    - they turn about a point 164 px above the avatar's centre (radius 242 px), 4.77° apart, about
      57° in all;
    - they are half to each side from the middle out, so the middle cards lie in front.
  - The cards keep a real card's shape, so the mockup's 46 px long backs are 29 px wide here (the
    painting draws them narrower), and they show the table's deck back (ADR 020).
  - As cards are played a fan shortens from its far end.
- **Online, the bots that fill seats say a quick phrase now and then** (`BotChatter`, on the
  hosting device), judged only from what every player can see:
  - "Namaste!" as the first hand is dealt, and back to a player who says it.
  - "Nice call!" to someone else's call of 5 or more (not one the timer made).
  - "Oops!" from a bot whose ace or king was cut by a spade, or that missed its call.
  - "Well played!" to a player who met a call of 5 or more.
  - "Good game!" as the match's last trick is taken (the results cover the table after that), and
    back to a player who says it.
  - Now and then "Thinking…" as a bot's turn begins.

  Each moment is remarked on only by chance (3-85%), by one bot, 0.2-3.5 s later. After a bot has
  spoken, the table's bots keep quiet for 8 s, and a remark whose moment has passed by 6 s is
  dropped. Every bot phrase also keeps to the people's limits (one every 3 s, ten a round), and
  muting an opponent hides its phrases as before. Offline the bots stay quiet.

**Consequences.**
- Mockup 08 (gameplay) and the match menu mockup still draw the old fans until they are redrawn;
  the game follows mockup 07.
- The chatter's generator is seeded from the match id, so the table draws its other seeds as before.
  The room host has a `botsChat` switch (on); the room test fixture turns it off, so the tests that
  count messages see only people's, and a test of its own plays a whole match with it on.
- About ten bot phrases in a five-round match; the chances are constants in `BotChatter`.
