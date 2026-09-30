# Bidding and match menu — current gameplay board

Date: 2026-09-30
Mode: built-in image editor (`image_gen`).

## Saved outputs

- `07-bidding.png`: latest scenic board and bottom-edge 13-card hand; original bidding choices and Confirm 4; opponent Waiting labels; 20-second local avatar-border countdown.
- `13-match-menu.png`: latest gameplay underlay with bottom-edge hand and lower-left player badge; original Game Menu controls and offline-paused scrim. Countdown is frozen in the intended paused state.

Both use `08-gameplay.png` as their scene/camera/layout source. Their prior versions are preserved at `../archive/gameplay/07-bidding-before-board-update.png` and `../archive/gameplay/13-match-menu-before-board-update.png` and supplied their screen-specific controls. All three active match screens now use the updated board; the gallery loads the replaced files at their existing paths.

## Bidding — exact prompt

Use case: precise-object-edit.
Create the updated BIDDING screen from two images.
IMAGE 1 is the CURRENT GAMEPLAY BASE and authoritative camera, table, scenery, layout and card positioning.
IMAGE 2 is the OLD BIDDING screen and supplies ONLY its bidding controls, text and bidding hand composition. Do not copy image 2's small floating board or detached hand.

Keep image 1's entire broad close-view ivory textile tabletop, gold/carved wood rim, daylight Nepal temple/mountain/lake panorama, top HUD and opponent positions. The table must have precisely the same camera, scale, outline and near rim as image 1. Keep the Home/CALLBREAK logo, ROUND 1/5, Trump spade, Scores and Settings. Keep Asha at far center, Bikash at left, Suman at right with their silhouette avatars and red card-back fans, but change each status from Bid/Won to 'Waiting', since bidding is not finished.

Replace the central three played cards with the bidding panel from image 2, adapted neatly into the open tabletop area. No played cards remain. Reproduce its ivory rounded panel, subtle gold trim, pale woven corner ornament, crimson serif title 'Make your call', subtitle 'How many tricks will you win?', a single row of circular bids 1 2 3 4 5 6 7 8 with 4 selected in crimson, and the broad crimson primary button 'Confirm 4'. Preserve the exact controls and readable original type hierarchy. Position the panel around x530–1140, y310–620 on a 1672x941 canvas, comfortably above the local hand. Use subtle woven geometry at the button edges. Do not retain the old separate 20s label beneath the button.

The local hand must EMERGE FROM THE BOTTOM SCREEN EDGE exactly as in image 1, with every card's lower edge cropped beyond the canvas. Keep a gentle shallow fan, rank/suit indices all readable, close held-card appearance, and no band of tabletop beneath the cards. Exactly THIRTEEN bidding cards, from left to right, matching image 2:
A♠ K♠ J♠ 9♠ 4♠ A♥ J♥ 8♥ K♦ 10♦ 5♦ Q♣ 7♣.
Use image 1's ivory/red/black card styling and large readable faces. All hand cards are neutral, fully readable and equally available: no selected-card glow and no dimmed 8♥ during bidding.

Keep the local You avatar/name badge at LOWER LEFT beside the hand, as in image 1. Show 'Choosing bid' rather than a won-tricks total. Put the 20-second bidding timer as an orange-gold progress ring around this avatar, with '20' integrated at its lower border. There is no floating countdown bar. Keep the chat button above the badge and card-options control lower right clear of the cards.
No Your turn/Beat Q banner. Preserve the original scenic art, polished UI, colors and image dimensions of image 1. The result should visibly belong to the same game scene as the latest gameplay, with only bidding-state UI and its 13-card hand substituted.

## Match menu — exact prompt

Use case: precise-object-edit.
Create the updated MATCH MENU screen using two inputs.
IMAGE 1 is the CURRENT GAMEPLAY SCREEN. It is the exact base scene to preserve.
IMAGE 2 is the OLD MATCH MENU SCREEN. Use ONLY its central Game Menu dialog design, controls and text.

Place the same Game Menu dialog from image 2 over image 1. Keep image 1's updated broad foreground ivory/gold/wood table, exact camera and scene scale, daytime Nepal panorama, player seats, logo/HUD and controls. Most importantly, preserve all THIRTEEN local hand cards emerging from the bottom screen edge, every lower card edge clipped by the viewport, and the local You badge with orange avatar-border timer '18' at LOWER LEFT beside the hand. Keep the relocated chat button above the badge. Do not reintroduce image 2's floating upright small board, detached eight-card hand, or old distant camera.

Apply a uniform translucent dark scrim over the whole gameplay background, including hand and HUD, to indicate an offline paused menu. The modern background should remain clearly recognizable, and the dialog stays bright. Keep the existing central trick in the background, naturally occluded by the dialog. Do not add a turn-suggestion banner.

Faithfully reproduce the old menu dialog, centered and roughly 570px wide by 510px tall, around x550–1120, y190–700 on the 1672x941 canvas, leaving the lower hand visible:
- Warm ivory textile surface, rounded antique-gold outlined frame and restrained geometric corner ornament.
- 'Game Menu' in crimson serif at top, black close X at upper right.
- Small status pill: 'Offline match · Paused'.
- Primary crimson button with subtle woven edge detail: 'Resume Game'.
- Ivory bordered button with clipboard icon: 'Scorecard'.
- 'Sound' row with speaker icon and red toggle ON.
- Lower 'Leave Match' action with red exit icon.
- Bottom helper: 'Your match is paused.'
Preserve exact spelling, controls, icon style, button hierarchy and spacing from image 2.

The underlay retains image 1's 13-card order A♠ K♠ 9♠ 6♠ 2♠ A♥ 8♥ 4♥ K♦ 5♦ 2♦ 7♣ 3♣, the selected A♥ gold highlight and faded 8♥, and the same player names and bid/won values. Do not change any underlying game state. The avatar countdown is frozen while this OFFLINE menu is open.
Output a polished landscape screen, same dimensions, visually an overlay of the exact latest gameplay board rather than an independently redesigned scene.

