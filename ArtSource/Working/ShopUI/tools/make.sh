#!/usr/bin/env bash
# The recipe: from ChatGPT's originals (ArtSource/Working/ShopUI/chatgpt/*.png) to the finished
# sprites in ArtSource/Working/ShopUI/final. Every number was measured on the mockups (Tools/art
# fitrect/profile/inkbox/probe, kitcontrols radial/rays/line/fit, shopui fitframe/fiticon/fitlayer/
# fitglow); ArtSource/Working/ShopUI/notes.md says where each comes from.
#   make.sh            everything
# Needs: kitcontrols (Tools/ArtPipeline/Kit/Controls) and shopui (this folder), both built outside
# the repo (KC and SU below point at wrappers in the session scratchpad; see notes.md).
# Sprite pixels = mockup pixels x 2.3 (@2x).
set -euo pipefail
cd "$(dirname "$0")/../../../.."           # the repo root
KC=${KC:-kc}
SU=${SU:-shopui}
W=ArtSource/Working/ShopUI
F=$W/final
G=Assets/ArtRefrences/game-screens
S15=$W/attach/S15-card-styles.png            # the NEW 15 (D:\Nepali Callbreak\game-screens\15-card-styles.png)
mkdir -p $F $W/work/{Disc,Plus,Coin,Coins,Play,Drawer,Block,Glow}

# --- cells -------------------------------------------------------------------------------------
$KC cells --in $W/chatgpt/S1_a1.png --def $W/attach/S1_sheet.cells.json --outdir $W/work/S1_a1 --key magenta
$KC crop  --in $W/chatgpt/S1_a1.png --out $W/work/S1_a1/drawer_region.png --box 0,0,670,1024   # the drawer outgrew its cell
$KC cut   --in $W/work/S1_a1/drawer_region.png --out $W/work/S1_a1/Drawer_full.png --key magenta
$SU trimto --in $W/work/S1_a1/Drawer_full.png --out $W/work/S1_a1/Drawer.png --pad 0
$KC cells --in $W/chatgpt/S2_a1.png --def $W/attach/S2_sheet.cells.json --outdir $W/work/S2_a1 --key none
$KC cells --in $W/chatgpt/S3_a1.png --def $W/attach/S3_sheet.cells.json --outdir $W/work/S3_a1 --key magenta
$KC cells --in $W/chatgpt/S4_a1.png --def $W/attach/S4_sheet.cells.json --outdir $W/work/S4_a1 --key magenta
$KC cells --in $W/chatgpt/S5_a1.png --def $W/attach/S5_sheet.cells.json --outdir $W/work/S5_a1 --key magenta

# --- drawer icons (S2): matte on white, fitted on the mockup (IoU 0.965-0.978), @2x, 2 px margin --
for n in Storefront Cards Mountains Table; do
  $SU icomatte --in $W/work/S2_a1/$n.png --out $W/work/S2_a1/${n}_matte.png --ground "#FFFFFF"
done
$SU rendericon --sprite $W/work/S2_a1/Storefront_matte.png --out $F/Icon_Storefront_Brown.png --rect 1531.52,74.75,58.57,60.25 --ink "#4A2C15"
for spec in "Cards:CardsStacked:1518.65,252.78,82.07,82.56" "Mountains:Mountains:1515.61,470.74,90.39,47.83" "Table:TableOval:1513.48,681.55,92.95,60.50"; do
  IFS=: read src name rect <<< "$spec"
  $SU rendericon --sprite $W/work/S2_a1/${src}_matte.png --out $F/Icon_${name}_Brown.png --rect $rect --ink "#4A2C15"
  $SU rendericon --sprite $W/work/S2_a1/${src}_matte.png --out $F/Icon_${name}_Ivory.png --rect $rect --ink "#FAF2E8"
done

# --- selected category disc (S1 cell 2): crimson to 0.922 R, white ring to 0.973 R, tan line ------
$KC rwarp --in $W/work/S1_a1/CircleSelected.png --out $W/work/Disc/sel_1_warp.png --center 200.75,200.25 --size 338 \
    --map "182.74>155.8;196.5>164.4;198.25>169" --outer 169
$KC crimson --in $W/work/Disc/sel_1_warp.png --out $W/work/Disc/sel_2_color.png --gc 26 \
    --ramp "138:#800C10;166:#941215;206:#A0151A" --white "#FEFDFC" --whiteto "#FCF8F2"
$SU ringfix --in $W/work/Disc/sel_2_color.png --out $F/UI_ButtonDisc_Selected.png --center 169,169 \
    --rings "155.8-164.4:#FDFAF6>#F7E8D2;164.4-169:#DFC9BA>#DAC2B1" --keepin 155.8 --outer 169

# --- ivory disc (S5): face, highlight ring 0.956-0.985 R, tan edge line to R ---------------------
$KC rwarp --in $W/work/S5_a1/DiscNormal.png --out $W/work/Disc/nor_1_warp.png --center 372.40,374.90 --size 316 \
    --map "355>151;364.43>155.7;371.76>158" --outer 158
$KC curve --in $W/work/Disc/nor_1_warp.png --out $W/work/Disc/nor_2_color.png --anchors "#F7EDDF>#F6EDE2;#FCF4E5>#F9F1E7;#FEFEFE>#FEFDFC"
$SU ringfix --in $W/work/Disc/nor_2_color.png --out $F/UI_ButtonDisc_Normal.png --center 158,158 \
    --rings "151-155.7:#FEFCF9>#F7EEE3;155.7-158:#DCCBBE>#D3BEAD" --keepin 151 --outer 158

# --- plus button (S1 cell 3): the crimson disc only (the mockup has no white ring), plus kept ------
$KC rwarp --in $W/work/S1_a1/PlusButton.png --out $W/work/Plus/plus_1_warp.png --center 153.10,152.20 --size 114 --map "140.28>57" --outer 57
$KC crimson --in $W/work/Plus/plus_1_warp.png --out $W/work/Plus/plus_2_color.png --gc 36 \
    --ramp "141:#880E10;169:#9E1A1A;238:#CA2024" --white "#FEFEFE" --whiteto "#FFFFFF"
$KC shape --in $W/work/Plus/plus_2_color.png --out $F/UI_ButtonPlus_Normal.png --type ellipse

# --- drawer (S1 cell 1): edges cropped, @2x, blue-channel colour map, exact left corners + rim -----
$KC crop --in $W/work/S1_a1/Drawer.png --out $W/work/Drawer/dr_1_crop.png --box 1,9,573,897
$KC resize --in $W/work/Drawer/dr_1_crop.png --out $W/work/Drawer/dr_2_size.png --w 534 --h 836
$KC gradmap --in $W/work/Drawer/dr_2_size.png --out $W/work/Drawer/dr_3_color.png --channel b \
    --stops "190:#EAD6C5;228:#F9F1E8;240:#FCF6EE;252:#FEFBF6"
$SU leftpanel --in $W/work/Drawer/dr_3_color.png --out $F/UI_Drawer.png --radius 66.7 --rim 2.3 --rimcolor "#FDFBF6" --rimalpha 0.9
$SU shadowsprite --sprite $F/UI_Drawer.png --out $F/UI_Drawer_Shadow.png --sigma 10.5 --dx 0 --dy 0 --color "#2A1A12" --op 0.5 --pad 3

# --- coin piles (S3): colour quantiles put on the mockup's, size from the fit on 22 -------------
$KC curve --in $W/work/S3_a1/Coins20.png --out $W/work/Coins/c20_1_color.png --anchors "#A84700>#A74D02;#F6A31C>#F1A52B;#FEE984>#FEEC8D"
$KC curve --in $W/work/S3_a1/Coins30.png --out $W/work/Coins/c30_1_color.png --anchors "#943500>#8B3500;#F09713>#EB9921;#FEE77F>#FEE983"
$KC curve --in $W/work/S3_a1/Coins50.png --out $W/work/Coins/c50_1_color.png --anchors "#923400>#8D3400;#F09712>#E99A21;#FEE477>#FEE580"
$KC resize --in $W/work/Coins/c20_1_color.png --out $F/Deco_CoinPile_Small.png  --w 312 --h 206
$KC resize --in $W/work/Coins/c30_1_color.png --out $F/Deco_CoinPile_Medium.png --w 388 --h 252
$KC resize --in $W/work/Coins/c50_1_color.png --out $F/Deco_CoinPile_Large.png  --w 436 --h 266
for n in Small Medium Large; do
  $SU shadowsprite --sprite $F/Deco_CoinPile_$n.png --out $F/Deco_CoinPile_${n}_Shadow.png --sigma 5 --dx 0 --dy 6 --color "#7A4A28" --op 0.8
done

# --- coin glow (G1): alpha = 1.6 x brightness^0.6, warm white, 236 mockup px ---------------------
$SU glowsprite --glow $W/chatgpt/G1_a1.png --out $F/Deco_CoinGlow.png --size 236 --k 1.6 --gamma 0.6 --color "#FFFCF2"

# --- coin, play triangle (S4) ------------------------------------------------------------------
$SU trimto --in $W/work/S4_a1/Coin.png --out $W/work/S4_a1/Coin_trim.png --thr 0.5 --pad 0
$KC curve --in $W/work/S4_a1/Coin_trim.png --out $W/work/Coin/coin_1_color.png --anchors "#A85501>#A55C09;#EB9806>#E29D29;#FDD555>#FDD66F"
$KC resize --in $W/work/Coin/coin_1_color.png --out $W/work/Coin/coin_2_size.png --w 132 --h 132
$KC shape --in $W/work/Coin/coin_2_size.png --out $F/Icon_Coin.png --type ellipse
$SU trimto --in $W/work/S4_a1/Play.png --out $W/work/S4_a1/Play_trim.png --thr 0.5 --pad 0
$KC defringe --in $W/work/S4_a1/Play_trim.png --out $W/work/Play/play_0_defringe.png --px 3
$KC curve --in $W/work/Play/play_0_defringe.png --out $W/work/Play/play_1_color.png --anchors "#FCDFB8>#F1D5B9;#FDE7C9>#F6DEC8;#FEF4E1>#FCEFDE"
$KC resize --in $W/work/Play/play_1_color.png --out $F/Icon_Play.png --w 108 --h 120

# --- reward blocks (S4 cells 1-2 give the look): drawn at the widths and colours measured on 22 ---
$SU frame2 --size 61.3,61.3 --pixels 140,140 --radius 26.5 --bands "1.9:#DDA75E;0.9:#FBF0DA;1.4:#ECB872;1.2:#FCF3E4" \
    --fill "#FAF1E7" --out $F/UI_RewardBlock_Normal.png
$SU frame2 --size 61.3,61.3 --pixels 140,140 --radius 24.5 --bands "2.5:#D41212>#BC0A0A;0.8:#FBEADB;1.2:#EDC08C;1.0:#FCF2E6" \
    --fill "#FBF0E3" --out $F/UI_RewardBlock_Selected.png

# --- connector line (a 3 px hairline: drawn at the measured colours, S4 cell 5 as the reference) --
$SU stripes --size 24,12 --rows "2.3:#FFFFFF@0.35;2.3:#DA952B;2.3:#D17D03;2.3:#C67F17;2.3:#FFFFFF@0.3" --out $F/Deco_ConnectorLine.png

# --- the kit's small card, a few levels lighter for the product tiles (optional) ------------------
$KC curve --in Assets/_Project/Art/UI/Panels/UI_Panel_Card.png --out $F/UI_Panel_Card_Light.png \
    --anchors "#F5EBDF>#F8EFE6;#F6ECE0>#F9F1E8;#FFFFFD>#FFFFFD"
echo "done: $(ls $F | wc -l) files in $F"
