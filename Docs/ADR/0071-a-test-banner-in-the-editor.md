# ADR 071 - A test banner in the Editor

**Status:** accepted (owner, 2 Nov 2026); off by default since the same day. The owner: "we have banner
ads right? how am i gonna test that how the game gonna look on mobile device?" Then, to the offer of a
test banner in the Editor: "Yes, add it". After trying it: "remove the editor test banner ads from both
game play and mainmenu i will check that on android" — so the switch below starts off.

## Context

The banner ad (ADR 051, 052, 057) is AdMob's own view, and AdMob runs only on phones. In the Editor,
`StandInBannerAds` stood in: it never had a banner, so the table and the main menu made no room for
one. What a player without No Ads sees on a phone could not be seen in the Editor:

- the logo gone from the table and the round line beside Scores;
- the top seat 80 units lower and the trick 40;
- the menu's smaller centre column;
- the banner itself.

That needed a Development Build on a phone.

## Decision

- **A game played in the Editor gets a banner network of its own,** `EditorTestBannerAdNetwork`
  (Infrastructure/Platform, Editor only). It loads at once with no consent and no SDK. Its banner is
  drawn by `EditorTestBannerView`: a white card with a dark "Test Ad" tag and "Editor test banner ·
  320 × 50", drawn over everything as AdMob's view is.
- **The real banner rule runs over it** (`BannerAds`), as on phones:
  - the table and the main menu make room;
  - the banner sits in the table's top-right slot and the menu's bottom-left one;
  - it goes off the screen under every page;
  - buying No Ads (the Editor's stand-in sells it at once) takes it away.
- **It is AdMob's 320 × 50 dp banner at a phone's scale:**
  - In the Device Simulator, the simulated phone's density (dpi / 160).
  - In the Game view, a common phone's for the view's short side: 1080 pixels at 2.625, so a
    1080-pixel-high view draws it 840 × 131 pixels, as a 1080p phone does.
  - The size follows the view as it is resized.
- **A switch:** Callbreak > Ads > Test Banner in Play Mode. It is the Editor's own setting
  (EditorPrefs `Callbreak.EditorTestBanner`), off until it is turned on. It is read as the game
  starts, so it counts from the next Play.
- **The PlayMode tests boot without it,** whatever the switch says (`GameFixture.Boot`'s
  `editorTestBanner`, false by default). Every other test sees the table and the menu as before.
  `EditorTestBannerFlowTests` boots with it.
- Desktop builds and phones are unchanged: the code is Editor-only (`#if UNITY_EDITOR`).

## Consequences

- With the switch on, pressing Play in the Editor shows the game as a player without No Ads sees it
  on a phone, banner included. With it off (the default), the Editor shows the table and the menu
  without a banner, as before.
- Use the Device Simulator for a phone's shape, notch and safe area. Turn the switch off for the game
  without a banner (as with No Ads).
- What a real AdMob banner looks like still needs a Development Build on a phone. AdMob's test
  banner there is safe to tap (Docs/Setup/AdMob.md).
- Tests:
  - `EditorTestBannerTests` (EditMode): the scale in the Game view and the Device Simulator, the
    banner's placing, hiding and release, and the switch;
  - `EditorTestBannerFlowTests` (PlayMode): room and placement on the menu and the table, and that
    the tests boot without it.
