#!/usr/bin/env bash
# Checks every exported sprite against its mockup: draws it at its rectangle on a ground with the
# mockup's own element erased, writes ArtSource/Working/Kit/Controls/check/<Name>*.png (mockup |
# sprite at mockup resolution | sprite sharp ; difference x6 | on black | on white) and prints
# the fit: the rectangle at which the sprite is closest to the mockup and how close (sRGB levels).
# Every 9-slice sprite is also drawn three times as wide and high (check/<Name>_stretch.png).
#   check.sh            everything        check.sh toggle | slider | ...   one group
# The rectangles here are the ones the swap map (ArtSource/Kit/Controls.map.csv) gives.
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
cd "$repo"
KC="$here/kc"
A=Assets/_Project/Art/UI/Controls
K=ArtSource/Working/Kit/Controls/check
mkdir -p $K

# one <name> <screen> <rect> <view options...>     view + fit of one sprite
one() {
  local name=$1 screen=$2 rect=$3; shift 3
  echo "== ${CHECKNAME:-$name}: $name on $screen at $rect"
  $KC view --sprite $A/$name.png --screen $screen --rect $rect "$@" --out $K/${CHECKNAME:-$name}.png | tail -1
  $KC fit --sprite $A/$name.png --screen $screen --rect $rect "$@" --mode ${FITMODE:-free} | tr '\n' ' '; echo
}

# stretch <name> <nine> <unit> <size in mockup px>     a 9-slice sprite at three times its size
stretch() {
  $KC stretch --sprite $A/$1.png --nine $2 --unit $3 --size $4 --zoom 1.5 --bg "#F8EFE5" --out $K/$1_stretch.png | sed 's/^/   /'
}

toggle() {
  one UI_Toggle_On 11 706.69,363.41,83.11,47.11 --zoom 7
  CHECKNAME=UI_Toggle_On_soundeffects one UI_Toggle_On 11 706.79,429.13,83.01,47.06 --zoom 7
  CHECKNAME=UI_Toggle_On_vibration one UI_Toggle_On 11 1162.05,433.83,83.61,47.46 --zoom 7
  CHECKNAME=UI_Toggle_On_13 one UI_Toggle_On 13 964.76,548.55,80.40,45.30 --zoom 7
  one UI_Toggle_Off 11 1161.73,502.18,83.71,47.76 --zoom 7
}

slider() {
  local T="$A/UI_Slider_Track.png;429.58,553.38,358.25,18.85;22,0,22,0;2.334"
  local F="$A/UI_Slider_Fill.png;427.80,551.88,256.20,21.65;26,0,26,0;2.309"
  local N="$A/UI_Slider_Knob.png;666.63,546.58,32.05,32.15"
  local E="--erase 426,542,366,42 --pad 8"
  one UI_Slider_Track 11 429.58,553.38,358.25,18.85 --nine 22,0,22,0 --unit 2.334 $E --over "$F|$N" --exclude "660,540,46,48" --window 650,536,150,52 --zoom 5
  one UI_Slider_Fill 11 427.80,551.88,256.20,21.65 --nine 26,0,26,0 --unit 2.309 $E --under "$T" --over "$N" --exclude "660,540,46,48" --window 420,540,70,46 --zoom 8
  FITMODE=uniform one UI_Slider_Knob 11 666.63,546.58,32.05,32.15 $E --under "$T|$F" --exclude "660,576,46,12" --window 650,536,150,52 --zoom 5
  stretch UI_Slider_Track 22,0,22,0 2.334 1075,18.85
  stretch UI_Slider_Fill 26,0,26,0 2.309 770,21.65
}

dropdown() {
  one UI_Dropdown 11 1067.61,362.73,176.80,54.40 --nine 30,0,98,0 --unit 2.316 --pad 8 --exclude "1076,366,106,48" --zoom 5
  CHECKNAME=UI_Dropdown_right one UI_Dropdown 11 1067.61,362.73,176.80,54.40 --nine 30,0,98,0 --unit 2.316 --window 1196,356,56,68 --zoom 10
  stretch UI_Dropdown 30,0,98,0 2.316 530,54.4
}

field() {
  one UI_Field 02 563.15,400.14,546.45,60.84 --nine 36,36,36,36 --pad 8 --exclude "578,408,190,48" --zoom 2.4
  CHECKNAME=UI_Field_left one UI_Field 02 563.15,400.14,546.45,60.84 --nine 36,36,36,36 --exclude "578,408,190,48" --window 554,392,60,78 --zoom 8
  CHECKNAME=UI_Field_right one UI_Field 02 563.15,400.14,546.45,60.84 --nine 36,36,36,36 --exclude "578,408,190,48" --window 1060,392,60,78 --zoom 8
  one UI_Field_Light 14 508.63,647.55,654.35,59.60 --nine 36,36,36,36 --pad 8 --exclude "520,656,100,42" --zoom 2
  CHECKNAME=UI_Field_Light_left one UI_Field_Light 14 508.63,647.55,654.35,59.60 --nine 36,36,36,36 --exclude "520,656,100,42" --window 500,640,60,76 --zoom 8
  CHECKNAME=UI_Field_Light_right one UI_Field_Light 14 508.63,647.55,654.35,59.60 --nine 36,36,36,36 --exclude "520,656,100,42" --window 1112,640,60,76 --zoom 8
  stretch UI_Field 36,36,36,36 2.3 800,182
  stretch UI_Field_Light 36,36,36,36 2.3 800,182
}

codebox() {
  one UI_CodeBox_Normal 03 719.38,482.35,108.45,109.44 --pad 8 --exclude "755,505,40,64" --zoom 5
  CHECKNAME=UI_CodeBox_Normal_box0 one UI_CodeBox_Normal 03 469.30,482.35,108.45,109.44 --pad 8 --exclude "500,505,48,64" --zoom 5
  CHECKNAME=UI_CodeBox_Normal_box5 one UI_CodeBox_Normal 03 1095.20,482.35,108.45,109.44 --pad 8 --exclude "1126,505,48,64" --zoom 5
  $KC onbg --in $A/UI_CodeBox_Focus.png --out $K/UI_CodeBox_Focus.png --bgs "#FBF4EC,#000000,#FFFFFF" | sed 's/^/   /'
  $KC onbg --in $A/UI_CodeBox_Error.png --out $K/UI_CodeBox_Error.png --bgs "#FBF4EC,#000000,#FFFFFF" | sed 's/^/   /'
}

segment() {
  local T="$A/UI_Segment_Track.png;602.15,634.26,468.97,59.40;70,0,70,0;2.323"
  local S="$A/UI_Segment_Selected.png;832.63,637.80,234.56,52.61;61,0,61,0;2.281"
  local E="--erase 598,630,478,70 --pad 8 --exclude 680,646,90,36;905,646,95,36"
  CHECKNAME=UI_Segment_Track_left one UI_Segment_Track 02 602.15,634.26,468.97,59.40 --nine 70,0,70,0 --unit 2.323 $E --over "$S" --window 596,626,80,76 --zoom 7
  CHECKNAME=UI_Segment_Track_right one UI_Segment_Track 02 602.15,634.26,468.97,59.40 --nine 70,0,70,0 --unit 2.323 $E --over "$S" --window 1000,626,80,76 --zoom 7
  CHECKNAME=UI_Segment_Selected_left one UI_Segment_Selected 02 832.63,637.80,234.56,52.61 --nine 61,0,61,0 --unit 2.281 $E --under "$T" --window 800,626,80,76 --zoom 7
  echo "== UI_Segment: track and selected pill together"
  $KC view --sprite $A/UI_Segment_Track.png --screen 02 --rect 602.15,634.26,468.97,59.40 --nine 70,0,70,0 --unit 2.323 $E --over "$S" --zoom 2.5 --out $K/UI_Segment.png | tail -1
  stretch UI_Segment_Track 70,0,70,0 2.323 1407,59.4
  stretch UI_Segment_Selected 61,0,61,0 2.281 704,52.61
}

radio() {
  FITMODE=uniform one UI_Radio_On 15 455.19,601.20,42.40,42.40 --zoom 9 --pad 6
  FITMODE=uniform one UI_Radio_Off 15 766.80,601.20,42.40,42.40 --zoom 9 --pad 6
  FITMODE=uniform CHECKNAME=UI_Radio_Off_deck2 one UI_Radio_Off 15 1066.55,601.20,42.40,42.40 --zoom 9 --pad 6
}

round() {
  FITMODE=uniform one UI_ReadyDot 04 752.30,498.78,31.92,31.92 --noerase --zoom 12 --pad 6
  FITMODE=uniform CHECKNAME=UI_ReadyDot_seat2 one UI_ReadyDot 04 991.00,498.77,31.92,31.92 --noerase --zoom 12 --pad 6
  FITMODE=uniform CHECKNAME=UI_ReadyDot_seat3 one UI_ReadyDot 04 1230.10,498.77,31.92,31.92 --noerase --zoom 12 --pad 6
  echo "== UI_Ring_Spinner in the chevron's place on the Create Room button (02); there is no mockup of it"
  $KC view --sprite $A/UI_Ring_Spinner.png --screen 02 --rect 1035.1,798.28,28,28 --erase 1036,794,26,37 --pad 14 --zoom 8 --out $K/UI_Ring_Spinner.png | sed -n 1p
  FITMODE=uniform one UI_Disc_Number 12 422.05,366.70,61.50,61.50 --exclude "437,376,32,43" --zoom 7 --pad 6
  FITMODE=uniform CHECKNAME=UI_Disc_Number_rule2 one UI_Disc_Number 12 422.10,462.50,61.50,61.50 --exclude "437,472,32,43" --zoom 7 --pad 6
  FITMODE=uniform CHECKNAME=UI_Disc_Number_rule3 one UI_Disc_Number 12 422.05,558.75,61.50,61.50 --exclude "437,568,32,43" --zoom 7 --pad 6
  FITMODE=uniform CHECKNAME=UI_Disc_Number_rule4 one UI_Disc_Number 12 422.05,651.10,61.50,61.50 --exclude "437,660,32,43" --zoom 7 --pad 6
  FITMODE=uniform one UI_Check_Badge 06 904.76,512.11,41.99,41.99 --zoom 10 --pad 5
}

outline() {
  local X="--exclude 717,507.3,238.2,203.7"
  CHECKNAME=UI_Outline_Selected_tl one UI_Outline_Selected 06 710.18,500.35,251.95,217.70 --nine 60,60,60,60 $X --window 700,490,80,80 --zoom 8
  CHECKNAME=UI_Outline_Selected_br one UI_Outline_Selected 06 710.18,500.35,251.95,217.70 --nine 60,60,60,60 $X --window 892,650,80,80 --zoom 8
  echo "== UI_Outline_Selected: whole outline"
  $KC view --sprite $A/UI_Outline_Selected.png --screen 06 --rect 710.18,500.35,251.95,217.70 --nine 60,60,60,60 $X --pad 8 --zoom 3 --out $K/UI_Outline_Selected.png | tail -1
  X="--exclude 397.5,225.7,277.6,443.7"
  CHECKNAME=UI_Outline_Thin_Selected_tl one UI_Outline_Thin_Selected 15 391.25,219.40,290.10,456.30 --nine 64,64,64,64 $X --window 382,210,80,80 --zoom 8
  CHECKNAME=UI_Outline_Thin_Selected_br one UI_Outline_Thin_Selected 15 391.25,219.40,290.10,456.30 --nine 64,64,64,64 $X --window 610,606,80,80 --zoom 8
  echo "== UI_Outline_Thin_Selected: whole outline"
  $KC view --sprite $A/UI_Outline_Thin_Selected.png --screen 15 --rect 391.25,219.40,290.10,456.30 --nine 64,64,64,64 $X --pad 8 --zoom 2 --out $K/UI_Outline_Thin_Selected.png | tail -1
  stretch UI_Outline_Selected 60,60,60,60 2.3 756,653
  stretch UI_Outline_Thin_Selected 64,64,64,64 2.3 870,700
}

seat() {
  one UI_Seat_Filled 05 499.22,506.19,119.60,117.35 --zoom 5 --pad 6
  CHECKNAME=UI_Seat_Filled_seat1 one UI_Seat_Filled 05 682.19,506.14,120.35,117.45 --zoom 5 --pad 6
  CHECKNAME=UI_Seat_Filled_seat2 one UI_Seat_Filled 05 866.37,506.14,120.30,117.45 --zoom 5 --pad 6
  one UI_Seat_Searching 05 1052.03,506.73,119.35,115.95 --zoom 5 --pad 6
}

pill() {
  one UI_Pill_RoomCode 04 490.13,281.83,376.15,77.55 --nine 68,68,68,68 --pad 8 --exclude "505,296,345,50" --zoom 3
  CHECKNAME=UI_Pill_RoomCode_left one UI_Pill_RoomCode 04 490.13,281.83,376.15,77.55 --nine 68,68,68,68 --exclude "505,296,345,50" --window 482,274,70,94 --zoom 8
  CHECKNAME=UI_Pill_RoomCode_right one UI_Pill_RoomCode 04 490.13,281.83,376.15,77.55 --nine 68,68,68,68 --exclude "505,296,345,50" --window 804,274,70,94 --zoom 8
  one UI_Pill_Highlight 12 591.38,738.85,489.05,44.90 --nine 53,0,53,0 --unit 2.316 --pad 8 --exclude "598,742,476,38" --zoom 2.4
  CHECKNAME=UI_Pill_Highlight_left one UI_Pill_Highlight 12 591.38,738.85,489.05,44.90 --nine 53,0,53,0 --unit 2.316 --exclude "598,742,476,38" --window 582,730,80,64 --zoom 8
  CHECKNAME=UI_Pill_Highlight_right one UI_Pill_Highlight 12 591.38,738.85,489.05,44.90 --nine 53,0,53,0 --unit 2.316 --exclude "598,742,476,38" --window 1010,730,80,64 --zoom 8
  stretch UI_Pill_RoomCode 68,68,68,68 2.3 1128,232
  stretch UI_Pill_Highlight 53,0,53,0 2.316 1467,44.9
}

# The crimson buttons: the pill on every screen that draws it (rect = the button's outer edge,
# kc edges; unit = sprite height / rect height, as the game scales a horizontally sliced sprite),
# the box on 13, the thin chevron where the mockups draw it; words, chevrons and icons are left
# out of the error. Then the soft shadow: drop and opacity for the shadow sprite on each screen.
button() {
  local P="--nine 160,0,160,0"
  bt() { # <check name> <screen> <rect> <unit> <exclude> [view options]
    local name=$1 screen=$2 rect=$3 unit=$4 ex=$5; shift 5
    echo "== $name: UI_Button_Crimson on $screen at $rect"
    $KC view --sprite $A/UI_Button_Crimson.png --screen $screen --rect $rect $P --unit $unit --exclude "$ex" --pad 10 --zoom ${ZOOM:-2} --out $K/$name.png "$@" | tail -1
    $KC fit --sprite $A/UI_Button_Crimson.png --screen $screen --rect $rect $P --unit $unit --exclude "$ex" --mode move | tr '\n' ' '; echo
  }
  ZOOM=3 bt UI_Button_Crimson_01_create 01 396.31,696.07,401.23,90.76 2.4240 "494,714,203,52;744,720,36,44"
  ZOOM=3 bt UI_Button_Crimson_01_join 01 875.52,695.94,401.45,90.90 2.4202 "991,714,170,52;1224,720,36,44"
  bt UI_Button_Crimson_04 04 542.27,729.26,587.77,95.90 2.2941 "665,745,60,64;734,746,280,64"
  bt UI_Button_Crimson_06 06 439.95,782.82,792.80,93.64 2.3494 "712,796,246,68;990,813,25,36"
  bt UI_Button_Crimson_07 07 624.34,554.74,423.45,82.15 2.6780 "738,569,194,52"
  bt UI_Button_Crimson_09 09 563.77,685.43,544.49,74.04 2.9714 "722,694,230,58;1035,705,25,35"
  bt UI_Button_Crimson_10 10 424.44,712.14,418.35,82.18 2.6771 "530,722,218,64;789,736,23,33"
  bt UI_Button_Crimson_11 11 587.05,747.15,498.62,89.30 2.4636 "778,762,118,56"
  bt UI_Button_Crimson_12 12 611.62,802.19,448.43,77.57 2.8362 "774,818,98,46;886,827,21,30"
  bt UI_Button_Crimson_14 14 508.13,733.70,655.74,78.74 2.7940 "721,748,228,50"
  bt UI_Button_Crimson_15 15 563.89,772.09,545.42,93.70 2.3479 "671,791,328,58"
  bt UI_Button_Crimson_16 16 565.17,754.39,542.18,77.48 2.8395 "690,764,296,56;1019,776,25,36"
  ZOOM=6 bt UI_Button_Crimson_06_left 06 439.95,782.82,792.80,93.64 2.3494 "712,796,246,68;990,813,25,36" --window 432,774,128,112
  ZOOM=6 bt UI_Button_Crimson_06_right 06 439.95,782.82,792.80,93.64 2.3494 "712,796,246,68;990,813,25,36" --window 1112,774,128,112
  echo "== UI_Button_Crimson_Box on 13 at 612.2,358.61,447.7,78.12"
  $KC view --sprite $A/UI_Button_Crimson_Box.png --screen 13 --rect 612.2,358.61,447.7,78.12 --nine 96,0,96,0 --unit 2.3041 --exclude "718,374,238,48" --pad 10 --zoom 3 --out $K/UI_Button_Crimson_Box.png | tail -1
  $KC view --sprite $A/UI_Button_Crimson_Box.png --screen 13 --rect 612.2,358.61,447.7,78.12 --nine 96,0,96,0 --unit 2.3041 --exclude "718,374,238,48" --window 604,350,84,96 --zoom 7 --out $K/UI_Button_Crimson_Box_left.png | tail -1
  $KC fit --sprite $A/UI_Button_Crimson_Box.png --screen 13 --rect 612.2,358.61,447.7,78.12 --nine 96,0,96,0 --unit 2.3041 --exclude "718,374,238,48" --mode free | tr '\n' ' '; echo
  echo "== Icon_Chevron_Thin (and the kit's Icon_Chevron_Light) at each chevron's ink box"
  ch() { # <screen> <rect> <label>
    for s in Assets/_Project/Art/UI/Icons/Icon_Chevron_Thin.png Assets/_Project/Art/UI/Common/Icon_Chevron_Light.png; do
      printf "%-10s %-22s " "$3" "$(basename $s .png)"; $KC fit --sprite $s --screen $1 --rect $2 --pad 6 --mode free | sed -n 2p | sed 's/^ *//'
    done
  }
  ch 01 754.05,728.96,14.73,25.59 01create; ch 01 1233.67,728.81,14.45,25.90 01join; ch 06 994.92,817.83,15.41,26.25 06start
  ch 09 1039.73,709.13,15.55,26.71 09next; ch 10 793.86,740.73,13.49,24.31 10again; ch 12 890.63,830.95,12.40,21.57 12gotit
  ch 16 1023.16,780.01,16.12,27.11 16back; ch 02 1040.35,797.27,17.41,29.83 02create; ch 03 971.37,736.43,20.09,33.33 03join
  $KC view --sprite Assets/_Project/Art/UI/Icons/Icon_Chevron_Thin.png --screen 06 --rect 994.92,817.83,15.41,26.25 --pad 6 --zoom 12 --out $K/Icon_Chevron_Thin.png | tail -1
  echo "== shadows: best drop / opacity for the shadow sprites (the face drawn over them hides the rest)"
  sh() { # <screen> <rect> <radius> <label> [shadow face nine]
    printf "%-10s " "$4"; $KC bshadow --screen $1 --rect $2 --radius $3 --band 1,11 --far 12,15 --nofit --shadow ${5:-$A/UI_Button_Crimson_Shadow.png} --face ${6:-336,220} --nine ${7:-196,0,196,0} | grep "best drop" | sed 's/^ *//'
  }
  sh 01 396.31,696.07,401.23,90.76 34.4 01create; sh 01 875.52,695.94,401.45,90.90 34.2 01join
  sh 04 542.27,729.26,587.77,95.90 37.5 04start; sh 06 439.95,782.82,792.80,93.64 37.6 06start
  sh 07 624.34,554.74,423.45,82.15 39.6 07confirm; sh 09 563.77,685.43,544.49,74.04 27.6 09next
  sh 10 424.44,712.14,418.35,82.18 26.2 10again; sh 11 587.05,747.15,498.62,89.30 37.0 11done
  sh 12 611.62,802.19,448.43,77.57 35.7 12gotit; sh 14 508.13,733.70,655.74,78.74 27.7 14save
  sh 15 563.89,772.09,545.42,93.70 35.7 15use; sh 16 565.17,754.39,542.18,77.48 30.2 16back
  sh 13 612.2,358.61,447.7,78.12 20.8 13resume $A/UI_Button_Crimson_Box_Shadow.png 208,180 126,0,126,0
  stretch UI_Button_Crimson 160,0,160,0 2.3494 1400,93.64
  stretch UI_Button_Crimson_Box 96,0,96,0 2.3041 900,78.12
}

case "${1:-all}" in
  all) toggle; slider; dropdown; field; codebox; segment; radio; round; outline; seat; pill; button ;;
  *) "$1" ;;
esac
