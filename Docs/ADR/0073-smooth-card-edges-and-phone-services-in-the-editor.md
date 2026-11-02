# ADR 073 - Smooth edges on turned cards; AdMob's test ads and the festival offers in the Editor

**Status:** accepted (owner, 2 Nov 2026):

> cant we check the tes banner ads in unity editor from ad mob ?? not just banner every other ad like
> interstitial rewarded the test ad enable so that the test ad will appear in editor as well and
> disable that during production release. and also look at his iamge the card taht are straight are ok
> like heart of 10 is clean but the card that are tilkted has rough edges why? how can i solve that?

Then: "also show offer gift panel in editor screen as well."

This replaces ADR 071's drawn test banner.

## Context

- **Rough edges on turned cards.** Every card picture was opaque right to its edge (cut_decks.py):
  - The card's straight edges were the edges of the quad it is drawn on.
  - A quad's edge is drawn without anti-aliasing (the UI has no MSAA), so a card turned 8° in the
    trick, or a few degrees along the hand's arc, showed a staircase.
  - An upright card's edges lie on the pixel grid, so it looked clean.
  - A phone's denser screen makes the steps smaller, not gone.
- **Ads in the Editor.** The Google Mobile Ads plugin (11.5.0) ships its own Editor client:
  - placeholder banners, interstitials, rewarded and app open ads;
  - a placeholder consent form.

  The game never called AdMob in the Editor (ProjectLifetimeScope registered stand-ins there), so
  none of it showed. ADR 071's drawn banner was the only ad, and the owner preferred to check the
  banner on Android.
- **Festival offers in the Editor.** Remote Config was read only on Android (ADR 067), so the offer's
  gift panel never opened in the Editor. Firebase's desktop SDK can read Remote Config in the Editor
  (`StreamingAssets/google-services-desktop.json`).

## Decision

- **Every card picture has a see-through frame:** `EDGE` = 2 px round the unchanged 278 × 386
  card, a 282 × 390 picture.
  - cut_decks.py writes it. The cut is otherwise the same: re-cut from the same Figma sheets, all 159
    pictures matched the old ones pixel for pixel.
  - The table's card layers (face, base, subdued overlay) reach `TableCardBuilder.ArtEdge` texels
    past the card, and the glow that much further, so the card keeps its size.
  - The texture's filtering now draws the card's edge, smooth at any angle. The quad's own edge lies
    in the see-through frame.
  - The silhouette and the glow are drawn again from the framed back.
  - No shader is needed. The pictures still import uncompressed with mip maps (ADR 048).
- **A game played in the Editor uses AdMob,** exactly as a development build does: Google's test
  units, whose ads the plugin draws as its placeholders.
  - That covers the banners on the table and the menu, the ads between matches, Earn Coins'
    rewarded ads and the app open ads, behind the plugin's placeholder consent form.
  - Release builds are unchanged: the owner's ad units on phones (`Debug.isDebugBuild` is false
    there).
- **A game played in the Editor reads the festival offers from Firebase Remote Config,** as Android
  does, so the offer's gift panel opens on the bare main menu while an offer is live.
- **Switches:** Callbreak > Editor Play > AdMob Test Ads and > Festival Offers. Each is the Editor's
  own setting (EditorPrefs), on until turned off, and counts from the next Play.
- **The PlayMode tests boot with the stand-ins,** whatever the switches say (`GameFixture.Boot`'s
  `editorPhoneServices`). No test touches an ad network or Remote Config.
- **ADR 071's drawn banner is gone:** `EditorTestBannerAdNetwork`, its view and its tests.

## Consequences

- Turned cards have smooth edges in the trick, along the hand's arc and in the menu's card fan.
- The main menu's fan and the shop's deck tiles draw the framed pictures in the same boxes, so their
  cards are about 1.4% smaller there. The table's cards keep their size.
- In the Editor, Play shows Google's placeholder ads where a phone shows AdMob's, and the Tihar offer
  while it is live. With no network, or Firebase unavailable, there is simply no offer.
- Not verified in the Editor at the owner's request (they test it themselves):
  - the plugin's placeholder sizes against the table's banner slot;
  - the placeholder consent form's behaviour on each Play;
  - Firebase Remote Config on the desktop SDK.

  The code compiles.
- Tests:
  - `DeckCardArtTests` checks the see-through frame round every card;
  - `TrickFitTests` reads the corner index inside the frame;
  - `EditorPhoneServicesTests` checks the switches.
