#!/usr/bin/env bash
# The whole Play Online screen rebuilt from sprites (the kit's and this batch's) at their mockup rects, over the
# mockup's scenery, for looking at: check/overview_<screen>.png = mockup | rebuild | difference x4. Words and numbers
# are drawn by the UI, so they show up in the difference. 24 = 100 selected, 23 = 50 selected.
set -uo pipefail
cd "$(dirname "$0")/../../../.."
SU=${SU:-shopui}
W=ArtSource/Working/PlayOnline
F=$W/final
A=Assets/_Project/Art/UI
S24=$W/attach/S24-online-play-stake-100.png
S23=$W/attach/S23n-online-play-stake-50-1672.png
COIN=$A/Icons/Icon_Coin.png
grow() { awk -v r="$1" -v g="$2" 'BEGIN{split(r,a,","); printf "%.2f,%.2f,%.2f,%.2f", a[1]-g, a[2]-g, a[3]+2*g, a[4]+2*g}'; }

common() {
  local L=""
  # the header: the flourishes' ground cleared, the back button and the wallet (kit)
  L+="$W/work/solid_F6EBE0.png@556,138,84,22;$W/work/solid_F6EBE0.png@1018,138,98,22;"
  L+="$A/Buttons/UI_ButtonCircle_Normal.png@38.42,29.3,96.5,96.5;$A/Icons/Icon_Back.png@66.52,58.39,39.86,38.02;"
  L+="$A/Buttons/UI_ButtonSecondary_Normal.png@1436.1,54.2,190.0,67.9@nine=117,0,117,0@unit=3.122;$COIN@1458.30,57.30,58.40,58.40;"
  L+="$F/Deco_SubtitleFlourish_Left.png@564.15,142.69,73.91,13.48;$F/Deco_SubtitleFlourish_Right.png@1025.98,142.66,85.22,13.48;"
  # the panel (kit), the red outline, the dividers
  L+="$A/Panels/UI_Panel_Large.png@305.9,187.1,1064.9,650.8@nine=400,370,400,260@unit=2.3;"
  L+="$F/UI_Outline_Thin_Red.png@766.45,200.15,594.1,622.9@nine=64,64,64,64@unit=2.3;"
  L+="$F/Deco_ModeDivider_Vertical.png@737.82,222.17,15.652,577.391;"
  L+="$F/Deco_ModeTitleDivider_Quick.png@371.5,500.5,337.39,20.87;$F/Deco_ModeTitleDivider_Stake.png@885.5,423.5,359.13,20;"
  # the two badges
  L+="$F/UI_ButtonDisc_Emblem_Shadow.png@$(grow 464.39,263.87,149.57,149.57 12.17);$F/UI_ButtonDisc_Emblem.png@464.39,263.87,149.57,149.57;$F/Icon_Lightning.png@509.64,292.10,60.0,95.65;"
  L+="$F/UI_ButtonDisc_Emblem_Shadow.png@$(grow 986.35,224.36,151.96,151.96 12.37);$F/UI_ButtonDisc_Emblem.png@986.35,224.36,151.96,151.96;"
  L+="$F/Deco_StakeCoins_Shadow.png@$(grow 1003.95,249.13,119.40,91.25 16.96);$F/Deco_StakeCoins.png@1003.95,249.13,119.40,91.25;"
  # the play buttons (kit) and their chevrons
  L+="$A/Buttons/UI_ButtonPrimary_Normal.png@344.5,692.5,371.5,91@nine=202,0,202,0@unit=3.582;$A/Icons/Icon_Chevron_Thin.png@636.66,726.22,14.57,25.01;"
  L+="$A/Buttons/UI_ButtonPrimary_Normal.png@790,692.5,373,91@nine=202,0,202,0@unit=3.582;$A/Icons/Icon_Chevron_Thin.png@1105.67,726.34,14.59,24.74;"
  echo "$L"
}
chips() {   # $1 = which chip is selected (50|100)
  local S50=UI_StakeChip_Normal.png S100=UI_StakeChip_Selected.png
  [ "$1" = 50 ] && { S50=UI_StakeChip_Selected.png; S100=UI_StakeChip_Normal.png; }
  local R50=816.0,488.0,196.7,71.2 R100=1025.0,488.0,185.9,71.2 L=""
  L+="$F/UI_StakeChip_Shadow.png@$(grow $R50 16.52)@nine=121,0,121,0@unit=2.303;$F/$S50@$R50@nine=83,0,83,0@unit=2.303;"
  L+="$F/UI_StakeChip_Shadow.png@$(grow $R100 16.52)@nine=121,0,121,0@unit=2.303;$F/$S100@$R100@nine=83,0,83,0@unit=2.303;"
  L+="$COIN@860.9,501.9,42.0,42.0;$COIN@1062.4,501.9,41.9,41.9;"
  echo "$L"
}
popover() {
  local L=""
  L+="$F/UI_ButtonInfo.png@1223.9,490.73,53.04,53.04;"
  L+="$F/UI_Popover_Shadow.png@$(grow 1175,557,283,257 29.57)@nine=120,120,120,120@unit=2.3;$F/UI_Popover_Pointer_Shadow.png@1195.93,513.43,109.57,73.04;"
  L+="$F/UI_Popover.png@1175,557,283,257@nine=52,52,52,52@unit=2.3;$F/UI_Popover_Pointer.png@1225.5,543,50,22;$F/Icon_Close_Brown.png@1415.76,578.07,20,20;"
  for y in 660.81 705.81 750.46; do L+="$F/UI_PrizeRowLine.png@1200.35,$y,231.3,6.087@nine=3,0,3,0;"; done
  L+="$COIN@1304.38,624.13,34.25,34.25;$COIN@1304.13,668.28,34.75,34.75;$COIN@1304.38,713.48,34.25,34.25;$COIN@1304.10,758.90,34.60,34.60"
  echo "$L"
}

$SU scene --screen $S24 --region 0,0,1672,941 --zoom 1 --out $W/check/overview_24.png --layers "$(common)$(chips 100)$(popover)" | head -1
$SU scene --screen $S23 --region 0,0,1672,941 --zoom 1 --out $W/check/overview_23.png --layers "$(common)$(chips 50)$(popover)" | head -1
