#!/usr/bin/env bash
# Checks: every final sprite (and the kit sprites reused around it) composited at its mockup rect and compared with
# the mockup (23 normalised to 1672 wide, and 24). Pictures: check/<name>_<screen>.png = mockup | rebuild | difference
# x4 (shopui scene). Numbers (rms / mean, sRGB levels) go to check/numbers.txt. Words and numbers drawn by the UI are
# left out (--exclude); semi-transparent pieces are composited over a flat ground measured beside them, never over the
# mockup's own drawing of the same piece.
#   check.sh       (after make.sh)
set -uo pipefail
cd "$(dirname "$0")/../../../.."           # the repo root
SU=${SU:-shopui}
PO=${PO:-"node ArtSource/Working/PlayOnline/tools/po.js"}
W=ArtSource/Working/PlayOnline
F=$W/final
C=$W/check
A=Assets/_Project/Art/UI
S24=$W/attach/S24-online-play-stake-100.png
S23=$W/attach/S23n-online-play-stake-50-1672.png
OUT=$C/numbers.txt
: > $OUT

# one check: name screen(24|23) region base layers [exclude]
chk() {
  local name=$1 scr=$2 region=$3 base=$4 layers=$5 excl=${6:-}
  local M=$S24; [ "$scr" = 23 ] && M=$S23
  local args=(--screen $M --region $region --layers "$layers" --base "$base" --zoom 3 --out $C/${name}_$scr.png)
  [ -n "$excl" ] && args+=(--exclude "$excl")
  local res; res=$($SU scene "${args[@]}" | head -1 | sed 's/^region \[[^]]*\]: //')
  printf '%-34s %s  %s\n' "$name" "$scr" "$res" | tee -a $OUT
}

# rects (mockup px) --------------------------------------------------------------------------------------------
CHIP_SH="@nine=121,0,121,0@unit=2.303"                       # the chip shadow, 9-sliced like the chip
grow() { awk -v r="$1" -v g="$2" 'BEGIN{split(r,a,","); printf "%.2f,%.2f,%.2f,%.2f", a[1]-g, a[2]-g, a[3]+2*g, a[4]+2*g}'; }
SEL23=816.75,487.85,195.4,71.2;  NOR23=1027.7,487.9,183.8,71.2
NOR24=815.2,488.1,198.0,71.2;  SEL24=1022.3,488.1,188.0,71.2
INFO=1223.9,490.73,53.04,53.04
BOLTDISC=464.39,263.87,149.57,149.57;  COINDISC=986.35,224.36,151.96,151.96
COINS=1003.95,249.13,119.40,91.25;  BOLT=509.64,292.10,60.0,95.65
POP=1175,557,283,257;  PTR=1225.5,543,50,22;  CLOSE=1415.76,578.07,20,20
ROW1=1200.35,660.81,231.3,6.087;  ROW2=1200.35,705.81,231.3,6.087;  ROW3=1200.35,750.46,231.3,6.087
OUTL=766.45,200.15,594.1,622.9
COIN=$A/Icons/Icon_Coin.png

echo "# rms / mean error against the mockup in the region (sRGB levels); pictures: check/<name>_<screen>.png" | tee -a $OUT

# stake chips (with the chip shadow); the coin and the number are drawn by the UI: left out --------------------------
chk chip_selected 23 810,482,208,90 "#F7ECE1" "$F/UI_StakeChip_Shadow.png@$(grow $SEL23 16.52)$CHIP_SH;$F/UI_StakeChip_Selected.png@$SEL23@nine=83,0,83,0@unit=2.303" "855,494,52,58;915,498,52,50"
chk chip_selected 24 1016,482,200,90 "#F7ECE1" "$F/UI_StakeChip_Shadow.png@$(grow $SEL24 16.52)$CHIP_SH;$F/UI_StakeChip_Selected.png@$SEL24@nine=83,0,83,0@unit=2.303" "1056,494,52,58;1115,498,62,50;1212,482,4,90;1170,555,46,17"
chk chip_normal 24 810,482,208,90 "#F7ECE1" "$F/UI_StakeChip_Shadow.png@$(grow $NOR24 16.52)$CHIP_SH;$F/UI_StakeChip_Normal.png@$NOR24@nine=83,0,83,0@unit=2.303" "855,494,52,58;918,498,48,50"
chk chip_normal 23 1022,482,194,90 "#F7ECE1" "$F/UI_StakeChip_Shadow.png@$(grow $NOR23 16.52)$CHIP_SH;$F/UI_StakeChip_Normal.png@$NOR23@nine=83,0,83,0@unit=2.303" "1060,494,50,58;1117,498,60,50;1170,555,46,17"
# the same chips with the kit coin drawn in (the number still left out)
chk chip_selected_coin 23 810,482,208,90 "#F7ECE1" "$F/UI_StakeChip_Shadow.png@$(grow $SEL23 16.52)$CHIP_SH;$F/UI_StakeChip_Selected.png@$SEL23@nine=83,0,83,0@unit=2.303;$COIN@861.88,502.38,41.25,41.25" "915,498,52,50"
chk chip_normal_coin 24 810,482,208,90 "#F7ECE1" "$F/UI_StakeChip_Shadow.png@$(grow $NOR24 16.52)$CHIP_SH;$F/UI_StakeChip_Normal.png@$NOR24@nine=83,0,83,0@unit=2.303;$COIN@859.88,501.38,42.75,42.75" "918,498,48,50"

# info button ------------------------------------------------------------------------------------------------------
chk info_button 24 1219,486,63,56 "#F8ECE1" "$F/UI_ButtonInfo.png@$INFO"
chk info_button 23 1219,486,63,56 "#F8ECE1" "$F/UI_ButtonInfo.png@$INFO"

# lightning badge: disc shadow, disc, lightning ------------------------------------------------------------------------
chk lightning_badge 24 452,252,174,174 "#F6EBE0" "$F/UI_ButtonDisc_Emblem_Shadow.png@$(grow $BOLTDISC 12.17);$F/UI_ButtonDisc_Emblem.png@$BOLTDISC;$F/Icon_Lightning.png@$BOLT"
chk lightning_badge 23 452,252,174,174 "#F6EBE0" "$F/UI_ButtonDisc_Emblem_Shadow.png@$(grow 464.47,263.92,149.57,149.57 12.17);$F/UI_ButtonDisc_Emblem.png@464.47,263.92,149.57,149.57;$F/Icon_Lightning.png@509.84,292.10,60.0,95.65"
chk lightning_only 24 505,288,70,104 "#F5EADF" "$F/Icon_Lightning.png@$BOLT"

# stake coins badge: disc shadow, disc, coins shadow, coins (the title's top is left out) ------------------------------
chk coins_badge 24 975,213,175,163 "#F9EDE2" "$F/UI_ButtonDisc_Emblem_Shadow.png@$(grow $COINDISC 12.37);$F/UI_ButtonDisc_Emblem.png@$COINDISC;$F/Deco_StakeCoins_Shadow.png@$(grow $COINS 16.96);$F/Deco_StakeCoins.png@$COINS" "975,368,175,8"
chk coins_badge 23 975,213,175,163 "#F9EDE2" "$F/UI_ButtonDisc_Emblem_Shadow.png@$(grow $COINDISC 12.37);$F/UI_ButtonDisc_Emblem.png@$COINDISC;$F/Deco_StakeCoins_Shadow.png@$(grow 1004.33,249.23,119.15,91.06 16.96);$F/Deco_StakeCoins.png@1004.33,249.23,119.15,91.06" "975,368,175,8"

# prize popover: shadows, box, pointer, close, row lines, kit coins (heading, places and amounts left out; the chip
# and the info button above it and the scenery right of the panel are left out) ----------------------------------------
POPL="$F/UI_Popover_Shadow.png@$(grow $POP 29.57)@nine=120,120,120,120@unit=2.3;$F/UI_Popover_Pointer_Shadow.png@1195.93,513.43,109.57,73.04;$F/UI_Popover.png@$POP@nine=52,52,52,52@unit=2.3;$F/UI_Popover_Pointer.png@$PTR;$F/Icon_Close_Brown.png@$CLOSE;$F/UI_PrizeRowLine.png@$ROW1@nine=3,0,3,0;$F/UI_PrizeRowLine.png@$ROW2@nine=3,0,3,0;$F/UI_PrizeRowLine.png@$ROW3@nine=3,0,3,0"
POPC="$COIN@1304.38,624.13,34.25,34.25;$COIN@1304.13,668.28,34.75,34.75;$COIN@1304.38,713.48,34.25,34.25;$COIN@1304.10,758.90,34.60,34.60"
POPX="1160,530,60,30;1220,530,62,13;1195,582,190,30;1205,628,52,160;1352,628,64,160;1372,530,100,300;1160,816,212,14"
chk popover 24 1160,530,300,300 "#F7ECE1" "$POPL;$POPC" "$POPX"
chk popover 23 1160,530,300,300 "#F7ECE1" "$POPL;$POPC" "$POPX"
chk popover_frame_only 24 1160,530,300,300 "#F7ECE1" "$F/UI_Popover.png@$POP@nine=52,52,52,52@unit=2.3;$F/UI_Popover_Pointer.png@$PTR" "1190,572,253,229;$POPX"
chk pointer 24 1222,540,58,26 "#F7ECE1" "$F/UI_Popover.png@$POP@nine=52,52,52,52@unit=2.3;$F/UI_Popover_Pointer.png@$PTR" "1222,540,58,3"
chk close 24 1410,572,32,32 "#F9EFE4" "$F/Icon_Close_Brown.png@$CLOSE"
chk close 23 1410,572,32,32 "#F9EFE4" "$F/Icon_Close_Brown.png@$CLOSE"
for i in 1 2 3; do r=ROW$i; chk row_line_$i 24 1200,$(awk -v r="${!r}" 'BEGIN{split(r,a,","); printf "%d", a[2]-2}'),95,10 "#F8EEE4" "$F/UI_PrizeRowLine.png@${!r}@nine=3,0,3,0"; done

# the red selected outline: four strips along its sides (flat ground) -------------------------------------------------
for scr in 24 23; do for st in "left:762,232,12,300" "right:1352,232,12,240" "top:800,195,160,12" "bottom:800,814,360,12" "corner_tl:762,196,36,36" "corner_tr:1328,196,36,36"; do
  chk outline_${st%%:*} $scr ${st#*:} "#F8EDE2" "$F/UI_Outline_Thin_Red.png@$OUTL@nine=64,64,64,64@unit=2.3"
done; done

# dividers and flourishes (flat ground) -----------------------------------------------------------------------------
for scr in 24 23; do
  chk divider_quick $scr 368,498,344,26 "#F6EBE0" "$F/Deco_ModeTitleDivider_Quick.png@371.5,500.5,337.39,20.87"
  chk divider_stake $scr 884,421,362,26 "#F7ECE1" "$F/Deco_ModeTitleDivider_Stake.png@885.5,423.5,359.13,20"
  chk divider_vertical $scr 734,218,24,586 "#F6EBE0" "$F/Deco_ModeDivider_Vertical.png@737.82,222.17,15.652,577.391"
  chk flourish_right $scr 1018,136,98,24 "#F6EBE0" "$F/Deco_SubtitleFlourish_Right.png@1025.98,142.66,85.22,13.48"
  chk flourish_left $scr 556,136,84,24 "#F6EBE0" "$F/Deco_SubtitleFlourish_Left.png@564.15,142.69,73.91,13.48"
done
# the kit's title divider in the same places, for comparison
chk kit_titledivider_quick 24 368,498,344,26 "#F6EBE0" "$A/Decor/Deco_TitleDivider.png@374.0,504.5,330.4,13.0"
chk kit_titledivider_stake 24 884,421,362,26 "#F7ECE1" "$A/Decor/Deco_TitleDivider.png@897.8,427.3,330.4,13.0"

echo "numbers: $OUT"
