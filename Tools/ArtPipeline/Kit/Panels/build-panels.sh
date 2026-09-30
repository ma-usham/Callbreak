#!/usr/bin/env bash
# Rebuilds every sprite of the Panels kit from the ChatGPT originals in
# ArtSource/Working/Kit/Panels/chatgpt/ and writes the checks into .../check/.
#   Tools/ArtPipeline/Kit/Panels/build-panels.sh [asset ...]     (no argument: all of them)
# Each block is the recipe that made the accepted sprite: key the magenta ground, make it
# symmetric, bring the colours to the mockup's, scale to @2x (mockup px x 2.3), squeeze the
# middle for the 9-slice, export, check against the mockup. Numbers that look arbitrary were
# measured on the mockups (Tools/art probe / profile) or on the extraction (kp stats / extent).
set -e
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
cd "$repo"
KP="$here/kp"
W=ArtSource/Working/Kit/Panels
G=$W/chatgpt
T=$W/work
C=$W/check
mkdir -p $T $C

ONLY=" $* "
want() { [ "$ONLY" = "  " ] && return 0; case "$ONLY" in *" $1 "*) return 0 ;; esac; return 1; }

# ---------------------------------------------------------------------------------------------
# UI_Panel_Large: the big ivory menu panel (02-create-room). Attempt 3.
if want UI_Panel_Large; then
  R="449.1,214.3,774.8,670.8"                       # the panel on 02
  $KP cut     --in $G/UI_Panel_Large_a3.png --out $T/PL_cut.png --key magenta --despeckle 3000
  $KP mirror  --in $T/PL_cut.png --out $T/PL_sym.png --axis h
  $KP resize  --in $T/PL_sym.png --out $T/PL_2x_raw.png --w 1782 --h 1544          # 774.8 x 670.8 mockup px @2x
  # ChatGPT's blue channel separates ornament (178), fill (239) and rim (255); each takes the
  # mockup's colour, running from the top row to the bottom row (the mockup's fill darkens downwards).
  $KP gradmap --in $T/PL_2x_raw.png --out $T/PL_2x.png --channel b \
      --stops "178:#F6DFCB;239:#F9F0E6;255:#FDFAF5" --stops2 "178:#F3DAC6;239:#F6EBE0;255:#F9F1E8"
  $KP nine    --in $T/PL_2x.png --out $T/PL_nine.png --border 400,370,400,260 --mid 64,64
  $KP export  --in $T/PL_nine.png --name UI/Panels/UI_Panel_Large.png
  N="--sprite Assets/_Project/Art/UI/Panels/UI_Panel_Large.png --nine 400,370,400,260"
  $KP check $N --screen 02 --rect $R --zoom 1 --noalpha --out $C/UI_Panel_Large_check_02.png
  $KP check $N --screen 02 --rect $R --view 439,204,250,230 --zoom 3 --out $C/UI_Panel_Large_check_02_tl.png
  $KP check $N --screen 02 --rect $R --view 984,204,250,230 --zoom 3 --noalpha --out $C/UI_Panel_Large_check_02_tr.png
  $KP check $N --screen 02 --rect $R --view 439,690,250,205 --zoom 3 --out $C/UI_Panel_Large_check_02_bl.png
  $KP check $N --screen 01 --rect 366.5,235.6,459.5,581.9 --zoom 1.5 --noalpha --out $C/UI_Panel_Large_check_01.png
  $KP check $N --screen 04 --rect 330.9,240,1010.9,606.4 --zoom 1 --noalpha --stack --out $C/UI_Panel_Large_check_04.png
  $KP check $N --screen 11 --rect 349.1,225.2,974.9,630.7 --zoom 1 --noalpha --stack --out $C/UI_Panel_Large_check_11.png
  $KP check $N --screen 14 --rect 383.7,195,904.2,675.6 --zoom 1 --noalpha --stack --out $C/UI_Panel_Large_check_14.png
  $KP stretch $N --size 2324,2012 --zoom 0.5 --out $C/UI_Panel_Large_stretch3x.png
fi

# ---------------------------------------------------------------------------------------------
# UI_Panel_Card: the small plain card/tile (the "Easy" card of 06-offline-setup). Attempt 1.
if want UI_Panel_Card; then
  R="440.9,504,252.6,211"
  $KP cut     --in $G/UI_Panel_Card_a1.png --out $T/PC_cut.png --key magenta --despeckle 3000
  $KP mirror  --in $T/PC_cut.png --out $T/PC_sym.png --axis hv
  $KP recolor --in $T/PC_sym.png --out $T/PC_col.png --anchors "#FAF0E7>#F6ECE1;#FFFFFF>#FDFAF6"   # fill, rim
  $KP resize  --in $T/PC_col.png --out $T/PC_2x.png --w 582 --h 486                               # 252.6 x 211 @2x
  $KP nine    --in $T/PC_2x.png --out $T/PC_nine.png --border 56,56,56,56 --mid 32,32
  $KP export  --in $T/PC_nine.png --name UI/Panels/UI_Panel_Card.png
  N="--sprite Assets/_Project/Art/UI/Panels/UI_Panel_Card.png --nine 56,56,56,56"
  $KP check $N --screen 06 --rect $R --zoom 3 --out $C/UI_Panel_Card_check_06.png
  $KP check $N --screen 06 --rect $R --view 432,496,70,60 --zoom 10 --noalpha --out $C/UI_Panel_Card_check_06_tl.png
  $KP check $N --screen 06 --rect $R --view 632,664,70,60 --zoom 10 --noalpha --out $C/UI_Panel_Card_check_06_br.png
  $KP check $N --screen 15 --rect 695.1,221.8,284.9,451.9 --zoom 2 --noalpha --out $C/UI_Panel_Card_check_15.png
  $KP check $N --screen 14 --rect 787.1,260.4,235,120.1 --zoom 4 --noalpha --out $C/UI_Panel_Card_check_14.png
  $KP stretch $N --size 758,633 --zoom 1 --out $C/UI_Panel_Card_stretch3x.png
fi

# ---------------------------------------------------------------------------------------------
# UI_Panel_Modal_Bid: the "Make your call" panel of 07-bidding. Attempt 1.
if want UI_Panel_Modal_Bid; then
  R="482.8,347.5,706.3,309.5"
  $KP cut     --in $G/UI_Panel_Modal_Bid_a1.png --out $T/PMB_cut.png --key magenta --despeckle 3000
  $KP mirror  --in $T/PMB_cut.png --out $T/PMB_sym.png --axis h
  $KP resize  --in $T/PMB_sym.png --out $T/PMB_2x_raw.png --w 1626 --h 712                        # 706.3 x 309.5 @2x
  # ornament 165, fill 231, rim 255 on ChatGPT's blue channel; the mockup's fill runs from
  # #FBEDDF under the title to #FBE8D5 at the bottom.
  $KP gradmap --in $T/PMB_2x_raw.png --out $T/PMB_2x.png --channel b \
      --stops "165:#FACCA5;231:#FBEDDF;255:#FFFBF6" --stops2 "165:#F9CBA6;231:#FBE8D5;255:#FDF5EC"
  $KP nine    --in $T/PMB_2x.png --out $T/PMB_nine.png --border 224,224,224,196 --mid 64,64
  $KP export  --in $T/PMB_nine.png --name UI/Panels/UI_Panel_Modal_Bid.png
  N="--sprite Assets/_Project/Art/UI/Panels/UI_Panel_Modal_Bid.png --nine 224,224,224,196"
  $KP check $N --screen 07 --rect $R --zoom 1.3 --noalpha --stack --out $C/UI_Panel_Modal_Bid_check_07.png
  $KP check $N --screen 07 --rect $R --view 474,339,230,160 --zoom 4 --out $C/UI_Panel_Modal_Bid_check_07_tl.png
  $KP check $N --screen 07 --rect $R --view 968,505,230,160 --zoom 4 --noalpha --out $C/UI_Panel_Modal_Bid_check_07_br.png
  $KP stretch $N --size 2119,929 --zoom 0.5 --out $C/UI_Panel_Modal_Bid_stretch3x.png
fi

# ---------------------------------------------------------------------------------------------
# UI_Panel_Modal_Menu: the "Game Menu" panel of 13-match-menu. Attempt 1.
if want UI_Panel_Modal_Menu; then
  R="549.6,217.7,573.0,517.9"                       # outer edge of the gold outline (Tools/art profile)
  $KP cut      --in $G/UI_Panel_Modal_Menu_a1.png --out $T/PMM_cut.png --key magenta --despeckle 3000
  $KP defringe --in $T/PMM_cut.png --out $T/PMM_df.png --px 2                     # a reddish line on the outermost pixels
  $KP flatfill --in $T/PMM_df.png --out $T/PMM_flat.png --thr 7 --grow 3 --min 400   # the faint weave cannot stretch
  $KP rot180   --in $T/PMM_flat.png --out $T/PMM_sym.png                          # bottom-right knot = top-left knot turned
  $KP resize   --in $T/PMM_sym.png --out $T/PMM_2x_raw.png --w 1318 --h 1192      # 573.0 x 517.9 @2x
  # outline dark edge, knot, outline highlight, fill, inner white line: ChatGPT -> mockup
  $KP curve    --in $T/PMM_2x_raw.png --out $T/PMM_2x.png \
      --anchors "#87411E>#C48C5C;#DEAB84>#DAAA87;#FAD2A7>#FCCC98;#FCEFE4>#F8EBE0;#FFFAEF>#FFF5E8"
  $KP nine     --in $T/PMM_2x.png --out $T/PMM_nine.png --border 200,200,200,200 --mid 64,64 --feather 24
  $KP export   --in $T/PMM_nine.png --name UI/Panels/UI_Panel_Modal_Menu.png
  N="--sprite Assets/_Project/Art/UI/Panels/UI_Panel_Modal_Menu.png --nine 200,200,200,200"
  $KP check $N --screen 13 --rect $R --zoom 1.5 --noalpha --out $C/UI_Panel_Modal_Menu_check_13.png
  $KP check $N --screen 13 --rect $R --view 542,210,200,150 --zoom 5 --out $C/UI_Panel_Modal_Menu_check_13_tl.png
  $KP check $N --screen 13 --rect $R --view 930,595,200,150 --zoom 5 --noalpha --out $C/UI_Panel_Modal_Menu_check_13_br.png
  $KP stretch $N --size 1719,1554 --zoom 0.5 --out $C/UI_Panel_Modal_Menu_stretch3x.png
fi

echo "done"
