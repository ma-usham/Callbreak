# No Ads button: ChatGPT prompts and verdicts (5 Oct 2026)

The main menu's No Ads button (owner, 5 Oct 2026: "A small No Ads button on the main menu; needs new
art and changes the menu layout"). The button reuses the kit's header disc (`UI/Buttons/UI_ButtonCircle_Normal.png`
and `UI_Shadow_Circle.png`, like the gear); only the glyph is new.

- Chat: one chat in the ChatGPT project "TestProject", opened with "hi" (archived when done):
  <https://chatgpt.com/g/g-p-6abf749240f881918a0877a98eb42245-testproject/c/6ac34d96-df00-83ee-9c16-ad6ed6be59df>
- Attached: `Assets/ArtRefrences/game-screens/23-coinsection.png` (the main menu with the coin wallet).
- Downloads were renamed to the files below (ChatGPT names them after the image's title).

## 1 · The spot: the whole main menu with the button added (`NoAds_MainMenu_a1.png`, 1672 x 941)

Prompt (with 23-coinsection.png attached):

> This is the main menu of my mobile card game "Callbreak" (1672 x 941 px). Please make an edited version
> of this exact image with ONE change only: add a small round "No Ads" button to the top-right header row.
>
> The button:
> - Same size and style as the round settings (gear) button: the same ivory disc with the thin warm gold/tan
>   rim and the same soft drop shadow.
> - Its icon is a "no ads" glyph in the same near-black colour as the gear and shop icons (#231C19): the
>   letters "AD" crossed out by a diagonal line (a prohibition slash, optionally with a circle around the
>   letters). Flat, bold and readable at small size, in the same solid style as the gear and storefront icons.
> - Put it in the top-right row, to the LEFT of the coin pill ("250 +"), with the same small gap as between
>   the other header buttons. If a different spot in the header clearly works better, you may use it, but do
>   not move or resize anything else.
>
> Keep everything else exactly as it is: the avatar and "Namaste, Player", the coin pill, the gear, the thin
> divider and the shop button, the Nepal flag, the CALLBREAK logo and "PLAY TOGETHER", the four cards, all
> three buttons, the tagline, the background, and all text. Same size (1672 x 941) and same composition.

Verdict: accepted as the placement reference. ChatGPT put the disc left of the coin pill, centre
(1054.9, 98.9), rim to rim 83.5 mockup px (the gear's is 92.0), with a ring-and-slash "AD" glyph 55.5 px
across (ink 55.6 x 55.7). The wallet, gear, divider and shop stand exactly where mockup 23 has them
(fitrect within 0.1 px); the rest of the picture is ChatGPT's redraw (logo, cards and scenery differ in
detail) and is not used.

The build follows the brief over ChatGPT where they differ: the disc is the gear's own (92 px), kept from
the wallet by the gap the gear keeps on the wallet's other side (12.9 px), so its centre is (1048.5, 97.0).

## 2 · The glyph alone (`Icon_NoAds_a1.png`, 1254 x 1254)

Prompt (same chat):

> Thank you, that spot works. Now I need the No Ads icon alone, as a clean game sprite:
> - Only the glyph from that button: the bold letters "AD" inside a circle, crossed by one diagonal slash
>   from upper left to lower right (a prohibition sign), with a thin clear gap where the slash crosses the
>   letters so it stays readable when small.
> - One flat solid colour, #231C19 (near-black), with crisp edges. No disc, no rim, no shadow, no gradient,
>   no texture, no glow, and no text other than "AD".
> - Centred on a plain pure white background (#FFFFFF), square canvas, the icon filling about 75% of it.
> - The same visual weight as the gear icon in the reference (thick strokes), so it still reads at about
>   115 x 115 px.

Verdict: accepted, one attempt. Ring 1074 x 1066 px (aspect 1.008), ink #261B15 on #FEFEFE.

## 3 · The sprite (`Assets/_Project/Art/UI/Icons/Icon_NoAds.png`, 134 x 134)

`../tools/make-noads.sh` (single-file `../tools/noads.cs`) makes it from `Icon_NoAds_a1.png`:
- alpha = the drawing's coverage, solved on luminance in linear light (the project renders in Linear);
- the ring 55.5 mockup px across, as on ChatGPT's menu: 127.7 px at @2x (x 2.3), 2-3 px clear margin;
- one flat ink, #0E0B0C: the gear sprite's own (`Icon_Settings.png`), so the two icons read alike side by
  side. #231C19 is what ChatGPT was asked for; the kit's menu icons were moved to the mockup's near-black.

Copy for the handoff: `ArtSource/Exports/Icons/Icon_NoAds.png`.
