# ADR 038 - Taps skip no animation

**Status:** accepted (owner, 5 Oct 2026).

**Context.** GDD §6 let a tap on the table skip any animation longer than 0.3 s. The owner found
that a tap during the deal, or while a won trick swept to its winner, cut the animation short, and
asked that it not.

## Decision

- **No tap skips an animation.** A tap on the table, the hand or the trick area never skips one, so
  the shuffle, the deal and a trick's sweep always play out.
- **A tap on the table** (beside every control, or on a hand that takes no input) only closes the
  emote panel when it is open.
- **A tap on the trick area** on a quiet table still shows the previous trick (GDD screen 08). While
  an animation plays, it does nothing.
- `MatchEventPlayer.Skip` stays, with its tests, but nothing in the game calls it any more.

## Consequences

- **Pacing.** A player who taps can no longer hurry the table. The deal takes its 2.7 s (0.4 s with
  Reduce motion), and the bots already wait for it (ADR 024).
- **Checks.** The table simulation now taps the table and the trick area a second into a deal, and
  while a won trick sweeps away. The deal still takes its 2.7 s, the sweep plays on, and no earlier
  trick shows.
- **The GDD is out of date here:** §6's tap to skip.

## Addendum, 5 Oct 2026 (night): no peek at the last trick, and no mute

The owner found that a tap in the middle of the table during play showed the previous trick's
cards, with one by each player's avatar, and took it for a bug. So the trick area's peek is removed
too (the owner, 5 Oct 2026), replacing GDD screen 08's peek. This replaces the third point of the
decision above. The avatar tap's mute is removed as well (the owner, 5 Oct 2026), replacing GDD
§8's tap on an opponent to mute their quick phrases.

- **A tap on the trick area is a tap on the table.** It only closes the emote panel when that is
  open, and it never shows a card.
  - The trick area keeps its own catcher in the Table prefab, so the prefab needs no rebuild.
    `TableView` sends its tap to `TableTapped`.
  - Removed: `TrickAreaView`'s peek (`IsPeeking`, `BeginPeek`, `AdoptPeek`, `EndPeek`),
    `TableView.TrickTapped`, and the presenter's `OnTrickTapped` and `EndPeek`.
  - `TableState.HasLastTrick` and `LastTrick` stay, with their tests, though the table no longer
    shows the last trick.
- **The cards "at the avatars" were the peek itself.** It laid the previous trick in all four
  seats' slots, each card toward the seat that played it. An avatar tap never reached the trick
  area, whose catcher covers only the middle of the table.
- **A tap on an avatar does nothing.** Nobody can be muted, so every quick phrase shows, a bot's
  chatter and an online player's phrases alike.
  - Removed: `SeatView.AvatarClicked` and `SetMuted` (the avatar dimmed to 0.45), and the
    presenter's `OnAvatarClicked` (which also hid the seat's call bubble and speech box). Also
    removed: its muted seats, by which `OnEmoteReceived` no longer filters.
  - The avatar keeps its button in the prefab, as removing it would need a rebuild. `SeatView`
    makes the button non-interactable.
- **Checks:**
  - The table simulation taps the trick area on a quiet table once a trick has been won. No card is
    rented, the trick lies as it was, and a tap there closes an open emote picker.
  - Its taps during the deal, during a sweep and under the scorecard now check that no card is
    rented. "Every rented card is in the hand, a fan or the trick" no longer allows four more cards
    for a peek.
  - Its mute checks now check that an avatar takes no tap and that a tap there changes nothing: the
    phrase showing stays, the avatar keeps its shade, and the next phrase shows.
  - A new PlayMode test (TableFlowTests) taps the middle of the table and each opponent's avatar
    through the event system. Each avatar's tap lands on its own seat, nothing dims, and no card
    shows.
  - Suites: EditMode 2160 and PlayMode 128 pass. The table simulation has 0 failures in both modes
    (480 and 465 kinds of check).
- **Out of date:** GDD screen 08's peek at the previous trick and §8's tap to mute, and ADR 027's
  "tap-to-mute ring".
