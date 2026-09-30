#!/usr/bin/env bash
# Rebuilds the table, results and Game Menu pieces of the Panels kit (Assets/_Project/Art/UI/Table)
# from the ChatGPT sheets in ArtSource/Working/Kit/Panels/chatgpt/ and writes their checks.
#   Tools/ArtPipeline/Kit/Panels/build-pieces.sh [Table_Pieces] [Results_Table] [Menu_Rows]
# (no argument: all three). The four panels are in build-panels.sh. Each sheet is one ChatGPT
# request that returned several pieces on magenta; a block keys the sheet, splits it and then
# finishes each piece. Rectangles and colours were measured on the mockups.
set -e
here="$(cd "$(dirname "$0")" && pwd)"
repo="$(cd "$here/../../../.." && pwd)"
cd "$repo"
KP="$here/kp"
W=ArtSource/Working/Kit/Panels
G=$W/chatgpt
T=$W/work
C=$W/check
A=Assets/_Project/Art/UI/Table
mkdir -p $T $C

ONLY=" $* "
want() { [ "$ONLY" = "  " ] && return 0; case "$ONLY" in *" $1 "*) return 0 ;; esac; return 1; }

# ---------------------------------------------------------------------------------------------
# The table pieces sheet: name plate, status pill, timer ring, a timer ball (not used: the badge
# is a flat oval, see Menu_Rows) and the two bid chips.
if want Table_Pieces; then
  P=$T/tp; mkdir -p $P
  $KP cut   --in $G/Table_Pieces_Sheet_a1.png --out $T/TP_key.png --key magenta --notrim --despeckle 300
  $KP split --in $T/TP_key.png --outdir $P --names NamePlate,StatusPill,TimerRing,TimerDisc,ChipNormal,ChipSelected --min 2000 --grow 4

  # UI_NamePlate: the white pill under a seat's avatar (08, Bikash: [104.9, 415.8, 133.2, 33.0]).
  $KP mirror   --in $P/NamePlate.png --out $P/NP_sym.png --axis hv
  $KP rowmatch --in $P/NP_sym.png --out $P/NP_col.png --screen 08 --x 124-130 --y0 415.8 --y1 448.8 --trust 0.15-0.85 --smooth 0.06
  $KP resize   --in $P/NP_col.png --out $P/NP_2x.png --w 320 --h 76                 # 33.0 mockup px tall @2x
  $KP nine     --in $P/NP_2x.png --out $P/NP_nine.png --border 44,0,44,0 --mid 16,0 --feather 6
  $KP export   --in $P/NP_nine.png --name UI/Table/UI_NamePlate.png

  # UI_StatusPill: the peach pill under it ([96.5, 448.6, 150.3, 37.8]). Its rows take the
  # mockup's shading, read left of "Bid" and right of the last digit, where no text is.
  $KP defringe --in $P/StatusPill.png --out $P/SP_df.png --px 2
  $KP mirror   --in $P/SP_df.png --out $P/SP_sym.png --axis h
  $KP rowmatch --in $P/SP_sym.png --out $P/SP_col.png --screen 08 --x 108-112,230-234 --y0 448.6 --y1 486.4 --trust 0.12-0.88 --smooth 0.04
  $KP resize   --in $P/SP_col.png --out $P/SP_2x.png --w 358 --h 88
  $KP nine     --in $P/SP_2x.png --out $P/SP_nine.png --border 56,0,56,0 --mid 16,0 --feather 12
  $KP export   --in $P/SP_nine.png --name UI/Table/UI_StatusPill.png

  # UI_TimerRing: ChatGPT drew the orange ring with the avatar's white ring inside it and with
  # tick marks. Kept: the orange band only (radii 181 to 214.5 of its drawing), redrawn from its
  # colour-by-radius so it is the same all the way round (the game fills it radially).
  $KP pad    --in $P/TimerRing.png --out $P/TR_pad.png --w 440 --h 440
  $KP radial --in $P/TR_pad.png --out $P/TR_round.png --rmin 181 --rmax 214.5
  $KP cut    --in $P/TR_round.png --out $P/TR_trim.png --key none --lo 0 --hi 255
  $KP resize --in $P/TR_trim.png --out $P/TR_2x_raw.png --w 214 --h 214               # 92.6 mockup px @2x
  $KP curve  --in $P/TR_2x_raw.png --out $P/TR_2x.png --anchors "#FDAD04>#F6960B;#D15F0E>#C57B10;#BE4A0D>#B86E12"
  $KP export --in $P/TR_2x.png --name UI/Table/UI_TimerRing.png

  # UI_Chip_Normal / _Selected (07). ChatGPT drew both about 2% wider than tall and gave them a
  # raised outer rim with a dark line around it. On the mockup the ivory chip ends in a thin tan
  # edge line and the crimson chip in a thin cream rim, both lighter at the top than at the
  # bottom; outside them is only the soft shadow (a separate sprite in the game). So each piece
  # is squared, redrawn exactly round from its colour-by-radius up to the end of that line / rim
  # (radius 170 and 173.5 of 178), its body shaded top to bottom like the mockup (read on a chord
  # beside the numeral) and its line / rim given the colours measured around the mockup's chip
  # (kp ringprobe).
  $KP resize    --in $P/ChipNormal.png --out $P/CN_sq.png --w 356 --h 356
  $KP pad       --in $P/CN_sq.png --out $P/CN_pad.png --w 360 --h 360
  $KP radial    --in $P/CN_pad.png --out $P/CN_round.png --rmax 170
  $KP cut       --in $P/CN_round.png --out $P/CN_trim.png --key none --lo 0 --hi 255
  $KP resize    --in $P/CN_trim.png --out $P/CN_2x_raw.png --w 166 --h 166             # 72.2 mockup px @2x: disc 68.8 + the edge line
  $KP rowmatch  --in $P/CN_2x_raw.png --out $P/CN_row.png --screen 07 --x 518-522 --y0 463.2 --y1 535.4 --trust 0.13-0.87 --smooth 0.05
  $KP ringshade --in $P/CN_row.png --out $P/CN_2x.png --rin 79.4 --rout 83 --top "#E4C9B5" --side "#D1AF98" --bottom "#CFAB8D"
  $KP export    --in $P/CN_2x.png --name UI/Table/UI_Chip_Normal.png
  $KP resize    --in $P/ChipSelected.png --out $P/CS_sq.png --w 355 --h 355
  $KP pad       --in $P/CS_sq.png --out $P/CS_pad.png --w 360 --h 360
  $KP radial    --in $P/CS_pad.png --out $P/CS_round.png --rmax 173.5
  $KP cut       --in $P/CS_round.png --out $P/CS_trim.png --key none --lo 0 --hi 255
  $KP resize    --in $P/CS_trim.png --out $P/CS_2x_raw.png --w 178 --h 178             # 77.4 mockup px @2x: disc 73.4 + the cream rim
  $KP rowmatch  --in $P/CS_2x_raw.png --out $P/CS_row.png --screen 07 --x 770-774 --y0 460.75 --y1 538.15 --trust 0.14-0.86 --smooth 0.05
  $KP ringshade --in $P/CS_row.png --out $P/CS_2x.png --rin 85.6 --rout 89 --top "#FBF8EE" --side "#EBD7C2" --bottom "#E3CDB7"
  $KP export    --in $P/CS_2x.png --name UI/Table/UI_Chip_Selected.png

  $KP check --sprite $A/UI_NamePlate.png --nine 44,0,44,0 --screen 08 --rect 104.9,415.8,133.2,33.0 --view 90,408,164,84 --zoom 6 --out $C/UI_NamePlate_check_08.png
  $KP check --sprite $A/UI_StatusPill.png --nine 56,0,56,0 --screen 08 --rect 96.5,448.6,150.3,37.8 --view 90,440,164,52 --zoom 8 --out $C/UI_StatusPill_check_08.png
  $KP stretch --sprite $A/UI_NamePlate.png --nine 44,0,44,0 --size 400,33 --zoom 2 --stack --out $C/UI_NamePlate_stretch3x.png
  $KP stretch --sprite $A/UI_StatusPill.png --nine 56,0,56,0 --size 451,37.8 --zoom 2 --stack --out $C/UI_StatusPill_stretch3x.png
  $KP place --screen 08 --view 84,404,176,92 --zoom 6 --stack --out $C/UI_Seat_pieces_check_08.png \
      --items "UI_NamePlate.png|44,0,44,0|104.9,415.8,133.2,33.0;UI_StatusPill.png|56,0,56,0|96.5,448.6,150.3,37.8"
  $KP place --screen 08 --view 1412,404,176,92 --zoom 6 --stack --out $C/UI_Seat_pieces_check_08_right.png \
      --items "UI_NamePlate.png|44,0,44,0|1432.9,415.8,133.2,32.9;UI_StatusPill.png|56,0,56,0|1424.3,448.6,149.7,37.8"
  $KP check --sprite $A/UI_TimerRing.png --screen 08 --rect 18.9,828.7,92.6,92.6 --view 10,820,112,112 --zoom 6 --out $C/UI_TimerRing_check_08.png
  $KP check --sprite $A/UI_Chip_Normal.png --screen 07 --rect 501.9,463.2,72.2,72.2 --view 495,456,86,88 --zoom 8 --out $C/UI_Chip_Normal_check_07.png
  $KP check --sprite $A/UI_Chip_Selected.png --screen 07 --rect 754.4,460.75,77.4,77.4 --view 748,454,90,92 --zoom 8 --out $C/UI_Chip_Selected_check_07.png
  # The whole row of chips on 07 (each chip keeps its centre; 72.2 and 77.4 mockup px).
  $KP place --screen 07 --view 483,448,706,104 --zoom 2.2 --stack --out $C/UI_Chips_row_check_07.png       --items "UI_Chip_Normal.png|-|501.88,463.25,72.2,72.2;UI_Chip_Normal.png|-|586.79,463.25,72.2,72.2;UI_Chip_Normal.png|-|671.49,463.25,72.2,72.2;UI_Chip_Selected.png|-|754.4,460.75,77.4,77.4;UI_Chip_Normal.png|-|842.43,463.25,72.2,72.2;UI_Chip_Normal.png|-|927.43,463.25,72.2,72.2;UI_Chip_Normal.png|-|1012.45,463.25,72.2,72.2;UI_Chip_Normal.png|-|1097.47,463.25,72.2,72.2"
  # Shadow settings for the engineers (the shared circle shadow under each chip): an estimate.
  $KP shadowfit --screen 07 --center 538.0,499.3 --size 72.2 --shadow Assets/_Project/Art/UI/Buttons/UI_Shadow_Circle.png --disc 212
  $KP shadowfit --screen 07 --center 793.07,499.45 --size 77.4 --shadow Assets/_Project/Art/UI/Buttons/UI_Shadow_Circle.png --disc 212
fi

# ---------------------------------------------------------------------------------------------
# The results table sheet: header band, highlighted row, table body.
if want Results_Table; then
  P=$T/rt; mkdir -p $P
  $KP cut   --in $G/Results_Table_Sheet_a1.png --out $T/RT_key.png --key magenta --notrim --despeckle 300
  $KP split --in $T/RT_key.png --outdir $P --names TableHeader,RowHighlight,TableBody --min 2000 --grow 4

  # UI_TableHeader: a flat tan band. From the extraction the shape is kept (top corners rounded,
  # bottom straight); the colour is set to the mockup's #E9D8C6 and the outline redrawn exactly.
  $KP crop     --in $P/TableHeader.png --out $P/TH_crop.png --box 4,2,1698,128       # without the fringed last rows
  $KP resize   --in $P/TH_crop.png --out $P/TH_2x_raw.png --w 1491 --h 118
  $KP flatfill --in $P/TH_2x_raw.png --out $P/TH_2x_flat.png --thr 255 --grow 0 --fill "#E9D8C6" --any
  $KP shape    --in $P/TH_2x_flat.png --out $P/TH_2x.png --type roundrect --radius 32 --square bottom
  $KP nine     --in $P/TH_2x.png --out $P/TH_nine.png --border 40,40,40,8 --mid 16,16
  $KP export   --in $P/TH_nine.png --name UI/Table/UI_TableHeader.png

  # UI_RowHighlight: cream band with a gold outline (09: [384.9, 378.1, 902.2, 61.9]).
  $KP mirror   --in $P/RowHighlight.png --out $P/RH_sym.png --axis hv
  $KP crop     --in $P/RH_sym.png --out $P/RH_crop.png --box 3,5,1704,151            # to the gold outline (a white halo lay outside it)
  $KP curve    --in $P/RH_crop.png --out $P/RH_col.png --anchors "#F2B437>#E6B76F;#FEF0D0>#FCE9C3"
  $KP shape    --in $P/RH_col.png --out $P/RH_shape.png --type roundrect --radius 34
  $KP rowmatch --in $P/RH_shape.png --out $P/RH_row.png --screen 09 --x 600-640 --y0 378.1 --y1 440.0 --trust 0.15-0.85 --smooth 0.06
  $KP resize   --in $P/RH_row.png --out $P/RH_2x.png --w 1625 --h 144
  $KP nine     --in $P/RH_2x.png --out $P/RH_nine.png --border 44,44,44,44 --mid 16,16 --feather 10
  $KP export   --in $P/RH_nine.png --name UI/Table/UI_RowHighlight.png

  # UI_TableBody: the ivory container with its soft light edge (09: [385.9, 322.8, 900, 311.3]).
  $KP mirror   --in $P/TableBody.png --out $P/TB_sym.png --axis hv
  $KP defringe --in $P/TB_sym.png --out $P/TB_df.png --px 2
  $KP curve    --in $P/TB_df.png --out $P/TB_col.png --anchors "#FEF7EC>#F7EDE2;#FFFBF4>#FCF3E8"
  $KP resize   --in $P/TB_col.png --out $P/TB_2x.png --w 1488 --h 306
  $KP nine     --in $P/TB_2x.png --out $P/TB_nine.png --border 40,40,40,40 --mid 16,16 --feather 8
  $KP export   --in $P/TB_nine.png --name UI/Table/UI_TableBody.png

  I9="UI_TableBody.png|40,40,40,40|385.9,322.8,900,311.3;UI_TableHeader.png|40,40,40,8|386,322.8,900,51.1;UI_RowHighlight.png|44,44,44,44|384.9,378.1,902.2,61.9"
  $KP place --screen 09 --view 366,302,940,352 --zoom 1.5 --stack --out $C/UI_Table_pieces_check_09.png --items "$I9"
  $KP place --screen 09 --view 376,312,230,150 --zoom 4 --out $C/UI_Table_pieces_check_09_tl.png --items "$I9"
  $KP place --screen 09 --view 1066,312,230,150 --zoom 4 --out $C/UI_Table_pieces_check_09_tr.png --items "$I9"
  $KP place --screen 10 --view 436,373,800,290 --zoom 1.7 --stack --out $C/UI_Table_pieces_check_10.png \
      --items "UI_TableBody.png|40,40,40,40|456.3,393.5,759.9,248.5;UI_RowHighlight.png|44,44,44,44|455.6,393.4,761.5,59.5"
  $KP place --screen 16 --view 324,320,1024,376 --zoom 1.4 --stack --out $C/UI_Table_pieces_check_16.png \
      --items "UI_TableBody.png|40,40,40,40|343.5,340.5,985.3,335.5;UI_TableHeader.png|40,40,40,8|343.8,340.5,984.5,53.6;UI_RowHighlight.png|44,44,44,44|342.8,398.4,986.4,68.2"
  $KP onbg --in $A/UI_TableBody.png --out $C/UI_TableBody_alpha.png --zoom 3 --bgs "#000000,#FFFFFF"
  $KP onbg --in $A/UI_TableHeader.png --out $C/UI_TableHeader_alpha.png --zoom 3 --bgs "#000000,#FFFFFF"
  $KP onbg --in $A/UI_RowHighlight.png --out $C/UI_RowHighlight_alpha.png --zoom 3 --bgs "#000000,#FFFFFF"
  $KP stretch --sprite $A/UI_RowHighlight.png --nine 44,44,44,44 --size 1200,186 --zoom 1 --stack --out $C/UI_RowHighlight_stretch3x.png
  $KP stretch --sprite $A/UI_TableBody.png --nine 40,40,40,40 --size 1200,420 --zoom 1 --one --out $C/UI_TableBody_stretch3x.png
fi

# ---------------------------------------------------------------------------------------------
# The Game Menu rows sheet: tag pill, Scorecard button, Sound row (not used: the kit card is the
# same element) and the timer badge (second attempt, after the ball on the first sheet).
if want Menu_Rows; then
  P=$T/mr; mkdir -p $P
  $KP cut   --in $G/MenuRows_TimerBadge_Sheet_a1.png --out $T/MR_key.png --key magenta --notrim --despeckle 300
  $KP split --in $T/MR_key.png --outdir $P --names TagPill,ScorecardButton,SoundRow,TimerBadge --min 2000 --grow 4

  # UI_TimerDisc: a flat dark oval with a thin orange rim. ChatGPT drew it 1.5 times as wide as
  # tall; on the mockup it is 42.3 x 35.3, so it is redrawn at that shape from its own colours.
  $KP defringe --in $P/TimerBadge.png --out $P/TB_df.png --px 3
  $KP reshape  --in $P/TB_df.png --out $P/TB_shape.png --type ellipse --w 98 --h 82
  $KP curve    --in $P/TB_shape.png --out $P/TB_2x.png --anchors "#2C1C0E>#2C1E0B;#FD8208>#F08505"
  $KP export   --in $P/TB_2x.png --name UI/Table/UI_TimerDisc.png

  # UI_Pill_Tag (13: [708.8, 304.3, 254.3, 36.9], flat #DFC6AF) and, from the same extraction,
  # UI_Pill_Elapsed (05: [1041.8, 780.9, 122.4, 52.3], flat #EADACB).
  $KP defringe --in $P/TagPill.png --out $P/TP_df.png --px 3
  $KP reshape  --in $P/TP_df.png --out $P/TP_shape.png --type pill --w 586 --h 86
  $KP recolor  --in $P/TP_shape.png --out $P/TP_2x.png --anchors "#E9D4BF>#DFC6AF" --mode offset
  $KP nine     --in $P/TP_2x.png --out $P/TP_nine.png --border 46,0,46,0 --mid 16,0
  $KP export   --in $P/TP_nine.png --name UI/Table/UI_Pill_Tag.png
  $KP reshape  --in $P/TP_df.png --out $P/EP_shape.png --type pill --w 282 --h 122
  $KP recolor  --in $P/EP_shape.png --out $P/EP_2x.png --anchors "#E9D4BF>#EADACB" --mode offset
  $KP nine     --in $P/EP_2x.png --out $P/EP_nine.png --border 64,0,64,0 --mid 16,0
  $KP export   --in $P/EP_nine.png --name UI/Table/UI_Pill_Elapsed.png

  # UI_ButtonSecondary_Box_Normal: the Scorecard button. Its outline's OUTER edge on 13 is
  # [611.75, 448.65, 447.65, 74.25] (kp line); the layout's [614.3, 450.2, 442.8, 70.6] is the inner edge.
  $KP defringe --in $P/ScorecardButton.png --out $P/SB_df.png --px 2
  $KP mirror   --in $P/SB_df.png --out $P/SB_sym.png --axis h
  $KP resize   --in $P/SB_sym.png --out $P/SB_2x_raw.png --h 172                     # 74.25 mockup px tall @2x
  $KP curve    --in $P/SB_2x_raw.png --out $P/SB_col.png --anchors "#CB7D17>#A86C3A;#FCF4E8>#F7EBE1;#FEFCFA>#FDF8F2"
  $KP rowmatch --in $P/SB_col.png --out $P/SB_2x.png --screen 13 --x 630-650,1020-1040 --y0 448.65 --y1 522.9 --sx 0.3-0.7 --trust 0.15-0.85 --smooth 0.06
  $KP nine     --in $P/SB_2x.png --out $P/SB_nine.png --border 54,0,54,0 --mid 16,0 --feather 8
  $KP export   --in $P/SB_nine.png --name UI/Table/UI_ButtonSecondary_Box_Normal.png

  $KP check --sprite $A/UI_TimerDisc.png --screen 08 --rect 43.6,893.7,42.3,35.3 --view 34,884,62,55 --zoom 10 --out $C/UI_TimerDisc_check_08.png
  $KP place --screen 08 --view 8,818,118,120 --zoom 7 --out $C/UI_Timer_pieces_check_08.png \
      --items "UI_TimerRing.png|-|18.9,828.7,92.6,92.6;UI_TimerDisc.png|-|43.6,893.7,42.3,35.3"
  $KP check --sprite $A/UI_Pill_Tag.png --nine 46,0,46,0 --screen 13 --rect 708.8,304.3,254.3,36.9 --view 698,296,275,54 --zoom 5 --out $C/UI_Pill_Tag_check_13.png
  $KP check --sprite $A/UI_Pill_Elapsed.png --nine 64,0,64,0 --screen 05 --rect 1041.8,780.9,122.4,52.3 --view 1030,770,146,74 --zoom 6 --out $C/UI_Pill_Elapsed_check_05.png
  $KP check --sprite $A/UI_ButtonSecondary_Box_Normal.png --nine 54,0,54,0 --screen 13 --rect 611.75,448.65,447.65,74.25 --view 602,438,467,95 --zoom 3 --out $C/UI_ButtonSecondary_Box_check_13.png
  $KP stretch --sprite $A/UI_ButtonSecondary_Box_Normal.png --nine 54,0,54,0 --size 1343,74.25 --zoom 1 --stack --out $C/UI_ButtonSecondary_Box_stretch3x.png
  # The three rows of the Game Menu together; the Sound row is the kit card at sliceScale 1.1.
  $KP place --screen 13 --view 600,296,470,316 --zoom 2.6 --unit 2.5 --stack --out $C/UI_MenuRows_check_13.png \
      --items "UI_Pill_Tag.png|46,0,46,0|708.8,304.3,254.3,36.9;UI_ButtonSecondary_Box_Normal.png|54,0,54,0|611.75,448.65,447.65,74.25;UI_Panel_Card.png|56,56,56,56|614,539.8,444.1,62.3"
fi

echo "done"
