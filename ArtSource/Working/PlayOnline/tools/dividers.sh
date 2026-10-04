#!/usr/bin/env bash
# The dividers of the Play Online screen, sourced by make.sh (uses W, F, KC, SU, PO, S23, S24):
# - the bronze diamond: ChatGPT's (sheet P3 cell 1, the title divider), its outline and fill put on the mockups'
#   colours; fitted by alpha on all three diamonds (IoU 0.95-0.99);
# - the thin lines: drawn (po.js line) at the opacity measured along each line on 23 and 24 (po.js alphaprof), core
#   #A88566, 1.6 px (1.4 px vertical), light lines either side; ChatGPT's lines were 2-3x too heavy at this size;
# - each divider assembled at its mockup rect (mockup px x 2.3).
set -euo pipefail
Ln=$W/work/Lines
mkdir -p $Ln
SCR="$S24;$S23"

# --- the diamond ---------------------------------------------------------------------------------------------------
$SU trimto --in $W/work/P3_a1/TitleDivider.png --out $Ln/td_0_trim.png --thr 0.3 --pad 0
$KC crop --in $Ln/td_0_trim.png --out $Ln/dia_0_crop.png --box 600,0,105,78
$SU trimto --in $Ln/dia_0_crop.png --out $Ln/dia_1_trim.png --thr 0.3 --pad 0
$KC defringe --in $Ln/dia_1_trim.png --out $Ln/dia_2_defringe.png --px 2
$KC curve --in $Ln/dia_2_defringe.png --out $Ln/dia_3_color.png --anchors "#52350A>#6B441A;#7B5929>#7B562E;#7F5E2D>#825A32"
$KC resize --in $Ln/dia_3_color.png --out $F/Deco_Diamond_Bronze.png --w 36 --h 42

LINE="--color #A88566 --hi #FFFDF8@0.6 --lo #FFFDF8@0.5 --hw 0.9 --pad 1.5 --caps round"

# --- under "Quick Match": lines 374-524 and 553-706 at y 511.0, diamond [531.45, 501.75, 15.55, 18.45] -------------
P=$($PO alphaprof --dir h --span 374,524 --across 507,515 --ground "#F7ECE1" --core "#A88566" --screens "$SCR")
$PO line --out $Ln/q_left.png --len 150 --thick 1.6 --profile "$P" $LINE
P=$($PO alphaprof --dir h --span 553,706 --across 507,515 --ground "#F7ECE1" --core "#A88566" --screens "$SCR")
$PO line --out $Ln/q_right.png --len 153 --thick 1.6 --profile "$P" $LINE
$PO assemble --rect 371.5,500.5,337.39,20.87 --out $F/Deco_ModeTitleDivider_Quick.png \
    --layers "$Ln/q_left.png@372.48,507.52,153.04,6.957;$Ln/q_right.png@551.24,507.52,156.52,6.957;$F/Deco_Diamond_Bronze.png@531.45,501.75,15.55,18.45"

# --- under "Stake Match": lines 888-1048 and 1076-1242 at y 433.8, diamond [1055.37, 424.69, 15.33, 18.23] ---------
P=$($PO alphaprof --dir h --span 888,1048 --across 430,438 --ground "#F7ECE1" --core "#A88566" --screens "$SCR")
$PO line --out $Ln/s_left.png --len 160 --thick 1.6 --profile "$P" $LINE
P=$($PO alphaprof --dir h --span 1076,1242 --across 430,438 --ground "#F7ECE1" --core "#A88566" --screens "$SCR")
$PO line --out $Ln/s_right.png --len 166 --thick 1.6 --profile "$P" $LINE
$PO assemble --rect 885.5,423.5,359.13,20 --out $F/Deco_ModeTitleDivider_Stake.png \
    --layers "$Ln/s_left.png@886.26,430.322,163.478,6.957;$Ln/s_right.png@1074.22,430.322,169.565,6.957;$F/Deco_Diamond_Bronze.png@1055.37,424.69,15.33,18.23"

# --- between the two modes: x 745.6, lines 224.5-478 and 500-797, diamond [738.58, 480.3, 14.25, 18.1] ---------------
P=$($PO alphaprof --dir v --span 224.5,478 --across 742,749 --ground "#F7ECE1" --core "#A88566" --screens "$SCR")
$PO line --out $Ln/v_top.png --vertical --len 253.5 --thick 1.4 --profile "$P" $LINE
P=$($PO alphaprof --dir v --span 500,797 --across 742,749 --ground "#F7ECE1" --core "#A88566" --screens "$SCR")
$PO line --out $Ln/v_bot.png --vertical --len 297 --thick 1.4 --profile "$P" $LINE
$PO assemble --rect 737.82,222.17,15.652,577.391 --out $F/Deco_ModeDivider_Vertical.png \
    --layers "$Ln/v_top.png@742.12,222.99,6.957,256.52;$Ln/v_bot.png@742.12,498.5,6.957,300;$F/Deco_Diamond_Bronze.png@738.58,480.3,14.25,18.1"

# --- the flourishes beside the subtitle: ChatGPT's (P3 cell 2), fitted by alpha (IoU 0.85-0.91), colours put on the
#     mockups'; the left one is the right one mirrored and fitted on its own --------------------------------------
$SU trimto --in $W/work/P3_a1/Flourish.png --out $Ln/fl_0_trim.png --thr 0.2 --pad 0
$KC defringe --in $Ln/fl_0_trim.png --out $Ln/fl_1_defringe.png --px 2
$KC curve --in $Ln/fl_1_defringe.png --out $Ln/fl_2_color.png --anchors "#58330A>#7A4F2D;#6B4619>#845C3A;#7F5B28>#90694B"
$KC resize --in $Ln/fl_2_color.png --out $F/Deco_SubtitleFlourish_Right.png --w 196 --h 31
$PO assemble --layers "$Ln/fl_2_color.png@0,0,401,70@flipx" --rect 0,0,401,70 --unit 1 --out $Ln/fl_2_flipx.png
$KC resize --in $Ln/fl_2_flipx.png --out $F/Deco_SubtitleFlourish_Left.png --w 170 --h 31
