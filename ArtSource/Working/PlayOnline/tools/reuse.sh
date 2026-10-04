#!/usr/bin/env bash
# Reuse checks: kit sprites (Assets/_Project/Art/UI) against the Play Online mockups 23 (normalised) and 24.
# Numbers are sRGB levels (rms / mean) over the element, its words left out; written to check/reuse_numbers.txt.
# Fits use shopui fitlayer / fiticon (alpha) / kitcontrols fit; composites use po.js compose (linear light, like Unity).
#   reuse.sh   (from anywhere)
set -uo pipefail
cd "$(dirname "$0")/../../../.."           # the repo root
KC=${KC:-kc}
SU=${SU:-shopui}
PO=${PO:-"node ArtSource/Working/PlayOnline/tools/po.js"}
W=ArtSource/Working/PlayOnline
C=$W/check
A=Assets/_Project/Art/UI
S24=$W/attach/S24-online-play-stake-100.png
S23=$W/attach/S23n-online-play-stake-50-1672.png
OUT=$C/reuse_numbers.txt
: > $OUT
say() { echo "$*" | tee -a $OUT; }
err() { sed 's/.*error in \[[^]]*\]: //' | tee -a $OUT; }

for M in $S24 $S23; do
  n=$(basename $M | cut -c1-3); say "== $n"
  say "-- large panel UI/Panels/UI_Panel_Large.png (nine 400,370,400,260) at [305.9, 187.1, 1064.9, 650.8]"
  for z in "plain interior:320,565,410,115" "top-left textile corner:308,190,115,120" "bottom-left corner:308,785,140,50"; do
    echo -n "   ${z%%:*}: " | tee -a $OUT
    $PO compose --screen $M --layers "$A/Panels/UI_Panel_Large.png@305.9,187.1,1064.9,650.8@nine=400,370,400,260@unit=2.3" --region ${z#*:} --out $W/work/tmp.png | err
  done
  say "-- back button: UI_ButtonCircle_Normal [38.42, 29.3, 96.5, 96.5] + Icon_Back [66.52, 58.39, 39.86, 38.02]"
  echo -n "   inside the disc: " | tee -a $OUT
  $PO compose --screen $M --layers "$A/Buttons/UI_ButtonCircle_Normal.png@38.42,29.3,96.5,96.5;$A/Icons/Icon_Back.png@66.52,58.39,39.86,38.02" --region 40,31,94,94 --measure 55,46,63,63 --out $C/reuse_back_$n.png | err
  echo -n "   arrow alone (fiticon): " | tee -a $OUT
  $SU fiticon --sprite $A/Icons/Icon_Back.png --screen $M --rect 66.19,58.56,39.61,37.87 --ground "#F3EAE1" --ink "#1E1A18" --search 3 | head -1 | sed 's/^fit Icon_Back.png: //' | tee -a $OUT
  say "-- wallet: UI_ButtonSecondary_Normal (nine 117,0,117,0) [1436.1, 54.2, 190.0, 67.9] + Icon_Coin [1458.30, 57.30, 58.40, 58.40], the balance left out"
  echo -n "   " | tee -a $OUT
  $PO compose --screen $M --layers "$A/Buttons/UI_ButtonSecondary_Normal.png@1436.1,54.2,190.0,67.9@nine=117,0,117,0@unit=3.122;$A/Icons/Icon_Coin.png@1458.30,57.30,58.40,58.40" --region 1430,50,202,77 --exclude "1528,66,74,46" --out $C/reuse_wallet_$n.png | err
  say "-- Icon_Coin (fitlayer): wallet, chips, prize rows"
  for r in 1458.5,59,59,59 860.5,503,42,42 1060.5,501.5,42,42 1305,626,34,34 1305,671,34,34 1305,716,34,34 1305,761,34,34; do
    echo -n "   " | tee -a $OUT; $SU fitlayer --sprite $A/Icons/Icon_Coin.png --screen $M --rect $r --pad 3 | tail -1 | sed 's/^fit Icon_Coin.png: //' | tee -a $OUT
  done
  say "-- play buttons: UI_ButtonPrimary_Normal (nine 202,0,202,0, unit 326/91), labels and chevrons left out (kitcontrols fit)"
  echo -n "   Play Quick: " | tee -a $OUT; $KC fit --sprite $A/Buttons/UI_ButtonPrimary_Normal.png --screen $M --rect 344.5,692.5,371.5,91 --nine 202,0,202,0 --unit 3.582 --mode free --exclude "420,715,250,48;628,720,30,38" | tail -1 | tee -a $OUT
  echo -n "   Play for N coins: " | tee -a $OUT; $KC fit --sprite $A/Buttons/UI_ButtonPrimary_Normal.png --screen $M --rect 790,692.5,373,91 --nine 202,0,202,0 --unit 3.582 --mode free --exclude "840,715,262,48;1098,720,30,38" | tail -1 | tee -a $OUT
  say "-- chevrons (fiticon, white on crimson): Icon_Chevron_Thin / Common/Icon_Chevron_Light"
  for ic in Icons/Icon_Chevron_Thin Common/Icon_Chevron_Light; do for r in 636.78,726.37,14.22,24.81 1105.74,726.14,14.24,24.84; do
    echo -n "   " | tee -a $OUT; $SU fiticon --sprite $A/$ic.png --screen $M --rect $r --ground "#8C0F17" --ink "#FFFFFF" --search 2 | head -1 | tee -a $OUT
  done; done
  say "-- close x: kit Icon_Close vs this batch's Icon_Close_Brown (fiticon)"
  for sp in $A/Icons/Icon_Close.png $W/work/Close/close_0_trim.png; do
    echo -n "   " | tee -a $OUT; $SU fiticon --sprite $sp --screen $M --rect 1417.26,579.47,17.37,17.36 --ground "#F8EEE4" --ink "#3A2412" --search 2 | head -1 | tee -a $OUT
  done
done
echo "numbers: $OUT"
