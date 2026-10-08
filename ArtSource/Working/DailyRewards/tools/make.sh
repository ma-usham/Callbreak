#!/usr/bin/env bash
# The recipe: from ChatGPT's originals (ArtSource/Working/DailyRewards/chatgpt/*.png) to the finished
# sprites in ArtSource/Working/DailyRewards/final. Every number was measured on mockup 26
# (Tools/art fitrect/profile/probe, kc line/hprof/radial, shopui fitframe/fitlayer/colorcmp/fitshadow);
# ArtSource/Working/DailyRewards/notes.md says where each comes from.
#   make.sh            everything
# Needs: kitcontrols (Tools/ArtPipeline/Kit/Controls), shopui (ArtSource/Working/ShopUI/tools) and drt
# (this folder), all built outside the repo (KC, SU, DRT below point at wrappers in the session
# scratchpad: dotnet build <csproj> -c Release --artifacts-path <scratchpad>/<name>).
# Sprite pixels = mockup pixels x 2.3 (@2x).
set -euo pipefail
cd "$(dirname "$0")/../../../.."           # the repo root
KC=${KC:-kc}
SU=${SU:-shopui}
DRT=${DRT:-drt}
W=ArtSource/Working/DailyRewards
F=$W/final
M=Assets/ArtRefrences/game-screens/26-daily-rewards.png
mkdir -p $F $W/work/{D1_a1,D2_a1,D3_a1,D4_a1,D5_a1,D6_a1,Tile,Coins,Ribbon,Laurel,Dot,Flourish}

# --- cells: each element cropped from the original (a box clear of its neighbours) and keyed ------
cut() { $KC crop --in $W/chatgpt/$1.png --out $W/work/$1/$2_region.png --box $3 >/dev/null
        $KC cut --in $W/work/$1/$2_region.png --out $W/work/$1/$2.png --key ${4:-magenta} >/dev/null; }
cut D1_a1 TileNormal 190,10,490,445
cut D1_a1 TileToday 855,10,495,445          # the look of the today tile (reference only, see below)
cut D1_a1 TileBonus 265,470,1005,500
cut D2_a1 Coins1 70,70,230,230
cut D2_a1 Coins2 375,80,290,230
cut D2_a1 Coins3 740,70,310,240
cut D2_a1 Coins4 1125,60,355,255
cut D2_a1 Coins5 230,365,410,260
cut D2_a1 Coins6 815,360,465,285
cut D2_a1 Coins7 445,655,640,320
cut D3_a1 Ribbon 160,660,1215,250
cut D5_a1 Laurel 150,20,640,900
cut D5_a1 Dot 780,230,420,420
$KC crop --in $W/chatgpt/D4_a1.png --out $W/work/D4_a1/gift_region.png --box 1180,360,320,290 >/dev/null

# --- day tiles: ChatGPT's inside (fill texture, peach corner motifs) re-toned onto the mockup's fill,
#     ChatGPT's own outline replaced, the frame drawn at the widths and colours measured on 26 ---------
T=$W/work/Tile
# the tiles' true boxes inside the keyed cells (the key left a faint fringe of 1-5 px)
$KC crop --in $W/work/D1_a1/TileNormal.png --out $T/n_0_box.png --box 2,0,462,416 >/dev/null
$KC crop --in $W/work/D1_a1/TileBonus.png  --out $T/b_0_box.png --box 2,1,985,476 >/dev/null

# normal tile = Day 2 [601.2, 254.2, 231.55, 207.95], radius 17.7, tan line 1.8 #E1C7B3, white 1.0
EXN="680,268,78,38;645,316,137,98;638,404,160,46"          # "Day 2", the pile and its shadow, "+15 coins"
$KC resize --in $T/n_0_box.png --out $T/n_1_size.png --w 533 --h 478 >/dev/null
$DRT overframe --in $T/n_1_size.png --out $T/n_2_clean.png --size 231.55,207.95 --radius 17.7 --clean 5.5
$DRT retone --in $T/n_2_clean.png --out $T/n_3_retone.png --screen $M --rect 601.2,254.2,231.55,207.95 --radius 17.7 \
    --inset 3.5 --exclude "$EXN" --gain 1,0.62,0.56 --deg 2
$DRT overframe --in $T/n_3_retone.png --out $F/UI_DayTile_Normal.png --size 231.55,207.95 --radius 17.7 \
    --bands "1.8:#E1C7B3;1.0:#FBF4EB"

# bonus tile = Day 7 [843.85, 473.65, 474.30, 227.50], radius 20.7, gold 0.9 + 2.4, pale 1.0, white 1.0
EXB="1030,486,100,44;922,520,58,108;1183,520,58,108;958,522,240,98;942,604,280,64;995,654,175,44"
$KC resize --in $T/b_0_box.png --out $T/b_1_size.png --w 1091 --h 523 >/dev/null
$DRT overframe --in $T/b_1_size.png --out $T/b_2_clean.png --size 474.3,227.5 --radius 20.7 --clean 7
$DRT retone --in $T/b_2_clean.png --out $T/b_3_retone.png --screen $M --rect 843.85,473.65,474.30,227.50 --radius 20.7 \
    --inset 5 --exclude "$EXB" --gain 1,0.58,0.57 --deg 2
$DRT overframe --in $T/b_3_retone.png --out $F/UI_DayTile_Bonus.png --size 474.3,227.5 --radius 20.7 \
    --bands "0.9:#DDA866>#D2832A;2.4:#F0BA60>#E39530;1.0:#F5D6A0>#F2CC8E;1.0:#FDF3DF"

# today-overlay: a light layer that turns the normal tile into the Day 1 look (Day 1's warmer inside:
# the normal tile re-toned onto Day 1's fill on 26, so only the smooth fill difference is solved; the
# alpha is capped at 0.3 so a tile under it - also the wide bonus tile - keeps its own look), then the
# crimson keyline with a white hair on both sides. The keyline's centre line on 26 is [357.9, 255.2]-
# [589.06, 461.4]: the overlay's box is the tile's grown by 2.105 mockup px left/right and 1.425
# top/bottom (542 x 485 px), the crimson centred 2.3 in. (ChatGPT's today tile, D1 cell 2, gave the look;
# its own inside is not used: solving against it made a noisy 0.7-alpha layer.)
EXT="430,270,88,42;383,288,182,137;398,404,165,46"         # "Day 1", the coin and its light, "+10 coins"
$DRT retone --in $F/UI_DayTile_Normal.png --out $T/t_4_shift.png --screen $M --rect 357.705,254.325,231.55,207.95 --radius 17.7     --inset 4.5 --exclude "$EXT" --gain 1,1,1 --deg 2
$DRT solveov --base $F/UI_DayTile_Normal.png --target $T/t_4_shift.png --out $T/o_1_solve.png --smooth 6 --cap 0.3
$DRT place --in $T/o_1_solve.png --out $T/o_2_place.png --canvas 542,485 --at 4.84,3.28
$DRT overframe --in $T/o_2_place.png --out $F/UI_DayTile_TodayFrame.png --size 235.652,210.870 --radius 19.5 \
    --bands "1.05:#FFFDF9;2.5:#E01A10>#EC2008/#B80606;1.0:#FEFBF6"

# --- coins of each day (D2): colour quantiles put on 26's, the size from the fit on 26 ------------------
C=$W/work/Coins
$KC curve --in $W/work/D2_a1/Coins1.png --out $C/c1_1_color.png --anchors "#A14503>#A95207;#F7AA2F>#F3AB3B;#FDF398>#FEEE99" >/dev/null
$KC curve --in $W/work/D2_a1/Coins2.png --out $C/c2_1_color.png --anchors "#9B4401>#9F5006;#F0A327>#EBA437;#FDEA8F>#FDE691" >/dev/null
$KC curve --in $W/work/D2_a1/Coins3.png --out $C/c3_1_color.png --anchors "#9E4501>#A35306;#EFA024>#EAA135;#FDEB8E>#FDE692" >/dev/null
$KC curve --in $W/work/D2_a1/Coins4.png --out $C/c4_1_color.png --anchors "#9D4401>#A14F03;#EE9E23>#EAA135;#FDE990>#FEE594" >/dev/null
$KC curve --in $W/work/D2_a1/Coins5.png --out $C/c5_1_color.png --anchors "#973E00>#954602;#EC9D22>#E79D34;#FDE88B>#FDE290" >/dev/null
$KC curve --in $W/work/D2_a1/Coins6.png --out $C/c6_1_color.png --anchors "#984001>#974603;#EDA126>#E7A038;#FDE686>#FDE394" >/dev/null
$KC curve --in $W/work/D2_a1/Coins7.png --out $C/c7_1_color.png --anchors "#9C4401>#9D4C02;#F3AC2D>#EEAB40;#FDEB91>#FDE89C" >/dev/null
for s in 1:156:160 2:216:166 3:240:180 4:274:192 5:316:194 6:360:216 7:480:242; do
  IFS=: read n w h <<< "$s"
  $KC resize --in $C/c${n}_1_color.png --out $F/Deco_DayCoins_$n.png --w $w --h $h >/dev/null
  # contact shadow: blurred silhouette, sigma 6.5 mockup px, 1 right, 4 down, opacity 0.8 (fitshadow on Day 2 and Day 5)
  $SU shadowsprite --sprite $F/Deco_DayCoins_$n.png --out $F/Deco_DayCoins_${n}_Shadow.png --sigma 6.5 --dx 1 --dy 4 --color "#7A4A28" --op 0.8 >/dev/null
done

# --- ribbon (D3 cell 2): colour put on 26's, fitted 4.5 % flatter than drawn -----------------------------
$KC curve --in $W/work/D3_a1/Ribbon.png --out $W/work/Ribbon/rb_1_color.png --anchors "#AF6E11>#B06E1F;#DA9B37>#D69A49;#FBD97B>#F9E5B6" >/dev/null
$KC resize --in $W/work/Ribbon/rb_1_color.png --out $F/Deco_Ribbon_Gold.png --w 584 --h 105 >/dev/null

# --- laurel (D5 cell 1, the second attempt): colour put on 26's, fitted 10 % narrower than drawn ---------
$KC curve --in $W/work/D5_a1/Laurel.png --out $W/work/Laurel/la_1_color.png --anchors "#E1A645>#DF9F48;#F1BB5E>#F1BA69;#FCE3BA>#FDEFD5" >/dev/null
$KC resize --in $W/work/Laurel/la_1_color.png --out $F/Deco_Laurel.png --w 86 --h 222 >/dev/null

# --- alert dot (D5 cell 2): ChatGPT's disc and ring (disc 0.775 R) at the ready dot's size, 74 px --------
$KC rwarp --in $W/work/D5_a1/Dot.png --out $W/work/Dot/dot_1_warp.png --center 124.5,123 --size 74 --map "122.21>37" --outer 37 >/dev/null
$KC shape --in $W/work/Dot/dot_1_warp.png --out $F/UI_Dot_Alert.png --type ellipse >/dev/null

# --- gift icon (D4 cell 4): matte on white, the ink box at the settings gear's scale (sheet 2.0 = sprite 1.0)
$SU icomatte --in $W/work/D4_a1/gift_region.png --out $W/work/D4_a1/Gift_matte.png --ground "#FFFFFF" >/dev/null
$SU trimto --in $W/work/D4_a1/Gift_matte.png --out $W/work/D4_a1/Gift_matte_trim.png --thr 0.5 --pad 0 >/dev/null
$SU rendericon --sprite $W/work/D4_a1/Gift_matte_trim.png --out $F/Icon_DailyReward.png --rect 0,0,48.48,51.52 --ink "#0E0B0C" >/dev/null

# --- heading flourish (D6): the star's brown put on 26's ------------------------------------------------
$KC crop --in $W/chatgpt/D6_a1.png --out $W/work/D6_a1/Flourish_region.png --box 150,330,1250,350 >/dev/null
$KC cut --in $W/work/D6_a1/Flourish_region.png --out $W/work/D6_a1/Flourish.png --key magenta >/dev/null
$KC curve --in $W/work/D6_a1/Flourish.png --out $W/work/Flourish/fl_1_color.png --anchors "#8C632C>#96683D" >/dev/null
$KC resize --in $W/work/Flourish/fl_1_color.png --out $F/Deco_HeadingFlourish.png --w 312 --h 49 >/dev/null
echo "done: $(ls $F | wc -l) files in $F"
