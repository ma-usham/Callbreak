# ADR 042 - A black cover under the store's purchase sheet

**Status:** accepted (owner, 7 Oct 2026).

**Context.** The main menu's No Ads button (ADR 035) opens Google Play's purchase sheet. On the
owner's phone the sheet comes up in portrait, and the game turns to portrait behind it, so the
menu shows sideways and squeezed until the sheet closes. Android lets a window like the sheet
choose the screen's orientation; the game cannot keep landscape under it. Earlier the owner was
offered a dark cover behind the sheet. On 7 Oct 2026 the owner asked for it: when the player taps
No Ads, show a black screen with our own loading emblem, the ring turning round the spade, in the
middle.

## Decision

- **The loading curtain gets a black look** (`ILoadingCurtain.CoverDarkAsync` / `UncoverDarkAsync`,
  `LoadingCurtain`):
  - black to the screen's edges, over everything else on the curtain;
  - in the middle of the screen, whichever way the sheet turned it, the loading emblem at the
    loading screen's size: the same ring, turning on unscaled time, round a gold spade.
  - The spade is the gold one from the menu's Play buttons, because the loading screen's black
    spade would not show on black.
  - Like the curtain, it fades in and out (0.25 s) and takes every touch. With Reduce motion
    the ring stands still.
  - The curtain was already above everything, so the black look covers the toast, the dialogs
    and the name question too.
- **The No Ads tap** (`NoAdsOffer.TapAsync`):
  - The game goes black first; the store's sheet opens once the cover is up, so the turn to
    portrait happens behind it.
  - When the store answers (bought, pending, closed, failed), the cover goes before the toast,
    so the toast is seen.
  - The cover goes in every case: a closed sheet, a store that throws, a menu that stops
    waiting.
- **Back to landscape first.** The black cover stays until the screen is landscape again, so the
  menu never shows sideways while the game turns back. It waits at most 1.5 s.
- **Scene changes win.** A scene change already under the curtain needs no black cover. A scene
  change that covers while the black cover is up turns it into the loading screen. Taking the
  black cover away then does nothing, so the scene change keeps its curtain.
- **No new copy:** the cover has no words.

## Consequences

- `LoadingCurtain` builds from `SceneLoading.prefab`, rebuilt with the black look
  (`SceneLoadingCustomizer.BuildDark`). The Boot scene's curtain was swapped with
  `SceneSetup.RebuildBootCurtain`, which changes only that prefab instance.
- `MainMenu.prefab` was not rebuilt: it holds the owner's own edits.
- **Tests.**
  - `NoAdsOfferTests`: the order (cover, sheet, cover gone, toast), and the cover going on a
    closed sheet, a store that throws and a cancelled wait. A tap that opens no sheet covers
    nothing.
  - `NoAdsButtonFlowTests` (PlayMode) wait for the sheet behind the cover and check that the
    cover is gone when the toast shows.
- **Not verified on a phone:** whether the sheet turns the game to portrait on every device, and
  how the cover looks while it does. The Editor is always landscape.
- The Earn Coins videos and the ads between matches (AdMob) are full-screen and landscape, so
  they get no cover.
