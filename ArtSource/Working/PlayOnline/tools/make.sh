#!/usr/bin/env bash
# The recipe: from ChatGPT's originals (ArtSource/Working/PlayOnline/chatgpt/P1_a1, P2_a1, P3_a1) to the finished
# sprites in ArtSource/Working/PlayOnline/final. Every number was measured on the mockups 23 / 24 (Tools/art
# fitrect/profile/inkbox, kitcontrols radial/rays/line, po.js ringfit/radprof/vprof/hprof2, shopui fitlayer/
# fiticon/colorcmp/fitshadow); ArtSource/Working/PlayOnline/notes.md says where each comes from.
#   make.sh            everything (about a minute)
# Needs: kitcontrols (Tools/ArtPipeline/Kit/Controls) and shopui (ArtSource/Working/ShopUI/tools), both built outside
# the repo (KC and SU point at wrappers in the session scratchpad, see notes.md), and node (po.js, this folder).
# Sprite pixels = mockup pixels x 2.3 (@2x). Mockups (the 1672 x 941 frame): attach/S24-online-play-stake-100.png and
# attach/S23n-online-play-stake-50-1672.png (23 is 1671 wide; resized to 1672 as the designer's spec asks).
set -euo pipefail
cd "$(dirname "$0")/../../../.."           # the repo root
KC=${KC:-kc}
SU=${SU:-shopui}
PO=${PO:-"node ArtSource/Working/PlayOnline/tools/po.js"}
W=ArtSource/Working/PlayOnline
F=$W/final
S24=$W/attach/S24-online-play-stake-100.png
S23=$W/attach/S23n-online-play-stake-50-1672.png
mkdir -p $F $W/work/{Chip,Info,Disc,Coins,Bolt,Close,Lines,Shadow}

[ -f $S23 ] || $KC resize --in $W/attach/S23-online-play-stake-50.png --out $S23 --w 1672 --h 941

# --- cells ---------------------------------------------------------------------------------------------------
$KC cells --in $W/chatgpt/P1_a1.png --def $W/attach/P1_sheet.cells.json --outdir $W/work/P1_a1 --key magenta
$KC cells --in $W/chatgpt/P2_a1.png --def $W/attach/P2_sheet.cells.json --outdir $W/work/P2_a1 --key magenta
$KC cells --in $W/chatgpt/P3_a1.png --def $W/attach/P3_sheet.cells.json --outdir $W/work/P3_a1 --key magenta

# --- stake chips (P1 cells 1-2): ChatGPT's pill at the chips' height (71.2 mockup px), the middle squeezed to the
#     50 chip's width (197.4), the fill matched row by row to the mockup's chip (selected: 23's 50 chip; normal:
#     24's 50 chip), the rim drawn at the band widths and colours measured on both mockups (top > middle > bottom),
#     the 9-slice middle made plain ----------------------------------------------------------------------------
C=$W/work/Chip
$SU trimto --in $W/work/P1_a1/ChipSelected.png --out $C/sel_0_trim.png --thr 0.5 --pad 0
$KC resize --in $C/sel_0_trim.png --out $C/sel_1_size.png --w 479 --h 164
$KC warp --in $C/sel_1_size.png --out $C/sel_2_warp.png --mapx "0>0;83>83;396>371;479>454" --size 454,164
$PO rowmatch --in $C/sel_2_warp.png --out $C/sel_3_rows.png --screen $S23 --rect 815.5,487.6,197.4,71.2 \
    --cols "828-850;966-1000" --rows "493.5-552.5" --smooth 1.0
$PO pillframe --in $C/sel_3_rows.png --out $C/sel_4_frame.png --unit 2.303 \
    --bands "1.0:#CBA070>#BA8548>#A66B33;1.0:#E6C47C>#D69E4B>#BD8130;1.0:#F2D586>#EBBA64>#F5DA8A;1.0:#B86230>#BF6C3E>#C69566;1.0:#AC1210>#9A100E>#821710"
$PO midflat --in $C/sel_4_frame.png --out $F/UI_StakeChip_Selected.png --x0 83 --x1 371

$SU trimto --in $W/work/P1_a1/ChipNeutral.png --out $C/nor_0_trim.png --thr 0.5 --pad 0
$KC resize --in $C/nor_0_trim.png --out $C/nor_1_size.png --w 476 --h 164
$KC warp --in $C/nor_1_size.png --out $C/nor_2_warp.png --mapx "0>0;83>83;393>371;476>454" --size 454,164
$PO rowmatch --in $C/nor_2_warp.png --out $C/nor_3_rows.png --screen $S24 --rect 815.3,487.8,197.3,71.2 \
    --cols "824-852;962-1004" --rows "494-553" --smooth 1.0
$PO pillframe --in $C/nor_3_rows.png --out $C/nor_4_frame.png --unit 2.303 \
    --bands "2.0:#D3B39E>#C9A78F>#C3A285;1.0:#F7EFE6>#F9F0E6>#F0DFCB;1.0:#FDF9F2>#F9F1E8>#F9EFE3" --insety 1.07
$PO midflat --in $C/nor_4_frame.png --out $F/UI_StakeChip_Normal.png --x0 83 --x1 371

# --- info button (P1 cell 3): rings moved to the mockup's radii (the "i" kept at one scale, 0.40), the face colour
#     put on the mockup's, the white inner line, the red-brown ring (its darkest core, angle by angle) and the light
#     outer line painted with 24's colours; ring 22.65-24.95 mockup px -------------------------------------------
I=$W/work/Info
$KC rwarp --in $W/work/P1_a1/Info.png --out $I/info_1_warp.png --center 148.6,144.7 --size 122 \
    --map "80>32;122.5>50.6;129.5>52.8;136.5>56.7;145.5>60.7" --outer 61
$KC curve --in $I/info_1_warp.png --out $I/info_2_color.png --anchors "#FCF7E5>#F8ECE1;#0E0E0C>#0A0D10"
$PO ringpaint --in $I/info_2_color.png --out $F/UI_ButtonInfo.png --center 61,61 \
    --bands "49.45-52.095;52.095-57.385;57.385-60.72" --screen $S24 --mcenter 1250.4,517.2 \
    --mbands "21.0-22.4;22.5-25.0;25.6-26.6" --minbands 1 --maxbands 0,2 --step 5 --keepin 49.45 --outer 60.72

# --- ivory badge disc (P1 cell 4): made round, rings moved to the mockup's radii, face colour put on the mockup's,
#     the highlight and the copper-tan rim painted in 0.4-0.5 px bands with 24's colours (bolt disc) -------------
D=$W/work/Disc
$SU trimto --in $W/work/P1_a1/Disc.png --out $D/disc_0_trim.png --thr 0.5 --pad 0
$KC resize --in $D/disc_0_trim.png --out $D/disc_1_square.png --w 376 --h 376
$KC rwarp --in $D/disc_1_square.png --out $D/disc_2_warp.png --center 188.3,187.25 --size 344 \
    --map "173.5>159.2;179.5>162.8;187.5>171.6" --outer 172
$KC curve --in $D/disc_2_warp.png --out $D/disc_3_color.png --anchors "#FCF7E5>#F6EADF"
$PO ringpaint --in $D/disc_3_color.png --out $F/UI_ButtonDisc_Emblem.png --center 172,172 \
    --bands "159.16-162.84;162.84-163.99;163.99-165.14;165.14-166.29;166.29-167.44;167.44-168.59;168.59-169.74;169.74-170.66;170.66-171.58" \
    --screen $S24 --mcenter 539.17,338.65 \
    --mbands "69.2-70.8;70.8-71.3;71.3-71.8;71.8-72.3;72.3-72.8;72.8-73.3;73.3-73.8;73.8-74.2;74.2-74.6" \
    --step 5 --keepin 159.16 --outer 171.58

# --- stacked coins (P1 cell 5): colour quantiles put on 24's / 23's, fitted on 24 (rms 17.7) -----------------------
Co=$W/work/Coins
$SU trimto --in $W/work/P1_a1/Coins.png --out $Co/coins_0_trim.png --thr 0.5 --pad 0
$KC curve --in $Co/coins_0_trim.png --out $Co/coins_1_color.png --anchors "#973D00>#923D01;#F29D0E>#EA9820;#FDE782>#FDE587"
$KC resize --in $Co/coins_1_color.png --out $F/Deco_StakeCoins.png --w 274 --h 210

# --- lightning (P3 cell 4): fitted by alpha (IoU 0.992 / 0.991), colours put on the mockups' bolt (curve, then the
#     vertical gradient row by row inside the bolt), @2x with a 2 px margin -----------------------------------------
B=$W/work/Bolt
$SU trimto --in $W/work/P3_a1/Bolt.png --out $B/bolt_0_trim.png --thr 0.5 --pad 0
$KC defringe --in $B/bolt_0_trim.png --out $B/bolt_1_defringe.png --px 2
$KC curve --in $B/bolt_1_defringe.png --out $B/bolt_2_color.png --anchors "#670401>#5C030C;#7A0503>#6C0812;#A70605>#8E0A1D"
$PO rowmatch --in $B/bolt_2_color.png --out $B/bolt_3_rows.png --screen $S24 --rect 510.51,292.97,58.42,94.16 \
    --cols "505-575" --masked --erode 1.5 --minsamples 6 --thr 0.98 --smooth 6
$KC resize --in $B/bolt_3_rows.png --out $B/bolt_4_size.png --w 134 --h 216
$KC pad --in $B/bolt_4_size.png --out $F/Icon_Lightning.png --w 138 --h 220

# --- close x (P3 cell 3): ChatGPT's x (IoU 0.986 / 0.965; the kit's Icon_Close: 0.966 / 0.932) in the mockup's
#     brown, one flat ink ----------------------------------------------------------------------------------------
X=$W/work/Close
$SU trimto --in $W/work/P3_a1/Close.png --out $X/close_0_trim.png --thr 0.5 --pad 0
$SU rendericon --sprite $X/close_0_trim.png --out $F/Icon_Close_Brown.png --rect 1417.02,579.16,17.52,17.84 --ink "#573011" --margin 2

# --- dividers: the bronze diamond (P3 cell 1) and lines drawn at the opacity measured along each line -------------
. $W/tools/dividers.sh

# --- the red selected outline of the Stake Match area: a hollow rounded rect, 2.1 px red between a faint warm halo
#     and a white inner line (colours top > sides > bottom from 23 / 24), compact 9-slice like the kit's --------------
$PO roundframe --out $F/UI_Outline_Thin_Red.png --size 62.6,62.6 --radius 27.05 --hollow \
    --bands "1.0:#F7E1C8>#F4DFCB>#FBDECF;2.1:#EC3A2E>#D70B04>#EC4337;1.0:#FEFBF6>#FFFFFF>#FCF3EB"

# --- prize popover (P2 gives the look): the box drawn at the 7 one-pixel bands measured on all four sides of 23 / 24
#     (top > sides > bottom), radius 21.7, fill #F9EFE5; the pointer drawn with a stretch of the box's top edge, the
#     lines meeting in mitred joints --------------------------------------------------------------------------------

BANDS="1:#C8AC8E>#B79579>#AA7744;1:#F6E6C3>#EAC58F>#BC7929;1:#FDF5D4>#F3CD91>#EDC482;1:#EAC27D>#F1CB97>#F9E9C8;1:#C68D40>#D5AC7F>#E8C49A;1:#CBA67A>#D9C3AD>#F8E9D7;1:#FDFCF9>#FDFCF8>#FEF8EE"
$PO roundframe --out $F/UI_Popover.png --size 283,257 --radius 21.7 --bands "$BANDS" --fill "#F9EFE5"
$PO pointer2 --out $F/UI_Popover_Pointer.png --base 30 --height 15 --cut 1 --ext 10 --below 8 --shift 2.5 \
    --bands "0.6:#EDD3B0;1.4:#E5B169;0.5:#EFD1A8;1.0:#FEFAF6" \
    --boxbands "1:#C8AC8E;1:#F6E6C3;1:#FDF5D4;1:#EAC27D;1:#C68D40;1:#CBA67A;1:#FDFCF9" \
    --nearbands "1:#D9B68C;1:#F2DAA8;1:#FCEBB8;1:#DEA751;1:#BD7F35;1:#C2A07B;1:#FDFCF9" --fill "#F9EFE5"

# --- the prize row line: 1.6 px #DAC3AE with light lines above and below, square ends; 9-slice 4,0,4,0 -------------
$PO line --out $F/UI_PrizeRowLine.png --len 229 --thick 1.6 --color "#DAC3AE" --profile "0:1;229:1" \
    --hi "#FFFBF5@0.6" --lo "#FFFBF5@0.6" --hw 0.9 --pad 1.0 --caps butt

# --- shadows: blurred silhouettes, blur / offset / opacity fitted on the mockups (shopui fitshadow) ----------------
Sh=$W/work/Shadow
$SU shadowsprite --sprite $F/UI_StakeChip_Selected.png --out $F/UI_StakeChip_Shadow.png --sigma 4.25 --dx 0 --dy 3.6 --color "#8A5A3A" --op 0.32
$SU shadowsprite --sprite $F/UI_ButtonDisc_Emblem.png --out $F/UI_ButtonDisc_Emblem_Shadow.png --sigma 2.85 --dx -0.3 --dy 3.2 --color "#8A5A3A" --op 0.37
$SU shadowsprite --sprite $F/Deco_StakeCoins.png --out $F/Deco_StakeCoins_Shadow.png --sigma 3.25 --dx 1.25 --dy 7 --color "#7A4A28" --op 0.625
$SU shadowsprite --sprite $F/UI_Popover.png --out $F/UI_Popover_Shadow.png --sigma 7.4 --dx -1.75 --dy 7 --color "#7A5A40" --op 0.55
$KC crop --in $F/UI_Popover_Pointer.png --out $Sh/pointer_triangle.png --box 0,0,115,32
$SU shadowsprite --sprite $Sh/pointer_triangle.png --out $F/UI_Popover_Pointer_Shadow.png --sigma 7.4 --dx -1.75 --dy 7 --color "#7A5A40" --op 0.55

echo "done: $(ls $F | wc -l) files in $F"
