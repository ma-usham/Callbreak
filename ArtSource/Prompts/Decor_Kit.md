# Decor kit (decorations, avatars, backgrounds)

Designer: Decor. Brief: `Docs/Tasks/UI-Art-ChatGPT-Brief.md`. Handbook prompts BG-2, UI-6, UI-7, IC-2.
Chat (the Decor designer's only chat, ChatGPT project "CardGame", auto-titled "Image outpainting
correction"; model setting left as the owner had it):
https://chatgpt.com/g/g-p-6abca6cae2308191809b0ea7bd175f62-cardgame/c/6abce176-12b8-83e8-a20a-bbc387de83f5
The same chat holds the wide menu background (`ArtSource/Prompts/Menu_Wide.md`). It was already
in use before the "first message is `hi`" rule, so no `hi` was sent; the Decor work starts at the
setup message below.

Files: attachments `ArtSource/Working/Kit/Decor/attach/`, ChatGPT originals
`ArtSource/Working/Kit/Decor/chatgpt/<Asset>_aN.png`, fitted sprites and check sheets
`ArtSource/Working/Kit/Decor/fit/`, scripts `Tools/ArtPipeline/Kit/Decor/`, finals
`Assets/_Project/Art/UI/Decor/` (copies in `ArtSource/Exports/Decor/`), import settings
`ArtSource/Kit/Decor.json`, swap map `ArtSource/Kit/Decor.map.csv`.

How every asset is checked (`Tools/ArtPipeline/Kit/Decor/fit-decor.ps1`): the cut-out is laid on
its mockup at the measured rectangle over the panel ivory (or over a UI-free plate for pieces on
scenery), and `fit/<Asset>_check.png` shows mockup | sprite | difference ×4 at up to 6×;
`fit/<Asset>_alpha.png` shows it on black and on white. Colour is matched to the mockup on the
sprite's opaque pixels (per-channel gain and offset) before export. Export size = mockup pixels
× 2.3, rounded up to even; ppu 200.

## Setup (P-0, sent once with nine screens attached)

Attached: 09-round-results, 10-match-results, 16-scorecard, 12-how-to-play, 05-matchmaking,
08-gameplay, 04-private-lobby, 14-profile, 01-play-with-friends (ChatGPT renamed them with a
`(1)`… suffix because the project already holds files with those names). Reply: "Ready".

```
New job in this same chat; the background work above is finished and approved. From here on you help me extract the small decorative art of "Callbreak - Play Together", a Nepali card game for phones in landscape. The reference images are the approved design; reproduce them.

Style (never change it): warm ivory parchment panels; deep crimson #861018 with a satin gradient and a thin antique-gold #B8914F rim; warm brown line ornaments with small diamonds; painted gold for the trophy and the crown; black silhouette icons #231C19; painterly Nepal scenery behind everything.

Rules for every image:
1. Match the reference exactly: shape, colours, ornament, lighting, proportions. Do not redesign, restyle or "improve".
2. Never draw letters or numbers unless I ask for the logo.
3. One asset per image unless I send a sheet. Front-on, no perspective, no mockup frame, no background scene, no drop shadow and no glow unless I ask for one.
4. Give a real transparent PNG. If you cannot, use a flat pure-magenta (#FF00FF) background instead; never a drawn checkerboard and never white.
5. Fill the image with the asset, leaving a small margin, as large and sharp as you can.
6. If my request is ambiguous, ask before generating.

I have attached nine approved screens for context: 09 round results, 10 match results, 16 scorecard, 12 how to play, 05 matchmaking, 08 gameplay, 04 private lobby, 14 profile and 01 play with friends. With each request I will attach an enlarged close-up of the one element I want; the close-up is what you copy. Do not generate anything yet. Reply "Ready" and list the attachments you can see.
```

## 1 · Deco_SpadeMedal (UI-7) — also the seal of 16

Attachment: `crop_09_medal.png` (09, rect 772,186,128,112 at 6×).

```
Asset 1: Deco_SpadeMedal.

Extract only the small round crimson medal with the ivory spade from the attached close-up (crop_09_medal.png). It sits under the heading on the Round Results screen (09), and the same medal is on the Scorecard screen (16).

Output it alone, centred, front-on, as large as fits, on a transparent background. Keep exactly: the thin gold rim with its soft highlight, the satin crimson disc with its gentle top-to-bottom shading, and the cream spade with its soft shading, all in the same proportions. It must be a perfect circle.

Add nothing: no lines or diamonds beside it, no text, no sparkles, no glow and no shadow.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Deco_SpadeMedal_a1.png` | 1254 × 1254, real alpha | Accepted. Disc, rim and spade as in the mockup; came back 0.6% out of round (1110 × 1103) and was made exactly round. Crimson a little too red and bright; corrected against the mockup (r ×1.04 −15, g ×0.96 +3, b ×1.07 −1). |

Export: `UI/Decor/Deco_SpadeMedal.png`, 180 × 180, no 9-slice. Mockup rect 09: 796.6, 203, 78.2 × 78
(edges measured on the row through its centre: 796.9 and 875.0). On 16 the same sprite is the
seal at 796.0, 217.0, 79.2 × 78.7 (checked: `fit/zz_seal16_with_medal_check.png`, error 10.0), so
`Deco_Seal_Spade` is not a separate file.
Still differs: on 09 the mockup's gold rim is thicker (about 4.2 px at the sides, 6 px top and
bottom) than ChatGPT's (about 3.4 px all round); on 16 the mockup's rim is as thin as ChatGPT's.
The soft shadow under it stays the shared `UI/Buttons/UI_Shadow_Circle.png`.

## 2 · Deco_Trophy (UI-7)

Attachment: `crop_10_trophy.png` (10, rect 650,0,400,146 at 4×).

```
Approved: the medal is right. Match this exact style for the next assets in this chat.

Asset 2: Deco_Trophy.

Extract the gold trophy cup with its two laurel branches from the attached close-up (crop_10_trophy.png; top centre of the Match Results screen, 10). Output the cup and the two branches alone, arranged exactly as in the close-up (one branch on each side of the foot, curving up and outward, not touching the cup), centred, front-on, as large as fits, on a transparent background.

Keep exactly: the polished gold with its bright vertical highlight, the dark bronze spade on the front of the cup, the two curled handles, the stepped foot, and the olive-gold leaves on their thin darker stems, all in the same proportions.

Do not include the small four-point sparkles, the soft glow or the faint light rays behind the cup. No shadow, no text.
```

| Attempt | File | Size | Result |
| --- | --- | --- | --- |
| a1 | `Deco_Trophy_a1.png` | 2075 × 758, real alpha | Accepted. Cup, handles, spade, foot and both branches as in the mockup, clean alpha on black and white. Gold slightly dark and saturated; corrected against the mockup (r ×0.90 +23, g ×0.93 +15, b ×0.98 +9). |

Export: `UI/Decor/Deco_Trophy.png`, 500 × 278, no 9-slice. Mockup rect 10 (cup and branches, fitted
over the UI-free plate): 728.1, 18.0, 217.0 × 120.2.
Still differs: ChatGPT's laurel branches are a little fuller (about eleven leaves each against
eight or nine) and reach about 4 px higher; the cup is about 2 px wider. The three sparkles are
the separate `Deco_TrophySparkle`. The very faint glow and the pale yellow rays behind the cup
(2–10 levels over the haze) are not in this sprite and not rebuilt: see the map.
